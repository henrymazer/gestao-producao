# Plano de Projeto — Sistema de Gestão de Produção

> **Cliente**: A definir  
> **Stack**: ASP.NET Core Razor Pages (.NET 10) + PostgreSQL (Neon)  
> **Data de início**: Fevereiro 2026  
> **Status**: 📋 Planejamento

---

## 1. Visão Geral

Sistema web para **acompanhamento e gestão de planejamento de produção, controle de estoque e rastreamento de insumos**, com foco em otimizar processos operacionais, reduzir desperdícios e garantir disponibilidade de materiais de forma proativa.

---

## 2. Decisões do Cliente ✅

| # | Pergunta | Resposta |
|---|----------|---------|
| 1 | Login com perfis de acesso? | **Não** — somente 1 admin com acesso total. Login nativo do .NET (Identity). |
| 2 | Múltiplas unidades? | **Não** — fábrica única. Sem multi-tenant. |
| 3 | Tipo de manufatura? | **Genérico** — sem regras específicas de setor. |
| 4 | Integrações externas? | **Não** — sistema standalone. |
| 5 | Unidades de medida? | **Unidades (un)** — simplificado. |
| 6 | Idioma da interface? | **Português BR**. |
| 7 | Deploy? | **Google Cloud Run** com buildpacks. |
| 8 | Usuários simultâneos? | **Poucos** — escala pequena. |
| 9 | Mobile? | **Sim** — layout responsivo (desktop + mobile). |
| 10 | Auditoria? | **Sim** — histórico completo de alterações. |

---

## 3. Arquitetura do Sistema

### 3.1 Stack Tecnológica

| Camada | Tecnologia |
|--------|-----------|
| **Frontend** | Razor Pages + Bootstrap 5 + JavaScript |
| **Backend** | ASP.NET Core (.NET 10) |
| **ORM** | Entity Framework Core |
| **Banco de Dados** | PostgreSQL (Neon) |
| **Autenticação** | ASP.NET Core Identity |
| **Relatórios** | Exportação CSV/PDF (QuestPDF ou similar) |
| **Validação** | FluentValidation + DataAnnotations |
| **Gráficos** | Chart.js (via JavaScript no front) |

### 3.2 Estrutura de Pastas (proposta)

```
gestao-producao/
├── Data/
│   ├── AppDbContext.cs              # DbContext principal
│   └── Migrations/                  # Migrations EF Core
├── Models/
│   ├── Produto.cs
│   ├── Insumo.cs
│   ├── Fornecedor.cs
│   ├── Estoque.cs
│   ├── MovimentacaoEstoque.cs
│   ├── OrdemProducao.cs
│   ├── PlanoProducao.cs
│   ├── Equipamento.cs
│   ├── Funcionario.cs
│   └── AlertaEstoque.cs
├── Services/
│   ├── EstoqueService.cs
│   ├── ProducaoService.cs
│   ├── InsumoService.cs
│   ├── RelatorioService.cs
│   └── AlertaService.cs
├── Pages/
│   ├── Producao/                    # CRUD + Planejamento
│   ├── Estoque/                     # Controle de Estoque
│   ├── Insumos/                     # Rastreamento de Insumos
│   ├── Fornecedores/                # Cadastro de Fornecedores
│   ├── Relatorios/                  # Relatórios e Dashboards
│   ├── Cadastros/                   # Produtos, Equipamentos, etc.
│   └── Shared/                      # Layouts, componentes
├── wwwroot/
│   ├── css/
│   ├── js/
│   └── lib/
├── docs/                            # Documentação
└── Program.cs
```

### 3.3 Diagrama de Contexto

```
┌─────────────────────────────────────────────────┐
│              Sistema Gestão Produção             │
│                                                  │
│  ┌───────────┐ ┌──────────┐ ┌────────────────┐  │
│  │Planejamento│ │ Estoque  │ │ Rastreamento   │  │
│  │ Produção  │ │          │ │ de Insumos     │  │
│  └─────┬─────┘ └────┬─────┘ └───────┬────────┘  │
│        │             │               │           │
│        └─────────────┼───────────────┘           │
│                      │                           │
│              ┌───────┴───────┐                   │
│              │  Relatórios   │                   │
│              │  & Dashboards │                   │
│              └───────────────┘                   │
└─────────────────────────────────────────────────┘
         │                        │
    ┌────┴────┐            ┌──────┴──────┐
    │PostgreSQL│            │ Usuários    │
    │ (Neon)  │            │ (Browser)   │
    └─────────┘            └─────────────┘
```

