import hashlib
import json

import pytest

import fragmentar as f


def test_limpieza_quita_maquetacion_y_llamadas_a_nota():
    texto = "\n".join([
        "7.1 Los reclamos se resuelven de acuerdo con la normativa sobre la materia.1",
        "17",
        "Los Laureles Nº 214 - Lima 27 - Perú   Telf.: (511) 6309000",
        "____",
        "1 Disposición vigente a partir del 28.02.2023",
        "según el párrafo 10.1",
        "de su respuesta. 3",
    ])
    assert f.limpiar_renglones(texto) == [
        "7.1 Los reclamos se resuelven de acuerdo con la normativa sobre la materia.",
        "según el párrafo 10.1",
        "de su respuesta.",
    ]


def test_union_de_parrafos():
    renglones = [
        "Artículo 24.- Servicio de atención de",
        "reclamos",
        "24.1. Los provee-",
        "dores atienden en el plazo del Artículo",
        "152.1 del presente Código.",
        "a) primer literal",
    ]
    assert f.unir_parrafos(renglones, articulo="24") == (
        "Artículo 24.- Servicio de atención de reclamos\n"
        "24.1. Los proveedores atienden en el plazo del Artículo 152.1 del presente Código.\n"
        "a) primer literal"
    )


def test_articulos_corta_en_el_siguiente_y_acepta_encabezado_sin_palabra():
    renglones = [
        "Artículo 16. Reporte",
        "16.1 Texto del dieciséis.",
        "17. Absolución de consultas",
        "17.1 Texto del diecisiete.",
        "CAPITULO VII",
        "Artículo 18. Tratamiento proporcional",
        "18.1 Texto.",
    ]
    r = f.articulos(renglones, ("16", "17", "18"), guion=False, sin_palabra=("17",))
    assert r["16"] == "Artículo 16. Reporte\n16.1 Texto del dieciséis."
    assert r["17"] == "17. Absolución de consultas\n17.1 Texto del diecisiete."
    assert r["18"].startswith("Artículo 18. Tratamiento proporcional")


def test_articulo_inexistente_falla():
    with pytest.raises(ValueError):
        f.articulos(["Artículo 1. Uno"], ("2",), guion=False)


def test_referencia_no_es_encabezado():
    # "Artículo 83 del presente Código." no es un encabezado de artículo.
    r = f.articulos(["Artículo 82.- Uno", "texto según el", "Artículo 83 del presente Código.", "Artículo 83.- Dos"],
                    ("82",), guion=True)
    assert r["82"] == "Artículo 82.- Uno texto según el Artículo 83 del presente Código."


@pytest.mark.skipif(not all((f.RAW / x.archivo).exists() for x in f.FUENTES), reason="faltan los PDF: --descargar")
def test_regenerar_reproduce_el_corpus_versionado(tmp_path):
    f.generar(tmp_path)
    nuevo = hashlib.sha256((tmp_path / "fragmentos.jsonl").read_bytes()).hexdigest()
    versionado = json.loads((f.KNOWLEDGE / "manifest.json").read_text(encoding="utf-8"))["fragmentos_sha256"]
    assert nuevo == versionado
