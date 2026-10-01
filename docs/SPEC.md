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

- Entrada: ruta del JPG. Preprocesamiento con **SixLabors.ImageSharp 3.1.12**: escala opcional, enderezado simple (búsqueda del ángulo que maximiza el perfil de proyección horizontal en ±`AnguloMaximo`), escala de grises y binarización (ninguna, umbral fijo o adaptativa). Parámetros en `Ocr:Preprocesamiento`.
- **Los parámetros del preprocesamiento se eligen solo con un banco de desarrollo generado con otra semilla (`--seed 7`)**; el banco de evaluación (`--seed 2026`) nunca se usa para ajustar.
  - Grilla explorada: escala {1, 1.5} × enderezar {no, sí} × binarización {ninguna, umbral 0.5, adaptativa}, sobre 60 vouchers del banco de desarrollo (prueba `AjusteOcr`, CI con `[ajuste-ocr]`).
  - Criterio: máximo número de vouchers con los tres campos correctos **y** válidos (`c_f ≥ 0.90`); desempate por la suma de aciertos por campo.
  - Configuración elegida (2026-10-01): **escala 1.5, sin enderezar, sin binarizar**. El enderezado se conserva como opción configurable.
- Motor: Tesseract 5 (paquete de Ubuntu 24.04; la versión exacta se registra en cada corrida). Modelo `spa.traineddata` de **tessdata_fast**, commit `923915d4ced2a7235221788285785a29c4a42d4a`, SHA-256 `6f2e04d02774a18f01bed44b1111f2cd7f3ba7ac9dc4373cd3f898a40ea6b464` (se verifica y se registra).
- Ejecuta `tesseract stdin stdout -l spa --psm 6 --tessdata-dir <dir> -c tessedit_create_tsv=1` (imagen preprocesada en PNG por la entrada estándar) y parsea el TSV (columna `conf` 0–100 por palabra; ignorar `-1`).
- Extrae con regex por línea de texto:
  - Monto: `(S\/|US\$)\s?\d{1,3}(,\d{3})*(\.\d{2})`; la moneda sale del símbolo.
  - Fecha: `dd/mm/aaaa` o `dd-mm-aaaa` (fecha de calendario válida). **Obligatoria.**
  - Hora: `hh:mm`. **Opcional**: si falta, `fechaHora` queda a las 00:00 (R2 compara por día).
  - Código de operación: `[A-Z0-9]{8,12}` precedido de «Operación» o «Cód. operación» (con o sin tilde y dos puntos). **Sin corrección de caracteres**: solo mayúsculas y sin espacios.
  - Si un patrón aparece varias veces, se toma la primera aparición en orden de lectura.
- Confianza del campo `c_f` = media de `conf/100` de las palabras que forman el valor (para la fecha: palabras de la fecha y de la hora, si existe), **redondeada a 3 decimales (half away from zero) antes de comparar**; así coincide con lo que se guarda en `DECIMAL(4,3)`.
- Campo válido ⇔ cumple regex **y** `c_f ≥ 0.90`.
- Si la imagen no se puede leer o Tesseract falla o excede el tiempo, los campos quedan nulos (R1 deriva) y se registra el error.
- Salida: `{ monto, moneda, fechaHora, codigo, conf: {monto, fecha, codigo} }` + TSV crudo + versión de Tesseract + SHA-256 del modelo.

### 2.2 Clasificador de intención (LLM, salida JSON)

- Prompt: `prompts/clasificador.v1.md`. Esquema: `prompts/clasificador.schema.json`.
- Salida esperada: `{ "intencion": "C1|C2|C3|FUERA_DE_CATALOGO", "monto": number|null, "moneda": "PEN|USD"|null, "fecha": "YYYY-MM-DD"|null, "codigo": string|null }`.
- Validación en C#: si el JSON no parsea o no cumple el esquema ⇒ 1 reintento; si vuelve a fallar ⇒ regla R4 (derivar).

### 2.3 Agente Legal (simbólico, sin LLM) — `Reclamos.Guardrails`

Evaluación en orden; la primera regla que dispara define la ruta. R1–R4 tienen prioridad.

