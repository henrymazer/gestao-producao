using gestao_producao.Models.Enums;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;

namespace gestao_producao.Pages.Estoque;

public class IndexModel : BasePageModel
{
    private readonly EstoqueService _estoqueService;
    private readonly AlertaService _alertaService;

    public IndexModel(EstoqueService estoqueService, AlertaService alertaService)
    {
        _estoqueService = estoqueService;
        _alertaService = alertaService;
    }

    [BindProperty(SupportsGet = true)]
    public string? Filtro { get; set; }

    [BindProperty(SupportsGet = true)]
    public TipoItem? TipoItem { get; set; }

    public List<EstoqueResumo> Estoques { get; private set; } = new();
    public List<AlertaResumo> AlertasAtivos { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await _alertaService.AtualizarAlertasAsync(cancellationToken);
        Estoques = await _estoqueService.ListarEstoqueAsync(Filtro, TipoItem, cancellationToken);
        AlertasAtivos = await _estoqueService.ListarAlertasAtivosAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostMarcarAlertaLidoAsync(int id, CancellationToken cancellationToken)
    {
        await _estoqueService.MarcarAlertaComoLidoAsync(id, cancellationToken);
        MensagemSucesso = "Alerta marcado como lido.";
        return RedirectToPage(new { Filtro, TipoItem });
    }
}
