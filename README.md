# claude-cli-protocol

**Português** · [English](README.en.md) · [Español](README.es.md) · [简体中文](README.zh-cn.md) · [繁體中文](README.zh-tw.md)

[![CI](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml/badge.svg)](https://github.com/carlosdealmeida/claude-cli-protocol/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**O protocolo stream-json do `claude -p`, especificado, gravado e implementado.** O
[Claude Code](https://github.com/anthropics/claude-code) fala uma linha JSON por mensagem quando
roda com `-p`. Este repositório junta três coisas que hoje estão espalhadas: a especificação
escrita desse protocolo, as gravações de sessões de verdade que a provam e as libs que o falam,
primeiro em .NET e depois em Delphi.

> Projeto da comunidade, sem vínculo com a Anthropic nem endosso dela.

## Estado

**Fase 2, em 2026-11-24.** A lib .NET lê e escreve o protocolo e sobe o `claude -p` desta máquina:
eventos, os dois parsers, comandos, sessão, replay de gravações e o `ProcessTransport`. Ela passa
nas 42 gravações de `fixtures/`, nos dois targets. O transporte é testado com um `claude` de
mentira; contra um `claude` de verdade (2.1.331, no Windows) ele rodou uma vez, com uma mensagem
curta, num teste que só roda a pedido. Nenhum pacote foi publicado e o site ainda não está no ar.

## O que tem aqui

| Pasta | O que é |
|---|---|
| `site/` | A landing e a especificação, em Astro + Starlight, nos cinco idiomas |
| `docs/` | As specs, os planos, o ROADMAP e o glossário das traduções |
| `fixtures/` | As gravações do `claude -p` e o resultado esperado de cada uma |
| `libs/dotnet/` | `ClaudeCliProtocol`, para `netstandard2.0` e `net6.0` |
| `libs/delphi/` | A lib em Delphi, com piso no 10.1 Berlin |
| `examples/` | Exemplos de console |
| `tools/` | O teste de estrutura do repositório e a conferência do pacote |

## As camadas

```
claude-cli-protocol (lib)      ← só fala o protocolo; não sabe que tela existe
   ↑
ClaudeVisualChat.Avalonia      ← só desenha; não abre processo nem rede
   ↑
o seu aplicativo               ← integra
```

Cada camada só conhece a de baixo, por pacote com versão. Tela nenhuma entra neste repositório,
em linguagem nenhuma.

## Como o claude -p é chamado

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

Com esses argumentos, o `claude` escreve uma linha JSON por evento no stdout (o início da sessão,
cada pedaço da resposta, cada chamada de ferramenta, cada pedido de permissão, o fim do turno) e
lê uma linha JSON por comando no stdin (a mensagem do usuário, a resposta a um pedido de
permissão, a troca de modo ou de modelo, o interrupt). A lib transforma as linhas em eventos
tipados e os comandos em linhas.

## Segurança

A resposta a um pedido de permissão leva sempre o `input` e as `permission_suggestions` que o lado
do processo recebeu do próprio `claude`. Quem manda o comando só escolhe o pedido, a decisão e a
mensagem: o comando de resposta não tem campo para o `input`. Quem escreve no stdin do `claude`
monta a resposta com o pedido que ele mesmo leu, e cada pedido é respondido uma vez só.

Para relatar uma falha, veja a [política de segurança](SECURITY.md).

## Roteiro

| Fase | O que sai |
|---|---|
| 0 | O esqueleto: estrutura, documentação, CI e site |
| 1 | A lib .NET: eventos, parsers, comandos, sessão e replay de gravações |
| 2 | O transporte que sobe o `claude`, o exemplo de console e a regravação semanal das fixtures |
| 5 | A especificação no site: protocolo, comandos, controle, transporte, fixtures |
| 6 | A lib em Delphi, passando nas mesmas fixtures |

As fases 3, 4 e 7 acontecem em outros repositórios: o Claude Visual Chat sobre esta lib, o
aplicativo que o usa e o componente de chat VCL. O desenho completo está em
[docs/specs/](docs/specs/).

## Contribuindo

Veja [CONTRIBUTING.md](CONTRIBUTING.md).

## Licença

[MIT](LICENSE)
