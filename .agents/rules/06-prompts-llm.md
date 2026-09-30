---
trigger: glob
globs: "prompts/**, **/*Classifier*.cs, **/*Generator*.cs"
description: "Reglas para modificar prompts del clasificador y del generador."
---
# Prompts

- Los prompts viven en `prompts/` con versión en el nombre (`clasificador.v1.md`). Un cambio = nueva versión; la versión usada se registra en `Ejecucion`.
- Mismo prompt para los 8 modelos del benchmark. No se ajustan prompts para favorecer a un modelo.
- El generador solo puede citar fragmentos como `[F:<id>]` y debe incluir la línea `DECISIÓN: PROCEDENTE` o `DECISIÓN: IMPROCEDENTE` según la ruta recibida.
