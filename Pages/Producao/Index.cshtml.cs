using gestao_producao.Models.Enums;
using gestao_producao.Services;

namespace gestao_producao.Pages.Producao;

public class IndexModel : BasePageModel
{
    private readonly ProducaoService _producaoService;

    public IndexModel(ProducaoService producaoService)
    {
        _producaoService = producaoService;
    }

    public List<PlanoResumo> Planos { get; private set; } = new();
    public List<OrdemResumo> Ordens { get; private set; } = new();
    public List<TimelineItem> Timeline { get; private set; } = new();
    public List<NecessidadeInsumoResumo> NecessidadesInsumos { get; private set; } = new();

    public int TotalPlanosAtivos => Planos.Count(x => x.Status == StatusPlano.Ativo);
    public int TotalOrdensEmAndamento => Ordens.Count(x => x.Status == StatusOrdemProducao.EmAndamento);
    public int TotalOrdensPlanejadas => Ordens.Count(x => x.Status == StatusOrdemProducao.Planejada);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Planos = await _producaoService.ListarPlanosAsync(cancellationToken);
        Ordens = await _producaoService.ListarOrdensAsync(cancellationToken);
        NecessidadesInsumos = await _producaoService.CalcularNecessidadeInsumosPorPlanosAtivosAsync(cancellationToken);

        var inicio = DateTime.Today.AddDays(-7);
        var fim = DateTime.Today.AddDays(30);
        Timeline = await _producaoService.ListarTimelineAsync(inicio, fim, cancellationToken);
    }
}
