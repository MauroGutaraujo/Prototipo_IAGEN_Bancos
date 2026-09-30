---
description: Fragmenta la normativa de knowledge/raw por artículo y la indexa en Pinecone.
---
1. Confirma que existen los textos en `knowledge/raw/` (Res. SBS 04036-2022 y modificatorias, Ley 29571, políticas simuladas). Si faltan, detente y pide al estudiante que los descargue de fuentes oficiales.
2. Fragmenta por artículo/numeral. Metadatos: `norma`, `articulo`, `vigencia`, `fuente`.
3. Genera embeddings con el modelo configurado en `Rag:EmbeddingModel` y verifica que la dimensión coincide con el índice.
4. Crea el índice Pinecone (serverless, `cosine`) si no existe y sube los vectores con ids estables (`<norma>-<articulo>`).
5. Prueba 3 consultas (una por tipología) e imprime ids y scores.
