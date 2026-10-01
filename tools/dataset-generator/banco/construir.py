"""
Construye el banco sintético completo a partir de una semilla.

Cada caso se diseña para una regla (escenario) y luego se verifica que las reglas
R1–R9, aplicadas sobre los datos verdaderos, lleguen exactamente a esa regla.
"""
from __future__ import annotations

import random
from dataclasses import dataclass, replace
from datetime import date, datetime, timedelta
from decimal import ROUND_HALF_UP, Decimal

from . import narraciones, reglas
from .modelo import CAMPOS_OCR, Caso, Cliente, DatosTexto, DatosVoucher, Transaccion

# (escenario = regla objetivo, variante, cantidad). Suma: 80 C1 + 80 C2 + 40 C3.
PLAN: dict[str, list[tuple[str, str, int]]] = {
    "C1": [
        ("R5", "normal", 25), ("R5", "usd", 2), ("R5", "borde_24h", 1),
        ("R6", "sin_otro", 16), ("R6", "senuelo_fuera_ventana", 6),
        ("R6", "senuelo_otro_monto", 5), ("R6", "borde_1000", 1),
        ("R3", "pen", 6), ("R3", "usd", 2),
        ("R2", "monto_texto", 3), ("R2", "codigo_voucher", 2), ("R2", "fecha_voucher", 2),
        ("R1", "ilegible", 7),
        ("R4", "ambigua", 2),
    ],
    "C2": [
        ("R8", "sin_auth", 12), ("R8", "riesgo", 10), ("R8", "sin_auth_riesgo", 6),
        ("R9", "normal", 26), ("R9", "usd", 2),
        ("R3", "pen", 6), ("R3", "usd", 2),
        ("R2", "monto_texto", 3), ("R2", "codigo_voucher", 2), ("R2", "fecha_voucher", 2),
        ("R1", "ilegible", 7),
        ("R4", "ambigua", 2),
    ],
    "C3": [
        ("R7", "fallida", 12), ("R7", "no_completada", 12), ("R7", "usd", 2),
        ("R3", "pen", 3), ("R3", "usd", 1),
        ("R2", "monto_texto", 2), ("R2", "codigo_voucher", 1), ("R2", "fecha_voucher", 1),
        ("R1", "ilegible", 4),
        ("R4", "ambigua", 2),
    ],
}
COMPOSICION = {"C1": 80, "C2": 80, "C3": 40}

# Nombres genéricos inventados; ninguno corresponde a una marca real a propósito.
COMERCIOS = {
    "C1": ["Minimarket La Esquina", "Pollería Don Tito", "Farmacia Vida Sana", "Grifo Ruta Norte",
           "Librería El Estudiante", "Ferretería El Tornillo", "Súper Buen Precio",
           "Cafetería Aroma Andino", "Zapatería Paso Firme", "Panadería Trigo de Oro"],
    "C2": ["Tienda MegaCompras Web", "Transf. Banco del Valle", "Recargas Express",
           "Juegos Digitales Play", "Electro Hogar Online", "Viajes Pronto Web", "Apuestas Rápidas Web"],
    "C3": ["Pasarela PagoFácil", "Luz del Norte Servicios", "Cine Estrella Online",
           "Delivery Rápido App", "Universidad Virtual", "Telefonía MóvilNet"],
    "otros": ["Estacionamiento Centro", "Peaje Vía Norte", "Taxi App Viajes",
              "Gimnasio Fuerza Total", "Óptica Visión Clara", "Mercado Central"],
}
TIPO_OPERACION = {
    "C1": ["Compra con tarjeta de débito", "Compra con tarjeta de crédito", "Pago con QR"],
    "C2": ["Compra por internet", "Pago con QR"],
    "C3": ["Pago en línea", "Pago de servicio"],
}

INICIO = datetime(2026, 1, 5)
FIN = datetime(2026, 8, 31)
N_CLIENTES_SIN_RECLAMO = 30
ALFABETO_CODIGO = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ"
CENT = Decimal("0.01")


@dataclass
class Banco:
    seed: int
    clientes: list[Cliente]
    tipo_cambio: dict[date, Decimal]
    transacciones: list[Transaccion]
    casos: list[Caso]


def _d(x: float) -> Decimal:
    return Decimal(str(x)).quantize(CENT, rounding=ROUND_HALF_UP)


