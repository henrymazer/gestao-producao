using gestao_producao.Data;
using gestao_producao.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Services;

public class RelatorioService
{
    private readonly AppDbContext _context;

    public RelatorioService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RelatoriosResultado> ObterRelatoriosAsync(DateTime? inicio, DateTime? fim, CancellationToken cancellationToken = default)
    {
        var (inicioUtc, fimUtc) = ObterPeriodo(inicio, fim);
        var dashboard = await ObterDashboardAsync(inicioUtc, fimUtc, cancellationToken);
        var desempenho = await ObterDesempenhoProducaoAsync(inicioUtc, fimUtc, cancellationToken);
        var giroEstoque = await ObterGiroEstoqueAsync(inicioUtc, fimUtc, cancellationToken);
        var custos = await ObterCustosAsync(inicioUtc, fimUtc, cancellationToken);
        var movimentacoes = await ObterMovimentacoesAsync(inicioUtc, fimUtc, cancellationToken);

        return new RelatoriosResultado(
            inicioUtc,
            fimUtc,
            dashboard,
            desempenho,
            giroEstoque,
            custos,
            movimentacoes);
    }

    private static (DateTime inicioUtc, DateTime fimUtc) ObterPeriodo(DateTime? inicio, DateTime? fim)
    {
        var fimPadrao = DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);
        var inicioPadrao = fimPadrao.Date.AddDays(-29);

        var inicioUtc = (inicio?.Date ?? inicioPadrao).ToUniversalTime();
        var fimUtc = (fim?.Date.AddDays(1).AddTicks(-1) ?? fimPadrao).ToUniversalTime();

        if (fimUtc < inicioUtc)
        {
            (inicioUtc, fimUtc) = (fimUtc.Date, inicioUtc.Date.AddDays(1).AddTicks(-1));
        }

