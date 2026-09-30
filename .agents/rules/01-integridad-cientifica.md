---
trigger: always_on
description: "Reglas de integridad científica de la tesis: nunca inventar resultados ni métricas."
---
# Integridad científica

- Nunca generes, estimes, redondees a conveniencia ni escribas a mano valores de P, TRA, CN, TA, TIG o latencias. Solo se calculan en `tools/analysis` a partir de la base de datos.
- No escribas números de resultados en código, tests, README, prompts ni documentos. Los tests verifican comportamiento, no metas de desempeño.
- Si una corrida falla o está incompleta, se reporta como tal; no se interpola ni se reemplaza.
- La tabla `eval.VerdadReferencia` no se lee desde `src/` en tiempo de ejecución. Solo la leen `tools/analysis` y los tests de evaluación.
- Cualquier cambio en umbrales (0.90, 0.78, S/ 1,000, k = 5), reglas R1–R9, prompts o métricas debe anunciarse al estudiante porque obliga a actualizar la tesis.
