---
title: Comandos
description: O que o host escreve no stdin do claude -p — a mensagem do usuário, com texto, imagens e uuid — e como a linha é codificada.
---

O host fala com o `claude` escrevendo uma linha JSON por comando no stdin. São três formas de
linha:

| `type` | O que é | Onde está |
|---|---|---|
| `user` | Uma mensagem do usuário | Nesta página |
| `control_request` | Um pedido de controle do host ao `claude`: o `initialize`, o interrupt, a troca de modo ou de modelo… | [Controle](../controle/#do-host-ao-claude) |
| `control_response` | A resposta do host a um pedido do `claude`, como o de permissão | [Controle](../controle/#do-claude-ao-host) |

As gravações de `fixtures/` guardam só o stdout. As linhas desta página saem do `StdinWriter` da
lib .NET e do gravador (`tools/gravar.py`), que mandaram as mensagens das gravações.

## A codificação

- **Uma linha por comando**, terminada em `\n`, com flush depois de cada uma.
- **Só ASCII.** O que passa de ASCII vai escapado como `\uXXXX` (`ã` vira `\u00E3`). A linha chega
  igual ao `node` qualquer que seja a codificação que o sistema dá ao pipe; no Windows, o pipe de um
  processo filho nem sempre é UTF-8. Isso é regra da lib, não do `claude`: ele lê JSON, e um `ã` em
  UTF-8 também é JSON válido. A lib escolheu não depender da codificação do pipe.
- **Não fechar o stdin para encerrar um turno.** Com `--input-format stream-json`, o fim do stdin
  depois de um turno encerra o processo na hora e perde o que estava pendente
  ([#111427](https://github.com/anthropics/claude-code/issues/111427)). Fechar o stdin é como se
  encerra a sessão, e só se faz de propósito.

## A mensagem do usuário

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default"}
```

- **`message.content`** é o texto, quando a mensagem não tem imagem.
- **`parent_tool_use_id`** vai `null`: a mensagem é para a conversa principal.
- **`session_id`** vai `"default"`. O `claude` não o usa para escolher a sessão: a sessão é a do
  processo, nova ou a do `--resume`. O id de verdade chega na linha `system/init`.

O mesmo texto aparece no arquivo da sessão, como a primeira linha `user` (ver
[Protocolo](../protocolo/#o-arquivo-da-sessão)). No stdout ele não volta.

### Com imagens

Com imagem, `content` vira uma lista: primeiro os blocos de imagem, na ordem, e depois o bloco de
texto. Um texto vazio não gera bloco.

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":[{"type":"image","source":{"type":"base64","media_type":"image/png","data":"iVBORw0KGgo…"}},{"type":"text","text":"What two colors do you see? One line."}]},"parent_tool_use_id":null,"session_id":"default"}
```

O `data` é o base64 do arquivo, sem o prefixo `data:`. O cenário `image` das fixtures manda uma
imagem PNG de 96 × 48 assim.

### Com `uuid`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"Say hi in one line."},"parent_tool_use_id":null,"session_id":"default","uuid":"5d0c9a3e-1b7f-4a52-9e64-0c2f8b1d7a41"}
```

Com um `uuid`, o `claude` conta o que foi feito da mensagem em linhas `command_lifecycle`
(desde a 2.1.310): `command_uuid` é o uuid mandado, e `state` é `queued`, `started`, `completed`,
`cancelled`, `discarded` ou `refused`. `ran_ahead: true` marca um comando que o `claude` rodou na
hora, enquanto trabalhava. Nenhuma gravação tem essas linhas; o evento é o
[`command_lifecycle`](../eventos/#os-que-nenhuma-gravação-exercita).

:::caution[Nunca reenviar com o `uuid` de outra mensagem]
A 2.1.327 corrigiu a perda de mensagens guardadas quando uma mensagem era reenviada com o mesmo
uuid depois de o processo morrer. Cada mensagem tem o seu uuid; reenviar é mandar uma mensagem
nova, com uuid novo.
:::

### Com `inline_pastes`

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"…"},"parent_tool_use_id":null,"session_id":"default","inline_pastes":["…"]}
```

`inline_pastes` lista os textos longos colados que continuam dentro de `content`. No arquivo da
sessão, cada colagem longa fica dentro de um envoltório, uma linha
`<pasted_content id="abcd">` (quatro dígitos hexadecimais minúsculos), o texto colado e uma linha
`</pasted_content id="abcd">`, e o leitor da lib tira o envoltório para mostrar a mensagem como foi
escrita. Nenhuma gravação tem colagem.

## Os arquivos anexados

O protocolo não tem bloco de arquivo. O jeito que a lib usa é uma linha no fim do texto, uma por
arquivo, com o caminho na máquina do `claude`:

```
Resuma este arquivo.
[attached file: C:\demo\app\notes.md]
```

O `claude` lê o arquivo de lá, com as ferramentas dele. Quem lê a conversa de volta tira essas
linhas do fim da mensagem (só as do fim, e só as que são exatamente a marcação) e mostra o arquivo
à parte. Isso é uma convenção da lib e do Claude Visual Chat, não do `claude`.

## Os comandos de barra

Um comando de barra (`/compact`, `/context`, um comando de um plugin) vai como texto de uma mensagem
comum:

<!-- stdin -->
```json
{"type":"user","message":{"role":"user","content":"/compact"},"parent_tool_use_id":null,"session_id":"default"}
```

Os que a sessão oferece chegam na resposta do `initialize` (`commands`) e numa linha
`system/commands_changed` quando mudam. A saída de um comando local volta numa linha `user` com
`<local-command-stdout>`; no cenário `commands`, o `/compact` devolve
`<local-command-stdout>Compacted </local-command-stdout>`.

:::note
Uma mensagem com marcação do próprio `claude` no texto (`<bash-stdout>`, por exemplo) não era
confirmada a um host stream-json local antes da 2.1.314. O que a lib vê quando manda uma assim com
`uuid` ainda não foi conferido (item 6 do [ROADMAP](https://github.com/carlosdealmeida/claude-cli-protocol/blob/master/docs/ROADMAP.md)).
:::
