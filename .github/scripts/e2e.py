"""Prueba de punta a punta (H6): un expediente por tipología vía la API.

    python e2e.py <modelo> <directorio_datos>

Elige, con el diseño del banco (herramienta de evaluación, no el sistema), el primer expediente de cada
escenario de ruta automática (C1-R5, C2-R9, C3-R7). Verifica que cada ejecución se persista con todos sus
tiempos y con la descomposición aditiva; REPORTA estados y tiempos sin fijar umbrales de desempeño.
"""
import csv
import json
import sys
import time
import urllib.error
import urllib.request

BASE = "http://localhost:8080"
CAMPOS = ["t_Ocr_ms", "l_Cls_ms", "t_Guardrail_ms", "t_Rag_ms", "l_Gen_ms", "t_Orq_ms", "l_Total_ms"]
ESTADOS_FINALES = {"Emitido", "Derivado", "Error"}


def anotar(nivel: str, titulo: str, texto: str) -> None:
    print(f"::{nivel} title={titulo}::" + texto.replace("%", "%25").replace("\r", "").replace("\n", "%0A"))


def main() -> int:
    modelo, datos = sys.argv[1], sys.argv[2]
    with open(f"{datos}/diseno_casos.csv", encoding="utf-8") as f:
        filas = list(csv.DictReader(f))
    casos = [min(int(f["id_expediente"]) for f in filas if f["escenario"] == esc) for esc in ("R5", "R9", "R7")]

    tabla = ["expediente,estado,motivo,regla,rag,regeneraciones,modelo_version," + ",".join(CAMPOS) + ",error"]
    fallas: list[str] = []
    ids_ejecucion: list[int] = []
    for i, expediente in enumerate(casos):
        if i:
            time.sleep(15)  # límites por minuto del plan gratuito; fuera de los tiempos medidos
        url = f"{BASE}/api/expedientes/{expediente}/procesar?modelo={modelo}&condicion=T1&repeticion=1"
        try:
            with urllib.request.urlopen(urllib.request.Request(url, method="POST"), timeout=300) as r:
                e = json.load(r)
        except urllib.error.HTTPError as ex:
            fallas.append(f"{expediente}: HTTP {ex.code} {ex.read()[:300]!r}")
            continue

        ids_ejecucion.append(e["idEjecucion"])
        t = e["tiempos"]
        if e["estadoFinal"] not in ESTADOS_FINALES:
            fallas.append(f"{expediente}: estado final inesperado {e['estadoFinal']}")
        if any(not isinstance(t.get(c), int) or t[c] < 0 for c in CAMPOS):
            fallas.append(f"{expediente}: tiempos incompletos {t}")
        elif abs(t["l_Total_ms"] - sum(t[c] for c in CAMPOS[:-1])) > 7:
            fallas.append(f"{expediente}: la suma de etapas no coincide con L_Total {t}")
        decision = e.get("decision") or {}
        rag = (e.get("recuperacion") or {}).get("calificacion", "")
        tabla.append(",".join(str(x) for x in [
            expediente, e["estadoFinal"], e.get("motivoDerivacion") or "", decision.get("regla", ""), rag,
            e["regeneraciones"], e.get("modeloVersion") or "", *(t.get(c) for c in CAMPOS),
            (e.get("error") or "").replace(",", ";")[:150]]))

    with urllib.request.urlopen(f"{BASE}/api/ejecuciones?modelo={modelo}", timeout=60) as r:
        persistidas = {e["idEjecucion"] for e in json.load(r)}
    if not set(ids_ejecucion) <= persistidas:
        fallas.append("No todas las ejecuciones quedaron persistidas en app.Ejecucion")

    anotar("notice", f"E2E - un caso por tipología - {modelo}", "\n".join(tabla))
    for f in fallas:
        anotar("error", "E2E", f)
    return 1 if fallas else 0


if __name__ == "__main__":
    sys.exit(main())
