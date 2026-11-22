# Release

O release é automático pelo GitHub Actions, no workflow `.github/workflows/release.yml`. Um
`git push` de uma tag `v<semver>` faz, nesta ordem:

1. confere que a tag é a `<Version>` do `libs/dotnet/src/ClaudeCliProtocol/ClaudeCliProtocol.csproj`;
2. compila e roda os testes;
3. empacota (`.nupkg` e `.snupkg`) e confere o pacote com `tools/check_package.py`;
4. cria o GitHub Release com o `CHANGELOG.md` no corpo e os pacotes anexados;
5. publica no NuGet, quando o secret `NUGET_USER` existe. Sem ele, esse passo é pulado e o pacote
   fica só no GitHub Release.

A lib Delphi entra no mesmo release como um zip, a partir da fase 6.

## Estado

Em 2026-11-22: nenhum release saiu, e nenhuma das configurações de "Uma vez só" foi feita. O
repositório no GitHub está privado e vazio. Este arquivo descreve o processo; ele ainda não rodou.

Os quatro workflows (`ci.yml`, `release.yml`, `pages.yml` e `fixtures.yml`) foram escritos e
conferidos como YAML pelo teste de estrutura. **Nenhum rodou no GitHub ainda.** No `fixtures.yml`,
os passos que instalam o Claude Code, gravam e abrem o pull request só rodam a partir da fase 2,
quando o gravador chegar; até lá o workflow avisa que não há gravador e termina sem erro.

## Fazer um release

```bash
# 1. A versão: <Version> em libs/dotnet/src/ClaudeCliProtocol/ClaudeCliProtocol.csproj

# 2. O CHANGELOG: o que está em [Unreleased] passa para uma seção "[X.Y.Z] - AAAA-MM-DD",
#    e [Unreleased] fica vazia, acima dela.

# 3. Um commit com as duas mudanças
git add libs/dotnet/src/ClaudeCliProtocol/ClaudeCliProtocol.csproj CHANGELOG.md
git commit -m "chore: versão X.Y.Z"

# 4. A tag, anotada
git tag -a vX.Y.Z -m vX.Y.Z

# 5. O push
git push origin master --follow-tags

# 6. Conferir que a tag chegou ao remoto (é ela que dispara o workflow)
git ls-remote --tags origin vX.Y.Z
```

> **Por que `-a`:** o `git push --follow-tags` só envia tag *anotada*. Uma tag leve fica na sua
> máquina sem aviso, e o workflow nunca dispara (aconteceu no `claude-todos` 0.13.0). Se o passo 6
> não imprimir nada, envie a tag à mão: `git push origin vX.Y.Z`.

Depois, acompanhe o workflow até o fim (`gh run watch`) e abra o GitHub Release para conferir os
anexos. O job verde não garante a publicação no NuGet: leia o log do passo de publicação.

## Uma vez só

### NuGet (Trusted Publishing)

O workflow não guarda chave do NuGet. Ele troca o token OIDC do GitHub por uma chave de uma hora.

1. No nuget.org, em **Trusted Publishing**, crie uma política para o repositório
   `carlosdealmeida/claude-cli-protocol` e o workflow `release.yml`.
2. No GitHub, crie o secret `NUGET_USER` com o **nome do perfil** do nuget.org (não o e-mail):

   ```bash
   gh secret set NUGET_USER -R carlosdealmeida/claude-cli-protocol
   ```

O nome do pacote, `ClaudeCliProtocol`, estava livre no nuget.org em 2026-11-22. Ele só fica
reservado com a primeira publicação.

### GitHub Pages

O site é publicado pelo workflow `.github/workflows/pages.yml`. Em **Settings → Pages**, escolha
**Source: GitHub Actions**. O GitHub Pages de um repositório privado depende do plano da conta; num
repositório público funciona em qualquer plano.

### A chave das fixtures

O workflow `.github/workflows/fixtures.yml` regrava as fixtures toda semana com o `claude -p` de
verdade, e para isso precisa de uma API key. Ela fica **só** como secret do GitHub:

```bash
gh secret set ANTHROPIC_API_KEY -R carlosdealmeida/claude-cli-protocol
```

Nenhuma chave entra no repositório, em arquivo nenhum. Sem o secret, o workflow avisa e termina
sem erro.

Para o workflow conseguir abrir o pull request, ligue em **Settings → Actions → General** a opção
**Allow GitHub Actions to create and approve pull requests**. Um pull request aberto com o
`GITHUB_TOKEN` não dispara o CI sozinho: feche e reabra o pull request antes de juntar.

## Tirar uma versão do ar

No nuget.org, uma versão não é apagada: ela é retirada da listagem (**Manage package → Listing**).
Quem já depende dela continua conseguindo restaurar.
