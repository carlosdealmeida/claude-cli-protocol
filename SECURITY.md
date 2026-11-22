# Política de Segurança

**Português** · [English](SECURITY.en.md) · [Español](SECURITY.es.md)

## Versões suportadas

Correções de segurança são aplicadas apenas à versão mais recente publicada no NuGet. Até a
primeira publicação, valem para o branch `master`.

## Como reportar uma vulnerabilidade

**Não abra uma issue pública** para falhas de segurança.

Use o relato privado de vulnerabilidades do GitHub:

1. Acesse a aba **Security** do repositório.
2. Clique em **Report a vulnerability**.
3. Descreva a falha, o impacto e, se possível, um passo a passo para reproduzir.

Link direto: <https://github.com/carlosdealmeida/claude-cli-protocol/security/advisories/new>

## O que esperar

- **Confirmação de recebimento:** em até 5 dias úteis.
- **Avaliação inicial:** em até 10 dias úteis, com uma estimativa de correção.
- Você é mantido informado sobre o progresso até a resolução.
- Após a correção, publicamos um GitHub Security Advisory creditando quem reportou (salvo pedido de anonimato).

## Escopo

As libs deste repositório sobem o `claude` na máquina de quem as usa e falam com ele por stdin e
stdout. Elas não se comunicam com nenhum servidor por conta própria. São considerados relevantes,
entre outros:

- **O processo que a lib sobe.** O executável, os argumentos e as variáveis de ambiente que ela
  passa, entre elas o `CLAUDE_CONFIG_DIR`, que escolhe a conta. Um argumento que a lib acrescente
  sem ser pedido, ou uma variável que vaze para outro processo, é uma falha.
- **Injeção na linha de comando.** No Windows, o `claude.cmd` do npm passa pelo `cmd.exe`. Um
  modelo, um modo ou um id de sessão que vire outro comando ou outra opção é uma falha.
- **A regra da resposta de permissão.** A resposta a um pedido de permissão leva sempre o `input`
  e as `permission_suggestions` que o lado do processo recebeu do próprio `claude`; quem chama só
  escolhe o pedido, a decisão e a mensagem. Qualquer caminho que deixe quem chama trocar um dos
  dois, ou responder duas vezes ao mesmo pedido, é uma falha.
- **O que a lib nunca grava em disco.** A conversa, o stderr do `claude` (lido e descartado, porque
  pode ter texto da conversa) e credenciais. Uma gravação em disco que a lib faça sem ser pedida é
  uma falha.
- **Dado pessoal numa gravação.** Nome de usuário, pasta, e-mail ou credencial de quem gravou
  dentro de um arquivo de `fixtures/`.

A lib chega em fases, e parte do que está acima ainda não foi escrita. O escopo vale para o que
já existe e para o que for chegando.

Fora de escopo: falhas do próprio Claude Code (reporte à Anthropic) e vulnerabilidades em
dependências de desenvolvimento que não entram no pacote publicado.
