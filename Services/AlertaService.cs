using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Services;

public class AlertaService
{
    private const int DiasAntecedenciaValidade = 30;
    private readonly AppDbContext _context;

    public AlertaService(AppDbContext context)
    {
        _context = context;
    }

    public async Task AtualizarAlertasAsync(CancellationToken cancellationToken = default)
    {
        var estoqueIds = await _context.Estoques
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var estoqueId in estoqueIds)
        {
            await AtualizarAlertasDoEstoqueAsync(estoqueId, cancellationToken);
        }
    }

    public async Task AtualizarAlertasDoEstoqueAsync(int estoqueId, CancellationToken cancellationToken = default)
    {
        var estoque = await _context.Estoques
            .Include(x => x.Insumo)
            .FirstOrDefaultAsync(x => x.Id == estoqueId, cancellationToken);

        if (estoque is null)
        {
            return;
        }

        var insumo = estoque.Insumo;
        if (insumo is not null)
        {
            await SincronizarAlertaAsync(
                estoqueId,
                TipoAlerta.EstoqueMinimo,
                estoque.QuantidadeAtual <= insumo.EstoqueMinimo,
                $"Estoque abaixo do mínimo para {insumo.Nome} (atual: {estoque.QuantidadeAtual:N2}; mínimo: {insumo.EstoqueMinimo:N2}).",
                cancellationToken);

            await SincronizarAlertaAsync(
                estoqueId,
                TipoAlerta.EstoqueMaximo,
                estoque.QuantidadeAtual >= insumo.EstoqueMaximo,
                $"Estoque acima do máximo para {insumo.Nome} (atual: {estoque.QuantidadeAtual:N2}; máximo: {insumo.EstoqueMaximo:N2}).",
                cancellationToken);
        }
        else
        {
            await EncerrarAlertasAtivosAsync(estoqueId, TipoAlerta.EstoqueMinimo, cancellationToken);
            await EncerrarAlertasAtivosAsync(estoqueId, TipoAlerta.EstoqueMaximo, cancellationToken);
        }

        var limiteValidade = DateTime.UtcNow.AddDays(DiasAntecedenciaValidade);
        var possuiValidadeProxima = estoque.DataValidade.HasValue && estoque.DataValidade.Value <= limiteValidade;

        await SincronizarAlertaAsync(
            estoqueId,
            TipoAlerta.Validade,
            possuiValidadeProxima,
            $"Item com validade próxima para o lote {estoque.Lote ?? "sem lote"} (validade: {estoque.DataValidade:dd/MM/yyyy}).",
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SincronizarAlertaAsync(
        int estoqueId,
        TipoAlerta tipoAlerta,
        bool condicaoAtiva,
        string mensagem,
        CancellationToken cancellationToken)
    {
        var alertaAtivo = await _context.AlertasEstoque
            .Where(x => x.EstoqueId == estoqueId && x.TipoAlerta == tipoAlerta && !x.Lido)
            .OrderByDescending(x => x.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        if (condicaoAtiva)
        {
            if (alertaAtivo is null)
            {
                _context.AlertasEstoque.Add(new AlertaEstoque
                {
                    EstoqueId = estoqueId,
                    TipoAlerta = tipoAlerta,
                    Mensagem = mensagem,
                    Lido = false
                });
            }
            else if (!string.Equals(alertaAtivo.Mensagem, mensagem, StringComparison.Ordinal))
            {
                alertaAtivo.Mensagem = mensagem;
            }

            return;
        }

        await EncerrarAlertasAtivosAsync(estoqueId, tipoAlerta, cancellationToken);
    }

    private async Task EncerrarAlertasAtivosAsync(int estoqueId, TipoAlerta tipoAlerta, CancellationToken cancellationToken)
    {
        var alertasAtivos = await _context.AlertasEstoque
            .Where(x => x.EstoqueId == estoqueId && x.TipoAlerta == tipoAlerta && !x.Lido)
            .ToListAsync(cancellationToken);

        foreach (var alerta in alertasAtivos)
        {
            alerta.Lido = true;
        }
    }
}