        return (inicioUtc, fimUtc);
    }

    private async Task<DashboardRelatorio> ObterDashboardAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken)
    {
        var diaReferencia = fimUtc.Date;
        var diaInicio = diaReferencia;
        var diaFim = diaReferencia.AddDays(1).AddTicks(-1);

        var diffSemana = (7 + (int)diaReferencia.DayOfWeek - (int)DayOfWeek.Monday) % 7;
        var semanaInicio = diaReferencia.AddDays(-diffSemana);
        var semanaFim = semanaInicio.AddDays(7).AddTicks(-1);

        var mesInicio = new DateTime(diaReferencia.Year, diaReferencia.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var mesFim = mesInicio.AddMonths(1).AddTicks(-1);

        var diaInicioEfetivo = diaInicio >= inicioUtc ? diaInicio : inicioUtc;
        var diaFimEfetivo = diaFim <= fimUtc ? diaFim : fimUtc;
        var semanaInicioEfetivo = semanaInicio >= inicioUtc ? semanaInicio : inicioUtc;
        var semanaFimEfetivo = semanaFim <= fimUtc ? semanaFim : fimUtc;
        var mesInicioEfetivo = mesInicio >= inicioUtc ? mesInicio : inicioUtc;
        var mesFimEfetivo = mesFim <= fimUtc ? mesFim : fimUtc;

        var ordensConcluidas = _context.OrdensProducao
            .AsNoTracking()
            .Where(x => x.Status == StatusOrdemProducao.Concluida)
            .Select(x => new
            {
                DataConclusao = x.DataFimReal ?? x.AtualizadoEm,
                x.QuantidadeProduzida
            });

        var producaoDia = await ordensConcluidas
            .Where(x =>
                x.DataConclusao >= diaInicioEfetivo &&
                x.DataConclusao <= diaFimEfetivo)
            .SumAsync(x => (decimal?)x.QuantidadeProduzida, cancellationToken) ?? 0m;

        var producaoSemana = await ordensConcluidas
            .Where(x =>
                x.DataConclusao >= semanaInicioEfetivo &&
                x.DataConclusao <= semanaFimEfetivo)
            .SumAsync(x => (decimal?)x.QuantidadeProduzida, cancellationToken) ?? 0m;

        var producaoMes = await ordensConcluidas
            .Where(x =>
                x.DataConclusao >= mesInicioEfetivo &&
                x.DataConclusao <= mesFimEfetivo)
            .SumAsync(x => (decimal?)x.QuantidadeProduzida, cancellationToken) ?? 0m;

        var valorEstoque = await _context.Estoques
            .AsNoTracking()
            .SumAsync(x =>
                x.QuantidadeAtual * (x.InsumoId.HasValue
                    ? (x.Insumo != null ? x.Insumo.PrecoUnitario : 0m)
                    : (x.Produto != null ? x.Produto.PrecoVenda : 0m)), cancellationToken);

        var alertasAtivos = await _context.AlertasEstoque
            .AsNoTracking()
            .CountAsync(x => !x.Lido, cancellationToken);

        var opsEmAndamento = await _context.OrdensProducao
            .AsNoTracking()
            .CountAsync(x => x.Status == StatusOrdemProducao.EmAndamento, cancellationToken);

        var inicioGrafico = inicioUtc.Date;
        var fimGrafico = fimUtc.Date;
        var diasGrafico = Math.Max(1, (fimGrafico - inicioGrafico).Days + 1);

        var datasGrafico = Enumerable.Range(0, diasGrafico)
            .Select(offset => inicioGrafico.AddDays(offset))
            .ToList();

        var producaoPorDia = await ordensConcluidas
            .Where(x => x.DataConclusao >= inicioUtc && x.DataConclusao <= fimUtc)
            .GroupBy(x => x.DataConclusao.Date)
            .Select(x => new { Data = x.Key, Quantidade = x.Sum(y => y.QuantidadeProduzida) })
            .ToDictionaryAsync(x => x.Data, x => x.Quantidade, cancellationToken);

        var saidaPorDia = await _context.MovimentacoesEstoque
            .AsNoTracking()
            .Where(x =>
                x.DataMovimentacao >= inicioUtc &&
                x.DataMovimentacao <= fimUtc &&
                x.TipoMovimentacao == TipoMovimentacao.Saida)
            .GroupBy(x => x.DataMovimentacao.Date)
            .Select(x => new { Data = x.Key, Quantidade = x.Sum(y => y.Quantidade) })
            .ToDictionaryAsync(x => x.Data, x => x.Quantidade, cancellationToken);

        var series = datasGrafico
            .Select(data => new DashboardPontoSerie(
                data,
                producaoPorDia.GetValueOrDefault(data, 0m),
                saidaPorDia.GetValueOrDefault(data, 0m)))
            .ToList();

        return new DashboardRelatorio(
            producaoDia,
            producaoSemana,
            producaoMes,
            valorEstoque,
            alertasAtivos,
            opsEmAndamento,
            series);
    }

    private async Task<List<DesempenhoProducaoItem>> ObterDesempenhoProducaoAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken)
    {
        var itens = await _context.OrdensProducao
            .AsNoTracking()
            .Include(x => x.Produto)
            .Where(x => x.DataInicioPrevista >= inicioUtc && x.DataInicioPrevista <= fimUtc)
            .OrderBy(x => x.DataInicioPrevista)
            .Select(x => new DesempenhoProducaoItem(
                x.Id,
                x.Codigo,
                x.Produto != null ? x.Produto.Nome : "-",
                x.Status,
                x.QuantidadePlanejada,
                x.QuantidadeProduzida,
                x.DataInicioPrevista,
                x.DataFimPrevista,
                x.DataFimReal))
            .ToListAsync(cancellationToken);

        return itens;
    }

    private async Task<List<GiroEstoqueItem>> ObterGiroEstoqueAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken)
    {
        var movimentos = await _context.MovimentacoesEstoque
            .AsNoTracking()
            .Where(x => x.DataMovimentacao >= inicioUtc && x.DataMovimentacao <= fimUtc)
            .GroupBy(x => new { x.EstoqueId, x.TipoMovimentacao })
            .Select(x => new
            {
                x.Key.EstoqueId,
                x.Key.TipoMovimentacao,
                Quantidade = x.Sum(y => y.Quantidade)
            })
            .ToListAsync(cancellationToken);

        var lookupMovimentos = movimentos
            .GroupBy(x => x.EstoqueId)
            .ToDictionary(
                x => x.Key,
                x => new
                {
                    Entradas = x.Where(i => i.TipoMovimentacao == TipoMovimentacao.Entrada).Sum(i => i.Quantidade),
                    Saidas = x.Where(i => i.TipoMovimentacao == TipoMovimentacao.Saida).Sum(i => i.Quantidade)
                });

        var estoques = await _context.Estoques
            .AsNoTracking()
            .Include(x => x.Insumo)
            .Include(x => x.Produto)
            .ToListAsync(cancellationToken);

        return estoques
            .Select(estoque =>
            {
                var movimentosEstoque = lookupMovimentos.GetValueOrDefault(estoque.Id);
                var entradas = movimentosEstoque?.Entradas ?? 0m;
                var saidas = movimentosEstoque?.Saidas ?? 0m;
                var saldoInicialEstimado = estoque.QuantidadeAtual - entradas + saidas;
                var saldoMedio = (saldoInicialEstimado + estoque.QuantidadeAtual) / 2m;
                var giro = saldoMedio <= 0 ? 0 : saidas / saldoMedio;
                var nomeItem = estoque.Insumo != null ? estoque.Insumo.Nome : estoque.Produto?.Nome ?? "-";

                return new GiroEstoqueItem(
                    estoque.Id,
                    nomeItem,
                    estoque.TipoItem,
                    estoque.QuantidadeAtual,
                    entradas,
                    saidas,
                    saldoMedio,
                    giro);
            })
            .OrderByDescending(x => x.Giro)
            .ThenBy(x => x.NomeItem)
            .ToList();
    }

    private async Task<List<CustoInsumoItem>> ObterCustosAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken)
    {
        var itens = await _context.MovimentacoesEstoque
            .AsNoTracking()
            .Where(x =>
                x.DataMovimentacao >= inicioUtc &&
                x.DataMovimentacao <= fimUtc &&
                x.TipoMovimentacao == TipoMovimentacao.Saida &&
                x.Estoque != null &&
                x.Estoque.InsumoId.HasValue)
            .Select(x => new
            {
                InsumoId = x.Estoque!.InsumoId!.Value,
                Nome = x.Estoque.Insumo != null ? x.Estoque.Insumo.Nome : "-",
                Quantidade = x.Quantidade,
                PrecoUnitario = x.Estoque.Insumo != null ? x.Estoque.Insumo.PrecoUnitario : 0m
            })
            .ToListAsync(cancellationToken);

        return itens
            .GroupBy(x => new { x.InsumoId, x.Nome })
            .Select(x =>
            {
                var quantidade = x.Sum(i => i.Quantidade);
                var custoMedio = x.Average(i => i.PrecoUnitario);
                return new CustoInsumoItem(
                    x.Key.InsumoId,
                    x.Key.Nome,
                    quantidade,
                    custoMedio,
                    quantidade * custoMedio);
            })
            .OrderByDescending(x => x.CustoTotal)
            .ThenBy(x => x.NomeInsumo)
            .ToList();
    }

    private async Task<List<MovimentacaoRelatorioItem>> ObterMovimentacoesAsync(DateTime inicioUtc, DateTime fimUtc, CancellationToken cancellationToken)
    {
        return await _context.MovimentacoesEstoque
            .AsNoTracking()
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Insumo)
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Produto)
            .Where(x => x.DataMovimentacao >= inicioUtc && x.DataMovimentacao <= fimUtc)
            .OrderByDescending(x => x.DataMovimentacao)
            .Select(x => new MovimentacaoRelatorioItem(
                x.Id,
                x.DataMovimentacao,
                x.TipoMovimentacao,
                x.Estoque != null
                    ? (x.Estoque.Insumo != null ? x.Estoque.Insumo.Nome : x.Estoque.Produto != null ? x.Estoque.Produto.Nome : "-")
                    : "-",
                x.Quantidade,
                x.Motivo,
                x.DocumentoReferencia,
                x.UsuarioId))
            .Take(500)
            .ToListAsync(cancellationToken);
    }
}

