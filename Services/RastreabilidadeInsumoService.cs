using gestao_producao.Data;
using gestao_producao.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Services;

public class RastreabilidadeInsumoService
{
    private readonly AppDbContext _context;

    public RastreabilidadeInsumoService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<EntradaInsumoDetalhe>> ListarEntradasDetalhadasAsync(
        int? insumoId = null,
        DateTime? inicio = null,
        DateTime? fim = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MovimentacoesEstoque
            .AsNoTracking()
            .Where(x => x.TipoMovimentacao == TipoMovimentacao.Entrada)
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Insumo)
                    .ThenInclude(x => x!.Fornecedor)
            .AsQueryable();

        query = query.Where(x => x.Estoque != null && x.Estoque.InsumoId.HasValue);

        if (insumoId.HasValue)
        {
            query = query.Where(x => x.Estoque!.InsumoId == insumoId.Value);
        }

        if (inicio.HasValue)
        {
            query = query.Where(x => x.DataMovimentacao >= inicio.Value);
        }

        if (fim.HasValue)
        {
            var fimInclusivo = fim.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.DataMovimentacao <= fimInclusivo);
        }

        return await query
            .OrderByDescending(x => x.DataMovimentacao)
            .Take(300)
            .Select(x => new EntradaInsumoDetalhe(
                x.Id,
                x.EstoqueId,
                x.Estoque!.InsumoId!.Value,
                x.Estoque.Insumo!.Nome,
                x.Estoque.Insumo.Fornecedor != null ? x.Estoque.Insumo.Fornecedor.Nome : null,
                x.Quantidade,
                x.Estoque.Lote,
                x.Estoque.DataValidade,
                x.Estoque.Localizacao,
                x.DocumentoReferencia,
                x.Motivo,
                x.UsuarioId,
                x.DataMovimentacao))
            .ToListAsync(cancellationToken);
    }

    public async Task<ConsumoOrdemDetalhe?> ObterConsumoPorOrdemAsync(int ordemId, CancellationToken cancellationToken = default)
    {
        var ordem = await _context.OrdensProducao
            .AsNoTracking()
            .Include(x => x.Produto)
            .FirstOrDefaultAsync(x => x.Id == ordemId, cancellationToken);

        if (ordem is null)
        {
            return null;
        }

        var query = _context.MovimentacoesEstoque
            .AsNoTracking()
            .Where(x => x.TipoMovimentacao == TipoMovimentacao.Saida)
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Insumo)
            .Where(x => x.Estoque != null && x.Estoque.InsumoId.HasValue)
            .AsQueryable();

        var marcadorOrdemNoMotivo = $"OP {ordem.Codigo}";

        query = query.Where(x =>
            x.DocumentoReferencia == ordem.Codigo
            || (x.Motivo != null && x.Motivo.Contains(marcadorOrdemNoMotivo)));

        var movimentacoes = await query
            .OrderByDescending(x => x.DataMovimentacao)
            .Select(x => new ConsumoOrdemMovimentacao(
                x.Id,
                x.EstoqueId,
                x.Estoque!.InsumoId!.Value,
                x.Estoque.Insumo!.Nome,
                x.Quantidade,
                x.Estoque.Lote,
                x.Estoque.DataValidade,
                x.Estoque.Localizacao,
                x.Motivo,
                x.DocumentoReferencia,
                x.UsuarioId,
                x.DataMovimentacao))
            .ToListAsync(cancellationToken);

        var totais = movimentacoes
            .GroupBy(x => new { x.InsumoId, x.InsumoNome })
            .Select(x => new ConsumoOrdemTotalInsumo(
                x.Key.InsumoId,
                x.Key.InsumoNome,
                x.Sum(y => y.Quantidade)))
            .OrderBy(x => x.InsumoNome)
            .ToList();

        return new ConsumoOrdemDetalhe(
            ordem.Id,
            ordem.Codigo,
            ordem.Produto?.Nome ?? $"Produto {ordem.ProdutoId}",
            ordem.QuantidadePlanejada,
            ordem.QuantidadeProduzida,
            totais,
            movimentacoes);
    }

    public async Task<List<HistoricoInsumoMovimentacao>> ListarHistoricoPorInsumoAsync(
        int insumoId,
        DateTime? inicio = null,
        DateTime? fim = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MovimentacoesEstoque
            .AsNoTracking()
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Insumo)
            .Where(x => x.Estoque != null && x.Estoque.InsumoId == insumoId)
            .AsQueryable();

        if (inicio.HasValue)
        {
            query = query.Where(x => x.DataMovimentacao >= inicio.Value);
        }

        if (fim.HasValue)
        {
            var fimInclusivo = fim.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(x => x.DataMovimentacao <= fimInclusivo);
        }

        var movimentacoes = await query
            .OrderByDescending(x => x.DataMovimentacao)
            .Take(500)
            .Select(x => new
            {
                x.Id,
                x.TipoMovimentacao,
                x.Quantidade,
                x.Motivo,
                x.DocumentoReferencia,
                x.UsuarioId,
                x.DataMovimentacao,
                Lote = x.Estoque!.Lote,
                DataValidade = x.Estoque.DataValidade,
                Localizacao = x.Estoque.Localizacao
            })
            .ToListAsync(cancellationToken);

        var documentosReferencia = movimentacoes
            .Select(x => x.DocumentoReferencia?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var codigosOrdens = documentosReferencia.Count == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await _context.OrdensProducao
                .AsNoTracking()
                .Where(x => documentosReferencia.Contains(x.Codigo))
                .Select(x => x.Codigo)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return movimentacoes
            .Select(x =>
            {
                var ordemCodigo = string.IsNullOrWhiteSpace(x.DocumentoReferencia)
                    ? null
                    : codigosOrdens.Contains(x.DocumentoReferencia.Trim())
                        ? x.DocumentoReferencia.Trim()
                        : null;

                return new HistoricoInsumoMovimentacao(
                    x.Id,
                    x.TipoMovimentacao,
                    x.Quantidade,
                    x.Motivo,
                    x.DocumentoReferencia,
                    ordemCodigo,
                    x.Lote,
                    x.DataValidade,
                    x.Localizacao,
                    x.UsuarioId,
                    x.DataMovimentacao);
            })
            .ToList();
    }

    public async Task<List<LoteInsumoResumo>> ListarControleLotesAsync(
        int? insumoId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Estoques
            .AsNoTracking()
            .Include(x => x.Insumo)
            .Where(x => x.InsumoId.HasValue && x.Lote != null)
            .AsQueryable();

        if (insumoId.HasValue)
        {
            query = query.Where(x => x.InsumoId == insumoId.Value);
        }

        return await query
            .GroupBy(x => new
            {
                InsumoId = x.InsumoId!.Value,
                InsumoNome = x.Insumo!.Nome,
                Lote = x.Lote!,
                x.DataValidade,
                x.Localizacao
            })
            .Select(x => new LoteInsumoResumo(
                x.Key.InsumoId,
                x.Key.InsumoNome,
                x.Key.Lote,
                x.Key.DataValidade,
                x.Key.Localizacao,
                x.Sum(y => y.QuantidadeAtual),
                x.Max(y => y.AtualizadoEm)))
            .OrderBy(x => x.InsumoNome)
            .ThenBy(x => x.Lote)
            .ThenBy(x => x.DataValidade)
            .Take(500)
            .ToListAsync(cancellationToken);
    }
}

