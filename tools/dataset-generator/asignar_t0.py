"""
Asignación de casos para la línea base manual T0 (SPEC §5). Se ejecuta OFFLINE: puede leer la verdad de
referencia para estratificar; la salida no incluye la clase y es lo único que carga la aplicación.

    python asignar_t0.py --seed 2026 --data data/ --out asignacion/asignacion_t0.csv

- 20 casos de calibración (8 C1, 8 C2, 4 C3) comunes a A1, A2 y A3.
- 180 casos restantes: 60 por analista (24 C1, 24 C2, 12 C3), sin solaparse.
- Cada analista recibe sus 80 casos en un orden aleatorio propio, con la calibración mezclada.
"""
from __future__ import annotations

import argparse
import csv
import random
import sys
from pathlib import Path

ANALISTAS = ("A1", "A2", "A3")
CALIBRACION = {"C1": 8, "C2": 8, "C3": 4}
POR_ANALISTA = {"C1": 24, "C2": 24, "C3": 12}


def asignar(clases: dict[int, str], seed: int) -> list[tuple[str, int, int, bool]]:
    """Devuelve (analista, orden, id_expediente, es_calibracion), ordenado por analista y orden."""
    rng = random.Random(seed)
    calibracion: list[int] = []
    restantes: dict[str, list[int]] = {}
    for clase in sorted(CALIBRACION):
        ids = sorted(i for i, c in clases.items() if c == clase)
        necesarios = CALIBRACION[clase] + POR_ANALISTA[clase] * len(ANALISTAS)
        if len(ids) != necesarios:
            raise ValueError(f"{clase}: hay {len(ids)} expedientes y se necesitan {necesarios}")
        rng.shuffle(ids)
        calibracion += ids[:CALIBRACION[clase]]
        restantes[clase] = ids[CALIBRACION[clase]:]

    filas = []
    for k, analista in enumerate(ANALISTAS):
        propios = [i for clase in sorted(POR_ANALISTA)
                   for i in restantes[clase][k * POR_ANALISTA[clase]:(k + 1) * POR_ANALISTA[clase]]]
        casos = [(i, False) for i in propios] + [(i, True) for i in calibracion]
        casos.sort()  # punto de partida estable antes de mezclar
        random.Random(f"{seed}-{analista}").shuffle(casos)
        filas += [(analista, orden, i, es_cal) for orden, (i, es_cal) in enumerate(casos, start=1)]
    return filas


def leer_clases(data: Path) -> dict[int, str]:
    with (data / "verdad_referencia.csv").open(encoding="utf-8", newline="") as f:
        return {int(r["id_expediente"]): r["intencion_real"] for r in csv.DictReader(f)}


def escribir(filas: list[tuple[str, int, int, bool]], out: Path) -> None:
    out.parent.mkdir(parents=True, exist_ok=True)
    with out.open("w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(["codigo_analista", "orden", "id_expediente", "es_calibracion"])
        w.writerows([a, o, i, int(c)] for a, o, i, c in filas)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--seed", type=int, required=True)
    ap.add_argument("--data", type=Path, default=Path("data"))
    ap.add_argument("--out", type=Path, default=Path("asignacion/asignacion_t0.csv"))
    args = ap.parse_args()
    filas = asignar(leer_clases(args.data), args.seed)
    escribir(filas, args.out)
    print(f"{args.out}: {len(filas)} filas ({len(ANALISTAS)} analistas)")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
