"""
Render de comprobantes JPG sintéticos (plantillas genéricas, banco ficticio, sin logos)
y perturbaciones: rotación leve, desenfoque, ruido gaussiano y compresión JPEG.
Las zonas ilegibles (R1) se destruyen localmente con desenfoque fuerte y una mancha.
"""
from __future__ import annotations

import io
from dataclasses import dataclass
from datetime import datetime
from decimal import Decimal
from functools import lru_cache
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

BANCO = "Banco Andino"
FUENTES = Path(__file__).resolve().parent.parent / "assets" / "fonts"

TINTA = (28, 28, 30)
GRIS = (110, 112, 118)
PAPEL = (246, 244, 236)
FONDO_FOTO = (176, 172, 164)
AZUL = (0, 82, 140)
ROJO = (178, 34, 34)

NIVELES = {
    #         rotación°      desenfoque px    sigma ruido   calidad JPEG
    "bajo":  ((0.3, 1.0), (0.0, 0.0), (3.0, 5.0), (85, 92)),
    "medio": ((1.0, 2.0), (0.5, 0.8), (8.0, 11.0), (62, 75)),
    "alto":  ((2.0, 3.0), (0.9, 1.2), (14.0, 18.0), (38, 50)),
}


@dataclass(frozen=True)
class ContenidoVoucher:
    plantilla: str  # comprobante | app
    titulo: str
    tipo_operacion: str
    comercio: str
    monto: Decimal
    moneda: str
    fecha_hora: datetime
    separador_fecha: str  # "/" | "-"
    codigo: str
    etiqueta_codigo: str  # "Operación" | "Cód. operación"
    estado: str
    cuenta_mascara: str
    alerta: bool = False  # banda roja (operación no completada)


@lru_cache(maxsize=None)
def fuente(tamano: int, negrita: bool = False) -> ImageFont.FreeTypeFont:
    nombre = "LiberationSans-Bold.ttf" if negrita else "LiberationSans-Regular.ttf"
    return ImageFont.truetype(str(FUENTES / nombre), tamano)


def formato_monto(monto: Decimal, moneda: str) -> str:
    return f"{'S/' if moneda == 'PEN' else 'US$'} {monto:,.2f}"


def formato_fecha(fh: datetime, sep: str) -> str:
    return fh.strftime(f"%d{sep}%m{sep}%Y")


Caja = tuple[int, int, int, int]


def _unir(a: Caja, b: Caja) -> Caja:
    return (min(a[0], b[0]), min(a[1], b[1]), max(a[2], b[2]), max(a[3], b[3]))


def _comprobante(c: ContenidoVoucher) -> tuple[Image.Image, dict[str, Caja]]:
    w, h = 560, 760
    img = Image.new("RGB", (w, h), PAPEL)
    d = ImageDraw.Draw(img)
    cajas: dict[str, Caja] = {}

    def centrado(y: int, texto: str, f: ImageFont.FreeTypeFont) -> None:
        d.text(((w - d.textlength(texto, font=f)) / 2, y), texto, font=f, fill=TINTA)

    def separador(y: int) -> None:
        for x in range(30, w - 30, 14):
            d.line((x, y, x + 7, y), fill=GRIS, width=2)

    centrado(34, BANCO.upper(), fuente(36, True))
    centrado(84, c.titulo.upper(), fuente(24, True))
    separador(128)

    y = 150
    etiquetas = fuente(22)
    valores = fuente(24)

    def fila(etiqueta: str, valor: str, negrita: bool = False) -> Caja:
        nonlocal y
        d.text((40, y), f"{etiqueta}:", font=etiquetas, fill=GRIS)
        f = fuente(26, True) if negrita else valores
        d.text((210, y - 1), valor, font=f, fill=TINTA)
        caja = d.textbbox((210, y - 1), valor, font=f)
        y += 48
        return caja

    fila("Comercio", c.comercio)
    fila("Tipo", c.tipo_operacion)
    fila("Cuenta", c.cuenta_mascara)
    caja_fecha = fila("Fecha", formato_fecha(c.fecha_hora, c.separador_fecha))
    caja_hora = fila("Hora", c.fecha_hora.strftime("%H:%M"))
    cajas["fecha"] = _unir(caja_fecha, caja_hora)
    cajas["monto"] = fila("Monto", formato_monto(c.monto, c.moneda), negrita=True)
    cajas["codigo"] = fila(c.etiqueta_codigo, c.codigo)
    fila("Estado", c.estado)

    separador(y + 8)
    centrado(y + 30, "Conserve este comprobante", fuente(20))
    centrado(y + 58, "Documento sin valor tributario", fuente(18))
    return img, cajas


