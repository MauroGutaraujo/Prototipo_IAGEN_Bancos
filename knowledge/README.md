# Base de conocimiento normativo

Corpus que recupera el CRAG (SPEC §2.4). Solo texto público oficial o simulado: nada de documentos internos reales.

## Archivos

| Archivo | Qué es |
|---|---|
| `fragmentos.jsonl` | Un fragmento por artículo: `id`, `norma`, `articulo`, `tipo` (`oficial`/`simulada`), `vigencia`, `fuente`, `texto`. **Versionado.** |
| `manifest.json` | SHA-256 de `fragmentos.jsonl` (define el namespace en Pinecone) y SHA-256 de cada fuente. |
| `raw/politicas_banco_simulado.txt` | Política interna **simulada** del banco ficticio (PIR-2026-V1). |
| `raw/*.pdf` | PDF oficiales descargados por `tools/knowledge/fragmentar.py --descargar`. No se versionan; su SHA-256 está fijado en el script. |

## Fuentes oficiales (texto literal)

| Norma | Fuente | Selección |
|---|---|---|
| Res. SBS N.° 04036-2022, Reglamento de Gestión de Reclamos y Requerimientos | [PDF de la SBS](https://intranet2.sbs.gob.pe/dv_int_cn/2230/v1.0/Adjuntos/4036-2022.r.pdf) | Reglamento, arts. 1–19 |
| Ley N.° 29571, Código de Protección y Defensa del Consumidor | [Texto actualizado de Indecopi en gob.pe (2023)](https://cdn.www.gob.pe/uploads/document/file/4265044/Co%CC%81digo%20de%20Proteccio%CC%81n%20y%20Defensa%20del%20Consumidor%20-%202023%20(1).pdf.pdf) | Arts. 18, 19 (idoneidad), 24 (atención de reclamos), 81–96 incl. 90-A (productos o servicios financieros), 150–152 (libro de reclamaciones) |
| Ley N.° 31435 | [Congreso de la República](https://leyes.congreso.gob.pe/Documentos/2021_2026/ADLP/Texto_Consolidado/31435-TXM.pdf) | Artículo único |

## Regenerar

```
cd tools/knowledge
py -3.12 -m venv .venv && .venv\Scripts\activate
pip install -r requirements.txt
python fragmentar.py --descargar   # descarga y verifica el SHA-256 de cada PDF
python fragmentar.py               # escribe ../../knowledge/fragmentos.jsonl y manifest.json
python -m pytest                   # incluye: regenerar reproduce el SHA-256 versionado
```

Si cambia `fragmentos.jsonl`, cambia su SHA-256: hay que actualizar `Rag:CorpusSha256` y volver a indexar (commit con `[indexar]`). Cada versión del corpus vive en su propio namespace de Pinecone.

## Limpieza y limitaciones conocidas

- La limpieza solo quita maquetación: números de página, pie de página de la SBS, llamadas y notas al pie («Disposición vigente a partir del 28.02.2023»), saltos de línea y guiones de corte. No se reescribe el contenido.
- En el PDF oficial de la SBS el artículo 17 aparece como «17. Absolución de consultas…», sin la palabra «Artículo»; se conserva así.
- En el PDF del Congreso, la elipsis «[…]» viene codificada como «[?]»; se restituye «[…]».
- La extracción de texto del PDF de la SBS separa algunas palabras («par a», «notifi cación», «re clamos»). Son artefactos del PDF, no del contenido; se dejan tal cual para no alterar el texto. Revísalos al validar el corpus.
