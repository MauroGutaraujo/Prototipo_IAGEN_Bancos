"""
Fragmenta la normativa por artículo → knowledge/fragmentos.jsonl (+ knowledge/manifest.json).

    python fragmentar.py --descargar      # descarga los PDF oficiales y verifica su SHA-256
    python fragmentar.py                  # extrae, limpia y fragmenta (determinista)

Las normas oficiales se toman LITERALMENTE de los PDF publicados por la fuente oficial (URL y
SHA-256 fijados abajo). La limpieza solo quita ruido de maquetación (números de página, pies de
página, llamadas y notas al pie, saltos de línea); no reescribe el contenido. La política interna
es simulada (banco ficticio) y está declarada como tal.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

import pypdf

RAIZ = Path(__file__).resolve().parents[2]
KNOWLEDGE = RAIZ / "knowledge"
RAW = KNOWLEDGE / "raw"


@dataclass(frozen=True)
class Fuente:
    id: str
    nombre: str
    tipo: str  # oficial | simulada
    archivo: str  # dentro de knowledge/raw
    sha256: str
    vigencia: str
    url: str = ""
    articulos: tuple[str, ...] = field(default_factory=tuple)  # vacío = todos
    # Artículos cuyo encabezado en el PDF oficial no incluye la palabra "Artículo".
    sin_palabra: tuple[str, ...] = field(default_factory=tuple)


def _rango(a: int, b: int) -> tuple[str, ...]:
    return tuple(str(i) for i in range(a, b + 1))


FUENTES = (
    Fuente(
        id="sbs-04036-2022",
        nombre="Res. SBS N.° 04036-2022 - Reglamento de Gestión de Reclamos y Requerimientos",
        tipo="oficial",
        archivo="sbs_04036_2022.pdf",
        sha256="594e312f22500ea18d89214856cc863a8fbf9421328632930bb7f6576011e31d",
        url="https://intranet2.sbs.gob.pe/dv_int_cn/2230/v1.0/Adjuntos/4036-2022.r.pdf",
        vigencia="Texto publicado por la SBS; según sus notas, disposiciones vigentes a partir del 28.02.2023",
        articulos=_rango(1, 19),
        sin_palabra=("17",),  # el PDF publica "17. Absolución de consultas…" sin "Artículo"
    ),
    Fuente(
        id="ley-29571",
        nombre="Ley N.° 29571 - Código de Protección y Defensa del Consumidor",
        tipo="oficial",
        archivo="ley_29571.pdf",
        sha256="b12fb8007bd246d8a9feb38015e15cc5c767706aeaa803f773ff990676682918",
        url=("https://cdn.www.gob.pe/uploads/document/file/4265044/"
             "Co%CC%81digo%20de%20Proteccio%CC%81n%20y%20Defensa%20del%20Consumidor%20-%202023%20(1).pdf.pdf"),
        vigencia="Texto actualizado publicado por Indecopi en gob.pe (edición 2023)",
        # Idoneidad, atención de reclamos, productos o servicios financieros y libro de reclamaciones.
        articulos=("18", "19", "24", *_rango(81, 90), "90-A", *_rango(91, 96), *_rango(150, 152)),
    ),
    Fuente(
        id="ley-31435",
        nombre="Ley N.° 31435 - Ley que modifica la Ley 29571 reduciendo el plazo de atención de reclamos",
        tipo="oficial",
        archivo="ley_31435.pdf",
        sha256="54f1cdc95f17a26e0ef78e1a522b362728e8d64a3feec329c40af09468ce0634",
        url="https://leyes.congreso.gob.pe/Documentos/2021_2026/ADLP/Texto_Consolidado/31435-TXM.pdf",
        vigencia="Publicada el 22.03.2022 en El Peruano; vigente a los 60 días calendario de su publicación",
        articulos=("unico",),
    ),
    Fuente(
        id="pir-2026-v1",
        nombre="Política interna PIR-2026-V1 del Banco Andino (POLÍTICA SIMULADA, banco ficticio)",
        tipo="simulada",
        archivo="politicas_banco_simulado.txt",
        sha256="",  # se calcula del archivo versionado en el repo
        vigencia="Simulada para el prototipo de tesis",
    ),
)

# ---------------------------------------------------------------- limpieza

_RUIDO = [
    re.compile(r"^\s*\d{1,3}\s*$"),                                   # número de página
    re.compile(r"^\s*_+\s*$"),                                        # separador de notas
    re.compile(r"Los Laureles N"),                                    # pie de página SBS
    re.compile(r"^\s*\d{1,2}\s+(Disposición|Párrafo|Artículo|Numeral|Literal|Modificad|Incorporad|Sustituid|Derogad)"),
]
# Llamada a nota al pie al final del renglón: "...materia.1", "...respuesta. 3".
_LLAMADA = re.compile(r"(?<=[A-Za-zÁÉÍÓÚáéíóúñÑ\)])([.;:])\s?\d{1,2}\s*$")
_NUMERAL = re.compile(r"^(\d+\.\d+\.?|\d+\.|[a-z]\)|[a-z]\.)\s")
_ESTRUCTURA = re.compile(r"^\s*(CAP[IÍ]TULO|Cap[ií]tulo|Subcap[ií]tulo|T[IÍ]TULO|T[ií]tulo|DISPOSICIONES|Disposiciones)\b")


def limpiar_renglones(texto: str) -> list[str]:
    """Quita ruido de maquetación renglón por renglón (no toca el contenido)."""
    salida = []
    for r in texto.replace("\r", "").split("\n"):
        if any(p.search(r) for p in _RUIDO):
            continue
        r = _LLAMADA.sub(r"\1", r.rstrip())
        if r.strip():
            salida.append(r.strip())
    return salida


def _abre_parrafo(renglon: str, articulo: str | None) -> bool:
    """Un numeral abre párrafo; "24.1" solo si es del propio artículo (si no, es una referencia partida)."""
    m = _NUMERAL.match(renglon)
    if not m:
        return False
    if articulo and re.match(r"^\d+\.\d+", m.group(1)):
        return m.group(1).startswith(f"{articulo}.")
    return True


def unir_parrafos(renglones: list[str], articulo: str | None = None) -> str:
    """Une renglones partidos; cada numeral (24.1., a), 1.) empieza un párrafo nuevo."""
    parrafos: list[str] = []
    for r in renglones:
        if parrafos and not _abre_parrafo(r, articulo):
            previo = parrafos[-1]
            if previo.endswith("-") and r[:1].islower():
                parrafos[-1] = previo[:-1] + r
            else:
                parrafos[-1] = previo + " " + r
        else:
            parrafos.append(r)
    texto = "\n".join(parrafos)
    texto = re.sub(r"[ \t]{2,}", " ", texto)
    return re.sub(r" ([,.;:)])", r"\1", texto).strip()


# ---------------------------------------------------------------- extracción por artículo

def _encabezado(numero: str, guion: bool, sin_palabra: bool = False) -> re.Pattern[str]:
    sep = r"(?:\.-|\.)" if guion else r"\."
    palabra = r"(?:Artículo\s+)?" if sin_palabra else r"Artículo\s+"
    return re.compile(rf"^{palabra}{re.escape(numero)}{sep}[ \t]+(?=[A-ZÁÉÍÓÚÑ])")


_CUALQUIER_ARTICULO = re.compile(r"^Artículo\s+\d+(?:-[A-Z])?(?:\.-|\.)[ \t]+[A-ZÁÉÍÓÚÑ]")


def articulos(renglones: list[str], numeros: tuple[str, ...], guion: bool, desde: int = 0,
              sin_palabra: tuple[str, ...] = ()) -> dict[str, str]:
    """Texto de cada artículo pedido: desde su encabezado hasta el siguiente artículo o título/capítulo."""
    excepciones = [_encabezado(n, guion, sin_palabra=True) for n in sin_palabra]

    def es_corte(r: str) -> bool:
        return bool(_CUALQUIER_ARTICULO.match(r) or _ESTRUCTURA.match(r) or any(p.match(r) for p in excepciones))

    resultado: dict[str, str] = {}
    for numero in numeros:
        patron = _encabezado(numero, guion, sin_palabra=numero in sin_palabra)
        inicio = next((i for i in range(desde, len(renglones)) if patron.match(renglones[i])), None)
        if inicio is None:
            raise ValueError(f"No se encontró el artículo {numero}")
        fin = next((j for j in range(inicio + 1, len(renglones)) if es_corte(renglones[j])), len(renglones))
        resultado[numero] = unir_parrafos(renglones[inicio:fin], articulo=numero)
    return resultado


def texto_pdf(ruta: Path) -> str:
    return "\n".join(p.extract_text() or "" for p in pypdf.PdfReader(ruta).pages)


def sha256(ruta: Path) -> str:
    return hashlib.sha256(ruta.read_bytes()).hexdigest()


def fragmentar_fuente(f: Fuente) -> dict[str, str]:
    ruta = RAW / f.archivo
    if f.id == "pir-2026-v1":
        renglones = limpiar_renglones(ruta.read_text(encoding="utf-8"))
        numeros = tuple(m.group(1) for r in renglones if (m := re.match(r"^Artículo\s+(\d+)\.\s", r)))
        return articulos(renglones, numeros, guion=False)

    if sha256(ruta) != f.sha256:
        raise ValueError(f"SHA-256 distinto en {ruta.name}: vuelve a descargar con --descargar")
    renglones = limpiar_renglones(texto_pdf(ruta))

    if f.id == "sbs-04036-2022":
        desde = next(i for i, r in enumerate(renglones) if r.lstrip("“\"").startswith("REGLAMENTO DE GESTIÓN DE RECLAMOS"))
        return articulos(renglones, f.articulos, guion=False, desde=desde, sin_palabra=f.sin_palabra)
    if f.id == "ley-29571":
        return articulos(renglones, f.articulos, guion=True)
    if f.id == "ley-31435":
        inicio = next(i for i, r in enumerate(renglones) if r.startswith("Artículo único."))
        fin = next(i for i, r in enumerate(renglones) if r.startswith("DISPOSICIONES COMPLEMENTARIAS"))
        # El PDF del Congreso codifica la elipsis "[…]" como "[?]".
        return {"unico": unir_parrafos(renglones[inicio:fin]).replace("[?]", "[…]")}
    raise ValueError(f"Fuente sin extractor: {f.id}")


def generar(destino: Path = KNOWLEDGE) -> Path:
    fragmentos = []
    fuentes_manifiesto = []
    for f in FUENTES:
        ruta = RAW / f.archivo
        digest = sha256(ruta)
        fuentes_manifiesto.append({"id": f.id, "tipo": f.tipo, "archivo": f.archivo, "sha256": digest, "url": f.url})
        for numero, texto in fragmentar_fuente(f).items():
            fragmentos.append({
                "id": f"{f.id}-art-{numero.lower()}",
                "norma": f.nombre,
                "articulo": numero,
                "tipo": f.tipo,
                "vigencia": f.vigencia,
                "fuente": f.url or f"knowledge/raw/{f.archivo}",
                "texto": texto,
            })

    salida = destino / "fragmentos.jsonl"
    with salida.open("w", encoding="utf-8", newline="\n") as fh:
        for fr in fragmentos:
            fh.write(json.dumps(fr, ensure_ascii=False) + "\n")

    manifiesto = {
        "fragmentos": "fragmentos.jsonl",
        "fragmentos_sha256": sha256(salida),
        "n_fragmentos": len(fragmentos),
        "pypdf": pypdf.__version__,
        "fuentes": fuentes_manifiesto,
    }
    (destino / "manifest.json").write_text(
        json.dumps(manifiesto, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return salida


def descargar() -> None:
    RAW.mkdir(parents=True, exist_ok=True)
    for f in FUENTES:
        if not f.url:
            continue
        ruta = RAW / f.archivo
        if not ruta.exists() or sha256(ruta) != f.sha256:
            req = urllib.request.Request(f.url, headers={"User-Agent": "Mozilla/5.0"})
            with urllib.request.urlopen(req, timeout=120) as r:
                ruta.write_bytes(r.read())
        if sha256(ruta) != f.sha256:
            raise SystemExit(f"{f.archivo}: el SHA-256 descargado no coincide con el fijado (¿cambió la publicación?)")
        print(f"OK {f.archivo}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--descargar", action="store_true", help="descarga y verifica los PDF oficiales")
    args = ap.parse_args()
    if args.descargar:
        descargar()
        return 0
    salida = generar()
    manifiesto = json.loads((KNOWLEDGE / "manifest.json").read_text(encoding="utf-8"))
    print(f"{salida}: {manifiesto['n_fragmentos']} fragmentos, SHA-256 {manifiesto['fragmentos_sha256']}")
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main())
