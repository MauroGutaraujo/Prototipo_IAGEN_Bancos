---
description: Genera el banco sintético de 200 expedientes con vouchers JPG y verdad de referencia, y lo carga a SQL Server.
---
1. Lee `docs/SPEC.md` §2.3 (reglas) y `tools/dataset-generator/README.md`.
2. Crea/activa el entorno virtual en `tools/dataset-generator` e instala `requirements.txt`.
3. Ejecuta `python generate.py --seed 2026 --out data/` (o implementa el script si aún no existe, siguiendo el README).
4. Verifica: 80 C1, 80 C2, 40 C3; al menos un caso por regla R1–R9; hash SHA-256 del manifiesto igual en dos ejecuciones con la misma semilla.
5. Carga a SQL Server con `python load.py --conn "$SQL_CONN"`.
6. Muestra un resumen de conteos por clase y por ruta correcta (no métricas del sistema).
