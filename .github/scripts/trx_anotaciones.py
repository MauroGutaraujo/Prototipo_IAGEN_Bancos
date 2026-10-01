"""Publica las pruebas fallidas de los reportes TRX como anotaciones de GitHub Actions.

    python trx_anotaciones.py <directorio>
"""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
MAX_ANOTACIONES = 9  # GitHub muestra hasta 10 anotaciones de error por paso


def escapar(texto: str) -> str:
    return texto.replace("%", "%25").replace("\r", "").replace("\n", "%0A")


def main() -> int:
    fallidas = []
    for trx in sorted(Path(sys.argv[1]).rglob("*.trx")):
        for r in ET.parse(trx).getroot().iterfind(".//t:UnitTestResult", NS):
            if r.get("outcome") == "Failed":
                mensaje = r.findtext(".//t:ErrorInfo/t:Message", default="", namespaces=NS)
                fallidas.append((r.get("testName", "?"), mensaje.strip()))

    for nombre, mensaje in fallidas[:MAX_ANOTACIONES]:
        print(f"::error title={escapar(nombre)}::{escapar(mensaje[:1500])}")
    if fallidas:
        lista = "\n".join(n for n, _ in fallidas)
        print(f"::error title={len(fallidas)} pruebas fallidas::{escapar(lista)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