public sealed record EntradaInsumoDetalhe(
    int MovimentacaoId,
    int EstoqueId,
    int InsumoId,
    string InsumoNome,
    string? FornecedorNome,
    decimal Quantidade,
    string? Lote,
    DateTime? DataValidade,
    string? Localizacao,
    string? DocumentoReferencia,
    string Motivo,
    string? UsuarioId,
    DateTime DataMovimentacao);

public sealed record ConsumoOrdemDetalhe(
    int OrdemId,
    string OrdemCodigo,
    string ProdutoNome,
    decimal QuantidadePlanejada,
    decimal QuantidadeProduzida,
    List<ConsumoOrdemTotalInsumo> TotaisPorInsumo,
    List<ConsumoOrdemMovimentacao> Movimentacoes);

public sealed record ConsumoOrdemTotalInsumo(
    int InsumoId,
    string InsumoNome,
    decimal QuantidadeConsumida);

public sealed record ConsumoOrdemMovimentacao(
    int MovimentacaoId,
    int EstoqueId,
    int InsumoId,
    string InsumoNome,
    decimal Quantidade,
    string? Lote,
    DateTime? DataValidade,
    string? Localizacao,
    string Motivo,
    string? DocumentoReferencia,
    string? UsuarioId,
    DateTime DataMovimentacao);

public sealed record HistoricoInsumoMovimentacao(
    int MovimentacaoId,
    TipoMovimentacao TipoMovimentacao,
    decimal Quantidade,
    string Motivo,
    string? DocumentoReferencia,
    string? OrdemCodigo,
    string? Lote,
    DateTime? DataValidade,
    string? Localizacao,
    string? UsuarioId,
    DateTime DataMovimentacao);

public sealed record LoteInsumoResumo(
    int InsumoId,
    string InsumoNome,
    string Lote,
    DateTime? DataValidade,
    string? Localizacao,
    decimal SaldoAtual,
    DateTime UltimaAtualizacao);
