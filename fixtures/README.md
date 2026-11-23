# fixtures

As gravações do `claude -p` e o resultado esperado de cada uma. É por elas que toda lib deste
repositório, em qualquer linguagem, prova que fala o protocolo: lê a gravação e produz o resultado
esperado.

## Estado

A pasta tem 42 gravações: 21 cenários, dois arquivos cada. Foram gravadas pelo `tools/gravar.py`
do [Claude Visual Chat](https://github.com/carlosdealmeida/claude-visual-chat) e copiadas de lá,
byte a byte, do commit `16809aa`, em 2026-11-22. O resultado esperado de cada uma ainda não está
aqui: chega no fim da fase 1.

Os cenários: `edit`, `bash`, `subagent`, `error`, `denied`, `interrupted`, `mode-and-model`,
`image`, `commands`, `effort`, `plan`, `plan-auto`, `plan-feedback`, `queued`, `hook-blocked`,
`queued-compact`, `error-long`, `error-wrapped`, `denied-long`, `hook-ask` e `seguranca`.

## O que entra aqui

Dois arquivos por cenário:

- `<cenário>.stream.ndjson`: a saída do `claude -p --output-format stream-json`, uma linha JSON por
  evento;
- `<cenário>.transcript.jsonl`: o arquivo da mesma sessão que o Claude Code grava em disco.

E, para cada um deles, o resultado esperado em JSON neutro: a lista de eventos, sem nada que seja
de uma linguagem só. O formato é definido na fase 1.

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