| Regla | Condición | Ruta |
|---|---|---|
| R1 | Algún campo OCR obligatorio (monto, fecha, código) sin valor que cumpla su regex o con `c_f < U_ocr` | Derivar |
| R2 | Monto, fecha o código del texto (clasificador) o del OCR no coinciden con la `Transaccion` del core | Derivar |
| R3 | Monto en soles > `U_riesgo` (USD se convierte con `TipoCambio` del día de la operación) | Derivar |
| R4 | Salida del clasificador inválida o `FUERA_DE_CATALOGO` | Derivar |
| R5 | C1 y existen 2 cargos con igual monto y comercio en ventana ≤ 24 h | Procedente (extorno) |
| R6 | C1 y no existe cargo duplicado | Improcedente |
| R7 | C3: transacción `FALLIDA` o `NO_COMPLETADA` con cargo registrado ⇒ Procedente (devolución). Transacción `COMPLETADA` (o sin cargo) ⇒ Derivar, motivo `CONCILIACION` | Procedente / Derivar |
| R8 | C2 y la operación no tiene autenticación reforzada, **o** tiene indicador de riesgo, **o** el dispositivo no es el registrado del titular (incluye operación sin dispositivo) | Derivar |
| R9 | C2, autenticación reforzada válida, sin indicador de riesgo y dispositivo registrado del titular | Improcedente |
| R10 | Toda ruta automática: verificar plazo y contenidos mínimos (lo aplica el guardrail de salida, §2.6) | — |

Precisiones (aprobadas por el estudiante, 2026-09-30/10-01):
- **R1:** un campo es válido ⇔ su valor cumple el regex **y** `c_f ≥ U_ocr` (0.90). `c_f = 0.90` exacto es válido.
- **R2:** monto exacto a 2 decimales y misma moneda; **fecha a nivel de día**; código normalizado (mayúsculas, sin espacios). Un dato **ausente** en el texto no se compara. Texto y OCR se comparan cada uno contra el core.
- **R3:** `monto > 1000.00` (1000.00 exacto no deriva). Si la operación es en USD y no hay `TipoCambio` para ese día ⇒ Derivar por R3, motivo `TIPO_CAMBIO_NO_DISPONIBLE`.
- **R4:** los casos de narración ambigua del banco sintético conservan `IntencionReal` = clase del estrato; si el modelo responde `FUERA_DE_CATALOGO` cuenta como error de intención en P.
- **R5:** mismo cliente, mismo monto, moneda y comercio, código distinto, `|Δt| ≤ VentanaDuplicadoHoras` (24 h exacta dispara R5).
- Parámetros (`Guardrail` en configuración): `UmbralOcr = 0.90`, `UmbralRiesgoPen = 1000.00`, `VentanaDuplicadoHoras = 24`, `PlazoRespuesta` (ver §2.6).
- Salida: `{ ruta: Derivar|Procedente|Improcedente, regla: "R1".."R9", motivo: string }`.
- 100 % de cobertura de pruebas unitarias en este proyecto.

### 2.4 Recuperación normativa CRAG

- **Corpus:** `knowledge/fragmentos.jsonl` (versionado, SHA-256 en `knowledge/manifest.json`), un fragmento por artículo con id estable `<norma>-art-<n>`. Las normas oficiales se extraen **literalmente** de los PDF publicados por la fuente oficial (URL y SHA-256 fijados en `tools/knowledge/fragmentar.py`); la política interna PIR-2026-V1 es **simulada** y está declarada como tal. Selección: Res. SBS N.° 04036-2022 (Reglamento, arts. 1–19); Ley N.° 29571 (arts. 18, 19, 24, 81–96 incl. 90-A, 150–152); Ley N.° 31435 (artículo único).
- **Embeddings:** `llama-text-embed-v2` de **Pinecone Inference** (1024 dimensiones, multilingüe; cambio decidido por el estudiante el 2026-10-01 para mantener el desarrollo sin costo: solo se usa `PINECONE_API_KEY`). El mismo modelo indexa (`input_type = passage`) y consulta (`input_type = query`), con `truncate = NONE` (si un fragmento excediera el límite del modelo, el indexador falla en vez de truncarlo). Se embebe «nombre de la norma + texto del artículo»; la consulta se embebe tal cual. Índice Pinecone serverless, métrica `cosine`, dimensión 1024. Los vectores de cada versión del corpus van en el namespace `corpus-<12 primeros caracteres del SHA-256>`, de modo que una consulta nunca mezcla versiones.
- **Consulta** (plantilla `consulta.v1`) = intención + ruta + regla aplicada. Como solo 4 combinaciones llegan a redacción (C1-R5, C1-R6, C2-R9, C3-R7), la recuperación es la misma para todos los casos de una combinación.
- `topK = 5`. Se admiten los fragmentos con `score ≥ θ` (**θ = 0.35**; el valor igual a θ se admite).
- **Calificación CRAG:**
  - **Correcta:** ≥ 1 fragmento admitido en la primera consulta.
  - **Ambigua:** ninguno en la primera; ≥ 1 tras **una** reformulación (se agregan los nombres de las normas y términos de la tipología).
  - **Incorrecta:** ninguno tras reformular ⇒ Derivar (estado `Derivado`, motivo `CRAG_INCORRECTA`).
  - **Omitida:** ablación T2 (`Rag:Enabled=false`): no se consulta Pinecone y el generador recibe solo hechos. En ablación **el guardrail de salida se ejecuta pero no bloquea** (se registra su veredicto para medir TA).
