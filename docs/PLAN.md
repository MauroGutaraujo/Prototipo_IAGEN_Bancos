# PLAN — Hitos de implementación

Cada hito termina con: pruebas en verde, commit y un resumen breve. No avanzar al siguiente sin cerrar el anterior.

## H0 — Entorno (½ día)
- Instalar .NET 10 SDK, Docker Desktop, Python 3.12, Git.
- `docker compose -f infra/docker-compose.yml up -d` levanta SQL Server.
- Copiar `.env.example` a `.env` y completar claves (no se sube a Git).
- **Listo cuando:** `docker ps` muestra SQL Server sano y `dotnet --version` ≥ 10.

## H1 — Esqueleto de la solución (1 día)
- Crear la solución y los proyectos de `AGENTS.md` (`/crear-solucion`).
- Referencias entre proyectos: Api → Application → Domain; Infrastructure → Application; Guardrails → Domain.
- EF Core + migración inicial con los esquemas `core`, `app`, `eval`.
- **Listo cuando:** `dotnet build` y `dotnet test` pasan; `/health` responde.

## H2 — Banco sintético (2 días)
- `tools/dataset-generator`: 200 expedientes (80/80/40), transacciones del core, narraciones, vouchers JPG con ruido, verdad de referencia. Semilla fija.
- Casos deliberados que disparan cada regla R1–R9 (incluye montos > S/ 1,000 y vouchers ilegibles).
- Carga a SQL Server.
- **Listo cuando:** conteos 80/80/40 verificados, cada regla tiene al menos un caso, y regenerar con la misma semilla produce archivos idénticos (hash).

## H3 — Agente Legal y guardrail de salida (2 días)
- `Reclamos.Guardrails` con R1–R9 y verificaciones del guardrail de salida. Parámetros desde config.
- **Listo cuando:** 100 % de cobertura en el proyecto y una prueba por regla, incluidos los bordes (monto = 1000.00 exacto, conf = 0.90 exacta).

## H4 — Agente OCR (2 días)
- Tesseract en contenedor o local; parseo TSV; regex; confianza por campo.
- **Listo cuando:** existe un test que corre el OCR sobre 10 vouchers del banco y compara con la verdad de referencia (el test reporta, no fija números).

## H5 — Base de conocimiento y CRAG (2 días)
- Colocar normas en `knowledge/raw`, fragmentar por artículo, indexar en Pinecone (`/indexar-normativa`).
- Implementar recuperación, umbral 0.78, reformulación única, derivación.
- **Listo cuando:** consulta de prueba devuelve fragmentos con score y el flag de ablación desactiva la recuperación.

## H6 — Clasificador y generador con Semantic Kernel (3 días)
- Registro de modelos por config (`models.json`), temperatura 0, salida JSON validada, prompts versionados.
- Orquestador completo con máquina de estados y tiempos por etapa.
- **Listo cuando:** `POST /api/expedientes/{id}/procesar` completa un caso de cada tipología con un modelo y deja todos los tiempos en `Ejecucion`.

## H7 — Ficha de cronometraje T0 (2 días)
- Página web para analistas, asignación 60+60+60 y 20 de calibración, pausas.
- **Listo cuando:** un analista de prueba completa 3 casos y los tiempos quedan en `RegistroManual`.

## H8 — Benchmark runner (2 días)
- Consola que ejecuta 8 modelos × 5 repeticiones × 200 casos, orden rotado, registro de versión y de la ventana.
- Modo `--condicion T2` para la ablación sin RAG.
- **Listo cuando:** una corrida "seca" con 2 modelos × 1 repetición × 10 casos funciona de punta a punta.

## H9 — Análisis estadístico (2 días)
- `tools/analysis`: lee SQL Server, calcula métricas y pruebas de la SPEC §7, exporta tablas CSV para el Capítulo IV.
- **Listo cuando:** con datos de la corrida seca produce el reporte sin errores (los números de la corrida seca no se usan en la tesis).

## H10 — Ejecución real (ventana ≤ 7 días)
- Medición manual T0 con los 3 analistas → benchmark T1 → ablación T2 con el modelo seleccionado → análisis.
- **Solo aquí** se generan los resultados del Capítulo IV.
