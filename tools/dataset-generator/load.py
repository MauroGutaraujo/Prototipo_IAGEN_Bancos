"""
Carga el banco sintético a SQL Server (esquemas core, app y eval).

    python load.py --conn "%SQL_CONN%" [--data data/]

- Acepta la cadena en formato ADO.NET (la de .env) y la convierte a ODBC.
- Verifica los SHA-256 del manifiesto antes de cargar.
- Reemplaza el banco anterior en una sola transacción. Se niega si ya existen
  ejecuciones o registros manuales (no borra datos experimentales).
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import os
import sys
from datetime import datetime
from decimal import Decimal
from pathlib import Path

import pyodbc

DRIVER = "ODBC Driver 18 for SQL Server"
_ADO_A_ODBC = {
    "server": "SERVER", "data source": "SERVER", "database": "DATABASE", "initial catalog": "DATABASE",
    "user id": "UID", "uid": "UID", "password": "PWD", "pwd": "PWD",
    "trustservercertificate": "TrustServerCertificate", "encrypt": "Encrypt",
    "integrated security": "Trusted_Connection", "trusted_connection": "Trusted_Connection",
}


def a_odbc(conn: str) -> str:
    if "driver=" in conn.lower():
        return conn
    partes = [f"DRIVER={{{DRIVER}}}"]
    for par in filter(None, (p.strip() for p in conn.split(";"))):
        k, _, v = par.partition("=")
        clave = _ADO_A_ODBC.get(k.strip().lower())
        if clave is None:
            continue
        v = v.strip()
        if v.lower() in ("true", "sspi"):
            v = "yes"
        elif v.lower() == "false":
            v = "no"
        partes.append(f"{clave}={{{v}}}" if clave == "PWD" else f"{clave}={v}")
    return ";".join(partes)


def leer_conn_de_env() -> str | None:
    if os.environ.get("SQL_CONN"):
        return os.environ["SQL_CONN"]
    for d in [Path.cwd(), *Path.cwd().parents]:
        env = d / ".env"
        if env.exists():
            for linea in env.read_text(encoding="utf-8").splitlines():
                if linea.startswith("SQL_CONN="):
                    return linea.partition("=")[2].strip()
    return None


def verificar_manifiesto(data: Path) -> dict:
    manifiesto = json.loads((data / "manifest.json").read_text(encoding="utf-8"))
    for rel, sha in manifiesto["archivos"].items():
        if hashlib.sha256((data / rel).read_bytes()).hexdigest() != sha:
            raise SystemExit(f"Hash distinto en {rel}: regenera el banco antes de cargar.")
    return manifiesto


def filas(data: Path, nombre: str) -> list[dict[str, str]]:
    with (data / nombre).open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def fh(s: str) -> datetime:
    return datetime.strptime(s, "%Y-%m-%d %H:%M:%S")


def nulo(s: str) -> str | None:
    return s or None


def cargar(cn: pyodbc.Connection, data: Path) -> None:
    cur = cn.cursor()
    cur.fast_executemany = True

    for tabla in ("app.Ejecucion", "app.RegistroManual"):
        n = cur.execute(f"SELECT COUNT(*) FROM {tabla}").fetchone()[0]
        if n:
            raise SystemExit(f"{tabla} tiene {n} filas: no se recarga el banco sobre datos experimentales.")

    for tabla in ("eval.VerdadReferencia", "app.Evidencia", "app.Expediente",
                  "core.Transaccion", "core.ClienteSintetico", "core.TipoCambio"):
        cur.execute(f"DELETE FROM {tabla}")

    cur.executemany("INSERT INTO core.TipoCambio (Fecha, UsdPen) VALUES (?, ?)",
                    [(datetime.strptime(r["fecha"], "%Y-%m-%d").date(), Decimal(r["usd_pen"]))
                     for r in filas(data, "tipo_cambio.csv")])

    cur.execute("SET IDENTITY_INSERT core.ClienteSintetico ON")
    cur.executemany("INSERT INTO core.ClienteSintetico (IdCliente, AliasSeudonimo, DispositivoRegistrado) VALUES (?, ?, ?)",
                    [(int(r["id_cliente"]), r["alias_seudonimo"], r["dispositivo_registrado"])
                     for r in filas(data, "clientes.csv")])
    cur.execute("SET IDENTITY_INSERT core.ClienteSintetico OFF")

    cur.executemany(
        "INSERT INTO core.Transaccion (CodigoOperacion, IdCliente, Monto, Moneda, FechaHora, Comercio, Estado,"
        " AutenticacionReforzada, Dispositivo, IndicadorRiesgo) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
        [(r["codigo_operacion"], int(r["id_cliente"]), Decimal(r["monto"]), r["moneda"], fh(r["fecha_hora"]),
          r["comercio"], r["estado"], r["autenticacion_reforzada"] == "1", nulo(r["dispositivo"]),
          r["indicador_riesgo"] == "1") for r in filas(data, "transacciones.csv")])

    cur.executemany(
        "INSERT INTO app.Expediente (IdExpediente, CodigoOperacion, NarracionCliente, FechaIngreso) VALUES (?, ?, ?, ?)",
        [(int(r["id_expediente"]), r["codigo_operacion"], r["narracion"], fh(r["fecha_ingreso"]))
         for r in filas(data, "expedientes.csv")])

    cur.execute("SET IDENTITY_INSERT app.Evidencia ON")
    cur.executemany(
        "INSERT INTO app.Evidencia (IdEvidencia, IdExpediente, RutaImagen, PerturbacionAplicada) VALUES (?, ?, ?, ?)",
        [(int(r["id_evidencia"]), int(r["id_expediente"]), r["ruta"], r["perturbacion"])
         for r in filas(data, "evidencias.csv")])
    cur.execute("SET IDENTITY_INSERT app.Evidencia OFF")

    cur.executemany(
        "INSERT INTO eval.VerdadReferencia (IdExpediente, IntencionReal, MontoReal, MonedaReal, FechaReal, CodigoReal,"
        " CamposLegibles, RutaCorrecta, ReglaEsperada) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)",
        [(int(r["id_expediente"]), r["intencion_real"], Decimal(r["monto_real"]), r["moneda_real"],
          fh(r["fecha_real"]), r["codigo_real"], r["campos_legibles"], r["ruta_correcta"], r["regla_esperada"])
         for r in filas(data, "verdad_referencia.csv")])


def resumen(cn: pyodbc.Connection) -> None:
    cur = cn.cursor()
    for tabla in ("core.ClienteSintetico", "core.TipoCambio", "core.Transaccion",
                  "app.Expediente", "app.Evidencia", "eval.VerdadReferencia"):
        print(f"  {tabla:<24} {cur.execute(f'SELECT COUNT(*) FROM {tabla}').fetchone()[0]}")
    print("  Intención × ruta correcta (eval.VerdadReferencia):")
    for fila in cur.execute("SELECT IntencionReal, RutaCorrecta, COUNT(*) FROM eval.VerdadReferencia"
                            " GROUP BY IntencionReal, RutaCorrecta ORDER BY 1, 2"):
        print(f"    {fila[0]}  {fila[1]:<13} {fila[2]}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--conn", help="cadena ADO.NET u ODBC; por defecto SQL_CONN (entorno o .env)")
    ap.add_argument("--data", type=Path, default=Path("data"))
    args = ap.parse_args()

    conn = args.conn or leer_conn_de_env()
    if not conn:
        raise SystemExit("Falta la cadena de conexión: usa --conn o define SQL_CONN.")

    manifiesto = verificar_manifiesto(args.data)
    print(f"Manifiesto verificado (seed={manifiesto['seed']}, {len(manifiesto['archivos'])} archivos).")

    with pyodbc.connect(a_odbc(conn), autocommit=False) as cn:
        try:
            cargar(cn, args.data)
            cn.commit()
        except BaseException:
            cn.rollback()
            raise
        print("Carga completada:")
        resumen(cn)
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
