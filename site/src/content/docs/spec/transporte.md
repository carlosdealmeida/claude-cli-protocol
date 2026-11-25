---
title: Transporte
description: O que leva as linhas e os comandos — a interface, o processo do claude, os argumentos e a conferência deles, a partida, o fim e os limites conhecidos.
---

O transporte é o que leva as linhas do `claude` num sentido e os comandos no outro. Pode ser o
próprio processo, uma rede até um `claude` remoto ou uma gravação. Esta página diz o que um
transporte faz e, em detalhe, o que faz o que sobe o `claude` na máquina (o `ProcessTransport` da
lib .NET). O que está aqui como regra é regra da lib; o que é do `claude` está dito como tal.

## A interface

A interface é de comandos, não de stdin cru:

```csharp
public interface IClaudeTransport : IAsyncDisposable
{
    /// The next stream-json line, or null once the transport has closed.
    Task<string?> ReceiveLineAsync(CancellationToken ct);

    /// A command; each transport delivers it in its own way.
    Task SendAsync(ClaudeCommand command, CancellationToken ct);

    /// Raised when the transport drops, comes back or closes.
    event EventHandler<TransportState>? StateChanged;
}
```

- **Linhas para cima, comandos para baixo.** Quem usa o transporte recebe as linhas do stdout, uma
  a uma, e manda comandos: uma mensagem, uma resposta de permissão, um pedido de controle. Quem
  escreve a linha do comando no stdin é o lado do processo.
