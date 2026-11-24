# Contribuindo

**Português** · [English](CONTRIBUTING.en.md) · [Español](CONTRIBUTING.es.md)

## Setup

São três árvores independentes. Rode da raiz do repositório.

```bash
# A lib .NET (SDK 8; os testes rodam em net6.0 e, no Windows, em net48)
dotnet build libs/dotnet -c Release
dotnet test libs/dotnet -c Release --no-build

# O site (Node 22.23.4 ou mais novo)
cd site && npm ci && npm run build && npm run check && cd ..

# A estrutura do repositório: arquivos, traduções, links e workflows
python -I tools/test_repo.py -v
```

O teste de estrutura usa só a biblioteca padrão do Python. Os testes que leem YAML precisam do
PyYAML (`python -m pip install pyyaml`) e são pulados sem ele.

## Estrutura do projeto

```
site/          # landing e especificação (Astro + Starlight), nos cinco idiomas
docs/          # specs, planos, ROADMAP e glossário (docs/i18n)
fixtures/      # as gravações do claude -p e o resultado esperado de cada uma
libs/dotnet/   # ClaudeCliProtocol (NuGet): netstandard2.0 e net6.0
libs/delphi/   # a lib em Delphi, piso 10.1 Berlin (fase 6)
examples/      # exemplos de console
tools/         # o teste de estrutura e a conferência do pacote
```

Tela nenhuma entra aqui. O componente Avalonia mora no
[claude-visual-chat](https://github.com/carlosdealmeida/claude-visual-chat).

## Como uma mudança entra

1. **Spec.** Uma decisão do protocolo ou da API vira um arquivo em [docs/specs/](docs/specs/).
2. **Plano.** O trabalho vira um plano em [docs/plans/](docs/plans/), em tarefas pequenas, cada uma
   com o seu teste.
3. **Um commit por tarefa**, com a mensagem em português: `feat(<área>): …`, `docs(<área>): …`,
   `test(<área>): …` ou `chore: …`.
4. **TDD.** O teste é escrito primeiro e visto falhar; depois vem o código que o faz passar.

Correção pequena (um erro de digitação, um link) não precisa de spec nem de plano.

## Fixtures

As gravações em `fixtures/` são sessões de verdade do `claude -p`. Nenhuma entra com dado pessoal de
quem gravou: nome de usuário, pastas, e-mail ou credencial. O gravador troca tudo isso por valores
de demonstração, e o teste `FixturePrivacyTests` confere cada arquivo. O gravador é o `tools/gravar.py`; ele sobe o `claude` de verdade e usa a conta da máquina.
Cada gravação tem ao lado o seu resultado esperado; o formato está em
[fixtures/README.md](fixtures/README.md).

## Traduções

O português é a fonte. Uma mudança no `README.md` vale para os cinco (`README.en.md`,
`README.es.md`, `README.zh-cn.md` e `README.zh-tw.md`), e o teste de estrutura acusa quando um
deles fica com menos seções. `SECURITY.md` e `CONTRIBUTING.md` têm três versões: português, inglês
e espanhol.

Os termos estão no glossário, em [docs/i18n/glossary.md](docs/i18n/glossary.md). As traduções foram feitas por IA e ainda
não foram revisadas por falante nativo: correção é muito bem-vinda, e o melhor lugar para começar
é o glossário.

## Estilo de código

- Código, nomes da API e comentários em inglês.
- Por padrão, sem comentários. Só adicione um quando o *porquê* não for óbvio.
- A lib não depende de nada além do que a spec permite: `System.Text.Json` e, no `netstandard2.0`,
  `Microsoft.Bcl.AsyncInterfaces`.
- Delphi: o piso é o 10.1 Berlin, então nada de variável inline nem de inferência de tipo.

## Release

Veja [RELEASING.md](RELEASING.md).
