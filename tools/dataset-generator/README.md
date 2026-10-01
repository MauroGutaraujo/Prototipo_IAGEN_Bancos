# Generador del banco sintético

Genera 200 expedientes reproducibles (semilla fija) con verdad de referencia conocida.

## Uso
```
py -3.12 -m venv .venv
.venv\Scripts\activate            # Linux/macOS: source .venv/bin/activate
pip install -r requirements.txt
python generate.py --seed 2026 --out data/
python generate.py --seed 2026 --out muestra/ --muestra   # solo 3 vouchers (C1, C2, C3)
python load.py --data data/        # usa SQL_CONN del entorno o de .env; también acepta --conn
python -m pytest
```

## Salidas (`data/`, fuera de Git)
- `clientes.csv`, `tipo_cambio.csv`, `transacciones.csv` → esquema `core`.
- `expedientes.csv` (id, codigo_operacion, narracion, fecha_ingreso) → `app.Expediente`.
- `vouchers/<id>.jpg` + `evidencias.csv` (id, ruta, perturbacion) → `app.Evidencia`.
- `verdad_referencia.csv` → `eval.VerdadReferencia`.
- `diseno_casos.csv`: escenario, variante, plantilla, nivel de ruido y datos que menciona la narración (trazabilidad; no se carga).
- `manifest.json`: semilla, versiones de numpy y Pillow, conteos del diseño y SHA-256 de cada archivo. No incluye marcas de tiempo, así que la misma semilla produce el mismo manifiesto.

## Estructura
| Archivo | Rol |
|---|---|
| `banco/construir.py` | Plan de escenarios, core transaccional, textos y verificación contra las reglas |
| `banco/reglas.py` | R1–R9 sobre los datos verdaderos → `RutaCorrecta`, `ReglaEsperada` |
| `banco/narraciones.py` | Narraciones coloquiales con erratas; nunca alteran monto, fecha ni código |
| `banco/vouchers.py` | Plantillas «comprobante» y «captura de app» + ruido |
| `banco/salida.py` | CSV, JPG y manifiesto |
| `load.py` | Carga transaccional a SQL Server |
| `assets/fonts/` | Liberation Sans (SIL OFL 1.1, ver `LICENSE_LIBERATION`). Va versionada para que el render no dependa de las fuentes del sistema |

## Diseño del banco
1. **Composición:** 80 C1, 80 C2 y 40 C3. Los ids 1–200 se asignan en orden mezclado con la semilla.
2. **Cada caso se diseña para una regla objetivo** (tabla `PLAN` en `construir.py`). Después se aplican R1–R9 sobre los datos verdaderos y el generador falla si la regla obtenida no coincide con la diseñada.
3. **Bordes incluidos:**
   - S/ 1,000.00 exacto (no dispara R3).
   - Duplicado a exactamente 24 h (dispara R5).
   - Señuelos de R6: mismo cargo fuera de la ventana de 24 h, o mismo comercio con otro monto.
   - Montos en USD convertidos con `TipoCambio`.
4. **R2** tiene tres variantes:
   - la narración menciona otro monto;
   - el voucher muestra otro código;
   - el voucher muestra la fecha del día anterior.
5. **R1:** zonas de monto, fecha o código destruidas con desenfoque fuerte y mancha, siempre sobre vouchers de nivel de ruido alto. `CamposLegibles` refleja la legibilidad diseñada.
6. **R4:** narraciones ambiguas que no permiten decidir la tipología. Por decisión del estudiante, `IntencionReal` conserva la clase del estrato (C1/C2/C3). Si el modelo responde `FUERA_DE_CATALOGO`, eso cuenta como error de intención en P.
7. **Ruido:** rotación (±0.3–3°), desenfoque gaussiano, ruido gaussiano, ganancia de brillo y JPEG (calidad 38–92). Los parámetros exactos de cada voucher quedan en `evidencias.csv`.
8. **Privacidad:**
   - banco ficticio «Banco Andino»; «Banco del Valle» aparece solo como destino de transferencias;
   - clientes `Cliente S-0001`, dispositivos `DEV-xxxxxx` y comercios inventados;
   - una prueba verifica que no aparezcan marcas de bancos reales.

## Convenciones que el sistema (H3/H4) debe respetar
- **`MontoReal`, `FechaReal` y `CodigoReal`** de `VerdadReferencia` son lo que el voucher muestra impreso (verdad del OCR). Los datos del core están en `core.Transaccion`.
- **Formatos del voucher:**
  - monto `S/ 1,234.50` o `US$ 45.00`;
  - fecha `dd/mm/aaaa` o `dd-mm-aaaa` y hora `hh:mm` en líneas separadas;
  - código `AN` + 8 caracteres `[0-9A-Z]` sin I ni O, precedido de «Operación» o «Cód. operación».
- **R2** compara el monto (misma moneda, 2 decimales), la **fecha a nivel de día** y el código normalizado. Un dato ausente en la narración no se compara.
- **R5:** mismo cliente, monto, moneda y comercio, con |Δt| ≤ 24 h.
- **R7 y R8 ampliadas** (SPEC §2.3): C3 `COMPLETADA` ⇒ Derivar por conciliación (R7); C2 con dispositivo no registrado ⇒ Derivar (R8). El banco no contiene casos de ese tipo.
