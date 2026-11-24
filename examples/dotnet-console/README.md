# Exemplo de console em .NET

Uma conversa com o `claude` desta máquina, no terminal, em cerca de cem linhas. Mostra as três
peças da lib juntas: o `ProcessTransport` sobe o `claude -p`, a `ClaudeSession` lê os eventos e
manda os comandos, e a resposta a um pedido de permissão leva só o pedido e a decisão.

## Estado

Em 2026-11-24 o exemplo **compila** junto com a solução da lib. **O exemplo em si ainda não foi
rodado contra um `claude` de verdade.** O transporte que ele usa é testado com um `claude` de
mentira, e rodou uma vez contra um `claude` de verdade (2.1.331, no Windows) no teste
`LiveClaudeTests`, que só roda quando pedido, com `CCP_LIVE_CLAUDE=1`.

## Rodar

Precisa do [Claude Code](https://github.com/anthropics/claude-code) instalado e com login feito, e
do SDK do .NET 8.

```bash
dotnet run --project examples/dotnet-console -- <pasta do projeto>
```

Sem a pasta, usa a pasta atual. Digite uma mensagem e Enter; a resposta aparece conforme chega.
Uma linha vazia encerra.

Quando o `claude` quer usar uma ferramenta, o exemplo mostra o nome dela e o que ela vai fazer e
pergunta: `y` permite, `a` permite sempre (o `claude` guarda a regra que ele mesmo sugeriu), e
qualquer outra coisa nega.

## O que ele usa da sua máquina

- **A sua conta do Claude Code.** O exemplo sobe o `claude` que está no PATH, com o login que ele
  tiver. Cada mensagem consome o plano ou a API key dessa conta.
- **A pasta que você passar.** O `claude` trabalha nela, no modo de permissão `default`: lê sem
  perguntar e pergunta antes de alterar.
- **Nada em disco além do que o próprio `claude` grava.** O exemplo não guarda a conversa.