---

## 4. Modelo de Dados

### 4.1 Entidades Principais

#### Fornecedor
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| Nome | string | Nome do fornecedor |
| CNPJ | string | CNPJ |
| Contato | string | Telefone/email |
| Endereco | string | Endereço completo |
| Ativo | bool | Status |
| CriadoEm | DateTime | Data de criação |
| AtualizadoEm | DateTime | Última atualização |

#### Insumo (Matéria-prima)
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| Nome | string | Nome do insumo |
| Descricao | string | Descrição |
| UnidadeMedida | string | kg, litros, un, etc. |
| FornecedorId | int (FK) | Fornecedor principal |
| EstoqueMinimo | decimal | Nível mínimo (alerta) |
| EstoqueMaximo | decimal | Nível máximo (alerta) |
| DataValidade | DateTime? | Validade (se aplicável) |
| PrecoUnitario | decimal | Custo unitário |
| Ativo | bool | Status |
| CriadoEm | DateTime | Data de criação |

#### Produto (Produto acabado)
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| Nome | string | Nome do produto |
| Descricao | string | Descrição |
| Codigo | string | Código interno/SKU |
| UnidadeMedida | string | Unidade |
| PrecoVenda | decimal | Preço |
| Ativo | bool | Status |
| CriadoEm | DateTime | Data de criação |

#### ProdutoInsumo (BOM — Bill of Materials)
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| ProdutoId | int (FK) | Produto |
| InsumoId | int (FK) | Insumo necessário |
| QuantidadeNecessaria | decimal | Qtd por unidade de produto |

#### Estoque
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| InsumoId | int? (FK) | Insumo (se matéria-prima) |
| ProdutoId | int? (FK) | Produto (se acabado) |
| TipoItem | enum | MateriaPrima, EmProcesso, Acabado |
| QuantidadeAtual | decimal | Quantidade em estoque |
| Lote | string | Identificação do lote |
| DataValidade | DateTime? | Validade |
| Localizacao | string | Local no armazém |
| AtualizadoEm | DateTime | Última atualização |

#### MovimentacaoEstoque
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| EstoqueId | int (FK) | Item de estoque |
| TipoMovimentacao | enum | Entrada, Saida, Ajuste |
| Quantidade | decimal | Quantidade movimentada |
| Motivo | string | Razão da movimentação |
| DocumentoReferencia | string | NF, OP, etc. |
| UsuarioId | string | Quem realizou |
| DataMovimentacao | DateTime | Quando |

#### Equipamento
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| Nome | string | Nome do equipamento |
| Descricao | string | Descrição |
| CapacidadePorHora | decimal | Capacidade produtiva |
| Status | enum | Disponivel, EmUso, Manutencao |
| Ativo | bool | Status |

#### Funcionario
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| Nome | string | Nome |
| Cargo | string | Cargo |
| Setor | string | Setor |
| Disponivel | bool | Disponibilidade |
| Ativo | bool | Status |

#### OrdemProducao
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| Codigo | string | Código da OP |
| ProdutoId | int (FK) | Produto a produzir |
| QuantidadePlanejada | decimal | Quantidade planejada |
| QuantidadeProduzida | decimal | Quantidade produzida |
| Status | enum | Planejada, EmAndamento, Concluida, Cancelada |
| DataInicioPrevista | DateTime | Início previsto |
| DataFimPrevista | DateTime | Fim previsto |
| DataInicioReal | DateTime? | Início real |
| DataFimReal | DateTime? | Fim real |
| EquipamentoId | int? (FK) | Equipamento utilizado |
| Observacoes | string | Observações |
| CriadoEm | DateTime | Data de criação |

#### PlanoProducao
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| Nome | string | Nome do plano |
| Descricao | string | Descrição |
| DataInicio | DateTime | Início do período |
| DataFim | DateTime | Fim do período |
| Status | enum | Rascunho, Ativo, Concluido |
| CriadoEm | DateTime | Data de criação |

#### PlanoProducaoItem
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| PlanoProducaoId | int (FK) | Plano |
| OrdemProducaoId | int (FK) | Ordem vinculada |
| Prioridade | int | Prioridade de execução |

#### AlertaEstoque
| Campo | Tipo | Descrição |
|-------|------|-----------|
| Id | int (PK) | Identificador |
| EstoqueId | int (FK) | Item de estoque |
| TipoAlerta | enum | EstoqueMinimo, EstoqueMaximo, Validade |
| Mensagem | string | Descrição do alerta |
| Lido | bool | Se foi visualizado |
| CriadoEm | DateTime | Quando foi gerado |

