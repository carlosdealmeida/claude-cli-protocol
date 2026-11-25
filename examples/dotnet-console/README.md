# Exemplo de console em .NET

Uma conversa com o `claude` desta máquina, no terminal, em cerca de cem linhas. Mostra as três
peças da lib juntas: o `ProcessTransport` sobe o `claude -p`, a `ClaudeSession` lê os eventos e
manda os comandos, e a resposta a um pedido de permissão leva só o pedido e a decisão.

## Estado

Em 2026-11-24 o exemplo **rodou contra um `claude` de verdade** (2.1.331, no Windows), duas vezes,
numa pasta temporária. Na primeira ele criou um arquivo com o `Write` e respondeu a uma segunda
mensagem na mesma conversa. Na segunda criou um notebook e pediu permissão para o `NotebookEdit`: o
pedido apareceu com o que a ferramenta ia fazer, a resposta `y` foi escrita, e a célula entrou no
notebook. As duas conversas terminaram com a linha vazia e código de saída 0.

## Rodar

Precisa do [Claude Code](https://github.com/anthropics/claude-code) instalado e com login feito, e
do SDK do .NET 8.

```bash
dotnet run --project examples/dotnet-console -- <pasta do projeto>
```

Sem a pasta, usa a pasta atual. Digite uma mensagem e Enter; a resposta aparece conforme chega.
Uma linha vazia encerra.

Quando o `claude` pede permissão para uma ferramenta, o exemplo mostra o nome dela e o que ela vai
fazer e pergunta: `y` permite, `a` permite sempre (o `claude` guarda a regra que ele mesmo sugeriu),
e qualquer outra coisa nega. Uma ferramenta que as regras da sua conta já permitem roda sem
pergunta; o exemplo só mostra o nome dela.

## O que ele usa da sua máquina

- **A sua conta do Claude Code.** O exemplo sobe o `claude` que está no PATH, com o login que ele
  tiver. Cada mensagem consome o plano ou a API key dessa conta.
- **A pasta que você passar.** O `claude` trabalha nela, no modo de permissão `default`, com as
  regras de permissão da sua conta e da pasta: pergunta antes do que elas não permitem.
- **Nada em disco além do que o próprio `claude` grava.** O exemplo não guarda a conversa.
