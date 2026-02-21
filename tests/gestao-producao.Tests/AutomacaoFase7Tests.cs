using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using gestao_producao.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace gestao_producao.Tests;

public class AutomacaoFase7Tests
{
    [Fact]
    public async Task EstoqueService_ListarSugestoesReabastecimentoAsync_DeveSugerirReposicaoAteEstoqueMaximo()
    {
        await using var connection = CriarConexaoEmMemoria();
        await using var context = CriarContexto(connection);

        var fornecedor = new Fornecedor
        {
            Nome = "Fornecedor Reabastecimento",
            Cnpj = "44444444000199"
        };

        var insumo = new Insumo
        {
            Nome = "Insumo Crítico",
            UnidadeMedida = "kg",
            Fornecedor = fornecedor,
            EstoqueMinimo = 10m,
            EstoqueMaximo = 40m,
            PrecoUnitario = 12m
        };

        context.AddRange(fornecedor, insumo);
        await context.SaveChangesAsync();

        context.Estoques.Add(new Estoque
        {
            InsumoId = insumo.Id,
            TipoItem = TipoItem.MateriaPrima,
            QuantidadeAtual = 8m
        });
        await context.SaveChangesAsync();

        var alertaService = new AlertaService(context, Options.Create(new AlertaEstoqueOptions()));
        var estoqueService = new EstoqueService(context, alertaService);

        var sugestoes = await estoqueService.ListarSugestoesReabastecimentoAsync();
        var sugestao = Assert.Single(sugestoes);

        Assert.Equal(insumo.Id, sugestao.InsumoId);
        Assert.Equal(8m, sugestao.QuantidadeAtual);
        Assert.Equal(32m, sugestao.QuantidadeSugerida);
    }

    [Fact]
    public async Task ProducaoService_CalcularNecessidadeInsumosPorPlanosAtivosAsync_DeveCalcularNecessidadeEDeficit()
    {
        await using var connection = CriarConexaoEmMemoria();
        await using var context = CriarContexto(connection);

        var fornecedor = new Fornecedor
        {
            Nome = "Fornecedor Necessidade",
            Cnpj = "55555555000199"
        };

        var insumo = new Insumo
        {
            Nome = "Insumo Planejado",
            UnidadeMedida = "un",
            Fornecedor = fornecedor,
            EstoqueMinimo = 0,
            EstoqueMaximo = 100,
            PrecoUnitario = 3m
        };

        var produto = new Produto
        {
            Nome = "Produto Planejado",
            Codigo = "PROD-PLANO-1",
            UnidadeMedida = "un",
            PrecoVenda = 20m
        };

        context.AddRange(fornecedor, insumo, produto);
        await context.SaveChangesAsync();

        context.ProdutoInsumos.Add(new ProdutoInsumo
        {
            ProdutoId = produto.Id,
            InsumoId = insumo.Id,
            QuantidadeNecessaria = 2m
        });

        context.Estoques.Add(new Estoque
        {
            InsumoId = insumo.Id,
            TipoItem = TipoItem.MateriaPrima,
            QuantidadeAtual = 15m
        });

        var ordem = new OrdemProducao
        {
            Codigo = "OP-F7-001",
            ProdutoId = produto.Id,
            QuantidadePlanejada = 20m,
            QuantidadeProduzida = 5m,
            Status = StatusOrdemProducao.Planejada,
            DataInicioPrevista = DateTime.UtcNow.Date,
            DataFimPrevista = DateTime.UtcNow.Date.AddDays(1)
        };

        context.OrdensProducao.Add(ordem);
        await context.SaveChangesAsync();

        var plano = new PlanoProducao
        {
            Nome = "Plano Ativo Fase 7",
            DataInicio = DateTime.UtcNow.Date,
            DataFim = DateTime.UtcNow.Date.AddDays(7),
            Status = StatusPlano.Ativo
        };

        plano.Itens.Add(new PlanoProducaoItem
        {
            OrdemProducaoId = ordem.Id,
            Prioridade = 1
        });

        context.PlanosProducao.Add(plano);
        await context.SaveChangesAsync();

        var alertaService = new AlertaService(context, Options.Create(new AlertaEstoqueOptions()));
        var producaoService = new ProducaoService(context, alertaService);

        var necessidades = await producaoService.CalcularNecessidadeInsumosPorPlanosAtivosAsync();
        var item = Assert.Single(necessidades);

        Assert.Equal(insumo.Id, item.InsumoId);
        Assert.Equal(30m, item.QuantidadeNecessaria);
        Assert.Equal(15m, item.QuantidadeDisponivel);
        Assert.Equal(15m, item.QuantidadeFaltante);
    }

