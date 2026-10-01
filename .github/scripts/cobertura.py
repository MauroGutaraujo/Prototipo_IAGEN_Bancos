"""Falla si un paquete del reporte Cobertura no tiene 100 % de líneas y ramas.

    python cobertura.py <directorio> <paquete>
"""
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


def main() -> int:
    directorio, paquete = Path(sys.argv[1]), sys.argv[2]
    reportes = sorted(directorio.rglob("*.cobertura.xml"))
    if not reportes:
        print(f"::error title=Cobertura::No se encontró ningún *.cobertura.xml en {directorio}")
        return 1

    encontrado = False
    for reporte in reportes:
        for pkg in ET.parse(reporte).getroot().iter("package"):
            if pkg.get("name") != paquete:
                continue
            encontrado = True
            lineas, ramas = float(pkg.get("line-rate", 0)), float(pkg.get("branch-rate", 0))
            print(f"::notice title=Cobertura {paquete}::líneas={lineas:.2%} ramas={ramas:.2%}")
            if lineas < 1 or ramas < 1:
                for clase in pkg.iter("class"):
                    for linea in clase.iter("line"):
                        if linea.get("hits") == "0" or "100%" not in linea.get("condition-coverage", "100%"):
                            print(f"::error file={clase.get('filename')},line={linea.get('number')},title=Sin cubrir::"
                                  f"{linea.get('condition-coverage', 'línea no ejecutada')}")
                return 1
    if not encontrado:
        print(f"::error title=Cobertura::El paquete {paquete} no aparece en los reportes")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
