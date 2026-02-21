using System.Security.Claims;
using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Services;

public class EstoqueService
{
    private readonly AppDbContext _context;
    private readonly AlertaService _alertaService;

    public EstoqueService(AppDbContext context, AlertaService alertaService)
    {
        _context = context;
        _alertaService = alertaService;
    }

    public async Task<List<EstoqueResumo>> ListarEstoqueAsync(string? filtro = null, TipoItem? tipoItem = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Estoques
            .AsNoTracking()
            .Include(x => x.Insumo)
            .Include(x => x.Produto)
            .AsQueryable();

        if (tipoItem.HasValue)
        {
            query = query.Where(x => x.TipoItem == tipoItem.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var termo = filtro.Trim().ToLower();
            query = query.Where(x =>
                (x.Insumo != null && x.Insumo.Nome.ToLower().Contains(termo))
                || (x.Produto != null && x.Produto.Nome.ToLower().Contains(termo))
                || (x.Lote != null && x.Lote.ToLower().Contains(termo))
                || (x.Localizacao != null && x.Localizacao.ToLower().Contains(termo)));
        }

        return await query
            .OrderBy(x => x.TipoItem)
            .ThenBy(x => x.Insumo != null ? x.Insumo.Nome : x.Produto!.Nome)
            .Select(x => new EstoqueResumo(
                x.Id,
                x.TipoItem,
                x.InsumoId,
                x.Insumo != null ? x.Insumo.Nome : x.Produto!.Nome,
                x.QuantidadeAtual,
                x.Lote,
                x.Localizacao,
                x.DataValidade,
                x.AtualizadoEm,
                x.Insumo != null ? x.Insumo.EstoqueMinimo : null,
                x.Insumo != null ? x.Insumo.EstoqueMaximo : null))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<MovimentacaoResumo>> ListarMovimentacoesAsync(
        TipoMovimentacao? tipoMovimentacao = null,
        DateTime? inicio = null,
        DateTime? fim = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.MovimentacoesEstoque
            .AsNoTracking()
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Insumo)
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Produto)
            .AsQueryable();

        if (tipoMovimentacao.HasValue)
        {
            query = query.Where(x => x.TipoMovimentacao == tipoMovimentacao.Value);
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
            .Select(x => new MovimentacaoResumo(
                x.Id,
                x.EstoqueId,
                x.TipoMovimentacao,
                x.Quantidade,
                x.Motivo,
                x.DocumentoReferencia,
                x.UsuarioId,
                x.DataMovimentacao,
                x.Estoque != null
                    ? (x.Estoque.Insumo != null ? x.Estoque.Insumo.Nome : x.Estoque.Produto!.Nome)
                    : "Item removido"))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<AlertaResumo>> ListarAlertasAtivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AlertasEstoque
            .AsNoTracking()
            .Where(x => !x.Lido)
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Insumo)
            .Include(x => x.Estoque)
                .ThenInclude(x => x!.Produto)
            .OrderByDescending(x => x.CriadoEm)
            .Select(x => new AlertaResumo(
                x.Id,
                x.EstoqueId,
                x.TipoAlerta,
                x.Mensagem,
                x.CriadoEm,
                x.Estoque != null
                    ? (x.Estoque.Insumo != null ? x.Estoque.Insumo.Nome : x.Estoque.Produto!.Nome)
                    : "-"))
            .ToListAsync(cancellationToken);
    }

