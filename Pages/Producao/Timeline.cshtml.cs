using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;

namespace gestao_producao.Pages.Producao;

public class TimelineModel : BasePageModel
{
    private readonly ProducaoService _producaoService;

    public TimelineModel(ProducaoService producaoService)
    {
        _producaoService = producaoService;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime Inicio { get; set; } = DateTime.Today.AddDays(-7);

    [BindProperty(SupportsGet = true)]
    public DateTime Fim { get; set; } = DateTime.Today.AddDays(30);

    public List<TimelineItem> Itens { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (Fim < Inicio)
        {
            var aux = Inicio;
            Inicio = Fim;
            Fim = aux;
        }

        Itens = await _producaoService.ListarTimelineAsync(Inicio, Fim, cancellationToken);
    }
}