- **Calibración de θ (2026-10-01, decisión del estudiante).** El valor original 0.78 no admitía ningún fragmento con `llama-text-embed-v2`: la escala del score coseno depende del modelo de embeddings. θ se recalibró con 13 consultas de DESARROLLO redactadas aparte (`knowledge/consultas_desarrollo.jsonl`: 11 con fragmentos pertinentes etiquetados y validados por el estudiante, y 2 controles negativos), nunca con casos de evaluación:
  - barrido de θ entre 0.30 y 0.60 (paso 0.05) sobre el top-K de cada consulta (prueba `CalibracionRag`, CI con la etiqueta de calibración);
  - criterio: máximo número de fragmentos pertinentes admitidos con **cero** admitidos en los controles negativos; desempate por menos fragmentos no pertinentes admitidos;
  - resultado: θ = 0.35. Limitaciones a declarar en la tesis: conjunto de desarrollo pequeño, y con este θ también se admiten fragmentos no pertinentes (el guardrail de salida verifica que las citas sean de fragmentos admitidos, no su pertinencia).
  - Si cambia el modelo de embeddings o el corpus, θ debe recalibrarse con el mismo procedimiento.
- Registrar en `app.Recuperacion`: calificación y `FragmentosJson` = `[{id, score, admitido, consulta}]` de cada consulta realizada; además `t_RAG` en `Ejecucion` y la versión del corpus.

### 2.5 Agente Generativo (LLM)

- Prompt: `prompts/generador.v2.md` (v1 se conserva sin cambios). Entradas: ruta y regla decididas, hechos verificados (número de reclamo, monto, fecha, código, comercio, estado), **plazo aplicable** (`Guardrail:PlazoRespuesta`) y fragmentos admitidos con su id.
- Formatos de los hechos: monto `S/ 1,234.50` o `US$ 45.00`; fecha `dd/mm/aaaa`; código tal cual el core.
- Debe citar normas solo por el id de fragmento recibido (formato `[F:<id>]`).

### 2.6 Guardrail de salida (simbólico)

Verificaciones sobre el borrador:
1. Toda cita `[F:id]` pertenece al conjunto de fragmentos admitidos.
2. Todo monto (`S/`/`US$`), fecha (`dd/mm/aaaa`, `dd-mm-aaaa` o `d de <mes> de aaaa`) y código de operación mencionado coincide con los hechos verificados.
3. El sentido coincide con la ruta del Agente Legal: debe existir una línea `DECISIÓN: PROCEDENTE` o `DECISIÓN: IMPROCEDENTE` y ninguna contradictoria.
4. Contenidos mínimos: número de reclamo, al menos una instancia (Defensoría del Cliente Financiero, SBS o Indecopi) y el **plazo**, que debe aparecer tal como está en `Guardrail:PlazoRespuesta`.
- **Plazo:** su valor lo fija el estudiante desde la normativa vigente (Res. SBS N.° 04036-2022); el código no trae valor por defecto y el guardrail falla al construirse si falta.
- Falla ⇒ 1 regeneración ⇒ si falla otra vez ⇒ Derivar (motivo `GUARDRAIL_SALIDA`).
- **Afirmaciones para TA:** *verificable* = cada mención de monto, fecha o código de operación en el borrador y cada cita `[F:id]`; *no sustentada* = la que no coincide con los hechos verificados o cita un fragmento no admitido. TA = no sustentadas / verificables.
- En ablación T2 el guardrail se ejecuta y registra su veredicto, pero no bloquea.

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
