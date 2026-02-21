# gestao-producao

Repositório construído como portfólio para um sistema de Produção feito com IA.

Projeto ASP.NET Core para gerenciamento de produção.

## Pré-requisitos

- .NET SDK 10 (ou versão compatível com o projeto)

## Como executar

```bash
dotnet restore
dotnet run
```

## Automações da fase 7

- Sugestão automática de reabastecimento:
  - Disponível em `Estoque > Gestão de Estoque`, seção `Sugestões automáticas de reabastecimento`.
  - A sugestão é calculada quando `QuantidadeAtual <= EstoqueMinimo`, propondo reposição até `EstoqueMaximo`.
- Geração automática de alertas:
  - Executada em background por `AlertaBackgroundService`.
  - Regras cobertas: estoque mínimo, estoque máximo e validade próxima.
- Cálculo automático de necessidade de insumos:
  - Disponível em `Produção`, seção `Necessidade de insumos (planos ativos)`.
  - Considera planos com status `Ativo`, ordens pendentes e composição BOM.

## Evidências de validação

```bash
dotnet test tests/gestao-producao.Tests/gestao-producao.Tests.csproj -m:1
dotnet build gestao-producao.sln -m:1
```

## Estrutura inicial

- `Pages/`: páginas Razor
- `wwwroot/`: arquivos estáticos
- `Program.cs`: configuração e bootstrap da aplicação
