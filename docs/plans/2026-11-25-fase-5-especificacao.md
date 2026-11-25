# Fase 5: a especificação no site — Implementation Plan

**Goal:** O site passa a ter a especificação do protocolo do `claude -p`: o que ele escreve no stdout,
o que lê no stdin, os pedidos de controle nos dois sentidos, o transporte que o sobe e as fixtures
que provam tudo isso. Uma lib em outra linguagem (a de Delphi, na fase 6) é escrita a partir dessas
páginas e das fixtures, sem ler o código da lib .NET.

**Architecture:** Seis páginas novas no Starlight, num grupo "Especificação" da barra lateral, logo
depois da "Visão geral". O português é escrito primeiro e revisado pelo dono do projeto; as
traduções vêm depois, numa segunda leva, com o glossário de `docs/i18n/`. Nenhuma página nova tem
componente interativo nesta fase: tocar uma fixture na página (o componente Svelte da spec) fica
para depois.

**Spec:** [docs/specs/2026-11-22-claude-cli-protocol-design.md](../specs/2026-11-22-claude-cli-protocol-design.md),
seções "O site", "Os transportes", "A regra de segurança da resposta de permissão", "As fixtures e a
conformidade" e as decisões 9 a 27. O [fixtures/README.md](../../fixtures/README.md) diz que a tabela
de todos os eventos é desta fase.

## Global Constraints

- **A fonte é o que existe.** Cada forma de linha, campo e valor citado sai de uma gravação de
  `fixtures/` (com o arquivo e a linha) ou do código da lib .NET no commit `1bbba94`. O que nenhuma
  das duas mostra não entra, ou entra dito como não gravado.
- **Exemplos das gravações.** Uma linha de exemplo é uma linha de uma gravação, cortada onde for
  longa (`…`), nunca inventada. As linhas do stdin, que as gravações não guardam, saem do
  `StdinWriter` e do `tools/gravar.py`.
- **Nomes do protocolo e da API** como estão, sem tradução (o glossário diz quais).
- **Os nomes de evento e de campo** são os dos arquivos esperados (`snake_case`), não os do C#.
- **O que é da lib e o que é do `claude`.** O que o `claude` faz é dito como fato observado; o que é
  regra da lib (a conferência dos argumentos, a regra da resposta de permissão, "ocupada") é dito
  como regra da lib, com a decisão da spec que a sustenta.
- **O protocolo não tem contrato.** As páginas dizem a versão do Claude Code em que foi conferido
  (2.1.331; o `image`, 2.1.286) e que ele pode mudar.
- **Build:** `cd site && npm ci && npm run build && npm run check`. O `check-dist.mjs` passa a
  conferir as páginas novas nos cinco idiomas.
- **Commits:** um por página, mensagem em português. Branch `fase-5-especificacao`; nada vai ao
  `master` sem pull request.

## As páginas

| Slug | Título | O que diz |
|---|---|---|
| `spec/protocolo` | Protocolo | Como o `claude` é chamado, a forma de uma linha, os tipos de linha do stdout, o que um leitor faz com o que não conhece, o turno |
| `spec/comandos` | Comandos | A linha da mensagem do usuário (texto, imagens, `uuid`, `inline_pastes`), a codificação do stdin |
| `spec/controle` | Controle | Os pedidos de controle do host ao `claude` e as respostas; o `can_use_tool`, a resposta de permissão e a regra dela; o `control_cancel_request`; o pedido que ninguém atende |
| `spec/transporte` | Transporte | A interface, os argumentos e a conferência deles, o ambiente, stdin/stdout/stderr, a partida, "ocupada", o fim e os limites conhecidos |
| `spec/eventos` | Eventos | A tabela dos 37 eventos: de que linha sai, os campos, e quais nenhuma gravação exercita |
| `spec/fixtures` | Fixtures | Os cenários, o formato do resultado esperado, como uma lib prova a conformidade, a higienização e a regravação |

O arquivo da sessão (`.jsonl`) entra em `spec/protocolo`, numa seção própria, e em `spec/eventos`
(a coluna de origem).

## Tarefas

### Task 1: O grupo da barra lateral e a conferência do build

- Modify: `site/astro.config.mjs` (o grupo "Especificação", com as traduções do rótulo)
- Modify: `site/scripts/check-dist.mjs` (as seis páginas em `PAGES`)
- O `check-dist` falha antes das páginas existirem e passa depois da Task 7.
- Commit: `docs(site): o grupo Especificação na barra lateral`

### Tasks 2 a 7: uma página por tarefa, em português

Para cada página: escrever, rodar o build e o `check`, conferir contra as gravações cada linha de
exemplo e cada nome de campo (o script de conferência da Task 8 ajuda), commit
`docs(site): a página <título> da especificação`.

As traduções das seis páginas, nos outros quatro idiomas, são cópias do português até a segunda
leva: o Starlight mostra o aviso de página não traduzida quando a página do idioma não existe, e o
`check-dist` confere só o que existe. Decidir isso com o dono do projeto antes da segunda leva.

### Task 8: A conferência das páginas contra as gravações

- Create: `tools/test_site_spec.py` (biblioteca padrão)
- Ele lê as páginas `site/src/content/docs/spec/*.md` e confere:
  1. cada bloco de código marcado como linha de gravação (` ```json ` precedido de um comentário
     `<!-- fixture: <arquivo>:<linha> -->`) é a linha daquele arquivo, ou um corte dela
     (todo par chave/valor do exemplo existe na linha);
  2. cada evento da tabela de `spec/eventos` tem exatamente os campos que os arquivos esperados dão;
  3. os nove eventos que as gravações não exercitam estão marcados assim.
- Commit: `test(site): as páginas da especificação conferem com as gravações`

### Task 9: Os textos que falam da fase 5

- Modify: `README.md` e as quatro traduções (o roteiro e o estado), `site/src/content/docs/index.mdx`
  e as quatro traduções (o cartão "Especificação"), `fixtures/README.md` ("O que este arquivo ainda
  não dá"), `CHANGELOG.md` (`Unreleased`).
- Commit: `docs: a especificação está no site`

### Segunda leva (depois da revisão do português)

As seis páginas em `en/`, `es/`, `zh-cn/` e `zh-tw/`, uma língua por commit, com o glossário. As
traduções para o chinês dizem que foram feitas por IA.

## Fora desta fase

- O componente que toca uma fixture na página.
- Mudar a lib, o gravador ou as fixtures. Uma diferença achada entre a página e o código vira item
  do ROADMAP, não correção aqui.
- Os itens 🔍 do ROADMAP: as páginas dizem o que ainda não foi conferido.