def _app(c: ContenidoVoucher) -> tuple[Image.Image, dict[str, Caja]]:
    w, h = 540, 920
    img = Image.new("RGB", (w, h), (250, 251, 253))
    d = ImageDraw.Draw(img)
    cajas: dict[str, Caja] = {}

    d.rectangle((0, 0, w, 120), fill=AZUL)
    d.text((32, 30), BANCO, font=fuente(34, True), fill=(255, 255, 255))
    d.text((32, 78), "Banca móvil", font=fuente(20), fill=(210, 225, 240))

    banda = ROJO if c.alerta else (232, 240, 248)
    d.rectangle((0, 120, w, 186), fill=banda)
    d.text((32, 138), c.titulo, font=fuente(26, True), fill=(255, 255, 255) if c.alerta else AZUL)

    monto = formato_monto(c.monto, c.moneda)
    f_monto = fuente(46, True)
    x_monto = (w - d.textlength(monto, font=f_monto)) / 2
    d.text((x_monto, 222), monto, font=f_monto, fill=TINTA)
    cajas["monto"] = d.textbbox((x_monto, 222), monto, font=f_monto)
    d.text(((w - d.textlength(c.tipo_operacion, font=fuente(20))) / 2, 286),
           c.tipo_operacion, font=fuente(20), fill=GRIS)

    y = 350
    etiqueta = fuente(21)
    valor = fuente(23)

    def fila(k: str, v: str) -> Caja:
        nonlocal y
        d.text((32, y), k, font=etiqueta, fill=GRIS)
        x = w - 32 - d.textlength(v, font=valor)
        d.text((x, y), v, font=valor, fill=TINTA)
        caja = d.textbbox((x, y), v, font=valor)
        d.line((32, y + 44, w - 32, y + 44), fill=(222, 226, 232), width=1)
        y += 62
        return caja

    fila("Comercio / destino", c.comercio if len(c.comercio) <= 24 else c.comercio[:23] + "…")
    fila("Cuenta de origen", c.cuenta_mascara)
    caja_fecha = fila("Fecha", formato_fecha(c.fecha_hora, c.separador_fecha))
    caja_hora = fila("Hora", c.fecha_hora.strftime("%H:%M"))
    cajas["fecha"] = _unir(caja_fecha, caja_hora)
    cajas["codigo"] = fila(c.etiqueta_codigo, c.codigo)
    fila("Estado", c.estado)

    d.rounded_rectangle((32, h - 110, w - 32, h - 50), radius=28, outline=AZUL, width=2)
    txt = "Compartir constancia"
    d.text(((w - d.textlength(txt, font=fuente(22))) / 2, h - 94), txt, font=fuente(22), fill=AZUL)
    return img, cajas


def render(c: ContenidoVoucher) -> tuple[Image.Image, dict[str, Caja]]:
    return _comprobante(c) if c.plantilla == "comprobante" else _app(c)


def _destruir_zona(img: Image.Image, caja: Caja, rng: np.random.Generator) -> None:
    """Vuelve ilegible un campo: desenfoque fuerte + mancha semitransparente."""
    x0, y0, x1, y1 = (round(v) for v in caja)
    x0, y0, x1, y1 = x0 - 10, y0 - 10, x1 + 10, y1 + 10
    zona = img.crop((x0, y0, x1, y1)).filter(ImageFilter.GaussianBlur(radius=9))
    img.paste(zona, (x0, y0))

    capa = Image.new("RGBA", img.size, (0, 0, 0, 0))
    dc = ImageDraw.Draw(capa)
    cx = (x0 + x1) / 2 + float(rng.uniform(-15, 15))
    cy = (y0 + y1) / 2 + float(rng.uniform(-4, 4))
    rx = (x1 - x0) / 2 * float(rng.uniform(0.85, 1.05))
    ry = (y1 - y0) / 2 * float(rng.uniform(1.1, 1.5))
    dc.ellipse((cx - rx, cy - ry, cx + rx, cy + ry), fill=(120, 90, 50, 150))
    capa = capa.filter(ImageFilter.GaussianBlur(radius=6))
    img.paste(Image.alpha_composite(img.convert("RGBA"), capa).convert("RGB"))


def perturbar(
    img: Image.Image,
    cajas: dict[str, Caja],
    nivel: str,
    ilegibles: tuple[str, ...],
    rng: np.random.Generator,
) -> tuple[bytes, str]:
    """Aplica el ruido del nivel y devuelve (bytes JPEG, descripción de la perturbación)."""
    (r0, r1), (b0, b1), (s0, s1), (q0, q1) = NIVELES[nivel]
    angulo = round(float(rng.uniform(r0, r1)) * (1 if rng.random() < 0.5 else -1), 2)
    blur = round(float(rng.uniform(b0, b1)), 2)
    sigma = round(float(rng.uniform(s0, s1)), 1)
    calidad = int(rng.integers(q0, q1 + 1))
    ganancia = round(float(rng.uniform(0.9, 1.04)), 3)

    img = img.copy()
    for campo in ilegibles:
        _destruir_zona(img, cajas[campo], rng)

    img = img.rotate(angulo, resample=Image.Resampling.BICUBIC, expand=True, fillcolor=FONDO_FOTO)
    if blur > 0:
        img = img.filter(ImageFilter.GaussianBlur(radius=blur))

    arr = np.asarray(img, dtype=np.float32) * ganancia
    arr += rng.normal(0.0, sigma, size=arr.shape).astype(np.float32)
    img = Image.fromarray(np.clip(np.rint(arr), 0, 255).astype(np.uint8))

    buf = io.BytesIO()
    img.save(buf, format="JPEG", quality=calidad, optimize=False)

    desc = (f"nivel={nivel};rot={angulo};blur={blur};sigma={sigma};jpeg={calidad};gain={ganancia}"
            f";ilegible={','.join(ilegibles) or '-'}")
    return buf.getvalue(), desc