class _Generador:
    def __init__(self, seed: int) -> None:
        self.seed = seed
        self.rng = random.Random(seed)
        self.codigos: set[str] = set()
        self.dispositivos: set[str] = set()

    # ---------- utilitarios ----------
    def codigo(self) -> str:
        while True:
            c = "AN" + "".join(self.rng.choice(ALFABETO_CODIGO) for _ in range(8))
            if c not in self.codigos:
                self.codigos.add(c)
                return c

    def codigo_alterado(self, original: str) -> str:
        while True:
            c = original[:-2] + "".join(self.rng.choice(ALFABETO_CODIGO) for _ in range(2))
            if c != original and c not in self.codigos:
                return c

    def dispositivo(self) -> str:
        while True:
            d = f"DEV-{self.rng.getrandbits(24):06X}"
            if d not in self.dispositivos:
                self.dispositivos.add(d)
                return d

    def fecha_hora(self) -> datetime:
        dias = self.rng.randrange((FIN - INICIO).days + 1)
        minutos = self.rng.randrange(7 * 60, 23 * 60 + 30)
        return INICIO + timedelta(days=dias, minutes=minutos)

    def monto(self, moneda: str, alto: bool) -> Decimal:
        if moneda == "PEN":
            lo, hi = (1050, 5000) if alto else (8, 950)
        else:
            lo, hi = (300, 1300) if alto else (5, 220)
        valor = lo * (hi / lo) ** self.rng.random()  # log-uniforme
        if self.rng.random() < 0.3:
            valor = round(valor)
        return _d(valor)

    def tipo_cambio(self) -> dict[date, Decimal]:
        tc, valor = {}, 3.72
        d = date(2026, 1, 1)
        while d <= date(2026, 9, 30):
            valor = min(3.85, max(3.60, valor + self.rng.uniform(-0.008, 0.008)))
            tc[d] = Decimal(str(round(valor, 4)))
            d += timedelta(days=1)
        return tc

    # ---------- construcción ----------
    def construir(self) -> Banco:
        tc = self.tipo_cambio()

        plan = [(c, esc, var) for c, filas in PLAN.items() for esc, var, n in filas for _ in range(n)]
        self.rng.shuffle(plan)

        n_clientes = len(plan) + N_CLIENTES_SIN_RECLAMO
        ids_clientes = self.rng.sample(range(1, n_clientes + 1), n_clientes)
        clientes = {i: Cliente(i, f"Cliente S-{i:04d}", self.dispositivo()) for i in range(1, n_clientes + 1)}

        core: list[Transaccion] = []
        casos: list[Caso] = []
        for idx, (intencion, escenario, variante) in enumerate(plan):
            cliente = clientes[ids_clientes[idx]]
            tx, extras = self._transaccion(intencion, escenario, variante, cliente)
            core.append(tx)
            core.extend(extras)
            texto = self._texto(intencion, escenario, variante, tx)
            voucher = self._voucher(escenario, variante, tx)
            casos.append(Caso(idx + 1, intencion, escenario, variante, tx, texto, voucher))

        for cliente in clientes.values():  # movimientos de relleno sin relación con los reclamos
            for _ in range(self.rng.randrange(0, 4)):
                core.append(self._relleno(cliente))

        for caso in casos:
            self._completar(caso)
            cliente = clientes[caso.tx.id_cliente]
            ruta, regla = reglas.evaluar(caso.texto, caso.voucher, caso.tx, core, tc, cliente.dispositivo_registrado)
            if regla != caso.escenario:
                raise AssertionError(f"Caso {caso.id} diseñado para {caso.escenario} pero dispara {regla}")
            caso.ruta, caso.regla = ruta, regla

        core.sort(key=lambda t: (t.fecha_hora, t.codigo))
        return Banco(self.seed, sorted(clientes.values(), key=lambda c: c.id), tc, core, casos)

    def _transaccion(self, intencion: str, escenario: str, variante: str,
                     cliente: Cliente) -> tuple[Transaccion, list[Transaccion]]:
        rng = self.rng
        if "usd" in variante:
            moneda = "USD"
        elif variante == "borde_1000" or escenario in ("R5", "R9", "R7"):
            moneda = "PEN"
        else:
            moneda = "USD" if rng.random() < 0.08 else "PEN"

        monto = Decimal("1000.00") if variante == "borde_1000" else self.monto(moneda, alto=escenario == "R3")
        fh = self.fecha_hora()
        comercio = rng.choice(COMERCIOS[intencion])

        estado, auth, riesgo = "COMPLETADA", rng.random() < 0.7, False
        dispositivo = cliente.dispositivo_registrado if rng.random() < 0.6 else None
        if intencion == "C3":
            estado = {"fallida": "FALLIDA", "no_completada": "NO_COMPLETADA"}.get(
                variante, rng.choice(["FALLIDA", "NO_COMPLETADA"]))
        if intencion == "C2":
            combo = variante if escenario in ("R8", "R9") else rng.choice(["sin_auth", "riesgo", "sin_auth_riesgo", "normal"])
            auth = combo in ("riesgo", "normal", "usd")
            riesgo = combo in ("riesgo", "sin_auth_riesgo")
            if combo in ("normal", "usd"):
                dispositivo = cliente.dispositivo_registrado
            else:
                dispositivo = rng.choice([None, self.dispositivo(), cliente.dispositivo_registrado])

        tx = Transaccion(self.codigo(), cliente.id, monto, moneda, fh, comercio, estado, auth, dispositivo, riesgo)

        extras: list[Transaccion] = []
        if intencion == "C1":
            con_duplicado = escenario == "R5" or (escenario in ("R1", "R2", "R3", "R4") and rng.random() < 0.5)
            if con_duplicado:
                delta = timedelta(hours=24) if variante == "borde_24h" else timedelta(minutes=rng.randrange(1, 41))
                extras.append(self._copia(tx, fh - delta, monto))
            elif variante == "senuelo_fuera_ventana":
                delta = timedelta(hours=24, minutes=rng.randrange(1, 48 * 60))
                extras.append(self._copia(tx, fh - delta, monto))
            elif variante == "senuelo_otro_monto":
                otro = monto + _d(rng.choice([1, 5, 10, 0.5]))
                extras.append(self._copia(tx, fh - timedelta(minutes=rng.randrange(1, 600)), otro))
        return tx, extras

    def _copia(self, tx: Transaccion, fh: datetime, monto: Decimal) -> Transaccion:
        return Transaccion(self.codigo(), tx.id_cliente, monto, tx.moneda, fh, tx.comercio, "COMPLETADA",
                           tx.autenticacion_reforzada, tx.dispositivo, False)

    def _relleno(self, cliente: Cliente) -> Transaccion:
        rng = self.rng
        moneda = "USD" if rng.random() < 0.1 else "PEN"
        return Transaccion(self.codigo(), cliente.id, self.monto(moneda, alto=rng.random() < 0.05), moneda,
                           self.fecha_hora(), rng.choice(COMERCIOS["otros"]), "COMPLETADA",
                           rng.random() < 0.7, cliente.dispositivo_registrado if rng.random() < 0.6 else None, False)

    def _texto(self, intencion: str, escenario: str, variante: str, tx: Transaccion) -> DatosTexto:
        rng = self.rng
        monto = tx.monto if rng.random() < 0.75 else None
        fecha = tx.fecha_hora.date() if rng.random() < 0.55 else None
        codigo = tx.codigo if rng.random() < 0.5 else None
        if variante == "monto_texto":
            delta = _d(rng.choice([1, 10, 20, 50, 0.5, 5]))
            monto = tx.monto + delta if rng.random() < 0.5 or tx.monto <= delta else tx.monto - delta
        return DatosTexto("FUERA_DE_CATALOGO" if escenario == "R4" else intencion,
                          monto, tx.moneda if monto is not None else None, fecha, codigo)

    def _voucher(self, escenario: str, variante: str, tx: Transaccion) -> DatosVoucher:
        rng = self.rng
        codigo, fh = tx.codigo, tx.fecha_hora
        if variante == "codigo_voucher":
            codigo = self.codigo_alterado(tx.codigo)
        elif variante == "fecha_voucher":
            fh = fh - timedelta(days=1)
        legibles = set(CAMPOS_OCR)
        if escenario == "R1":
            k = rng.choices([1, 2, 3], weights=[6, 3, 1])[0]
            legibles -= set(rng.sample(CAMPOS_OCR, k))
        return DatosVoucher(tx.monto, tx.moneda, fh, codigo, frozenset(legibles))

    def _completar(self, caso: Caso) -> None:
        """Narración, fecha de ingreso y parámetros de presentación del voucher."""
        rng = self.rng
        tipo = "AMBIGUA" if caso.escenario == "R4" else caso.intencion
        t = caso.texto
        caso.narracion, incluidos = narraciones.redactar(
            tipo, caso.tx.comercio, t.monto, t.moneda, t.fecha, t.codigo, rng)
        caso.texto = replace(  # la verdad del texto es solo lo que quedó escrito
            t,
            monto=t.monto if "monto" in incluidos else None,
            moneda=t.moneda if "monto" in incluidos else None,
            fecha=t.fecha if "fecha" in incluidos else None,
            codigo=t.codigo if "codigo" in incluidos else None,
        )
        caso.fecha_ingreso = caso.tx.fecha_hora + timedelta(days=rng.randrange(1, 13), minutes=rng.randrange(0, 600))

        caso.plantilla = "comprobante" if caso.intencion == "C1" and rng.random() < 0.6 else "app"
        nivel = "alto" if caso.escenario == "R1" else rng.choices(["bajo", "medio", "alto"], weights=[45, 35, 20])[0]
        caso.extra = {
            "nivel": nivel,
            "tipo_operacion": ("Transferencia a terceros" if caso.tx.comercio.startswith("Transf.")
                               else rng.choice(TIPO_OPERACION[caso.intencion])),
            "separador_fecha": "-" if rng.random() < 0.2 else "/",
            "etiqueta_codigo": "Operación" if rng.random() < 0.6 else "Cód. operación",
            "cuenta": rng.choice(["***-", "**** "]) + f"{rng.randrange(10000):04d}",
        }


def construir(seed: int) -> Banco:
    return _Generador(seed).construir()
