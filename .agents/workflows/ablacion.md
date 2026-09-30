---
description: Ejecuta la condición T2 (sin RAG) con el modelo seleccionado para contrastar H4.
---
1. Pregunta qué modelo fue seleccionado (lo define el análisis del benchmark T1).
2. Ejecuta `dotnet run --project src/Reclamos.Benchmark -- --condicion T2 --modelo <id> --repeticiones 5`.
3. Verifica que en T2 no hubo llamadas a Pinecone (`t_RAG = 0`) y que el guardrail registró su veredicto sin bloquear.
