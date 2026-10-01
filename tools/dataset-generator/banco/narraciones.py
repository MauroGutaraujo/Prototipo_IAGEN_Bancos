"""
Narraciones sintéticas en español coloquial, con errores ortográficos y omisiones.
Los datos que se mencionan (monto, fecha, código) se escriben exactamente como en
DatosTexto; los errores ortográficos nunca tocan esos valores.
"""
from __future__ import annotations

import random
from datetime import date
from decimal import Decimal

MESES = ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio",
         "agosto", "setiembre", "octubre", "noviembre", "diciembre"]

C1 = [
    "Buenas, revisando mi estado de cuenta veo que {comercio} me cobro dos veces la misma compra{monto}{fecha}{codigo}. Solo hice una compra, pido que me devuelvan el cobro repetido.",
    "Hola, me aparecen dos cargos iguales de {comercio}{monto}{fecha}. Yo pague una sola vez{codigo}, quiero que lo revisen por favor.",
    "Reclamo por cobro duplicado. Pague en {comercio}{fecha}{monto} y el banco me desconto 2 veces{codigo}. Solicito la devolucion.",
    "Que tal, en mis movimientos sale doble el pago a {comercio}{monto}{codigo}{fecha}. No autorice dos pagos, necesito que me extornen uno.",
    "Me cobraron doble!! compre en {comercio}{fecha}{monto} y aparece el mismo cargo dos veces{codigo}. Espero su pronta respuesta.",
    "Buenas tardes, al pagar en {comercio} el pos se demoro y despues vi que me cargaron dos veces{monto}{fecha}{codigo}. Pido regularizar.",
]

C2 = [
    "Hola, en mi cuenta aparece una operacion que yo no hice{comercio_a}{monto}{fecha}{codigo}. No reconozco ese movimiento, pido que lo investiguen.",
    "Buenas, me acaban de sacar plata de mi cuenta sin mi autorizacion{monto}{fecha}{codigo}. Figura como {comercio}. Yo no hice eso.",
    "Desconozco un cargo de {comercio}{monto}{fecha}{codigo}. Nunca he comprado ahi y tengo mi tarjeta conmigo, solicito la devolucion.",
    "Reclamo por operacion no reconocida{fecha}: {comercio}{monto}{codigo}. No di mis claves a nadie, quiero saber que paso.",
    "Hola, revisando la app vi un movimiento a {comercio}{monto}{codigo} que no reconozco{fecha}. Porfavor bloqueen y devuelvan.",
    "Me llego una notificacion de un pago{monto} a {comercio}{fecha} que yo no realice{codigo}. Necesito que me ayuden urgente.",
]

C3 = [
    "Hola, intente pagar en {comercio}{monto}{fecha} y la app me salio error, pero el dinero si se desconto de mi cuenta{codigo}. Quiero que me devuelvan.",
    "Buenas, hice un pago{monto} a {comercio}{fecha} y la operacion no se completo, igual me cobraron{codigo}. Solicito devolucion.",
    "Se cayo la pasarela cuando pagaba en {comercio}{codigo}{monto}. El comercio dice que no le llego el pago pero a mi si me descontaron{fecha}.",
    "Reclamo: el pago a {comercio}{fecha} quedo como no completado pero se me debito{monto}{codigo}. Espero la devolucion de mi dinero.",
    "Pague con la app en {comercio}{monto}, se colgo y salio operacion fallida{fecha}{codigo}. Ya me descontaron y no tengo el producto.",
]

# R4: narración que no permite decidir la tipología (debe clasificarse FUERA_DE_CATALOGO).
AMBIGUAS = [
    "Hola, tengo un problema con un movimiento de mi cuenta{fecha}{monto}. No entiendo bien que paso, necesito que alguien me llame.",
    "Buenas, quiero informacion sobre una operacion{codigo}{monto}. Algo raro hay ahi, revisen porfa.",
    "Hola, no estoy conforme con algo que salio en mi cuenta{fecha}{codigo}. Quiero hablar con un asesor.",
]

PLANTILLAS = {"C1": C1, "C2": C2, "C3": C3, "AMBIGUA": AMBIGUAS}


def texto_monto(monto: Decimal, moneda: str, rng: random.Random) -> str:
    base = f"{monto:.2f}"
    if moneda == "USD":
        return rng.choice([f"US$ {base}", f"{base} dolares", f"$ {base} dolares"])
    return rng.choice([f"S/ {base}", f"S/{base}", f"{base} soles", f"s/. {base}"])


def texto_fecha(f: date, rng: random.Random) -> str:
    return rng.choice([
        f.strftime("%d/%m/%Y"),
        f"{f.day} de {MESES[f.month - 1]} de {f.year}",
        f"{f.day} de {MESES[f.month - 1]} del {f.year}",
    ])


def _conectores(monto: str | None, fecha: str | None, codigo: str | None, rng: random.Random) -> dict[str, str]:
    return {
        "monto": f" por {monto}" if monto else "",
        "fecha": f" el {fecha}" if fecha else "",
        "codigo": (f", {rng.choice(['nro de operacion', 'cod. operacion', 'codigo de operacion', 'operacion'])} {codigo}"
                   if codigo else ""),
    }


_ERRATAS = {"que": "q", "por favor": "porfa", "tambien": "tb", "porque": "xq", "para": "pa"}


def _con_erratas(texto: str, protegidos: list[str], rng: random.Random) -> str:
    """Errores de tipeo en palabras comunes; no toca montos, fechas ni códigos."""
    tokens_protegidos = {t for x in protegidos for t in x.split()}
    palabras = texto.split(" ")
    salida = []
    for p in palabras:
        if p.strip(".,:;!") in tokens_protegidos or not p.isalpha() or len(p) < 5:
            salida.append(p)
            continue
        r = rng.random()
        if r < 0.05:
            i = rng.randrange(1, len(p) - 1)
            p = p[:i] + p[i + 1] + p[i] + p[i + 2:]  # transposición
        elif r < 0.08:
            i = rng.randrange(1, len(p))
            p = p[:i] + p[i + 1:]  # omisión
        salida.append(p)
    texto = " ".join(salida)
    for correcto, errado in _ERRATAS.items():
        if rng.random() < 0.3:
            texto = texto.replace(f" {correcto} ", f" {errado} ")
    if rng.random() < 0.4:
        texto = texto[0].lower() + texto[1:]
    return texto


def redactar(
    tipo: str,
    comercio: str,
    monto: Decimal | None,
    moneda: str | None,
    fecha: date | None,
    codigo: str | None,
    rng: random.Random,
) -> tuple[str, set[str]]:
    """Devuelve la narración y los datos que realmente quedaron en ella (monto, fecha, codigo)."""
    plantilla = rng.choice(PLANTILLAS[tipo])
    s_monto = texto_monto(monto, moneda, rng) if monto is not None and moneda else None
    s_fecha = texto_fecha(fecha, rng) if fecha is not None else None
    campos = _conectores(s_monto, s_fecha, codigo, rng)
    texto = plantilla.format(comercio=comercio, comercio_a=f" a {comercio}", **campos)
    incluidos = {k for k, v in (("monto", s_monto), ("fecha", s_fecha), ("codigo", codigo))
                 if v is not None and "{" + k + "}" in plantilla}
    return _con_erratas(texto, [s_monto or "", s_fecha or "", codigo or ""], rng), incluidos
