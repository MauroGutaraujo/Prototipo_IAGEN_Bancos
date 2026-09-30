# SPEC — Especificación técnica del prototipo

Fuente: Capítulos II y III de la tesis. Este documento traduce la tesis a requisitos implementables.

## 1. Flujo del expediente (máquina de estados)

```
Recibido → OcrExtraido → Clasificado → Decidido ─┬─ (derivar) ────────────────→ Derivado
                                                 └─ (procedente/improcedente) → Fundamentado → Redactado → Verificado ─┬─→ Emitido
                                                                                                                      └─→ (1 regeneración) → Derivado
```

- Cada transición se persiste en `TransicionEstado` con marca de tiempo UTC y milisegundos.
- Etapas estrictamente secuenciales: OCR → Clasificación → Agente Legal → CRAG → Generación → Guardrail de salida.
- Si una etapa lanza excepción: estado `Error`, se registra, el expediente cuenta como derivado en métricas.

## 2. Componentes

### 2.1 Agente OCR (neuronal determinista + validación simbólica)

- Entrada: ruta del JPG. Preprocesamiento: escala de grises, deskew simple, binarización (ImageSharp u OpenCvSharp; elegir uno y fijar versión).
- Ejecuta `tesseract <img> stdout -l spa --psm 6 tsv` y parsea el TSV (columna `conf` 0–100 por palabra; ignorar `-1`).
- Extrae con regex:
  - Monto: `(S\/|US\$)\s?\d{1,3}(,\d{3})*(\.\d{2})`
  - Fecha/hora: `dd/mm/aaaa` y `hh:mm` (tolerar `dd-mm-aaaa`)
  - Código de operación: definir patrón según las plantillas del generador (p. ej. `\b[A-Z0-9]{8,12}\b` precedido de "Operación"/"Cód.").
- Confianza del campo `c_f` = media de `conf/100` de las palabras que forman el valor.
- Campo válido ⇔ cumple regex **y** `c_f ≥ 0.90`.
- Salida: `{ monto, moneda, fechaHora, codigo, conf: {monto, fecha, codigo}, valido: bool }`.

### 2.2 Clasificador de intención (LLM, salida JSON)

- Prompt: `prompts/clasificador.v1.md`. Esquema: `prompts/clasificador.schema.json`.
- Salida esperada: `{ "intencion": "C1|C2|C3|FUERA_DE_CATALOGO", "monto": number|null, "moneda": "PEN|USD"|null, "fecha": "YYYY-MM-DD"|null, "codigo": string|null }`.
- Validación en C#: si el JSON no parsea o no cumple el esquema ⇒ 1 reintento; si vuelve a fallar ⇒ regla R4 (derivar).

### 2.3 Agente Legal (simbólico, sin LLM) — `Reclamos.Guardrails`

Evaluación en orden; la primera regla que dispara define la ruta. R1–R4 tienen prioridad.

| Regla | Condición | Ruta |
|---|---|---|
| R1 | Algún campo OCR obligatorio con `c_f < 0.90` o inválido | Derivar |
| R2 | Monto, fecha o código no coinciden entre texto (clasificador), OCR y `Transaccion` del core | Derivar |
| R3 | Monto en soles > 1000.00 (USD se convierte con `TipoCambio` del core) | Derivar |
| R4 | Salida del clasificador inválida o `FUERA_DE_CATALOGO` | Derivar |
| R5 | C1 y existen 2 cargos con igual monto y comercio en ventana ≤ 24 h | Procedente (extorno) |
| R6 | C1 y no existe cargo duplicado | Improcedente |
| R7 | C3 y la transacción tiene estado `FALLIDA` o `NO_COMPLETADA` con cargo registrado | Procedente (devolución) |
| R8 | C2 y la operación no tiene autenticación reforzada o tiene indicador de riesgo | Derivar |
| R9 | C2, autenticación reforzada válida y dispositivo registrado del titular | Improcedente |
| R10 | Toda ruta automática: verificar plazo y contenidos mínimos (lo aplica el guardrail de salida) | — |

- Tolerancias de coincidencia (R2): monto exacto a 2 decimales; fecha exacta; código exacto (normalizado a mayúsculas, sin espacios). La ventana de 24 h de R5 es parámetro configurable.
- Salida: `{ ruta: Derivar|Procedente|Improcedente, regla: "R1".."R9", motivo: string }`.
- 100 % de cobertura de pruebas unitarias en este proyecto.

### 2.4 Recuperación normativa CRAG

- Consulta = plantilla con intención + ruta + regla aplicada.
- Embeddings: un único modelo fijo (definir en config; p. ej. `text-embedding-3-small`, dimensión 1536). El índice Pinecone debe tener la misma dimensión, métrica `cosine`.
- `topK = 5`. Conservar fragmentos con `score ≥ 0.78`.
- Si ninguno ≥ 0.78 ⇒ reformular 1 vez (agregar nombre de norma + términos de la tipología) ⇒ si sigue sin ninguno ⇒ Derivar (estado `Derivado`, motivo `CRAG_INCORRECTA`).
- Registrar: scores, ids de fragmentos, calificación (Correcta/Ambigua/Incorrecta), `t_RAG`.
- Flag de ablación `Rag:Enabled=false`: no se consulta Pinecone y el generador recibe solo hechos (condición T2 de la tesis). En ablación **el guardrail de salida se ejecuta pero no bloquea** (se registra su veredicto para medir TA).

### 2.5 Agente Generativo (LLM)

- Prompt: `prompts/generador.v1.md`. Entradas: ruta y regla decididas, hechos verificados (monto, fecha, código, comercio, estado), fragmentos admitidos con su id.
- Debe citar normas solo por el id de fragmento recibido (formato `[F:<id>]`).

