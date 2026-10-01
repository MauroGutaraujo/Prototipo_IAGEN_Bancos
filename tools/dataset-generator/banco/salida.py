"""Escritura determinista de CSV, vouchers JPG y manifiesto con hashes SHA-256."""
from __future__ import annotations

import csv
import hashlib
import json
import platform
import shutil
from collections import Counter
from pathlib import Path

import numpy as np
import PIL

from . import vouchers
from .construir import Banco
from .modelo import CAMPOS_OCR, Caso

TITULO_APP = {"C1": "Constancia de pago", "C2": "Detalle del movimiento", "C3": "Operación no completada"}
ESTADO_VOUCHER = {"COMPLETADA": "Aprobada", "FALLIDA": "Fallida", "NO_COMPLETADA": "No completada"}


def contenido_voucher(caso: Caso) -> vouchers.ContenidoVoucher:
    v, e = caso.voucher, caso.extra
    return vouchers.ContenidoVoucher(
        plantilla=caso.plantilla,
        titulo="Constancia de operación" if caso.plantilla == "comprobante" else TITULO_APP[caso.intencion],
        tipo_operacion=e["tipo_operacion"],
        comercio=caso.tx.comercio,
        monto=v.monto,
        moneda=v.moneda,
        fecha_hora=v.fecha_hora,
        separador_fecha=e["separador_fecha"],
        codigo=v.codigo,
        etiqueta_codigo=e["etiqueta_codigo"],
        estado=ESTADO_VOUCHER[caso.tx.estado],
        cuenta_mascara=e["cuenta"],
        alerta=caso.intencion == "C3",
    )


def escribir_voucher(caso: Caso, seed: int, destino: Path) -> None:
    img, cajas = vouchers.render(contenido_voucher(caso))
    ilegibles = tuple(c for c in CAMPOS_OCR if c not in caso.voucher.campos_legibles)
    rng = np.random.default_rng([seed, caso.id])
    datos, caso.perturbacion = vouchers.perturbar(img, cajas, caso.extra["nivel"], ilegibles, rng)
    destino.write_bytes(datos)


def _csv(ruta: Path, encabezado: list[str], filas: list[list[object]]) -> None:
    with ruta.open("w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(encabezado)
        w.writerows(filas)


def _fh(dt) -> str:
    return dt.strftime("%Y-%m-%d %H:%M:%S")


def _sha256(ruta: Path) -> str:
    return hashlib.sha256(ruta.read_bytes()).hexdigest()


def escribir(banco: Banco, out: Path) -> Path:
    """Escribe todo el banco en `out` y devuelve la ruta del manifiesto."""
    out.mkdir(parents=True, exist_ok=True)
    dir_v = out / "vouchers"
    if dir_v.exists():
        shutil.rmtree(dir_v)
    dir_v.mkdir()

    casos = sorted(banco.casos, key=lambda c: c.id)
    for caso in casos:
        escribir_voucher(caso, banco.seed, dir_v / f"{caso.id:04d}.jpg")

    _csv(out / "clientes.csv", ["id_cliente", "alias_seudonimo", "dispositivo_registrado"],
         [[c.id, c.alias, c.dispositivo_registrado] for c in banco.clientes])
    _csv(out / "tipo_cambio.csv", ["fecha", "usd_pen"],
         [[d.isoformat(), f"{v:.4f}"] for d, v in sorted(banco.tipo_cambio.items())])
    _csv(out / "transacciones.csv",
         ["codigo_operacion", "id_cliente", "monto", "moneda", "fecha_hora", "comercio", "estado",
          "autenticacion_reforzada", "dispositivo", "indicador_riesgo"],
         [[t.codigo, t.id_cliente, f"{t.monto:.2f}", t.moneda, _fh(t.fecha_hora), t.comercio, t.estado,
           int(t.autenticacion_reforzada), t.dispositivo or "", int(t.indicador_riesgo)]
          for t in banco.transacciones])
    _csv(out / "expedientes.csv", ["id_expediente", "codigo_operacion", "narracion", "fecha_ingreso"],
         [[c.id, c.tx.codigo, c.narracion, _fh(c.fecha_ingreso)] for c in casos])
    _csv(out / "evidencias.csv", ["id_evidencia", "id_expediente", "ruta", "perturbacion"],
         [[c.id, c.id, f"vouchers/{c.id:04d}.jpg", c.perturbacion] for c in casos])
    _csv(out / "verdad_referencia.csv",
         ["id_expediente", "intencion_real", "monto_real", "moneda_real", "fecha_real", "codigo_real",
          "campos_legibles", "ruta_correcta", "regla_esperada"],
         [[c.id, c.intencion, f"{c.voucher.monto:.2f}", c.voucher.moneda, _fh(c.voucher.fecha_hora),
           c.voucher.codigo, ",".join(x for x in CAMPOS_OCR if x in c.voucher.campos_legibles), c.ruta, c.regla]
          for c in casos])
    # Trazabilidad del diseño (no se carga a la BD): qué se buscó en cada caso.
    _csv(out / "diseno_casos.csv",
         ["id_expediente", "intencion", "escenario", "variante", "plantilla", "nivel_ruido",
          "texto_intencion", "texto_monto", "texto_moneda", "texto_fecha", "texto_codigo"],
         [[c.id, c.intencion, c.escenario, c.variante, c.plantilla, c.extra["nivel"], c.texto.intencion,
           "" if c.texto.monto is None else f"{c.texto.monto:.2f}", c.texto.moneda or "",
           c.texto.fecha.isoformat() if c.texto.fecha else "", c.texto.codigo or ""] for c in casos])

    archivos = sorted(p for p in out.rglob("*") if p.is_file() and p.name != "manifest.json")
    manifiesto = {
        "seed": banco.seed,
        "versiones": {"python": platform.python_version(), "numpy": np.__version__, "pillow": PIL.__version__},
        "conteos": {
            "expedientes": len(casos),
            "por_intencion": dict(sorted(Counter(c.intencion for c in casos).items())),
            "por_regla_esperada": dict(sorted(Counter(c.regla for c in casos).items(), key=lambda kv: int(kv[0][1:]))),
            "por_ruta_correcta": dict(sorted(Counter(c.ruta for c in casos).items())),
            "clientes": len(banco.clientes),
            "transacciones": len(banco.transacciones),
        },
        "archivos": {p.relative_to(out).as_posix(): _sha256(p) for p in archivos},
    }
    ruta = out / "manifest.json"
    ruta.write_text(json.dumps(manifiesto, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return ruta
