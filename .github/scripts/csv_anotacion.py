"""Publica un CSV como una anotación ::notice (tabla en texto) para leerla sin autenticación.

    python csv_anotacion.py <archivo.csv> <título>
"""
import sys
from pathlib import Path


def main() -> int:
    ruta, titulo = Path(sys.argv[1]), sys.argv[2]
    if not ruta.exists():
        print(f"::warning title={titulo}::No existe {ruta}")
        return 0
    texto = ruta.read_text(encoding="utf-8").strip()
    print(f"::notice title={titulo}::" + texto.replace("%", "%25").replace("\r", "").replace("\n", "%0A"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
