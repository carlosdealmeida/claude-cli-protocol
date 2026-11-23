# Contribuir

[Português](CONTRIBUTING.md) · [English](CONTRIBUTING.en.md) · **Español**

## Setup

Son tres árboles independientes. Ejecuta desde la raíz del repositorio.

```bash
# La biblioteca .NET (SDK 8; las pruebas corren en net6.0 y, en Windows, en net48)
dotnet build libs/dotnet -c Release
dotnet test libs/dotnet -c Release --no-build

# El sitio (Node 22.23.4 o más reciente)
cd site && npm ci && npm run build && npm run check && cd ..

# La estructura del repositorio: archivos, traducciones, enlaces y workflows
python -I tools/test_repo.py -v
```

La prueba de estructura usa solo la biblioteca estándar de Python. Las pruebas que leen YAML
necesitan PyYAML (`python -m pip install pyyaml`) y se omiten sin él.

## Estructura del proyecto

```
site/          # landing y especificación (Astro + Starlight), en los cinco idiomas
docs/          # specs, planes, ROADMAP y glosario (docs/i18n)
fixtures/      # las grabaciones de claude -p y el resultado esperado de cada una
libs/dotnet/   # ClaudeCliProtocol (NuGet): netstandard2.0 y net6.0
libs/delphi/   # la biblioteca en Delphi, piso 10.1 Berlin (fase 6)
examples/      # ejemplos de consola
tools/         # la prueba de estructura y la verificación del paquete
```

Aquí no entra ninguna interfaz gráfica. El componente Avalonia vive en
[claude-visual-chat](https://github.com/carlosdealmeida/claude-visual-chat).

## Cómo entra un cambio

1. **Spec.** Una decisión del protocolo o de la API se convierte en un archivo en
   [docs/specs/](docs/specs/).
2. **Plan.** El trabajo se convierte en un plan en [docs/plans/](docs/plans/), en tareas pequeñas,
   cada una con su prueba.
3. **Un commit por tarea**, con el mensaje en portugués: `feat(<área>): …`, `docs(<área>): …`,
   `test(<área>): …` o `chore: …`.
4. **TDD.** La prueba se escribe primero y se ve fallar; después viene el código que la hace pasar.

Una corrección pequeña (una errata, un enlace) no necesita spec ni plan. Las specs y los planes se
escriben en portugués; una issue o un pull request en español o en inglés es bienvenido.

## Fixtures

Las grabaciones de `fixtures/` son sesiones reales de `claude -p`. Ninguna entra con datos
personales de quien la grabó: nombre de usuario, carpetas, correo o credenciales. El grabador cambia
todo eso por valores de demostración, y la prueba `FixturePrivacyTests` revisa cada archivo. El
grabador llega en la fase 2. Cada grabación tiene al lado su resultado esperado; el formato está en
[fixtures/README.md](fixtures/README.md) (en portugués).

## Traducciones

El portugués es la fuente. Un cambio en `README.md` vale para los cinco (`README.en.md`,
`README.es.md`, `README.zh-cn.md` y `README.zh-tw.md`), y la prueba de estructura avisa cuando uno
de ellos queda con menos secciones. `SECURITY.md` y `CONTRIBUTING.md` tienen tres versiones:
portugués, inglés y español.

Los términos están en el glosario, en [docs/i18n/glossary.md](docs/i18n/glossary.md). Las traducciones fueron hechas por
IA y aún no han sido revisadas por un hablante nativo: las correcciones son muy bienvenidas, y el
glosario es el mejor lugar para empezar.

## Estilo de código

- Código, nombres de la API y comentarios en inglés.
- Por defecto, sin comentarios. Añade uno solo cuando el *porqué* no sea obvio.
- La biblioteca no depende de nada más allá de lo que la spec permite: `System.Text.Json` y, en
  `netstandard2.0`, `Microsoft.Bcl.AsyncInterfaces`.
- Delphi: el piso es 10.1 Berlin, así que nada de variables inline ni de inferencia de tipos.

## Release

Consulta [RELEASING.md](RELEASING.md) (en portugués).
