---
description: Ejecuta el benchmark de modelos (T1) con 5 repeticiones y orden rotado.
---
1. Confirma con el estudiante si es corrida seca (2 modelos × 1 repetición × 10 casos) o corrida real. La corrida real solo se hace en la ventana acordada.
2. Verifica disponibilidad de cada modelo de `src/Reclamos.Benchmark/models.json` con una llamada mínima; si alguno falla, detente y avisa.
3. Registra el inicio en `CorridaBenchmark`.
4. Ejecuta `dotnet run --project src/Reclamos.Benchmark -- --condicion T1 --repeticiones 5`.
5. Al terminar registra el fin y muestra solo: filas escritas por modelo y repetición, errores y duración. No calcules ni resumas métricas aquí.
