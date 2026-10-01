"""
Reglas R1–R9 (SPEC §2.3) aplicadas sobre los DATOS VERDADEROS para obtener
RutaCorrecta y ReglaEsperada. Para R1 se usa la legibilidad diseñada, no el OCR.

Convenciones (deben coincidir con el Agente Legal de H3):
- R2 compara monto (2 decimales, misma moneda), fecha (día) y código (mayúsculas, sin espacios)
  del texto y del voucher contra la transacción del core. Un dato ausente en el texto no se compara.
- R3: monto en soles > 1000.00; USD × TipoCambio del día de la transacción.
- R5: otro cargo del mismo cliente, igual monto, moneda y comercio, |Δt| ≤ 24 h.
- R7: C3 COMPLETADA (o sin cargo) ⇒ Derivar por conciliación.
- R8: C2 sin autenticación reforzada, con riesgo o con dispositivo no registrado ⇒ Derivar.
"""
from __future__ import annotations

from datetime import date, timedelta
from decimal import Decimal

from .modelo import CAMPOS_OCR, DatosTexto, DatosVoucher, Transaccion

UMBRAL_RIESGO_PEN = Decimal("1000.00")
VENTANA_DUPLICADO = timedelta(hours=24)


def normalizar_codigo(codigo: str) -> str:
    return "".join(codigo.split()).upper()


def monto_en_soles(tx: Transaccion, tipo_cambio: dict[date, Decimal]) -> Decimal:
    if tx.moneda == "PEN":
        return tx.monto
    return tx.monto * tipo_cambio[tx.fecha_hora.date()]


def hay_discrepancia(texto: DatosTexto, voucher: DatosVoucher, tx: Transaccion) -> bool:
    if texto.monto is not None and (texto.monto != tx.monto or texto.moneda != tx.moneda):
        return True
    if texto.fecha is not None and texto.fecha != tx.fecha_hora.date():
        return True
    if texto.codigo is not None and normalizar_codigo(texto.codigo) != normalizar_codigo(tx.codigo):
        return True
    if voucher.monto != tx.monto or voucher.moneda != tx.moneda:
        return True
    if voucher.fecha_hora.date() != tx.fecha_hora.date():
        return True
    return normalizar_codigo(voucher.codigo) != normalizar_codigo(tx.codigo)


def existe_duplicado(tx: Transaccion, core: list[Transaccion]) -> bool:
    return any(
        o.codigo != tx.codigo
        and o.id_cliente == tx.id_cliente
        and o.monto == tx.monto
        and o.moneda == tx.moneda
        and o.comercio == tx.comercio
        and abs(o.fecha_hora - tx.fecha_hora) <= VENTANA_DUPLICADO
        for o in core
    )


def evaluar(
    texto: DatosTexto,
    voucher: DatosVoucher,
    tx: Transaccion,
    core: list[Transaccion],
    tipo_cambio: dict[date, Decimal],
    dispositivo_registrado: str,
) -> tuple[str, str]:
    """Devuelve (ruta, regla). La primera regla que dispara define la ruta."""
    if any(c not in voucher.campos_legibles for c in CAMPOS_OCR):
        return "Derivar", "R1"
    if hay_discrepancia(texto, voucher, tx):
        return "Derivar", "R2"
    if monto_en_soles(tx, tipo_cambio) > UMBRAL_RIESGO_PEN:
        return "Derivar", "R3"
    if texto.intencion not in ("C1", "C2", "C3"):
        return "Derivar", "R4"

    if texto.intencion == "C1":
        return ("Procedente", "R5") if existe_duplicado(tx, core) else ("Improcedente", "R6")

    if texto.intencion == "C3":
        if tx.estado in ("FALLIDA", "NO_COMPLETADA") and tx.monto > 0:
            return "Procedente", "R7"
        return "Derivar", "R7"  # conciliación

    # C2
    if not tx.autenticacion_reforzada or tx.indicador_riesgo or tx.dispositivo != dispositivo_registrado:
        return "Derivar", "R8"
    return "Improcedente", "R9"