### 2.6 Guardrail de salida (simbólico)

Verificaciones sobre el borrador:
1. Toda cita `[F:id]` pertenece al conjunto de fragmentos admitidos.
2. Todo monto/fecha/código mencionado coincide con los hechos verificados.
3. El sentido (procedente/improcedente) coincide con la ruta del Agente Legal (buscar marcadores obligatorios definidos en el prompt, p. ej. `DECISIÓN: PROCEDENTE`).
4. Contenidos mínimos presentes (instancias a las que puede acudir el usuario, plazo, número de reclamo).
- Falla ⇒ 1 regeneración ⇒ si falla otra vez ⇒ Derivar (motivo `GUARDRAIL_SALIDA`).
- Registrar afirmaciones verificables y no sustentadas (para TA).

## 3. Datos

### 3.1 Esquemas SQL Server

- `core`: `ClienteSintetico`, `Transaccion`, `TipoCambio`.
- `app`: `Expediente`, `Evidencia`, `Ejecucion`, `TransicionEstado`, `DecisionLegal`, `Recuperacion`, `Resolucion`, `RegistroManual`.
- `eval`: `VerdadReferencia` (solo lectura para `tools/analysis`; el usuario de BD de la API no tiene permisos sobre `eval`).

Ver `db/schema.sql` (referencia; la fuente de verdad serán las migraciones de EF Core).

### 3.2 `Ejecucion` (una fila por expediente × modelo × repetición × condición)

`IdEjecucion, IdExpediente, Condicion (T0|T1|T2), ModeloId, ModeloVersion, Repeticion (1–5), InicioUtc, FinUtc, T_Ocr_ms, T_Guardrail_ms, T_Rag_ms, L_Cls_ms, L_Gen_ms, T_Orq_ms, L_Total_ms, TokensIn, TokensOut, EstadoFinal, IntencionPredicha, Regeneraciones`.

## 4. Benchmark (`src/Reclamos.Benchmark`)

- Config `models.json` con 8 entradas: `{ id, proveedor, endpoint, apiKeyEnv, modelName }`.
- Recomendado: usar el conector OpenAI de Semantic Kernel con **endpoint compatible OpenAI** para todos los proveedores que lo ofrezcan (verificar documentación vigente de cada uno). Si un proveedor no lo ofrece, usar su conector oficial.
- Orden: `for rep in 1..5: for model in rotate(models, rep): for exp in 200: run(exp)`.
- Parámetros iguales para todos: temperatura 0, `max_tokens` fijo por tipo de llamada.
- Antes de correr: verificar disponibilidad de cada modelo; registrar `ModeloVersion` devuelto por la API.
- Ventana: toda la ejecución en ≤ 7 días. Guardar inicio y fin en tabla `CorridaBenchmark`.

## 5. Línea base manual (T0) — ficha web

- Página en `Reclamos.Api` (Razor Pages o HTML estático + endpoints).
- Login simple por código de analista (A1, A2, A3). Muestra narración + imagen; buscador de transacción por código en el core; enlace a PDFs normativos.
- Registra `t_ingreso` al abrir y `t_final` al enviar; botón Pausa/Reanudar (descuenta pausas).
- El analista registra intención, monto, fecha, código, ruta y texto de respuesta.
- Asignación: 180 casos (60 por analista, 24 C1 + 24 C2 + 12 C3 cada uno) + 20 casos de calibración (8/8/4) que ven los tres. Orden aleatorio por analista.

## 6. API mínima

| Método | Ruta | Uso |
|---|---|---|
| POST | `/api/expedientes/{id}/procesar?modelo=...&condicion=T1` | Procesa un expediente |
| GET | `/api/expedientes/{id}` | Estado, decisión y resolución |
| GET | `/api/ejecuciones?modelo=&repeticion=` | Registros |
| GET/POST | `/ficha/...` | Ficha de cronometraje (T0) |
| GET | `/health` | Salud (SQL, Pinecone, Tesseract) |

## 7. Métricas (se calculan en `tools/analysis`, nunca en la API)

- `P` = aciertos de intención / N. Precision, recall y F1 por clase y macro (one-vs-rest).
- `EM_OCR` = evidencias con los 3 campos correctos / N_ev. Matriz binaria por campo (VP, FP, FN, VN).
- `L_Total`, `L_LLM = L_CLS + L_GEN`: media, P50, P95.
- `TRA` = emitidos sin derivación **y** con ruta correcta / N. Reportar `TRA_max` = casos con ruta correcta automática / N.
- `CN` = emitidas conformes / emitidas. `TIG` = bloqueadas por guardrail / N. `TA` = afirmaciones no sustentadas / afirmaciones verificables (sobre borradores).
- Pruebas: McNemar (H1, H4), Shapiro-Wilk → t pareada o Wilcoxon unilateral (H2), binomial exacta unilateral π0 = 0.60 (H3), Q de Cochran + McNemar-Holm entre modelos, ICC(2,1) ≥ 0.85 en calibración, α = 0.05.
- Media de 5 repeticiones ± s, IC 95 % con t(0.975, 4) = 2.776.

## 8. Requisitos no funcionales

- Presupuesto de latencia P95 (meta, no resultado): t_OCR ≤ 1.0 s · t_Guardrail ≤ 0.1 s · t_RAG ≤ 0.5 s · L_CLS ≤ 2.0 s · L_GEN ≤ 6.0 s · t_orq ≤ 0.15 s · L_Total ≤ 10 s.
- Reproducibilidad: semilla fija en el generador; prompts, reglas y parámetros versionados en Git.
- Sin datos personales reales; sin marcas reales.
