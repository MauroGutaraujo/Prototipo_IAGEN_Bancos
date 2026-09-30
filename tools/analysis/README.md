# Análisis estadístico

Lee `app.*` y `eval.VerdadReferencia` desde SQL Server y produce en `out/`:

- `metricas_por_modelo.csv`: P, precision/recall/F1 macro, TRA, TRA_max, CN, TIG, TA, L_LLM (media, P50, P95) por modelo y repetición; media ± s e IC 95 % de las 5 repeticiones.
- `matriz_confusion_<modelo>.csv` (intención) y `ocr_por_campo.csv` (VP, FP, FN, VN, EM_OCR).
- `seleccion_modelo.csv`: filtro CN ≥ 99 %, normalización mín-máx, S_m = 0.30 P + 0.30 TRA + 0.25 CN + 0.15 L, sensibilidad de pesos ±0.10.
- `comparacion_modelos.csv`: Q de Cochran + McNemar por pares con corrección de Holm (criterio de mayoría ≥ 3/5).
- `h1_mcnemar.csv`, `h2_latencia.csv` (Shapiro-Wilk, t pareada o Wilcoxon unilateral, d_z o r, RR %), `h3_binomial.csv` (π0 = 0.60, unilateral), `h4_mcnemar.csv` (T1 vs T2 sobre borradores), `icc_calibracion.csv` (ICC2,1 ≥ 0.85).

Línea base manual: para los 20 casos de calibración se usa la media de los tres analistas; para los 180 restantes, el tiempo del analista asignado. Tiempo = TFinal − TIngreso − Pausas.

No redacta conclusiones: solo exporta valores.

```
pip install -r requirements.txt
python analyze.py --conn "%SQL_CONN%" --out out/
```
