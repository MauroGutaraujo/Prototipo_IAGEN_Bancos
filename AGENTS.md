# AGENTS.md — Contexto del proyecto (lectura obligatoria para el agente)

## Qué es este proyecto

Prototipo de tesis (UPN, Ingeniería de Sistemas Computacionales, Trujillo 2026):
**"Modelo de Inteligencia Artificial Generativa para automatizar la atención al cliente y resolución de reclamos en entidades bancarias, Perú 2026"**.

Un sistema **híbrido neuro-simbólico** que recibe un reclamo bancario (narración + voucher JPG), extrae datos, decide la ruta con reglas deterministas y, solo si la ruta es automática, redacta una resolución fundamentada en normativa recuperada de una base vectorial. Todo se evalúa sobre un **banco sintético de 200 expedientes** con verdad de referencia conocida.

La especificación completa está en `docs/SPEC.md`. El plan por hitos está en `docs/PLAN.md`. Si algo de este archivo contradice a SPEC.md, manda SPEC.md.

## Principio de diseño (no negociable)

- **Lo neuronal percibe y redacta; lo simbólico decide y controla.**
- El LLM **nunca** decide si un reclamo procede. Esa decisión la toma el **Agente Legal** (motor de reglas en C#, sin LLM).
- El LLM solo: (1) clasifica la intención con salida JSON validada, y (2) redacta la resolución a partir de hechos verificados y fragmentos normativos recuperados.
- Toda salida del LLM pasa por un **guardrail de salida** determinista antes de emitirse.

## Stack

- C# 14 / .NET 10, ASP.NET Core (Minimal API), Semantic Kernel (orquestación LLM).
- SQL Server 2022 + Entity Framework Core (persistencia, núcleo transaccional simulado, auditoría).
- Pinecone serverless, métrica coseno (solo texto normativo público).
- Tesseract OCR 5 (CLI, salida TSV con confianza por palabra), idioma `spa`.
- Python 3.12 en `tools/` para generar el banco sintético y para el análisis estadístico.
- Docker Compose para el entorno sandbox.

## Estructura de la solución

```
src/Reclamos.Domain          Entidades y enums (sin dependencias)
src/Reclamos.Guardrails      Reglas R1–R10 y guardrail de salida (puro, sin IO, 100 % testeable)
src/Reclamos.Application     Orquestador (máquina de estados), interfaces de agentes, métricas
src/Reclamos.Infrastructure  EF Core, Pinecone, OCR (Tesseract CLI), LLM (Semantic Kernel)
src/Reclamos.Api             Endpoints REST + ficha web de cronometraje para analistas
src/Reclamos.Benchmark       Consola: corre N modelos × 5 repeticiones sobre los 200 casos
tests/*                      xUnit + Moq
tools/dataset-generator      Python: genera expedientes, vouchers JPG con ruido y verdad de referencia
tools/analysis               Python: métricas y pruebas estadísticas desde SQL Server
prompts/                     Prompts versionados del clasificador y del generador
knowledge/                   Normativa (SBS, Indecopi) para indexar en Pinecone
db/schema.sql                Esquema de referencia
```

## Parámetros fijos (vienen de la tesis; no cambiarlos sin avisar)

| Parámetro | Valor |
|---|---|
| Umbral OCR `U_ocr` | 0.90 (confianza normalizada 0–1 por campo) |
| Umbral similitud `θ` (Pinecone, coseno) | 0.35 (calibrado para `llama-text-embed-v2`; originalmente 0.78, ver SPEC §2.4) |
| Fragmentos recuperados `k` | 5 |
| Umbral de riesgo monetario `U_riesgo` | S/ 1,000.00 (monto > umbral ⇒ derivar) |
| Regeneraciones del borrador | 1 como máximo |
| Temperatura LLM | 0 |
| Repeticiones por modelo en benchmark | 5 (NO es "5-fold") |
| Clases de intención | C1 cobro duplicado · C2 operación no reconocida · C3 caída de pasarela |
| Banco sintético | 200 = 80 C1 + 80 C2 + 40 C3 |

## Reglas de integridad científica (críticas)

1. **Nunca inventes, estimes ni "rellenes" resultados.** Toda métrica (P, TRA, CN, TA, latencias) se calcula solo a partir de registros reales en la tabla `Ejecucion` y afines. Si no hay datos, el reporte dice "sin datos".
2. No escribas números de resultados en código, README, prompts ni en la tesis.
3. La verdad de referencia (`VerdadReferencia`) está en un esquema aislado (`eval`) y **ningún agente del sistema la lee en tiempo de ejecución**; solo `tools/analysis` y los tests de evaluación.
4. Registra siempre el identificador exacto del modelo, la fecha/hora y los parámetros de cada corrida.
5. No uses datos reales de clientes ni logotipos/marcas de bancos reales en vouchers.

## Convenciones de código

- Dominio en español (Expediente, Transaccion, Ruta, Intencion) para calzar con la tesis; infraestructura y nombres técnicos en inglés.
- Nullable habilitado, `async` de punta a punta, `CancellationToken` en toda llamada de IO.
- Cada etapa del orquestador mide su tiempo con `Stopwatch` y lo persiste (t_OCR, t_Guardrail, t_RAG, L_CLS, L_GEN, t_orq).
- Las etapas del pipeline se ejecutan **en secuencia** (la tesis usa una descomposición aditiva de la latencia).
- Secretos solo por variables de entorno / user-secrets. Nunca en el repo.
- Antes de usar un paquete NuGet o PyPI, verifica su versión actual y su API real; no inventes métodos.
- Todo cambio en reglas del guardrail va con su prueba unitaria.

## Cómo trabajar conmigo (estudiante)

- Explica en español, breve. Antes de tareas grandes, muestra el plan y espera confirmación.
- Trabaja por hitos de `docs/PLAN.md`; al cerrar un hito, corre los tests y resume qué quedó y qué falta.
- Si una decisión cambia algo descrito en la tesis (umbrales, arquitectura, métricas), avísame explícitamente para actualizar el documento.