---

## 5. Módulos e Funcionalidades

### 5.1 Módulo de Cadastros Base
- **F01** — CRUD de Fornecedores
- **F02** — CRUD de Insumos (matérias-primas)
- **F03** — CRUD de Produtos (acabados)
- **F04** — CRUD de Equipamentos
- **F05** — CRUD de Funcionários
- **F06** — Composição de Produto (BOM — Bill of Materials)

### 5.2 Módulo de Planejamento de Produção
- **F07** — Criar/editar Plano de Produção (período, metas)
- **F08** — Criar/editar Ordens de Produção (OP)
- **F09** — Vincular OPs ao Plano de Produção com prioridades
- **F10** — Visualização de calendário/timeline de produção
- **F11** — Verificação de disponibilidade de insumos antes de iniciar OP
- **F12** — Acompanhamento do status da OP (progresso em tempo real)
- **F13** — Ajuste dinâmico do plano (reagendar, cancelar, repriorizar)

### 5.3 Módulo de Gestão de Estoque
- **F14** — Visualização do estoque atual (matéria-prima, em processo, acabado)
- **F15** — Registro de entradas de estoque (compras, devoluções)
- **F16** — Registro de saídas de estoque (produção, vendas, descarte)
- **F17** — Ajustes de inventário (correções manuais)
- **F18** — Alertas automáticos para estoque mínimo
- **F19** — Alertas automáticos para estoque máximo
- **F20** — Alertas de validade próxima ao vencimento
- **F21** — Histórico completo de movimentações

### 5.4 Módulo de Rastreamento de Insumos
- **F22** — Registro de entrada de insumos (com fornecedor, lote, validade)
- **F23** — Rastreamento de consumo por Ordem de Produção
- **F24** — Consulta de histórico por insumo (de onde veio, onde foi usado)
- **F25** — Controle de lotes e rastreabilidade

### 5.5 Módulo de Relatórios e Análises
- **F26** — Dashboard principal com KPIs:
  - Produção do dia/semana/mês
  - Valor do estoque atual
  - Alertas ativos
  - OPs em andamento
- **F27** — Relatório de desempenho de produção (planejado vs realizado)
- **F28** — Relatório de giro de estoque
- **F29** — Relatório de custos de insumos
- **F30** — Relatório de movimentações de estoque (por período)
- **F31** — Exportação de relatórios em CSV e PDF

### 5.6 Automação de Processos
- **F32** — Sugestão automática de reabastecimento quando estoque atinge nível mínimo
- **F33** — Baixa automática de insumos ao concluir Ordem de Produção
- **F34** — Geração automática de alertas de validade
- **F35** — Cálculo automático de necessidade de insumos baseado no plano de produção

---

## 6. Fases de Desenvolvimento

### Fase 1 — Fundação (Semana 1-2) ✅
> Objetivo: Estrutura base, banco de dados, layout

- [x] Configurar Entity Framework Core + PostgreSQL (Neon)
- [x] Criar DbContext com todas as entidades
- [x] Gerar Migration inicial e aplicar no banco
- [x] Configurar ASP.NET Core Identity (autenticação)
- [x] Criar layout principal com Bootstrap 5 (sidebar + navbar)
- [x] Implementar páginas de Login/Registro
- [x] Configurar menu de navegação por módulos

### Fase 2 — Cadastros Base (Semana 2-3)
> Objetivo: CRUDs fundamentais que alimentam o sistema

- [ ] F01 — CRUD Fornecedores
- [ ] F02 — CRUD Insumos
- [ ] F03 — CRUD Produtos
- [ ] F04 — CRUD Equipamentos
- [ ] F05 — CRUD Funcionários
- [ ] F06 — Composição de Produto (BOM)

### Fase 3 — Gestão de Estoque (Semana 3-4)
> Objetivo: Controle completo de estoque

- [ ] F14 — Tela de visualização de estoque
- [ ] F15 — Entrada de estoque
- [ ] F16 — Saída de estoque
- [ ] F17 — Ajustes de inventário
- [ ] F18/F19 — Sistema de alertas (mínimo/máximo)
- [ ] F20 — Alerta de validade
- [ ] F21 — Histórico de movimentações

### Fase 4 — Planejamento de Produção (Semana 4-6)
> Objetivo: Planejamento e execução de produção

