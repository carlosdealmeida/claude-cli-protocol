# fixtures

As gravações do `claude -p` e o resultado esperado de cada uma. É por elas que toda lib deste
repositório, em qualquer linguagem, prova que fala o protocolo: lê a gravação e produz o resultado
esperado.

## Estado

Em 2026-11-22 a pasta está vazia. As gravações chegam na fase 1, vindas do
[Claude Visual Chat](https://github.com/carlosdealmeida/claude-visual-chat), onde existem hoje
(21 cenários).

## O que entra aqui

Dois arquivos por cenário:

- `<cenário>.stream.ndjson`: a saída do `claude -p --output-format stream-json`, uma linha JSON por
  evento;
- `<cenário>.transcript.jsonl`: o arquivo da mesma sessão que o Claude Code grava em disco.

E, para cada um deles, o resultado esperado em JSON neutro: a lista de eventos, sem nada que seja
de uma linguagem só. O formato é definido na fase 1.

## Privacidade

Uma gravação é uma sessão de verdade. Nenhuma entra aqui com dado pessoal de quem gravou: nome de
usuário, pastas, e-mail ou credencial. O gravador troca tudo isso por valores de demonstração, e um
teste confere cada arquivo.

## Regravação

A partir da fase 2, o workflow `.github/workflows/fixtures.yml` regrava os cenários toda semana com
o `claude -p` de verdade e abre um pull request quando alguma gravação muda. Assim uma mudança do
CLI aparece aqui antes de aparecer em uso.
