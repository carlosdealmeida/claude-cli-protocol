# fixtures

As gravações do `claude -p` e o resultado esperado de cada uma. É por elas que toda lib deste
repositório, em qualquer linguagem, prova que fala o protocolo: lê a gravação e produz o resultado
esperado.

## Estado

A pasta tem 42 gravações: 21 cenários, dois arquivos cada. Foram gravadas pelo `tools/gravar.py`
do [Claude Visual Chat](https://github.com/carlosdealmeida/claude-visual-chat) e copiadas de lá,
byte a byte, do commit `16809aa`, em 2026-11-22. Cada gravação tem ao lado o seu resultado
esperado (`.expected.json`), 42 arquivos gerados pela lib .NET em 2026-11-23.

Os cenários: `edit`, `bash`, `subagent`, `error`, `denied`, `interrupted`, `mode-and-model`,
`image`, `commands`, `effort`, `plan`, `plan-auto`, `plan-feedback`, `queued`, `hook-blocked`,
`queued-compact`, `error-long`, `error-wrapped`, `denied-long`, `hook-ask` e `seguranca`.

## O que entra aqui

Dois arquivos por cenário:

- `<cenário>.stream.ndjson`: a saída do `claude -p --output-format stream-json`, uma linha JSON por
  evento;
- `<cenário>.transcript.jsonl`: o arquivo da mesma sessão que o Claude Code grava em disco.

E, para cada um deles, o resultado esperado em JSON neutro: a lista de eventos, sem nada que seja
de uma linguagem só.

- `<cenário>.stream.expected.json`: os eventos que saem do `.stream.ndjson`;
- `<cenário>.transcript.expected.json`: os eventos que saem do `.transcript.jsonl`.

## O formato do resultado esperado (versão 1)

```json
{
  "format": 1,
  "events": [
    {"line":1,"event":"session_started","fields":{"session_id":"…","model":"…","working_directory":"C:\\demo\\app","permission_mode":"default","claude_version":"…","terminal_commands":[],"fast_mode_state":null,"ultrathink_available":null}}
  ]
}
```

Um evento por linha do arquivo, para uma mudança aparecer como uma linha no diff.

**Os nomes são os dos arquivos.** Os nomes de evento e de campo deste formato são os que aparecem
nos 42 arquivos esperados, e são eles a referência, não os nomes do stream. Alguns diferem do que
o `claude` escreve: `working_directory` é o `cwd` da linha `system/init`, e `claude_version` é o
`claude_code_version`. Uma lib em outra linguagem copia os nomes daqui.

- **Como ler a gravação:** uma linha do arquivo é uma linha do stream. As linhas são separadas por
  `
`; um `` no fim é ignorado, e uma linha em branco não conta. O `.stream.ndjson` é lido com
  **um leitor por arquivo, que guarda estado entre as linhas** (o `message_id` do último
  `message_start` de cada conversa, para os deltas que vêm depois). O `.transcript.jsonl` é lido
  linha a linha, sem estado.
- **`line`:** o número da linha na gravação, contando do 1 e sem contar as linhas em branco. Uma
  linha pode dar nenhum, um ou mais eventos; a ordem dos eventos de uma linha importa.
- **`event`:** o nome do evento, em `snake_case`.
- **`fields`:** os dados do evento. O conjunto de chaves é fixo para cada evento: uma chave a mais
  ou a menos é uma diferença. Um campo sem valor aparece como `null`; ele não some.
- **Texto, número e booleano** vão como são. Um enum vai pelo nome em `snake_case` (`input_json`).
- **Duração:** em milissegundos inteiros.
- **Data:** em ISO 8601, em UTC, com exatamente três casas de milissegundo e `Z`
  (`2026-11-22T13:30:05.250Z`). O que passa do milissegundo é cortado, não arredondado.
- **JSON do `claude`:** um valor que o `claude` mandou e a lib só repassa vai como veio, com os
  nomes de chave dele e sem conversão. São eles: o `input` de um bloco `tool_use` e de um
  `permission_requested`, as `suggestions` de um `permission_requested`, o `tool_use_result` de um
  `user_block` e o `response` de um `control_acknowledged`.
- **Lista:** um array, na ordem em que os itens vieram. **Objeto aninhado:** segue a mesma regra
  dos campos.
- **Bloco de conteúdo:** um objeto com `"kind"` (`text`, `thinking`, `tool_use`, `tool_result` ou
  `image`) e os campos dele.
- **Imagem:** o `data` não vai inteiro. Vira `{"length": <caracteres do base64>, "sha256": "<hex
  minúsculo dos bytes UTF-8 do texto base64>"}`.

**A comparação é estrutural.** A ordem das chaves de um objeto não importa; a ordem dos itens de
um array importa. Dois números são iguais quando diferem em no máximo `1e-9`, em valor absoluto,
inclusive dentro do JSON repassado do `claude`; por isso `1` e `1.0` são iguais. A tolerância
existe para duas implementações que escrevem o mesmo número com casas diferentes.

**O que este arquivo ainda não dá.** Não há uma tabela de todos os eventos e campos: a forma de
cada evento está nos próprios arquivos esperados. Os nove eventos que nenhuma gravação exercita
(lista abaixo) não aparecem neles, e hoje a única descrição da forma deles é o código da lib .NET.
A especificação da fase 5 cobre isso.

### De onde vêm e o que provam

Os 42 arquivos foram escritos pela própria lib .NET. Por isso, passar neles prova duas coisas e
só essas: que as duas builds da lib (`net6.0` e `netstandard2.0`) dão o mesmo resultado, e que uma
mudança de comportamento aparece como diferença. Não prova, sozinho, que cada campo está certo.
Três cenários (`edit`, `denied` e `mode-and-model`) foram conferidos por fora, com um script que
lê a gravação crua: o início da sessão, cada pedido de permissão com o `input` inteiro e cada fim
de turno com os tokens somados. Os outros 39 estão como foram gerados.

As gravações exercitam 27 dos 36 tipos de evento que os leitores produzem (um 37º,
`control_replied`, é da sessão e não sai de um leitor). Não aparecem em nenhuma:
`hook_started`, `hook_finished`, `command_lifecycle`, `per_turn_effort_changed`,
`usage_limits_reported`, `account_reported`, `remote_control_changed`, `model_refusal_fallback`
e `peer_message_hold`. Esses só têm os testes de unidade.

Para regenerar depois de uma mudança de propósito, rode os testes de conformidade com a variável
`CCP_UPDATE_EXPECTED=1` (eles falham dizendo que escreveram os arquivos), leia o diff e rode de
novo sem a variável:

```bash
CCP_UPDATE_EXPECTED=1 dotnet test libs/dotnet -c Release --framework net6.0 --filter "FullyQualifiedName~ConformanceTests"
```

## Privacidade

Uma gravação é uma sessão de verdade. Nenhuma entra aqui com dado pessoal de quem gravou: nome de
usuário, pastas, e-mail ou credencial. O gravador troca tudo isso por valores de demonstração, e o
teste de privacidade (`FixturePrivacyTests`, em `libs/dotnet/tests/`) confere cada arquivo. O
gravador ainda não está neste repositório: chega na fase 2.

Para o teste procurar também as suas próprias palavras (seu usuário, sua empresa, suas pastas),
escreva uma por linha em `libs/dotnet/tests/private-words.txt`. O git ignora esse arquivo.

## Regravação

A partir da fase 2, o workflow `.github/workflows/fixtures.yml` regrava os cenários toda semana com
o `claude -p` de verdade e abre um pull request quando alguma gravação muda. Assim uma mudança do
CLI aparece aqui antes de aparecer em uso.
