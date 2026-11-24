---
title: Visão geral
description: O que é o claude-cli-protocol, como ele se divide em camadas e em que fase está.
---

O [Claude Code](https://github.com/anthropics/claude-code) fala uma linha JSON por mensagem quando
roda com `-p`. O **claude-cli-protocol** junta três coisas que hoje estão espalhadas:

- a **especificação** escrita desse protocolo;
- as **gravações** de sessões de verdade que a provam;
- as **libs** que o falam, primeiro em .NET e depois em Delphi.

Projeto da comunidade, sem vínculo com a Anthropic nem endosso dela.

## Estado

**Fase 2, em 2026-11-24.** A lib .NET lê e escreve o protocolo, sobe o `claude -p` desta máquina e
passa nas 42 gravações do repositório. O transporte é testado com um `claude` de mentira; ela ainda
não foi rodada contra um `claude` de verdade aqui, e nenhum pacote foi publicado.

## As camadas

```
claude-cli-protocol (lib)      ← só fala o protocolo; não sabe que tela existe
   ↑
ClaudeVisualChat.Avalonia      ← só desenha; não abre processo nem rede
   ↑
o seu aplicativo               ← integra
```

Cada camada só conhece a de baixo, por pacote com versão. Tela nenhuma entra neste repositório.

## Como o claude -p é chamado

```
claude -p --input-format stream-json --output-format stream-json --verbose
       --include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
```

O `claude` escreve uma linha JSON por evento no stdout e lê uma linha JSON por comando no stdin.

## Fases

| Fase | O que sai |
|---|---|
| 0 | O esqueleto: estrutura, documentação, CI e site |
| 1 | A lib .NET: eventos, parsers, comandos, sessão e replay de gravações |
| 2 | O transporte que sobe o `claude`, o exemplo de console e a regravação semanal das fixtures |
| 5 | A especificação neste site: protocolo, comandos, controle, transporte, fixtures |
| 6 | A lib em Delphi, passando nas mesmas fixtures |

As fases 3, 4 e 7 acontecem em outros repositórios.
