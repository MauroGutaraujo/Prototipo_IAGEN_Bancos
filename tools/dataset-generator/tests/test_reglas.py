"""Reglas usadas para calcular la verdad de referencia, con sus bordes."""
from datetime import date, datetime, timedelta
from decimal import Decimal

import pytest

from banco import reglas
from banco.modelo import CAMPOS_OCR, DatosTexto, DatosVoucher, Transaccion

FH = datetime(2026, 3, 10, 12, 0)
TC = {date(2026, 3, 10): Decimal("3.7000")}
DEV = "DEV-000001"


def tx(**kw) -> Transaccion:
    base = dict(codigo="AN00000001", id_cliente=1, monto=Decimal("100.00"), moneda="PEN", fecha_hora=FH,
                comercio="Comercio X", estado="COMPLETADA", autenticacion_reforzada=True, dispositivo=DEV,
                indicador_riesgo=False)
    return Transaccion(**{**base, **kw})


def texto(t: Transaccion, intencion="C1", **kw) -> DatosTexto:
    base = dict(intencion=intencion, monto=t.monto, moneda=t.moneda, fecha=t.fecha_hora.date(), codigo=t.codigo)
    return DatosTexto(**{**base, **kw})


def voucher(t: Transaccion, legibles=CAMPOS_OCR, **kw) -> DatosVoucher:
    base = dict(monto=t.monto, moneda=t.moneda, fecha_hora=t.fecha_hora, codigo=t.codigo,
                campos_legibles=frozenset(legibles))
    return DatosVoucher(**{**base, **kw})


def evaluar(t, intencion="C1", core=None, txt=None, v=None):
    return reglas.evaluar(txt or texto(t, intencion), v or voucher(t), t, core or [t], TC, DEV)


def test_r1_campo_ilegible_tiene_prioridad():
    t = tx(monto=Decimal("5000.00"))
    assert evaluar(t, v=voucher(t, legibles=("monto", "fecha"))) == ("Derivar", "R1")


@pytest.mark.parametrize("cambio", [
    {"txt_monto": Decimal("100.50")},
    {"v_codigo": "AN00000099"},
    {"v_fecha": FH - timedelta(days=1)},
])
def test_r2_discrepancia(cambio):
    t = tx()
    txt = texto(t, monto=cambio.get("txt_monto", t.monto))
    v = voucher(t, codigo=cambio.get("v_codigo", t.codigo), fecha_hora=cambio.get("v_fecha", t.fecha_hora))
    assert evaluar(t, txt=txt, v=v) == ("Derivar", "R2")


def test_r2_dato_ausente_en_texto_no_es_discrepancia():
    t = tx()
    assert evaluar(t, txt=texto(t, monto=None, moneda=None, fecha=None, codigo=None))[1] != "R2"


def test_r2_codigo_se_normaliza():
    t = tx()
    assert evaluar(t, txt=texto(t, codigo=" an0000 0001 "))[1] != "R2"


def test_r3_borde_1000_exacto_no_deriva():
    assert evaluar(tx(monto=Decimal("1000.00")))[1] == "R6"


def test_r3_mayor_a_1000_deriva():
    assert evaluar(tx(monto=Decimal("1000.01"))) == ("Derivar", "R3")


def test_r3_usd_se_convierte_con_tipo_cambio():
    assert evaluar(tx(monto=Decimal("270.28"), moneda="USD"))[1] == "R3"   # 1000.036
    assert evaluar(tx(monto=Decimal("270.27"), moneda="USD"))[1] == "R6"   # 999.999


def test_r4_fuera_de_catalogo():
    assert evaluar(tx(), intencion="FUERA_DE_CATALOGO") == ("Derivar", "R4")


def test_r5_duplicado_en_borde_de_24h():
    t = tx()
    d = tx(codigo="AN00000002", fecha_hora=FH - timedelta(hours=24))
    assert evaluar(t, core=[t, d]) == ("Procedente", "R5")


def test_r6_fuera_de_ventana_u_otro_monto():
    t = tx()
    fuera = tx(codigo="AN00000002", fecha_hora=FH - timedelta(hours=24, minutes=1))
    otro = tx(codigo="AN00000003", monto=Decimal("101.00"))
    assert evaluar(t, core=[t, fuera, otro]) == ("Improcedente", "R6")


@pytest.mark.parametrize("estado", ["FALLIDA", "NO_COMPLETADA"])
def test_r7(estado):
    assert evaluar(tx(estado=estado), intencion="C3") == ("Procedente", "R7")


def test_r7_c3_completada_se_deriva_por_conciliacion():
    assert evaluar(tx(), intencion="C3") == ("Derivar", "R7")


@pytest.mark.parametrize("auth,riesgo,dispositivo", [
    (False, False, DEV), (True, True, DEV), (False, True, DEV),
    (True, False, "DEV-OTRO"), (True, False, None),
])
def test_r8(auth, riesgo, dispositivo):
    t = tx(autenticacion_reforzada=auth, indicador_riesgo=riesgo, dispositivo=dispositivo)
    assert evaluar(t, intencion="C2") == ("Derivar", "R8")


def test_r9():
    assert evaluar(tx(), intencion="C2") == ("Improcedente", "R9")