- [ ] F07 — CRUD Plano de Produção
- [ ] F08 — CRUD Ordens de Produção
- [ ] F09 — Vincular OPs ao Plano
- [ ] F10 — Visualização de timeline/calendário
- [ ] F11 — Verificação de disponibilidade
- [ ] F12 — Acompanhamento de status
- [ ] F13 — Ajuste dinâmico do plano
- [ ] F33 — Baixa automática de insumos ao concluir OP

### Fase 5 — Rastreamento de Insumos (Semana 6-7)
> Objetivo: Rastreabilidade completa

- [ ] F22 — Registro detalhado de entrada
- [ ] F23 — Rastreamento por OP
- [ ] F24 — Histórico por insumo
- [ ] F25 — Controle de lotes

### Fase 6 — Relatórios e Dashboard (Semana 7-8)
> Objetivo: Visibilidade e análise

- [ ] F26 — Dashboard principal com Chart.js
- [ ] F27 — Relatório de desempenho de produção
- [ ] F28 — Relatório de giro de estoque
- [ ] F29 — Relatório de custos
- [ ] F30 — Relatório de movimentações
- [ ] F31 — Exportação CSV/PDF

### Fase 7 — Automação e Polimento (Semana 8-9)
> Objetivo: Automações e refinamento

- [ ] F32 — Sugestão de reabastecimento
- [ ] F34 — Geração automática de alertas
- [ ] F35 — Cálculo de necessidade de insumos
- [ ] Testes e ajustes finais
- [ ] Otimização de performance
- [ ] Documentação de uso

---

## 7. Pacotes NuGet Necessários

```xml
<!-- Entity Framework Core + PostgreSQL -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.*" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.*" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.*" />

<!-- Identity (Autenticação) -->
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.*" />
<PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.*" />

<!-- Relatórios PDF -->
<PackageReference Include="QuestPDF" Version="*" />

<!-- Validação -->
<PackageReference Include="FluentValidation.AspNetCore" Version="*" />
```

---

## 8. Regras de Negócio Principais

1. **Estoque mínimo**: Quando `QuantidadeAtual <= EstoqueMinimo`, gerar alerta automaticamente.
2. **Estoque máximo**: Quando `QuantidadeAtual >= EstoqueMaximo`, gerar alerta de excesso.
3. **Validade**: Alertar quando um item estiver a 30 dias (configurável) do vencimento.
4. **Baixa de insumos**: Ao concluir uma OP, debitar automaticamente os insumos conforme a BOM × quantidade produzida.
5. **Verificação pré-produção**: Antes de iniciar uma OP, verificar se há insumos suficientes.
6. **Sugestão de compra**: Quando estoque atinge nível mínimo, sugerir quantidade para reabastecimento (até nível máximo).
7. **Rastreabilidade**: Toda movimentação de estoque deve registrar quem, quando, por quê e documento de referência.
8. **Auditoria**: Manter timestamps de criação e atualização em todas as entidades.

---

## 9. Padrões e Convenções

- **Idioma do código**: Inglês para nomes técnicos, Português para labels/UI e nomes de entidades de negócio
- **Nomenclatura de entidades**: PascalCase em português (ex: `OrdemProducao`, `MovimentacaoEstoque`)
- **Padrão de projeto**: Service Layer (Services/) para lógica de negócio, separado das Pages
- **Validação**: Server-side obrigatória, client-side como UX extra
- **Datas**: UTC no banco, convertidas para fuso local na exibição
- **Decimais**: `decimal(18,4)` para quantidades, `decimal(18,2)` para valores monetários
- **Soft delete**: Usar campo `Ativo` em vez de excluir registros

---

## 10. Critérios de Aceite (Definition of Done)

- [ ] Funcionalidade implementada e funcionando
- [ ] Validações server-side aplicadas
- [ ] Layout responsivo (desktop + tablet)
- [ ] Dados persistidos corretamente no PostgreSQL
- [ ] Navegação integrada ao menu principal
- [ ] Sem erros no console do navegador
- [ ] Código limpo e seguindo convenções do projeto

---

## 11. Riscos e Mitigações

| Risco | Probabilidade | Impacto | Mitigação |
|-------|:---:|:---:|---------|
| Latência do Neon (DB na nuvem) | Média | Médio | Cache local, queries otimizadas |
| Complexidade da BOM | Baixa | Alto | Começar simples (1 nível), expandir depois |
| Requisitos não claros | Baixa | Médio | Perguntas respondidas (Seção 2) |
| Performance com muitos dados | Baixa | Médio | Paginação, índices no banco |

---

*Documento vivo — será atualizado conforme respostas do cliente e progresso do desenvolvimento.*
