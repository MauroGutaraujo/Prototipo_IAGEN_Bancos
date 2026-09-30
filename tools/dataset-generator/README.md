# Generador del banco sintético

Genera 200 expedientes reproducibles (semilla fija) con verdad de referencia conocida.

## Salidas (`data/`)
- `transacciones.csv`, `clientes.csv`, `tipo_cambio.csv` → esquema `core`.
- `expedientes.csv` (id, codigo_operacion, narracion, fecha_ingreso) → `app.Expediente`.
- `vouchers/<id>.jpg` + `evidencias.csv` (id, ruta, perturbacion) → `app.Evidencia`.
- `verdad_referencia.csv` → `eval.VerdadReferencia`.
- `manifest.json` con conteos y hash SHA-256 de cada archivo.

## Reglas de generación
1. Composición exacta: 80 C1, 80 C2, 40 C3. Ids 1–200, orden mezclado con la semilla.
2. Montos: mayoría ≤ S/ 1,000; incluir a propósito casos > S/ 1,000 (R3) y algunos en USD.
3. C1: la mitad con duplicado real en el core (R5) y la mitad sin duplicado (R6).
4. C2: con y sin autenticación reforzada, con y sin indicador de riesgo (R8/R9).
5. C3: transacción con estado FALLIDA o NO_COMPLETADA (R7).
6. Inconsistencias deliberadas texto/voucher/core en algunos casos (R2).
7. Narraciones en español coloquial, con errores ortográficos y omisiones; algunas sin monto o sin código.
8. Vouchers JPG: plantillas genéricas (banco ficticio, sin logos reales) tipo comprobante y captura de app. Contienen monto, fecha/hora y código de operación.
9. Ruido: gaussiano, desenfoque, rotación leve (±3°) y compresión JPEG; nivel bajo/medio/alto asignado con la semilla y registrado. Parte de los de nivel alto deben quedar ilegibles para disparar R1.
10. `RutaCorrecta` y `ReglaEsperada` se calculan aplicando las mismas reglas R1–R9 de la SPEC sobre los datos verdaderos (para R1 se usa la legibilidad diseñada, no el OCR).

## Uso
```
python -m venv .venv && source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
python generate.py --seed 2026 --out data/
python load.py --conn "%SQL_CONN%"
```
