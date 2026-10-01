"""Estructuras de datos del banco sintético."""
from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date, datetime
from decimal import Decimal

CAMPOS_OCR = ("monto", "fecha", "codigo")


@dataclass(frozen=True)
class Cliente:
    id: int
    alias: str
    dispositivo_registrado: str


@dataclass(frozen=True)
class Transaccion:
    codigo: str
    id_cliente: int
    monto: Decimal
    moneda: str  # PEN | USD
    fecha_hora: datetime
    comercio: str
    estado: str  # COMPLETADA | FALLIDA | NO_COMPLETADA
    autenticacion_reforzada: bool
    dispositivo: str | None
    indicador_riesgo: bool


@dataclass(frozen=True)
class DatosTexto:
    """Lo que la narración menciona de verdad (lo que extraería un clasificador perfecto)."""
    intencion: str  # C1 | C2 | C3 | FUERA_DE_CATALOGO
    monto: Decimal | None
    moneda: str | None
    fecha: date | None
    codigo: str | None


@dataclass(frozen=True)
class DatosVoucher:
    """Lo que el comprobante muestra impreso (verdad para evaluar el OCR)."""
    monto: Decimal
    moneda: str
    fecha_hora: datetime
    codigo: str
    campos_legibles: frozenset[str]


@dataclass
class Caso:
    id: int
    intencion: str  # estrato: C1 | C2 | C3
    escenario: str  # regla para la que se diseñó: R1..R9
    variante: str
    tx: Transaccion
    texto: DatosTexto
    voucher: DatosVoucher
    narracion: str = ""
    fecha_ingreso: datetime | None = None
    plantilla: str = ""
    perturbacion: str = ""
    ruta: str = ""
    regla: str = ""
    extra: dict = field(default_factory=dict)
