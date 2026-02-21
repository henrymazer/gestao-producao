using gestao_producao.Data;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Insumos;

public class RastreabilidadeModel : BasePageModel
{
    private readonly AppDbContext _context;
    private readonly RastreabilidadeInsumoService _rastreabilidadeService;

    public RastreabilidadeModel(AppDbContext context, RastreabilidadeInsumoService rastreabilidadeService)
    {
        _context = context;
        _rastreabilidadeService = rastreabilidadeService;
    }

    [BindProperty(SupportsGet = true)]
    public int? InsumoId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? OrdemId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Inicio { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Fim { get; set; }

    public string? InsumoSelecionadoNome { get; private set; }
    public List<SelectListItem> InsumosOptions { get; private set; } = new();
    public List<SelectListItem> OrdensOptions { get; private set; } = new();

    public List<EntradaInsumoDetalhe> EntradasDetalhadas { get; private set; } = new();
    public ConsumoOrdemDetalhe? ConsumoPorOrdem { get; private set; }
    public List<HistoricoInsumoMovimentacao> HistoricoInsumo { get; private set; } = new();
    public List<LoteInsumoResumo> Lotes { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await CarregarFiltrosAsync(cancellationToken);

        EntradasDetalhadas = await _rastreabilidadeService
            .ListarEntradasDetalhadasAsync(InsumoId, Inicio, Fim, cancellationToken);

        if (OrdemId.HasValue)
        {
            ConsumoPorOrdem = await _rastreabilidadeService
                .ObterConsumoPorOrdemAsync(OrdemId.Value, cancellationToken);

            if (ConsumoPorOrdem is null)
            {
                MensagemErro = "A ordem selecionada não foi encontrada.";
            }
        }

        if (InsumoId.HasValue)
        {
            HistoricoInsumo = await _rastreabilidadeService
                .ListarHistoricoPorInsumoAsync(InsumoId.Value, Inicio, Fim, cancellationToken);
        }

        Lotes = await _rastreabilidadeService
            .ListarControleLotesAsync(InsumoId, cancellationToken);
    }

    private async Task CarregarFiltrosAsync(CancellationToken cancellationToken)
    {
        var insumos = await _context.Insumos
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .Select(x => new { x.Id, x.Nome })
            .ToListAsync(cancellationToken);

        InsumosOptions = insumos
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToList();

        if (InsumoId.HasValue)
        {
            InsumoSelecionadoNome = insumos
                .FirstOrDefault(x => x.Id == InsumoId.Value)?.Nome;
        }

        OrdensOptions = await _context.OrdensProducao
            .AsNoTracking()
            .Include(x => x.Produto)
            .OrderByDescending(x => x.CriadoEm)
            .Take(200)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{x.Codigo} - {x.Produto!.Nome}"
            })
            .ToListAsync(cancellationToken);
    }
}
