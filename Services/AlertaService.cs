using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace gestao_producao.Services;

public class AlertaService
{
    private readonly AppDbContext _context;
    private readonly AlertaEstoqueOptions _options;

    public AlertaService(AppDbContext context, IOptions<AlertaEstoqueOptions> options)
    {
        _context = context;
        _options = options.Value;
    }

    public async Task AtualizarAlertasAsync(CancellationToken cancellationToken = default)
    {
        var estoques = await _context.Estoques
            .Include(x => x.Insumo)
            .ToListAsync(cancellationToken);

        if (estoques.Count == 0)
        {
            return;
        }

        var estoqueIds = estoques.Select(x => x.Id).ToList();
        var alertasAtivos = await _context.AlertasEstoque
            .Where(x => !x.Lido && estoqueIds.Contains(x.EstoqueId))
            .ToListAsync(cancellationToken);

        var alertasPorEstoque = alertasAtivos
            .GroupBy(x => x.EstoqueId)
            .ToDictionary(x => x.Key, x => x.ToList());

        foreach (var estoque in estoques)
        {
            alertasPorEstoque.TryGetValue(estoque.Id, out var alertasDoEstoque);
            AplicarRegras(estoque, alertasDoEstoque ?? []);
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task AtualizarAlertasDoEstoqueAsync(int estoqueId, CancellationToken cancellationToken = default)
        => AtualizarAlertasDoEstoqueAsync(estoqueId, salvarAlteracoes: true, cancellationToken);

    public async Task AtualizarAlertasDoEstoqueAsync(int estoqueId, bool salvarAlteracoes, CancellationToken cancellationToken = default)
    {
        var estoque = await _context.Estoques
            .Include(x => x.Insumo)
            .FirstOrDefaultAsync(x => x.Id == estoqueId, cancellationToken);

        if (estoque is null)
        {
            return;
        }

        var alertasAtivos = await _context.AlertasEstoque
            .Where(x => x.EstoqueId == estoqueId && !x.Lido)
            .ToListAsync(cancellationToken);

        AplicarRegras(estoque, alertasAtivos);

        if (salvarAlteracoes)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private void AplicarRegras(Estoque estoque, List<AlertaEstoque> alertasAtivos)
    {
        var insumo = estoque.Insumo;
        if (insumo is not null)
        {
            SincronizarAlerta(
                estoque,
                TipoAlerta.EstoqueMinimo,
                estoque.QuantidadeAtual <= insumo.EstoqueMinimo,
                $"Estoque abaixo do mínimo para {insumo.Nome} (atual: {estoque.QuantidadeAtual:N2}; mínimo: {insumo.EstoqueMinimo:N2}).",
                alertasAtivos);

            SincronizarAlerta(
                estoque,
                TipoAlerta.EstoqueMaximo,
                estoque.QuantidadeAtual >= insumo.EstoqueMaximo,
                $"Estoque acima do máximo para {insumo.Nome} (atual: {estoque.QuantidadeAtual:N2}; máximo: {insumo.EstoqueMaximo:N2}).",
                alertasAtivos);
        }
        else
        {
            EncerrarAlertasAtivos(alertasAtivos, TipoAlerta.EstoqueMinimo);
            EncerrarAlertasAtivos(alertasAtivos, TipoAlerta.EstoqueMaximo);
        }

        var diasAntecedencia = Math.Max(1, _options.AntecedenciaValidadeDias);
        var limiteValidade = DateTime.UtcNow.AddDays(diasAntecedencia);
        var possuiValidadeProxima = estoque.DataValidade.HasValue && estoque.DataValidade.Value <= limiteValidade;

        SincronizarAlerta(
            estoque,
            TipoAlerta.Validade,
            possuiValidadeProxima,
            $"Item com validade próxima para o lote {estoque.Lote ?? "sem lote"} (validade: {estoque.DataValidade:dd/MM/yyyy}).",
            alertasAtivos);
    }

    private void SincronizarAlerta(
        Estoque estoque,
        TipoAlerta tipoAlerta,
        bool condicaoAtiva,
        string mensagem,
        List<AlertaEstoque> alertasAtivos)
    {
        var alertaAtivo = alertasAtivos
            .Where(x => x.TipoAlerta == tipoAlerta)
            .OrderByDescending(x => x.CriadoEm)
            .FirstOrDefault();

        if (condicaoAtiva)
        {
            if (alertaAtivo is null)
            {
                var novoAlerta = new AlertaEstoque
                {
                    EstoqueId = estoque.Id,
                    TipoAlerta = tipoAlerta,
                    Mensagem = mensagem,
                    Lido = false
                };

                _context.AlertasEstoque.Add(novoAlerta);
                alertasAtivos.Add(novoAlerta);
            }
            else if (!string.Equals(alertaAtivo.Mensagem, mensagem, StringComparison.Ordinal))
            {
                alertaAtivo.Mensagem = mensagem;
            }

            return;
        }

        EncerrarAlertasAtivos(alertasAtivos, tipoAlerta);
    }

    private static void EncerrarAlertasAtivos(List<AlertaEstoque> alertasAtivos, TipoAlerta tipoAlerta)
    {
        foreach (var alerta in alertasAtivos.Where(x => x.TipoAlerta == tipoAlerta && !x.Lido))
        {
            alerta.Lido = true;
        }
    }
}
