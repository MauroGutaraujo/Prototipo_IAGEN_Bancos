---
trigger: glob
globs: "tools/**/*.py, tools/**/requirements.txt"
description: "Convenciones para el generador del banco sintético y el análisis estadístico."
---
# Python (tools/)

- Python 3.12, entorno virtual por herramienta, dependencias fijadas en `requirements.txt`.
- Generador: semilla global fija (`--seed`), salida determinista, vouchers con plantillas genéricas sin marcas reales, sin nombres de personas reales (usar seudónimos).
- Composición obligatoria: 80 C1, 80 C2, 40 C3. Incluir casos que activen cada regla R1–R9.
- Análisis: `pandas`, `scipy.stats` (`shapiro`, `wilcoxon`, `ttest_rel`, `binomtest`), `statsmodels` (`mcnemar`, `cochrans_q`), `pingouin` (`intraclass_corr`, ICC2). α = 0.05.
- El análisis lee de SQL Server (pyodbc o SQLAlchemy) y exporta CSV en `tools/analysis/out/`. No imprime conclusiones inventadas; solo valores calculados.
