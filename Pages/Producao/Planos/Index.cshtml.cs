using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;

namespace gestao_producao.Pages.Producao.Planos;

public class IndexModel : BasePageModel
{
    private readonly ProducaoService _producaoService;

    public IndexModel(ProducaoService producaoService)
    {
        _producaoService = producaoService;
    }

    public List<PlanoResumo> Planos { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Planos = await _producaoService.ListarPlanosAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var resultado = await _producaoService.ExcluirPlanoAsync(id, cancellationToken);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
            return RedirectToPage();
        }

        MensagemSucesso = "Plano de produção excluído com sucesso.";
        return RedirectToPage();
    }
}
