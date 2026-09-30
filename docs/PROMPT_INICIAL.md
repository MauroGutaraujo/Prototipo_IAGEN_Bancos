# Prompts para Antigravity

## 1. Prompt inicial (pegar en el Agent Manager al abrir el repo por primera vez)

```
Eres el asistente de desarrollo de mi tesis. Antes de escribir código, lee AGENTS.md, docs/SPEC.md,
docs/PLAN.md y las reglas en .agents/rules/. Luego:

1. Resúmeme en 10 líneas qué vamos a construir y cuáles son las reglas que no se pueden romper.
2. Verifica mi entorno: versión de .NET (necesito 10.x), Docker, Python 3.12 y Git. Dime qué falta instalar.
3. Propón el plan del hito H0 y H1 de docs/PLAN.md como lista de tareas y espera mi confirmación.

Restricciones: no inventes APIs ni versiones de paquetes (consúltalas), no escribas números de
resultados en ningún archivo, y trabaja en español.
```

## 2. Prompts por hito (usar uno a la vez)

**H1 – Esqueleto**
```
Ejecuta el workflow /crear-solucion. Cuando termine, muéstrame el árbol de carpetas, el resultado de
dotnet build y dotnet test, y la migración Inicial generada.
```

**H2 – Banco sintético**
```
Implementa tools/dataset-generator siguiendo su README y la SPEC §2.3. Empieza por el modelo de datos
y el cálculo de RutaCorrecta/ReglaEsperada; luego las narraciones; al final los vouchers con ruido.
Muéstrame 3 vouchers de ejemplo (uno por tipología) antes de generar los 200.
Después ejecuta /generar-banco.
```

**H3 – Agente Legal**
```
Implementa Reclamos.Guardrails: reglas R1–R9 según la SPEC §2.3 y el guardrail de salida de la §2.6,
con parámetros por IOptions. Escribe primero los tests (uno por regla, con casos borde: monto 1000.00
exacto y confianza 0.90 exacta), luego la implementación. Meta: 100 % de cobertura del proyecto.
```

**H4 – OCR**
```
Implementa IOcrAgent en Infrastructure usando la CLI de Tesseract con salida TSV (-l spa --psm 6),
preprocesamiento con ImageSharp y extracción por regex según la SPEC §2.1. Agrega un test de integración
que procese 10 vouchers del banco y reporte aciertos por campo contra verdad_referencia.csv (sin fijar
umbrales de desempeño en el test).
```

**H5 – CRAG**
```
Ejecuta /indexar-normativa y luego implementa INormativeRetriever con Pinecone (topK 5, θ = 0.78,
una reformulación, derivación si falla) y el flag Rag:Enabled para la ablación. Registra todo en
app.Recuperacion.
```

**H6 – LLM y orquestador**
```
Implementa IIntentClassifier e IResolutionGenerator con Semantic Kernel usando docs/models.example.json
(endpoint compatible OpenAI por proveedor, temperatura 0), cargando prompts desde prompts/. Valida la
salida del clasificador con prompts/clasificador.schema.json. Luego implementa el orquestador secuencial
con la máquina de estados de la SPEC §1 y mide cada etapa con Stopwatch. Prueba un caso por tipología.
```

**H7 – Ficha de cronometraje**
```
Implementa la ficha web T0 de la SPEC §5 en Reclamos.Api: asignación 60/60/60 estratificada + 20 de
calibración, orden aleatorio por analista, pausa/reanudar, guardado en app.RegistroManual.
```

**H8 – Benchmark**
```
Implementa Reclamos.Benchmark según la SPEC §4 (8 modelos × 5 repeticiones, orden rotado, registro de
versión y CorridaBenchmark, modo --condicion T2). Haz solo una corrida seca de 2 modelos × 1 repetición
× 10 casos y muéstrame las filas escritas.
```

**H9 – Análisis**
```
Implementa tools/analysis según su README y la SPEC §7. Pruébalo con los datos de la corrida seca y
muéstrame la lista de CSV generados. No redactes conclusiones.
```

## 3. Frases útiles durante el trabajo

- "Antes de seguir, ¿esto cambia algo de lo descrito en la tesis?"
- "Ejecuta /cerrar-hito."
- "Muéstrame el diff y explícame cada cambio en una línea."
- "¿Qué versión exacta de este paquete estás usando y dónde lo verificaste?"
