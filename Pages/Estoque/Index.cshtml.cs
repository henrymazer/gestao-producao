using gestao_producao.Models.Enums;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;

namespace gestao_producao.Pages.Estoque;

public class IndexModel : BasePageModel
{
    private readonly EstoqueService _estoqueService;

    public IndexModel(EstoqueService estoqueService)
    {
        _estoqueService = estoqueService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Filtro { get; set; }

    [BindProperty(SupportsGet = true)]
    public TipoItem? TipoItem { get; set; }

    public List<EstoqueResumo> Estoques { get; private set; } = new();
    public List<AlertaResumo> AlertasAtivos { get; private set; } = new();
    public List<SugestaoReabastecimentoResumo> SugestoesReabastecimento { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Estoques = await _estoqueService.ListarEstoqueAsync(Filtro, TipoItem, cancellationToken);
        AlertasAtivos = await _estoqueService.ListarAlertasAtivosAsync(cancellationToken);
        SugestoesReabastecimento = await _estoqueService.ListarSugestoesReabastecimentoAsync(cancellationToken: cancellationToken);
    }

    public async Task<IActionResult> OnPostMarcarAlertaLidoAsync(int id, CancellationToken cancellationToken)
    {
        await _estoqueService.MarcarAlertaComoLidoAsync(id, cancellationToken);
        MensagemSucesso = "Alerta marcado como lido.";
        return RedirectToPage(new { Filtro, TipoItem });
    }
}
