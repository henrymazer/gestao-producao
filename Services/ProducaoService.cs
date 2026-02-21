using System.Security.Claims;
using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Services;

public class ProducaoService
{
    private readonly AppDbContext _context;
    private readonly AlertaService _alertaService;

    public ProducaoService(AppDbContext context, AlertaService alertaService)
    {
        _context = context;
        _alertaService = alertaService;
    }

    public async Task<List<PlanoResumo>> ListarPlanosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PlanosProducao
            .AsNoTracking()
            .Select(x => new PlanoResumo(
                x.Id,
                x.Nome,
                x.DataInicio,
                x.DataFim,
                x.Status,
                x.Itens.Count))
            .OrderByDescending(x => x.DataInicio)
            .ThenBy(x => x.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlanoDetalhe?> ObterPlanoAsync(int planoId, CancellationToken cancellationToken = default)
    {
        return await _context.PlanosProducao
            .AsNoTracking()
            .Where(x => x.Id == planoId)
            .Select(x => new PlanoDetalhe(
                x.Id,
                x.Nome,
                x.Descricao,
                x.DataInicio,
                x.DataFim,
                x.Status,
                x.Itens
                    .OrderBy(i => i.Prioridade)
                    .ThenBy(i => i.OrdemProducao!.Codigo)
                    .Select(i => new PlanoOrdemVinculada(
                        i.OrdemProducaoId,
                        i.OrdemProducao!.Codigo,
                        i.OrdemProducao.Produto!.Nome,
                        i.OrdemProducao.Status,
                        i.Prioridade))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OperacaoResultado<int>> SalvarPlanoAsync(SalvarPlanoRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return OperacaoResultado<int>.Falha("Informe o nome do plano de produção.");
        }

        if (request.DataFim < request.DataInicio)
        {
            return OperacaoResultado<int>.Falha("A data de fim deve ser maior ou igual à data de início.");
        }

        if (request.Itens.Any(x => x.Prioridade <= 0))
        {
            return OperacaoResultado<int>.Falha("A prioridade das ordens deve ser maior que zero.");
        }

        var itensSelecionados = request.Itens
            .GroupBy(x => x.OrdemProducaoId)
            .Select(x => x.OrderBy(y => y.Prioridade).First())
            .ToList();

        if (itensSelecionados.Count != request.Itens.Count)
        {
            return OperacaoResultado<int>.Falha("Existem ordens duplicadas na seleção do plano.");
        }

        var ordemIds = itensSelecionados.Select(x => x.OrdemProducaoId).ToList();

        var ordensValidas = await _context.OrdensProducao
            .AsNoTracking()
            .Where(x => ordemIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Status })
            .ToListAsync(cancellationToken);

        if (ordensValidas.Count != ordemIds.Count)
        {
            return OperacaoResultado<int>.Falha("Uma ou mais ordens selecionadas não existem.");
        }

        var possuiOrdemFechada = ordensValidas.Any(x => x.Status is StatusOrdemProducao.Cancelada or StatusOrdemProducao.Concluida);
        if (possuiOrdemFechada)
        {
            return OperacaoResultado<int>.Falha("Apenas ordens planejadas ou em andamento podem ser vinculadas ao plano.");
        }

        PlanoProducao? plano;
        if (request.Id == 0)
        {
            plano = new PlanoProducao();
            _context.PlanosProducao.Add(plano);
        }
        else
        {
            plano = await _context.PlanosProducao
                .Include(x => x.Itens)
                .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

            if (plano is null)
            {
                return OperacaoResultado<int>.Falha("Plano de produção não encontrado.");
            }
        }

        plano.Nome = request.Nome.Trim();
        plano.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        plano.DataInicio = request.DataInicio;
        plano.DataFim = request.DataFim;
        plano.Status = request.Status;

        var itensExistentes = plano.Itens.ToDictionary(x => x.OrdemProducaoId);
        var idsSelecionados = new HashSet<int>(ordemIds);

        foreach (var item in plano.Itens.Where(x => !idsSelecionados.Contains(x.OrdemProducaoId)).ToList())
        {
            _context.PlanosProducaoItens.Remove(item);
        }

        foreach (var itemSelecionado in itensSelecionados)
        {
            if (itensExistentes.TryGetValue(itemSelecionado.OrdemProducaoId, out var itemExistente))
            {
                itemExistente.Prioridade = itemSelecionado.Prioridade;
                continue;
            }

            plano.Itens.Add(new PlanoProducaoItem
            {
                OrdemProducaoId = itemSelecionado.OrdemProducaoId,
                Prioridade = itemSelecionado.Prioridade
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return OperacaoResultado<int>.Ok(plano.Id);
    }

    public async Task<OperacaoResultado> ExcluirPlanoAsync(int planoId, CancellationToken cancellationToken = default)
    {
        var plano = await _context.PlanosProducao
            .Include(x => x.Itens)
            .FirstOrDefaultAsync(x => x.Id == planoId, cancellationToken);

        if (plano is null)
        {
            return OperacaoResultado.Falha("Plano de produção não encontrado.");
        }

        _context.PlanosProducao.Remove(plano);
        await _context.SaveChangesAsync(cancellationToken);

        return OperacaoResultado.Ok();
    }

    public async Task<List<OrdemResumo>> ListarOrdensAsync(CancellationToken cancellationToken = default)
    {
        return await _context.OrdensProducao
            .AsNoTracking()
            .Include(x => x.Produto)
            .Include(x => x.Equipamento)
            .Select(x => new OrdemResumo(
                x.Id,
                x.Codigo,
                x.Produto!.Nome,
                x.Equipamento != null ? x.Equipamento.Nome : null,
                x.QuantidadePlanejada,
                x.QuantidadeProduzida,
                x.Status,
                x.DataInicioPrevista,
                x.DataFimPrevista,
                x.DataInicioReal,
                x.DataFimReal,
                x.PlanosProducao
                    .OrderBy(p => p.Prioridade)
                    .Select(p => p.PlanoProducao!.Nome)
                    .FirstOrDefault()))
            .OrderBy(x => x.DataInicioPrevista)
            .ThenBy(x => x.Codigo)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrdemDetalhe?> ObterOrdemAsync(int ordemId, CancellationToken cancellationToken = default)
    {
        return await _context.OrdensProducao
            .AsNoTracking()
            .Where(x => x.Id == ordemId)
            .Select(x => new OrdemDetalhe(
                x.Id,
                x.Codigo,
                x.ProdutoId,
                x.QuantidadePlanejada,
                x.QuantidadeProduzida,
                x.Status,
                x.DataInicioPrevista,
                x.DataFimPrevista,
                x.DataInicioReal,
                x.DataFimReal,
                x.EquipamentoId,
                x.Observacoes))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<OperacaoResultado<int>> SalvarOrdemAsync(SalvarOrdemRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
        {
            return OperacaoResultado<int>.Falha("Informe o código da ordem de produção.");
        }

        if (request.QuantidadePlanejada <= 0)
        {
            return OperacaoResultado<int>.Falha("A quantidade planejada deve ser maior que zero.");
        }

        if (request.QuantidadeProduzida < 0)
        {
            return OperacaoResultado<int>.Falha("A quantidade produzida não pode ser negativa.");
        }

        if (request.DataFimPrevista < request.DataInicioPrevista)
        {
            return OperacaoResultado<int>.Falha("A data de fim prevista deve ser maior ou igual à data de início prevista.");
        }

        var codigoNormalizado = request.Codigo.Trim();
        var codigoDuplicado = await _context.OrdensProducao
            .AsNoTracking()
            .AnyAsync(x => x.Codigo == codigoNormalizado && x.Id != request.Id, cancellationToken);

        if (codigoDuplicado)
        {
            return OperacaoResultado<int>.Falha("Já existe uma ordem de produção com este código.");
        }

        var produtoAtivo = await _context.Produtos
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.ProdutoId && x.Ativo, cancellationToken);

        if (!produtoAtivo)
        {
            return OperacaoResultado<int>.Falha("Selecione um produto válido e ativo.");
        }

        if (request.EquipamentoId.HasValue)
        {
            var equipamentoValido = await _context.Equipamentos
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.EquipamentoId.Value && x.Ativo, cancellationToken);

            if (!equipamentoValido)
            {
                return OperacaoResultado<int>.Falha("Selecione um equipamento válido e ativo.");
            }
        }

        OrdemProducao? ordem;
        if (request.Id == 0)
        {
            ordem = new OrdemProducao();
            _context.OrdensProducao.Add(ordem);
        }
        else
        {
            ordem = await _context.OrdensProducao.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
            if (ordem is null)
            {
                return OperacaoResultado<int>.Falha("Ordem de produção não encontrada.");
            }
        }

        ordem.Codigo = codigoNormalizado;
        ordem.ProdutoId = request.ProdutoId;
        ordem.QuantidadePlanejada = request.QuantidadePlanejada;
        ordem.QuantidadeProduzida = request.QuantidadeProduzida;
        ordem.Status = request.Status;
        ordem.DataInicioPrevista = request.DataInicioPrevista;
        ordem.DataFimPrevista = request.DataFimPrevista;
        ordem.DataInicioReal = request.DataInicioReal;
        ordem.DataFimReal = request.DataFimReal;
        ordem.EquipamentoId = request.EquipamentoId;
        ordem.Observacoes = string.IsNullOrWhiteSpace(request.Observacoes) ? null : request.Observacoes.Trim();

        await _context.SaveChangesAsync(cancellationToken);
        return OperacaoResultado<int>.Ok(ordem.Id);
    }

    public async Task<OperacaoResultado> ExcluirOrdemAsync(int ordemId, CancellationToken cancellationToken = default)
    {
        var ordem = await _context.OrdensProducao
            .Include(x => x.PlanosProducao)
            .FirstOrDefaultAsync(x => x.Id == ordemId, cancellationToken);

        if (ordem is null)
        {
            return OperacaoResultado.Falha("Ordem de produção não encontrada.");
        }

        if (ordem.PlanosProducao.Count > 0)
        {
            return OperacaoResultado.Falha("A ordem está vinculada a um plano de produção e não pode ser excluída.");
        }

        _context.OrdensProducao.Remove(ordem);
        await _context.SaveChangesAsync(cancellationToken);

        return OperacaoResultado.Ok();
    }

    public async Task<VerificacaoDisponibilidadeResultado> VerificarDisponibilidadeAsync(
        int ordemId,
        decimal? quantidadeParaProduzir = null,
        CancellationToken cancellationToken = default)
    {
        var ordem = await _context.OrdensProducao
            .AsNoTracking()
            .Include(x => x.Produto)
            .FirstOrDefaultAsync(x => x.Id == ordemId, cancellationToken);

        if (ordem is null)
        {
            return new VerificacaoDisponibilidadeResultado(false, []);
        }

        var quantidadeBase = quantidadeParaProduzir ?? Math.Max(0, ordem.QuantidadePlanejada - ordem.QuantidadeProduzida);
        if (quantidadeBase <= 0)
        {
            quantidadeBase = ordem.QuantidadePlanejada;
        }

        var bom = await _context.ProdutoInsumos
            .AsNoTracking()
            .Include(x => x.Insumo)
            .Where(x => x.ProdutoId == ordem.ProdutoId)
            .ToListAsync(cancellationToken);

        if (bom.Count == 0)
        {
            return new VerificacaoDisponibilidadeResultado(true, []);
        }

        var insumoIds = bom.Select(x => x.InsumoId).Distinct().ToList();

        var saldos = await _context.Estoques
            .AsNoTracking()
            .Where(x => x.InsumoId.HasValue && insumoIds.Contains(x.InsumoId.Value))
            .GroupBy(x => x.InsumoId!.Value)
            .Select(x => new
            {
                InsumoId = x.Key,
                Quantidade = x.Sum(y => y.QuantidadeAtual)
            })
            .ToListAsync(cancellationToken);

        var saldoPorInsumo = saldos.ToDictionary(x => x.InsumoId, x => x.Quantidade);

        var detalhes = bom.Select(item =>
            {
                var necessario = item.QuantidadeNecessaria * quantidadeBase;
                var disponivel = saldoPorInsumo.TryGetValue(item.InsumoId, out var valor) ? valor : 0m;
                return new DisponibilidadeInsumo(
                    item.InsumoId,
                    item.Insumo?.Nome ?? $"Insumo {item.InsumoId}",
                    necessario,
                    disponivel,
                    disponivel >= necessario);
            })
            .OrderBy(x => x.InsumoNome)
            .ToList();

        return new VerificacaoDisponibilidadeResultado(detalhes.All(x => x.Atende), detalhes);
    }

    public async Task<OperacaoResultado> IniciarOrdemAsync(int ordemId, CancellationToken cancellationToken = default)
    {
        var ordem = await _context.OrdensProducao
            .FirstOrDefaultAsync(x => x.Id == ordemId, cancellationToken);

        if (ordem is null)
        {
            return OperacaoResultado.Falha("Ordem de produção não encontrada.");
        }

        if (ordem.Status == StatusOrdemProducao.Cancelada)
        {
            return OperacaoResultado.Falha("A ordem está cancelada e não pode ser iniciada.");
        }

        if (ordem.Status == StatusOrdemProducao.Concluida)
        {
            return OperacaoResultado.Falha("A ordem já foi concluída.");
        }

        var disponibilidade = await VerificarDisponibilidadeAsync(ordemId, ordem.QuantidadePlanejada, cancellationToken);
        if (!disponibilidade.Disponivel)
        {
            return OperacaoResultado.Falha("Não há insumos suficientes para iniciar a ordem.");
        }

        ordem.Status = StatusOrdemProducao.EmAndamento;
        ordem.DataInicioReal ??= DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return OperacaoResultado.Ok();
    }

    public async Task<OperacaoResultado> CancelarOrdemAsync(int ordemId, CancellationToken cancellationToken = default)
    {
        var ordem = await _context.OrdensProducao.FirstOrDefaultAsync(x => x.Id == ordemId, cancellationToken);
        if (ordem is null)
        {
            return OperacaoResultado.Falha("Ordem de produção não encontrada.");
        }

        if (ordem.Status == StatusOrdemProducao.Concluida)
        {
            return OperacaoResultado.Falha("Não é possível cancelar uma ordem concluída.");
        }

        ordem.Status = StatusOrdemProducao.Cancelada;
        await _context.SaveChangesAsync(cancellationToken);

        return OperacaoResultado.Ok();
    }

    public async Task<OperacaoResultado> ConcluirOrdemAsync(
        int ordemId,
        decimal? quantidadeProduzida,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        var ordem = await _context.OrdensProducao
            .Include(x => x.Produto)
            .FirstOrDefaultAsync(x => x.Id == ordemId, cancellationToken);

        if (ordem is null)
        {
            return OperacaoResultado.Falha("Ordem de produção não encontrada.");
        }

        if (ordem.Status == StatusOrdemProducao.Cancelada)
        {
            return OperacaoResultado.Falha("A ordem está cancelada e não pode ser concluída.");
        }

        if (ordem.Status == StatusOrdemProducao.Concluida)
        {
            return OperacaoResultado.Falha("A ordem já está concluída.");
        }

        var quantidadeFinal = quantidadeProduzida ?? ordem.QuantidadePlanejada;
        if (quantidadeFinal <= 0)
        {
            return OperacaoResultado.Falha("Informe uma quantidade produzida válida para concluir a ordem.");
        }

        var disponibilidade = await VerificarDisponibilidadeAsync(ordemId, quantidadeFinal, cancellationToken);
        if (!disponibilidade.Disponivel)
        {
            return OperacaoResultado.Falha("Não há insumos suficientes para concluir a ordem e baixar estoque automaticamente.");
        }

        var usuarioId = user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? user?.Identity?.Name;

        await using var transacao = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var composicao = await _context.ProdutoInsumos
                .AsNoTracking()
                .Where(x => x.ProdutoId == ordem.ProdutoId)
                .ToListAsync(cancellationToken);

            foreach (var item in composicao)
            {
                var quantidadeNecessaria = item.QuantidadeNecessaria * quantidadeFinal;
                if (quantidadeNecessaria <= 0)
                {
                    continue;
                }

                var estoquesInsumo = await _context.Estoques
                    .Where(x => x.InsumoId == item.InsumoId && x.QuantidadeAtual > 0)
                    .OrderBy(x => x.DataValidade.HasValue ? 0 : 1)
                    .ThenBy(x => x.DataValidade)
                    .ThenBy(x => x.AtualizadoEm)
                    .ToListAsync(cancellationToken);

                var restante = quantidadeNecessaria;

                foreach (var estoque in estoquesInsumo)
                {
                    if (restante <= 0)
                    {
                        break;
                    }

                    var baixa = Math.Min(estoque.QuantidadeAtual, restante);
                    if (baixa <= 0)
                    {
                        continue;
                    }

                    estoque.QuantidadeAtual -= baixa;
                    estoque.AtualizadoEm = DateTime.UtcNow;
                    restante -= baixa;

                    _context.MovimentacoesEstoque.Add(new MovimentacaoEstoque
                    {
                        EstoqueId = estoque.Id,
                        TipoMovimentacao = TipoMovimentacao.Saida,
                        Quantidade = baixa,
                        Motivo = $"Baixa automática da OP {ordem.Codigo}",
                        DocumentoReferencia = ordem.Codigo,
                        UsuarioId = usuarioId,
                        DataMovimentacao = DateTime.UtcNow
                    });

                    await _alertaService.AtualizarAlertasDoEstoqueAsync(estoque.Id, salvarAlteracoes: false, cancellationToken);
                }

                if (restante > 0)
                {
                    await transacao.RollbackAsync(cancellationToken);
                    return OperacaoResultado.Falha("Falha na baixa automática de insumos. Verifique os saldos de estoque e tente novamente.");
                }
            }

            ordem.QuantidadeProduzida = quantidadeFinal;
            ordem.Status = StatusOrdemProducao.Concluida;
            ordem.DataInicioReal ??= DateTime.UtcNow;
            ordem.DataFimReal = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            await transacao.CommitAsync(cancellationToken);

            return OperacaoResultado.Ok();
        }
        catch
        {
            await transacao.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<TimelineItem>> ListarTimelineAsync(DateTime? inicio = null, DateTime? fim = null, CancellationToken cancellationToken = default)
    {
        var query = _context.OrdensProducao
            .AsNoTracking()
            .Include(x => x.Produto)
            .Include(x => x.PlanosProducao)
                .ThenInclude(x => x.PlanoProducao)
            .AsQueryable();

        if (inicio.HasValue)
        {
            query = query.Where(x => x.DataFimPrevista >= inicio.Value);
        }

        if (fim.HasValue)
        {
            query = query.Where(x => x.DataInicioPrevista <= fim.Value);
        }

        return await query
            .Select(x => new TimelineItem(
                x.Id,
                x.Codigo,
                x.Produto!.Nome,
                x.Status,
                x.DataInicioPrevista,
                x.DataFimPrevista,
                x.PlanosProducao
                    .OrderBy(p => p.Prioridade)
                    .Select(p => p.PlanoProducao!.Nome)
                    .FirstOrDefault()))
            .OrderBy(x => x.DataInicioPrevista)
            .ThenBy(x => x.OrdemCodigo)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<NecessidadeInsumoResumo>> CalcularNecessidadeInsumosPorPlanosAtivosAsync(CancellationToken cancellationToken = default)
    {
        var ordensPlanejadas = await _context.PlanosProducaoItens
            .AsNoTracking()
            .Where(x =>
                x.PlanoProducao != null &&
                x.PlanoProducao.Status == StatusPlano.Ativo &&
                x.OrdemProducao != null &&
                x.OrdemProducao.Status != StatusOrdemProducao.Cancelada &&
                x.OrdemProducao.Status != StatusOrdemProducao.Concluida)
            .Select(x => new
            {
                OrdemProducaoId = x.OrdemProducaoId,
                x.OrdemProducao!.ProdutoId,
                QuantidadePendente = Math.Max(0m, x.OrdemProducao.QuantidadePlanejada - x.OrdemProducao.QuantidadeProduzida)
            })
            .Where(x => x.QuantidadePendente > 0)
            .ToListAsync(cancellationToken);

        if (ordensPlanejadas.Count == 0)
        {
            return [];
        }

        var ordensUnicas = ordensPlanejadas
            .GroupBy(x => x.OrdemProducaoId)
            .Select(x => x.First())
            .ToList();

        var quantidadePorProduto = ordensUnicas
            .GroupBy(x => x.ProdutoId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.QuantidadePendente));

        var produtoIds = quantidadePorProduto.Keys.ToList();

        var composicao = await _context.ProdutoInsumos
            .AsNoTracking()
            .Include(x => x.Insumo)
            .Where(x => produtoIds.Contains(x.ProdutoId))
            .ToListAsync(cancellationToken);

        if (composicao.Count == 0)
        {
            return [];
        }

        var necessidadePorInsumo = composicao
            .GroupBy(x => new { x.InsumoId, Nome = x.Insumo != null ? x.Insumo.Nome : $"Insumo {x.InsumoId}" })
            .Select(x =>
            {
                var necessidade = x.Sum(item =>
                {
                    var quantidadeProduto = quantidadePorProduto.GetValueOrDefault(item.ProdutoId, 0m);
                    return quantidadeProduto * item.QuantidadeNecessaria;
                });

                return new
                {
                    x.Key.InsumoId,
                    x.Key.Nome,
                    QuantidadeNecessaria = necessidade
                };
            })
            .Where(x => x.QuantidadeNecessaria > 0)
            .ToList();

        var insumoIds = necessidadePorInsumo.Select(x => x.InsumoId).Distinct().ToList();

        var disponibilidade = await _context.Estoques
            .AsNoTracking()
            .Where(x => x.InsumoId.HasValue && insumoIds.Contains(x.InsumoId.Value))
            .GroupBy(x => x.InsumoId!.Value)
            .Select(x => new
            {
                InsumoId = x.Key,
                QuantidadeDisponivel = x.Sum(y => y.QuantidadeAtual)
            })
            .ToDictionaryAsync(x => x.InsumoId, x => x.QuantidadeDisponivel, cancellationToken);

        return necessidadePorInsumo
            .Select(x =>
            {
                var quantidadeDisponivel = disponibilidade.GetValueOrDefault(x.InsumoId, 0m);
                var quantidadeFaltante = Math.Max(0m, x.QuantidadeNecessaria - quantidadeDisponivel);

                return new NecessidadeInsumoResumo(
                    x.InsumoId,
                    x.Nome,
                    x.QuantidadeNecessaria,
                    quantidadeDisponivel,
                    quantidadeFaltante);
            })
            .OrderByDescending(x => x.QuantidadeFaltante)
            .ThenBy(x => x.InsumoNome)
            .ToList();
    }
}

public sealed record PlanoResumo(
    int Id,
    string Nome,
    DateTime DataInicio,
    DateTime DataFim,
    StatusPlano Status,
    int TotalOrdens);

public sealed record PlanoDetalhe(
    int Id,
    string Nome,
    string? Descricao,
    DateTime DataInicio,
    DateTime DataFim,
    StatusPlano Status,
    List<PlanoOrdemVinculada> Ordens);

public sealed record PlanoOrdemVinculada(
    int OrdemProducaoId,
    string OrdemCodigo,
    string ProdutoNome,
    StatusOrdemProducao Status,
    int Prioridade);

public sealed record OrdemResumo(
    int Id,
    string Codigo,
    string ProdutoNome,
    string? EquipamentoNome,
    decimal QuantidadePlanejada,
    decimal QuantidadeProduzida,
    StatusOrdemProducao Status,
    DateTime DataInicioPrevista,
    DateTime DataFimPrevista,
    DateTime? DataInicioReal,
    DateTime? DataFimReal,
    string? PlanoNome);

public sealed record OrdemDetalhe(
    int Id,
    string Codigo,
    int ProdutoId,
    decimal QuantidadePlanejada,
    decimal QuantidadeProduzida,
    StatusOrdemProducao Status,
    DateTime DataInicioPrevista,
    DateTime DataFimPrevista,
    DateTime? DataInicioReal,
    DateTime? DataFimReal,
    int? EquipamentoId,
    string? Observacoes);

public sealed record TimelineItem(
    int OrdemId,
    string OrdemCodigo,
    string ProdutoNome,
    StatusOrdemProducao Status,
    DateTime DataInicioPrevista,
    DateTime DataFimPrevista,
    string? PlanoNome);

public sealed record DisponibilidadeInsumo(
    int InsumoId,
    string InsumoNome,
    decimal QuantidadeNecessaria,
    decimal QuantidadeDisponivel,
    bool Atende);

public sealed record VerificacaoDisponibilidadeResultado(
    bool Disponivel,
    List<DisponibilidadeInsumo> Itens);

public sealed record NecessidadeInsumoResumo(
    int InsumoId,
    string InsumoNome,
    decimal QuantidadeNecessaria,
    decimal QuantidadeDisponivel,
    decimal QuantidadeFaltante);

public sealed record SalvarPlanoRequest(
    int Id,
    string Nome,
    string? Descricao,
    DateTime DataInicio,
    DateTime DataFim,
    StatusPlano Status,
    List<SalvarPlanoItemRequest> Itens);

public sealed record SalvarPlanoItemRequest(
    int OrdemProducaoId,
    int Prioridade);

public sealed record SalvarOrdemRequest(
    int Id,
    string Codigo,
    int ProdutoId,
    decimal QuantidadePlanejada,
    decimal QuantidadeProduzida,
    StatusOrdemProducao Status,
    DateTime DataInicioPrevista,
    DateTime DataFimPrevista,
    DateTime? DataInicioReal,
    DateTime? DataFimReal,
    int? EquipamentoId,
    string? Observacoes);

public sealed record OperacaoResultado(bool Sucesso, string? Erro)
{
    public static OperacaoResultado Ok() => new(true, null);

    public static OperacaoResultado Falha(string erro) => new(false, erro);
}

public sealed record OperacaoResultado<T>(bool Sucesso, T? Valor, string? Erro)
{
    public static OperacaoResultado<T> Ok(T valor) => new(true, valor, null);

    public static OperacaoResultado<T> Falha(string erro) => new(false, default, erro);
}
