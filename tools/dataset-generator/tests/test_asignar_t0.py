"""Asignación T0 (SPEC §5): conteos por estrato, calibración común, sin solapes, orden propio y determinismo."""
from collections import Counter

import pytest

from asignar_t0 import ANALISTAS, asignar, escribir
from banco.construir import construir


@pytest.fixture(scope="module")
def clases():
    return {c.id: c.intencion for c in construir(2026).casos}


@pytest.fixture(scope="module")
def filas(clases):
    return asignar(clases, 2026)


def _de(filas, analista, calibracion=None):
    return [f for f in filas if f[0] == analista and (calibracion is None or f[3] == calibracion)]


def test_cada_analista_tiene_80_casos_en_orden_1_a_80(filas):
    for a in ANALISTAS:
        assert sorted(f[1] for f in _de(filas, a)) == list(range(1, 81))
        assert len({f[2] for f in _de(filas, a)}) == 80


def test_estratos_por_analista_y_calibracion(filas, clases):
    calibraciones = [frozenset(f[2] for f in _de(filas, a, True)) for a in ANALISTAS]
    assert len(set(calibraciones)) == 1  # la misma para los tres
    assert Counter(clases[i] for i in calibraciones[0]) == {"C1": 8, "C2": 8, "C3": 4}
    for a in ANALISTAS:
        assert Counter(clases[f[2]] for f in _de(filas, a, False)) == {"C1": 24, "C2": 24, "C3": 12}


def test_los_propios_no_se_solapan_y_cubren_los_200(filas):
    propios = [f[2] for f in filas if not f[3]]
    calibracion = {f[2] for f in filas if f[3]}
    assert len(propios) == len(set(propios)) == 180
    assert not calibracion & set(propios)
    assert len(calibracion | set(propios)) == 200


def test_la_calibracion_va_mezclada_y_cada_analista_tiene_su_orden(filas):
    for a in ANALISTAS:
        posiciones = sorted(f[1] for f in _de(filas, a, True))
        assert posiciones != list(range(1, 21)) and posiciones != list(range(61, 81))
    ordenes = [tuple(f[2] for f in sorted(_de(filas, a), key=lambda f: f[1])) for a in ANALISTAS]
    assert len(set(ordenes)) == 3


def test_misma_semilla_mismo_archivo(clases, tmp_path):
    escribir(asignar(clases, 2026), tmp_path / "a.csv")
    escribir(asignar(clases, 2026), tmp_path / "b.csv")
    assert (tmp_path / "a.csv").read_bytes() == (tmp_path / "b.csv").read_bytes()
    assert "intencion" not in (tmp_path / "a.csv").read_text(encoding="utf-8")  # la salida no lleva la clase


def test_composicion_distinta_falla():
    with pytest.raises(ValueError):
        asignar({1: "C1", 2: "C2"}, 2026)
