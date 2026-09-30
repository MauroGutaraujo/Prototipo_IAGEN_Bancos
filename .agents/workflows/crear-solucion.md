---
description: Crea la solución .NET 10 con los proyectos, referencias y paquetes base del prototipo.
---
1. Verifica `dotnet --version` (debe ser 10.x). Si no, detente y avisa.
2. Desde la raíz del repo ejecuta:
   - `dotnet new sln -n Reclamos`
   - `dotnet new classlib -n Reclamos.Domain -o src/Reclamos.Domain`
   - `dotnet new classlib -n Reclamos.Guardrails -o src/Reclamos.Guardrails`
   - `dotnet new classlib -n Reclamos.Application -o src/Reclamos.Application`
   - `dotnet new classlib -n Reclamos.Infrastructure -o src/Reclamos.Infrastructure`
   - `dotnet new web -n Reclamos.Api -o src/Reclamos.Api`
   - `dotnet new console -n Reclamos.Benchmark -o src/Reclamos.Benchmark`
   - `dotnet new xunit -n Reclamos.Guardrails.Tests -o tests/Reclamos.Guardrails.Tests`
   - `dotnet new xunit -n Reclamos.Application.Tests -o tests/Reclamos.Application.Tests`
   - Agrega todos a la solución con `dotnet sln add`.
3. Referencias: Guardrails→Domain; Application→Domain, Guardrails; Infrastructure→Application; Api→Application, Infrastructure; Benchmark→Application, Infrastructure; tests→proyecto probado.
4. Paquetes (consulta la versión estable actual en NuGet antes de instalar): `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.SemanticKernel`, `Pinecone.Client`, `SixLabors.ImageSharp` en Infrastructure; `Moq` en tests.
5. Activa Nullable y TreatWarningsAsErrors en Domain y Guardrails.
6. Crea las entidades de `db/schema.sql` en Domain y el `DbContext` con esquemas `core`, `app`, `eval` en Infrastructure. Genera la migración `Inicial`.
7. Agrega `/health` en Api.
8. Corre `dotnet build` y `dotnet test`. Resume lo creado.