- **Por que comandos.** A resposta de permissão é montada com o pedido que o lado do processo leu
  (ver [a regra](../controle/#a-regra-da-resposta-de-permissão)). Com stdin cru, quem chama poderia
  escrever qualquer `input`.
- **Os estados:** `Open` (as linhas vêm e os comandos vão), `Dropped` (caiu e pode voltar: um
  transporte por rede que reconecta) e `Closed` (acabou: nenhuma linha vem mais, e toda leitura
  devolve nulo).
- **Os transportes da lib:** `ProcessTransport`, que sobe o `claude`, e `ReplayTransport`, que toca
  uma gravação e ignora os comandos. Um transporte por rede é do aplicativo que o usa, não da lib.

## O processo

### Os argumentos

Nesta ordem, e nenhum outro:

```
-p --input-format stream-json --output-format stream-json --verbose
--include-partial-messages --permission-prompt-tool stdio --permission-mode <modo>
[--resume <id>] [--model <modelo>] [--effort <nível>]
```

Os três últimos só quando pedidos. **Nunca `--setting-sources`:** as regras de bloqueio de cada
máquina valem em qualquer modo.

### A conferência

No Windows, o `claude` instalado pelo npm é um `claude.cmd`, que passa pelo `cmd.exe`. Ali um
caractere solto num valor vira outro comando. Por isso cada valor que vai à linha de comando é
conferido, e um valor recusado não sobe nada:

| Valor | Aceito |
|---|---|
| modo | `plan`, `default`, `acceptEdits` ou `auto` |
| modelo | `^[A-Za-z0-9][A-Za-z0-9._:@/\[\]-]{0,99}$`: aliases (`sonnet`, `opus[1m]`), ids, perfis do Bedrock (`arn:…`) e ids do Vertex (`…@20250514`) |
| id de sessão | `^[A-Za-z0-9][A-Za-z0-9_-]{0,199}$` |
| effort | `low`, `medium`, `high`, `xhigh` ou `max` |
| pasta | não vazia |
| caminho do `claude` | absoluto, quando dado |

O mesmo vale para o modo pedido em uso: um `set_permission_mode` com um modo fora da lista não é
escrito. Exemplos de valores que a conferência barra: o modelo `sonnet & calc`, o id de sessão
`a" --dangerously-skip-permissions`, o modo `bypassPermissions`.

### Achar o `claude`

Sem caminho dado, o transporte procura `claude` no PATH, **só nas pastas absolutas dele, nunca na
pasta atual**: a pasta atual é a pasta da conversa, e um `claude.cmd` deixado lá não pode ser o que
roda.

### O ambiente

- O desta máquina, mais as variáveis que o host passa para este `claude` (uma variável com valor
  nulo é removida). Entre elas, o `CLAUDE_CONFIG_DIR`, que escolhe a conta. O transporte não
  conhece nenhuma variável pelo nome.
- Mais **`NoDefaultCurrentDirectoryInExePath=1`**, para o `cmd.exe` não procurar o `node` na pasta
  da conversa.
- A conta é a da máquina: o login da assinatura ou a `ANTHROPIC_API_KEY`. Quem embute o chat num
  produto de terceiros deve usar API key.

### stdin, stdout e stderr

- **stdin:** uma linha por comando, em bytes ASCII, com `\n` e flush (ver
  [Comandos](../comandos/#a-codificação)). Uma escrita começada vai até o fim: meia linha quebraria
  o stream. O transporte nunca fecha o stdin sozinho.
- **stdout:** UTF-8, lido linha a linha.
- **stderr:** lido e descartado. Ler é preciso, senão o pipe enche e o `claude` trava; guardar não,
  porque pode ter texto da conversa. A lib só conta as linhas.
- **As duas leituras andam juntas, cada uma no seu fio.** Ler o stdout até o fim e só depois o
  stderr trava os dois lados. Na lib, cada leitura tem uma thread própria: com threads do pool,
  cada `claude` vivo prendia três delas pela vida inteira, e numa máquina de 4 núcleos dois
  transportes já deixavam o resto do programa esperando.

## A partida

1. Conferir os valores e montar os argumentos.
2. Subir o processo na pasta da conversa.
3. **Começar a ler o stdout e o stderr.**
4. **Escrever o `initialize`**, como fazem os SDKs oficiais (pode ser desligado).

A ordem de 3 e 4 importa. Achado em teste: na build `netstandard2.0`, sob o .NET Framework, a
escrita do `initialize` não terminava quando o `claude` escrevia muito antes de ler o stdin; ela
termina quando alguém lê o stdout. O motivo exato não foi apurado.

- **Demora.** Desde a 2.1.323 há relatos de 5 a 7 segundos até o primeiro byte
  ([#109534](https://github.com/anthropics/claude-code/issues/109534)); a 2.1.325 passou a esperar
  os dados da conta antes do primeiro pedido, e a 2.1.327 espera até 10 segundos pelos ajustes do
  modelo. Prazos curtos de partida falham.
- **A partida que falha** fecha o transporte: a leitura devolve nulo, e o código de saída fica
  disponível. Desde a 2.1.314, um `claude -p` que não consegue partir imprime o erro no stderr e sai
  com 1 (antes saía com 0 e sem saída). O código de saída é o único sinal que sobra, já que o
  stderr é descartado.
- **Descartar durante a partida** não deixa processo: o transporte confere de novo depois de subir
  o `claude` e, se já foi descartado, encerra a árvore.

## Ocupada

A `ClaudeSession` diz se a sessão está ocupada. A regra segue o stream, não a contagem de
mensagens:

> A sessão está ocupada de uma mensagem enviada, ou de um `system/init`, até o próximo `result`.

Contar turnos (cada mensagem abre um, cada `result` fecha um) não fecha: no cenário `queued`, duas
mensagens deram um turno só (ver [Protocolo](../protocolo/#o-turno)). O custo da regra: uma
mensagem que esperou a vez deixa a sessão livre por um instante, entre o `result` do turno
anterior e o `system/init` dela.

A sessão também guarda o `session_id`, o modo e o modelo confirmados (pelo `system/init`, pelo
`system/status` e pelas respostas de sucesso dos pedidos) e os pedidos de permissão pendentes.
Um comando mandado depois de o transporte fechar não é enviado, não deixa a sessão ocupada e
devolve `false`.

## O fim

- **O fim vem do processo, não do fim do stdout.** Um filho do `claude` que herdou o stdout pode
  segurá-lo aberto. Depois que o processo termina, o transporte espera as últimas linhas (o
  `result`) por um tempo limitado (10 segundos na lib) e fecha.
- **O código de saída** fica disponível depois do fim.
- **Encerrar é matar a árvore de processos, na hora.** Sem tentar um fim educado antes: depois que
  o `claude` sai sozinho, os filhos dele não podem mais ser achados pelo pid dele, e esperar
  deixaria processos para trás. O custo: o `claude` não termina uma gravação em disco se o host o
  descarta no meio de um turno.
- **Depois do fim, mandar não faz nada:** nenhuma escrita e nenhuma exceção de pipe.

## Os limites conhecidos

- No `netstandard2.0` fora do Windows, encerrar mata só o `claude`, não os filhos.
- Um filho que sobrevive a um `claude` que saiu por conta própria não é encerrado.
- O `ClaudeLocator` não confere o bit de execução.
- Enquanto um filho órfão segura o stdout, a thread de leitura continua viva, mas nenhuma linha é
  entregue depois do fim.
- Contra um `claude` de verdade (2.1.331, no Windows), o transporte rodou uma vez, com uma mensagem
  curta, num teste que só roda a pedido (`CCP_LIVE_CLAUDE=1`). O resto dos testes usa um `claude`
  de mentira.

## O que o transporte nunca faz

- Gravar em disco a conversa, o stderr ou credenciais.
- Passar um argumento fora da lista, ou um valor que não passou pela conferência.
- Escrever uma resposta de permissão com um `input` que não leu do próprio `claude`.
- Fechar o stdin com mensagem pendente.
