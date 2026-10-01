"""
Genera el banco sintético de 200 expedientes.

    python generate.py --seed 2026 --out data/
    python generate.py --seed 2026 --out muestra/ --muestra   # solo 3 vouchers (C1, C2, C3)
"""
from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

from banco.construir import COMPOSICION, construir
from banco.salida import escribir, escribir_voucher


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--seed", type=int, required=True)
    ap.add_argument("--out", type=Path, required=True)
    ap.add_argument("--muestra", action="store_true", help="escribe solo un voucher por tipología")
    args = ap.parse_args()

    banco = construir(args.seed)

    if args.muestra:
        args.out.mkdir(parents=True, exist_ok=True)
        for intencion in COMPOSICION:
            caso = next(c for c in sorted(banco.casos, key=lambda c: c.id) if c.intencion == intencion)
            destino = args.out / f"muestra_{intencion}_{caso.id:04d}.jpg"
            escribir_voucher(caso, banco.seed, destino)
            print(f"{destino}  [{caso.escenario}/{caso.variante}] {caso.perturbacion}")
            print(f"    narración: {caso.narracion}")
        return 0

    manifiesto = escribir(banco, args.out)
    sha = hashlib.sha256(manifiesto.read_bytes()).hexdigest()
    print(f"Manifiesto: {manifiesto}\nSHA-256 del manifiesto: {sha}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
