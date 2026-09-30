# Prototipo de tesis — IA generativa para reclamos bancarios (UPN 2026)

Kit de arranque para desarrollar el prototipo con **Google Antigravity**.

## Qué contiene

| Ruta | Para qué sirve |
|---|---|
| `AGENTS.md` | Contexto permanente del proyecto (Antigravity lo lee siempre) |
| `.agents/rules/` | Reglas: integridad científica, arquitectura, C#, Python, seguridad, prompts |
| `.agents/workflows/` | Comandos `/crear-solucion`, `/generar-banco`, `/indexar-normativa`, `/benchmark`, `/ablacion`, `/analisis`, `/cerrar-hito` |
| `docs/SPEC.md` | Especificación técnica derivada de la tesis |
| `docs/PLAN.md` | Hitos H0–H10 con criterio de "listo" |
| `docs/PROMPT_INICIAL.md` | Prompt para empezar y prompts por hito |
| `docs/models.example.json` | Registro de los 8 modelos del benchmark (completar nombres exactos) |
| `prompts/` | Prompts versionados del clasificador y del generador |
| `db/schema.sql` | Esquema de referencia (core, app, eval) |
| `infra/` | Docker Compose con SQL Server y plantilla de Dockerfile con Tesseract |
| `tools/` | Especificación del generador del banco sintético y del análisis estadístico |
| `knowledge/` | Dónde colocar la normativa para Pinecone |

## Primeros pasos

1. Instala: .NET 10 SDK, Docker Desktop, Python 3.12, Git y Antigravity.
2. Descomprime este kit en una carpeta vacía, ejecuta `git init` y haz el primer commit.
3. `copy .env.example .env` (Windows) o `cp .env.example .env` y completa claves.
4. `docker compose -f infra/docker-compose.yml --env-file .env up -d`
5. Abre la carpeta en Antigravity, ve al Agent Manager y pega el prompt de `docs/PROMPT_INICIAL.md` §1.
6. Avanza hito por hito con los prompts de §2 y cierra cada uno con `/cerrar-hito`.

## Cuentas y claves que necesitas

- Pinecone (plan gratuito alcanza para la normativa).
- Proveedor de embeddings (el que definan en `Rag:EmbeddingModel`).
- OpenAI, Anthropic, Google AI Studio, Mistral y un proveedor de inferencia para Llama 4 Maverick (solo para el benchmark).
