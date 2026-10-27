"""Prueba de punta a punta de la ficha T0 (H7): un analista de prueba completa 3 casos vía HTTP.

    python ficha_e2e.py <directorio tools/dataset-generator>

Requiere la API en http://localhost:8080 con Ficha__Pines__A1 = $FICHA_PIN y SQL_CONN para verificar la base.
Verifica funcionamiento (orden, PIN, registro, pausa descontada); los tiempos solo se reportan.
"""
import http.cookiejar
import json
import os
import sys
import time
import urllib.error
import urllib.request

BASE = "http://localhost:8080"
PAUSA_S = 3


def anotar(nivel: str, titulo: str, texto: str) -> None:
    print(f"::{nivel} title={titulo}::" + texto.replace("%", "%25").replace("\r", "").replace("\n", "%0A"))


class Cliente:
    def __init__(self) -> None:
        self.http = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def llamar(self, metodo: str, ruta: str, cuerpo: dict | None = None) -> tuple[int, bytes, str]:
        datos = json.dumps(cuerpo).encode() if cuerpo is not None else None
        req = urllib.request.Request(BASE + ruta, data=datos, method=metodo,
                                     headers={"Content-Type": "application/json"} if datos else {})
        try:
            with self.http.open(req, timeout=60) as r:
                return r.status, r.read(), r.headers.get("Content-Type", "")
        except urllib.error.HTTPError as e:
            return e.code, e.read(), ""

    def json(self, metodo: str, ruta: str, cuerpo: dict | None = None, esperado: int = 200):
        codigo, cuerpo_r, _ = self.llamar(metodo, ruta, cuerpo)
        if codigo != esperado:
            raise AssertionError(f"{metodo} {ruta}: HTTP {codigo} (esperado {esperado}) {cuerpo_r[:200]!r}")
        return json.loads(cuerpo_r) if cuerpo_r else None


def main() -> int:
    sys.path.insert(0, sys.argv[1])
    from load import a_odbc, leer_conn_de_env  # noqa: E402 (reutiliza la conversión de cadena del cargador)
    import pyodbc  # noqa: E402

    pin = os.environ["FICHA_PIN"]
    fallas: list[str] = []
    c = Cliente()

    def comprobar(condicion: bool, mensaje: str) -> None:
        if not condicion:
            fallas.append(mensaje)

    comprobar(c.llamar("GET", "/ficha/api/casos")[0] == 401, "Sin sesión, /casos debería responder 401")
    comprobar(c.llamar("POST", "/ficha/api/login", {"codigo": "A1", "pin": pin + "x"})[0] == 401, "Un PIN incorrecto debería rechazarse")
    c.json("POST", "/ficha/api/login", {"codigo": "A1", "pin": pin})

    casos = c.json("GET", "/ficha/api/casos")
    comprobar(len(casos) == 80, f"A1 debería tener 80 casos asignados y tiene {len(casos)}")
    comprobar([x["habilitado"] for x in casos[:3]] == [True, False, False], "Solo el primer caso debería estar habilitado")
    comprobar(c.llamar("POST", f"/ficha/api/casos/{casos[1]['idExpediente']}/abrir")[0] == 409,
              "Abrir fuera de orden debería responder 409")

    tabla = ["expediente,tiempo_efectivo_ms,pausas_ms,interrupciones"]
    enviados = []
    for k, caso in enumerate(casos[:3]):
        idx = caso["idExpediente"]
        abierto = c.json("POST", f"/ficha/api/casos/{idx}/abrir")
        comprobar(bool(abierto["narracion"]), f"{idx}: la narración llegó vacía")
        codigo, _, tipo = c.llamar("GET", f"/ficha/api/casos/{idx}/voucher")
        comprobar(codigo == 200 and tipo.startswith("image/jpeg"), f"{idx}: el voucher no se sirvió como JPEG")
        operacion = c.json("GET", f"/api/expedientes/{idx}")["codigoOperacion"]
        core = c.json("GET", f"/ficha/api/core/transacciones?codigo={operacion}")
        comprobar(any(m["codigoOperacion"] == operacion for m in core["movimientos"]), f"{idx}: el core no devolvió la operación")
        c.json("POST", f"/ficha/api/casos/{idx}/latido", esperado=204)
        if k == 0:
            c.json("POST", f"/ficha/api/casos/{idx}/pausa", esperado=204)
            time.sleep(PAUSA_S)
            c.json("POST", f"/ficha/api/casos/{idx}/reanudar", esperado=204)
        time.sleep(1)
        r = c.json("POST", f"/ficha/api/casos/{idx}/enviar", {
            "intencion": "C1", "monto": 10.5, "fecha": "2026-03-10", "codigo": operacion,
            "ruta": "Derivar", "textoRespuesta": "Respuesta de prueba de la ficha",
        })
        enviados.append(idx)
        tabla.append(f"{idx},{r['tiempoEfectivoMs']},{r['pausasMs']},{r['interrupciones']}")
    comprobar(c.llamar("POST", f"/ficha/api/casos/{enviados[0]}/abrir")[0] == 409, "Un caso enviado no debería reabrirse")

    with pyodbc.connect(a_odbc(leer_conn_de_env())) as cn:
        filas = cn.cursor().execute(
            "SELECT r.IdExpediente, r.TIngresoUtc, r.TFinalUtc, r.PausasMs, s.IdRegistro "
            "FROM app.RegistroManual r LEFT JOIN app.SesionManual s "
            "  ON s.IdRegistro = r.IdRegistro "
            "WHERE r.CodigoAnalista = 'A1' ORDER BY r.IdRegistro").fetchall()
    comprobar([f[0] for f in filas] == enviados, f"RegistroManual debería tener {enviados} y tiene {[f[0] for f in filas]}")
    for idx, ingreso, final, pausas, id_sesion in filas:
        comprobar(final > ingreso, f"{idx}: TFinal debería ser mayor que TIngreso")
        comprobar((final - ingreso).total_seconds() * 1000 - pausas >= 0, f"{idx}: tiempo efectivo negativo")
        comprobar(id_sesion is not None, f"{idx}: la sesión no quedó enlazada al registro")
    if filas:
        comprobar(filas[0][3] >= PAUSA_S * 1000, f"La pausa de {PAUSA_S} s no se descontó (PausasMs={filas[0][3]})")

    anotar("notice", "Ficha T0 - analista de prueba - 3 casos", "\n".join(tabla))
    for f in fallas:
        anotar("error", "Ficha T0", f)
    return 1 if fallas else 0


if __name__ == "__main__":
    sys.exit(main())