    public async Task MarcarAlertaComoLidoAsync(int alertaId, CancellationToken cancellationToken = default)
    {
        var alerta = await _context.AlertasEstoque.FirstOrDefaultAsync(x => x.Id == alertaId, cancellationToken);
        if (alerta is null)
        {
            return;
        }

        alerta.Lido = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResultadoMovimentacao> RegistrarMovimentacaoAsync(MovimentacaoRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        if (request.Quantidade <= 0)
        {
            return ResultadoMovimentacao.Falha("A quantidade deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return ResultadoMovimentacao.Falha("Informe o motivo da movimentação.");
        }

        var estoque = await ObterOuCriarEstoqueAsync(request, cancellationToken);
        if (estoque is null)
        {
            return ResultadoMovimentacao.Falha("Não foi possível identificar o item de estoque para movimentação.");
        }

        if (request.TipoMovimentacao == TipoMovimentacao.Saida && estoque.QuantidadeAtual < request.Quantidade)
        {
            return ResultadoMovimentacao.Falha("Quantidade insuficiente em estoque para realizar a saída.");
        }

        estoque.QuantidadeAtual = request.TipoMovimentacao switch
        {
            TipoMovimentacao.Entrada => estoque.QuantidadeAtual + request.Quantidade,
            TipoMovimentacao.Saida => estoque.QuantidadeAtual - request.Quantidade,
            _ => request.TipoAjuste == TipoAjusteEstoque.DefinirSaldo
                ? request.Quantidade
                : estoque.QuantidadeAtual + request.Quantidade
        };

        if (!string.IsNullOrWhiteSpace(request.Lote))
        {
            estoque.Lote = request.Lote.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Localizacao))
        {
            estoque.Localizacao = request.Localizacao.Trim();
        }

        if (request.DataValidade.HasValue)
        {
            estoque.DataValidade = request.DataValidade.Value;
        }

        var usuarioId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.Identity?.Name;

        _context.MovimentacoesEstoque.Add(new MovimentacaoEstoque
        {
            EstoqueId = estoque.Id,
            TipoMovimentacao = request.TipoMovimentacao,
            Quantidade = request.Quantidade,
            Motivo = request.Motivo.Trim(),
            DocumentoReferencia = request.DocumentoReferencia?.Trim(),
            UsuarioId = usuarioId,
            DataMovimentacao = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        await _alertaService.AtualizarAlertasDoEstoqueAsync(estoque.Id, cancellationToken);

        return ResultadoMovimentacao.Ok(estoque.Id);
    }

    private async Task<Estoque?> ObterOuCriarEstoqueAsync(MovimentacaoRequest request, CancellationToken cancellationToken)
    {
        if (request.EstoqueId.HasValue)
        {
            return await _context.Estoques
                .FirstOrDefaultAsync(x => x.Id == request.EstoqueId.Value, cancellationToken);
        }

        if (request.TipoMovimentacao != TipoMovimentacao.Entrada)
        {
            return null;
        }

        if (!request.TipoItem.HasValue)
        {
            return null;
        }

        if (request.TipoItem == TipoItem.MateriaPrima && !request.InsumoId.HasValue)
        {
            return null;
        }

        if (request.TipoItem != TipoItem.MateriaPrima && !request.ProdutoId.HasValue)
        {
            return null;
        }

        var query = _context.Estoques.AsQueryable();

        if (request.TipoItem == TipoItem.MateriaPrima)
        {
            query = query.Where(x => x.InsumoId == request.InsumoId && x.TipoItem == request.TipoItem);
        }
        else
        {
            query = query.Where(x => x.ProdutoId == request.ProdutoId && x.TipoItem == request.TipoItem);
        }

        var lote = request.Lote?.Trim();
        var localizacao = request.Localizacao?.Trim();

        query = query.Where(x => x.Lote == lote && x.Localizacao == localizacao && x.DataValidade == request.DataValidade);

        var estoque = await query.FirstOrDefaultAsync(cancellationToken);
        if (estoque is not null)
        {
            return estoque;
        }

        estoque = new Estoque
        {
            TipoItem = request.TipoItem.Value,
            InsumoId = request.InsumoId,
            ProdutoId = request.ProdutoId,
            Lote = lote,
            Localizacao = localizacao,
            DataValidade = request.DataValidade,
            QuantidadeAtual = 0
        };

        _context.Estoques.Add(estoque);
        await _context.SaveChangesAsync(cancellationToken);

        return estoque;
    }
}

public sealed record EstoqueResumo(
    int Id,
    TipoItem TipoItem,
    int? InsumoId,
    string NomeItem,
    decimal QuantidadeAtual,
    string? Lote,
    string? Localizacao,
    DateTime? DataValidade,
    DateTime AtualizadoEm,
    decimal? EstoqueMinimo,
    decimal? EstoqueMaximo);

public sealed record MovimentacaoResumo(
    int Id,
    int EstoqueId,
    TipoMovimentacao TipoMovimentacao,
    decimal Quantidade,
    string Motivo,
    string? DocumentoReferencia,
    string? UsuarioId,
    DateTime DataMovimentacao,
    string NomeItem);

public sealed record AlertaResumo(
    int Id,
    int EstoqueId,
    TipoAlerta TipoAlerta,
    string Mensagem,
    DateTime CriadoEm,
    string NomeItem);

public class MovimentacaoRequest
{
    public int? EstoqueId { get; set; }
    public TipoMovimentacao TipoMovimentacao { get; set; }
    public TipoAjusteEstoque TipoAjuste { get; set; } = TipoAjusteEstoque.Somar;
    public TipoItem? TipoItem { get; set; }
    public int? InsumoId { get; set; }
    public int? ProdutoId { get; set; }
    public decimal Quantidade { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public string? DocumentoReferencia { get; set; }
    public string? Lote { get; set; }
    public DateTime? DataValidade { get; set; }
    public string? Localizacao { get; set; }
}

public enum TipoAjusteEstoque
{
    Somar = 1,
    DefinirSaldo = 2
}

public sealed record ResultadoMovimentacao(bool Sucesso, string? Erro, int? EstoqueId)
{
    public static ResultadoMovimentacao Falha(string erro) => new(false, erro, null);

    public static ResultadoMovimentacao Ok(int estoqueId) => new(true, null, estoqueId);
}
