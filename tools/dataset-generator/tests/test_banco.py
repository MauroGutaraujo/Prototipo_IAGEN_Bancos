"""Criterios de 'listo' de H2: composición, cobertura de reglas, privacidad y determinismo."""
import csv
import hashlib
import re
from collections import Counter
from pathlib import Path

import pytest

from banco.construir import construir
from banco.salida import escribir

SEED = 2026
# Marcas de bancos reales que nunca deben aparecer en el banco sintético.
MARCAS_REALES = ["BCP", "Crédito del Perú", "Credito del Peru", "Interbank", "BBVA", "Scotiabank",
                 "BanBif", "Pichincha", "Mibanco", "Banco de la Nación", "Falabella", "Ripley", "Yape", "Plin"]


@pytest.fixture(scope="module")
def banco():
    return construir(SEED)


@pytest.fixture(scope="module")
def salida(tmp_path_factory, banco):
    out = tmp_path_factory.mktemp("banco")
    escribir(banco, out)
    return out


def test_composicion_80_80_40(banco):
    assert Counter(c.intencion for c in banco.casos) == {"C1": 80, "C2": 80, "C3": 40}
    assert sorted(c.id for c in banco.casos) == list(range(1, 201))


def test_cada_regla_tiene_al_menos_un_caso(banco):
    reglas = {c.regla for c in banco.casos}
    assert {f"R{i}" for i in range(1, 10)} <= reglas


def test_regla_calculada_coincide_con_diseno(banco):
    assert all(c.regla == c.escenario for c in banco.casos)


def test_casos_r4_conservan_clase_del_estrato(banco):
    r4 = [c for c in banco.casos if c.regla == "R4"]
    assert r4 and all(c.intencion in ("C1", "C2", "C3") and c.texto.intencion == "FUERA_DE_CATALOGO" for c in r4)


def test_r1_solo_en_vouchers_de_ruido_alto(banco):
    assert all(c.extra["nivel"] == "alto" for c in banco.casos if c.regla == "R1")


def test_datos_mencionados_aparecen_en_la_narracion(banco):
    for c in banco.casos:
        if c.texto.codigo:
            assert c.texto.codigo in c.narracion
        if c.texto.monto is not None:
            assert f"{c.texto.monto:.2f}" in c.narracion


def test_sin_marcas_reales(salida):
    for nombre in ("expedientes.csv", "transacciones.csv", "clientes.csv"):
        texto = (salida / nombre).read_text(encoding="utf-8")
        for marca in MARCAS_REALES:
            assert not re.search(rf"\b{re.escape(marca)}\b", texto, re.IGNORECASE), f"{marca} en {nombre}"


def test_archivos_y_conteos(salida):
    with (salida / "verdad_referencia.csv").open(encoding="utf-8") as f:
        assert len(list(csv.DictReader(f))) == 200
    assert len(list((salida / "vouchers").glob("*.jpg"))) == 200


def test_misma_semilla_mismo_manifiesto(salida, tmp_path):
    escribir(construir(SEED), tmp_path)
    a = hashlib.sha256((salida / "manifest.json").read_bytes()).hexdigest()
    b = hashlib.sha256((tmp_path / "manifest.json").read_bytes()).hexdigest()
    assert a == b
