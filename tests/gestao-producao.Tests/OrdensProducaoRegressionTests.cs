using System.Security.Claims;
using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using gestao_producao.Pages.Producao.Ordens;
using gestao_producao.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace gestao_producao.Tests;

public class OrdensProducaoRegressionTests
{
    [Fact]
    public void Form_EditarOrdem_DeveExibirOpcaoStatusConcluida()
    {
        var conteudo = File.ReadAllText(ObterCaminhoProjeto("Pages", "Producao", "Ordens", "Form.cshtml"));

        Assert.Contains("value=\"@StatusOrdemProducao.Concluida\"", conteudo);
        Assert.Contains(">Concluida</option>", conteudo);
    }

    [Fact]
    public async Task Index_OnPostConcluirAsync_DeveUsarQuantidadeProduzidaAtual_EmOrdemParcial()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = CriarContexto(connection);

        var fornecedor = new Fornecedor
        {
            Nome = "Fornecedor teste",
            Cnpj = "33333333000199"
        };

        var insumo = new Insumo
        {
            Nome = "Insumo A",
            UnidadeMedida = "kg",
            Fornecedor = fornecedor,
            EstoqueMinimo = 0,
            EstoqueMaximo = 1000,
            PrecoUnitario = 10
        };

        var produto = new Produto
        {
            Nome = "Produto A",
            Codigo = "PROD-A",
            UnidadeMedida = "un",
            PrecoVenda = 100
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
            QuantidadeAtual = 25m
        });

        var ordem = new OrdemProducao
        {
            Codigo = "OP-REG-001",
            ProdutoId = produto.Id,
            QuantidadePlanejada = 20m,
            QuantidadeProduzida = 10m,
            Status = StatusOrdemProducao.EmAndamento,
            DataInicioPrevista = DateTime.UtcNow.Date,
            DataFimPrevista = DateTime.UtcNow.Date.AddDays(1)
        };

        context.OrdensProducao.Add(ordem);
        await context.SaveChangesAsync();

        var alertaService = new AlertaService(context, Options.Create(new AlertaEstoqueOptions()));
        var producaoService = new ProducaoService(context, alertaService);
        var pageModel = new IndexModel(producaoService)
        {
            PageContext = new PageContext
            {
                HttpContext = CriarHttpContextUsuarioTeste()
            }
        };

        var resultado = await pageModel.OnPostConcluirAsync(ordem.Id, CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(resultado);

        var ordemAtualizada = await context.OrdensProducao.SingleAsync(x => x.Id == ordem.Id);
        var estoqueAtualizado = await context.Estoques.SingleAsync();
        var movimentacao = await context.MovimentacoesEstoque.SingleAsync();

        Assert.Equal(StatusOrdemProducao.Concluida, ordemAtualizada.Status);
        Assert.Equal(10m, ordemAtualizada.QuantidadeProduzida);
        Assert.Equal(5m, estoqueAtualizado.QuantidadeAtual);
        Assert.Equal(TipoMovimentacao.Saida, movimentacao.TipoMovimentacao);
        Assert.Equal(20m, movimentacao.Quantidade);
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

    private static DefaultHttpContext CriarHttpContextUsuarioTeste()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "usuario-teste"),
            new Claim(ClaimTypes.Name, "usuario-teste")
        };

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
    }

    private static string ObterCaminhoProjeto(params string[] segmentos)
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (diretorio is not null)
        {
            var caminhoSolucao = Path.Combine(diretorio.FullName, "gestao-producao.sln");
            if (File.Exists(caminhoSolucao))
            {
                return Path.Combine(new[] { diretorio.FullName }.Concat(segmentos).ToArray());
            }

            diretorio = diretorio.Parent;
        }

        throw new InvalidOperationException("Não foi possível localizar a raiz do repositório.");
    }
}
