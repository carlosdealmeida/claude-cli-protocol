# libs/delphi

A lib do protocolo do `claude -p` em Delphi.

## Estado

Em 2026-11-22 a pasta está vazia. A lib chega na fase 6, depois da lib .NET e da especificação.

## O que já está decidido

- **Piso: Delphi 10.1 Berlin.** Ele tem `System.JSON`, `System.Threading` (`TTask`) e
  `System.Generics.Collections`. Não tem variável inline nem inferência de tipo, que só existem a
  partir do 10.3, então o código da lib não usa nenhuma das duas.
- **Sem pacote de terceiros.** O processo sobe pela API do Windows (`CreateProcess` com pipes).
- **As mesmas fixtures.** A lib passa nas gravações de `fixtures/` e produz o mesmo resultado
  esperado que a lib .NET.
- **Sem tela.** O componente de chat VCL não entra aqui: fica num repositório próprio, como o
  componente Avalonia.
- **Fim de linha:** os fontes do Delphi ficam em CRLF (veja o `.gitattributes`), porque a IDE do
  10.1 Berlin perde a linha do breakpoint com LF.