    [Fact]
    public async Task ProducaoService_CalcularNecessidadeInsumosPorPlanosAtivosAsync_NaoDeveDuplicarOrdemEmPlanosAtivos()
    {
        await using var connection = CriarConexaoEmMemoria();
        await using var context = CriarContexto(connection);

        var fornecedor = new Fornecedor
        {
            Nome = "Fornecedor Dedupe",
            Cnpj = "77777777000199"
        };

        var insumo = new Insumo
        {
            Nome = "Insumo Dedupe",
            UnidadeMedida = "un",
            Fornecedor = fornecedor,
            EstoqueMinimo = 0,
            EstoqueMaximo = 100,
            PrecoUnitario = 2m
        };

        var produto = new Produto
        {
            Nome = "Produto Dedupe",
            Codigo = "PROD-DEDUPE-1",
            UnidadeMedida = "un",
            PrecoVenda = 10m
        };

        context.AddRange(fornecedor, insumo, produto);
        await context.SaveChangesAsync();

        context.ProdutoInsumos.Add(new ProdutoInsumo
        {
            ProdutoId = produto.Id,
            InsumoId = insumo.Id,
            QuantidadeNecessaria = 2m
        });

        context.Estoques.Add(new Estoque
        {
            InsumoId = insumo.Id,
            TipoItem = TipoItem.MateriaPrima,
            QuantidadeAtual = 20m
        });

        var ordem = new OrdemProducao
        {
            Codigo = "OP-F7-DEDUPE",
            ProdutoId = produto.Id,
            QuantidadePlanejada = 20m,
            QuantidadeProduzida = 5m,
            Status = StatusOrdemProducao.Planejada,
            DataInicioPrevista = DateTime.UtcNow.Date,
            DataFimPrevista = DateTime.UtcNow.Date.AddDays(1)
        };

        context.OrdensProducao.Add(ordem);
        await context.SaveChangesAsync();

        var planoA = new PlanoProducao
        {
            Nome = "Plano Ativo A",
            DataInicio = DateTime.UtcNow.Date,
            DataFim = DateTime.UtcNow.Date.AddDays(7),
            Status = StatusPlano.Ativo
        };
        planoA.Itens.Add(new PlanoProducaoItem { OrdemProducaoId = ordem.Id, Prioridade = 1 });

        var planoB = new PlanoProducao
        {
            Nome = "Plano Ativo B",
            DataInicio = DateTime.UtcNow.Date,
            DataFim = DateTime.UtcNow.Date.AddDays(5),
            Status = StatusPlano.Ativo
        };
        planoB.Itens.Add(new PlanoProducaoItem { OrdemProducaoId = ordem.Id, Prioridade = 2 });

        context.PlanosProducao.AddRange(planoA, planoB);
        await context.SaveChangesAsync();

        var alertaService = new AlertaService(context, Options.Create(new AlertaEstoqueOptions()));
        var producaoService = new ProducaoService(context, alertaService);

        var necessidades = await producaoService.CalcularNecessidadeInsumosPorPlanosAtivosAsync();
        var item = Assert.Single(necessidades);

        Assert.Equal(30m, item.QuantidadeNecessaria);
        Assert.Equal(20m, item.QuantidadeDisponivel);
        Assert.Equal(10m, item.QuantidadeFaltante);
    }

    [Fact]
    public async Task AlertaService_AtualizarAlertasAsync_DeveGerarAlertaDeValidadeAutomaticamente()
    {
        await using var connection = CriarConexaoEmMemoria();
        await using var context = CriarContexto(connection);

        var fornecedor = new Fornecedor
        {
            Nome = "Fornecedor Validade",
            Cnpj = "66666666000199"
        };

        var insumo = new Insumo
        {
            Nome = "Insumo com Validade",
            UnidadeMedida = "kg",
            Fornecedor = fornecedor,
            EstoqueMinimo = 0,
            EstoqueMaximo = 100,
            PrecoUnitario = 5m
        };

        context.AddRange(fornecedor, insumo);
        await context.SaveChangesAsync();

        context.Estoques.Add(new Estoque
        {
            InsumoId = insumo.Id,
            TipoItem = TipoItem.MateriaPrima,
            QuantidadeAtual = 20m,
            DataValidade = DateTime.UtcNow.AddDays(5)
        });
        await context.SaveChangesAsync();

        var options = Options.Create(new AlertaEstoqueOptions
        {
            AntecedenciaValidadeDias = 10
        });
        var alertaService = new AlertaService(context, options);

        await alertaService.AtualizarAlertasAsync();

        var alerta = await context.AlertasEstoque.SingleAsync();
        Assert.Equal(TipoAlerta.Validade, alerta.TipoAlerta);
        Assert.False(alerta.Lido);
    }

    private static AppDbContext CriarContexto(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options, new HttpContextAccessor());
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteConnection CriarConexaoEmMemoria()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }
}