public sealed record RelatoriosResultado(
    DateTime InicioUtc,
    DateTime FimUtc,
    DashboardRelatorio Dashboard,
    IReadOnlyList<DesempenhoProducaoItem> DesempenhoProducao,
    IReadOnlyList<GiroEstoqueItem> GiroEstoque,
    IReadOnlyList<CustoInsumoItem> CustosInsumo,
    IReadOnlyList<MovimentacaoRelatorioItem> Movimentacoes);

public sealed record DashboardRelatorio(
    decimal ProducaoDia,
    decimal ProducaoSemana,
    decimal ProducaoMes,
    decimal ValorEstoqueAtual,
    int AlertasAtivos,
    int OpsEmAndamento,
    IReadOnlyList<DashboardPontoSerie> Serie);

public sealed record DashboardPontoSerie(
    DateTime Data,
    decimal QuantidadeProduzida,
    decimal QuantidadeSaidaEstoque);

public sealed record DesempenhoProducaoItem(
    int OrdemId,
    string CodigoOrdem,
    string Produto,
    StatusOrdemProducao Status,
    decimal QuantidadePlanejada,
    decimal QuantidadeProduzida,
    DateTime DataInicioPrevista,
    DateTime DataFimPrevista,
    DateTime? DataFimReal)
{
    public decimal PercentualAtendimento => QuantidadePlanejada <= 0 ? 0m : (QuantidadeProduzida / QuantidadePlanejada) * 100m;
}

public sealed record GiroEstoqueItem(
    int EstoqueId,
    string NomeItem,
    TipoItem TipoItem,
    decimal SaldoAtual,
    decimal EntradasPeriodo,
    decimal SaidasPeriodo,
    decimal SaldoMedioEstimado,
    decimal Giro);

public sealed record CustoInsumoItem(
    int InsumoId,
    string NomeInsumo,
    decimal QuantidadeConsumida,
    decimal CustoMedioUnitario,
    decimal CustoTotal);

public sealed record MovimentacaoRelatorioItem(
    int Id,
    DateTime DataMovimentacao,
    TipoMovimentacao TipoMovimentacao,
    string NomeItem,
    decimal Quantidade,
    string Motivo,
    string? DocumentoReferencia,
    string? UsuarioId);
