using gestao_producao.Models.Enums;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;

namespace gestao_producao.Pages.Estoque;

public class MovimentacoesModel : BasePageModel
{
    private readonly EstoqueService _estoqueService;

    public MovimentacoesModel(EstoqueService estoqueService)
    {
        _estoqueService = estoqueService;
    }

    [BindProperty(SupportsGet = true)]
    public TipoMovimentacao? TipoMovimentacao { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Inicio { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Fim { get; set; }

    public List<MovimentacaoResumo> Movimentacoes { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Movimentacoes = await _estoqueService.ListarMovimentacoesAsync(TipoMovimentacao, Inicio, Fim, cancellationToken);
    }
}
