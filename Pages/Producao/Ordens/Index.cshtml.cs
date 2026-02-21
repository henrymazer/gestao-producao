using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;

namespace gestao_producao.Pages.Producao.Ordens;

public class IndexModel : BasePageModel
{
    private readonly ProducaoService _producaoService;

    public IndexModel(ProducaoService producaoService)
    {
        _producaoService = producaoService;
    }

    public List<OrdemResumo> Ordens { get; private set; } = new();
    public VerificacaoDisponibilidadeResultado? Disponibilidade { get; private set; }
    public int? OrdemDisponibilidadeId { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Ordens = await _producaoService.ListarOrdensAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        var resultado = await _producaoService.ExcluirOrdemAsync(id, cancellationToken);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
            return RedirectToPage();
        }

        MensagemSucesso = "Ordem excluída com sucesso.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostIniciarAsync(int id, CancellationToken cancellationToken)
    {
        var resultado = await _producaoService.IniciarOrdemAsync(id, cancellationToken);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
            return RedirectToPage();
        }

        MensagemSucesso = "Ordem iniciada com sucesso.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConcluirAsync(int id, CancellationToken cancellationToken)
    {
        var resultado = await _producaoService.ConcluirOrdemAsync(id, null, User, cancellationToken);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
            return RedirectToPage();
        }

        MensagemSucesso = "Ordem concluída com baixa automática de insumos.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCancelarAsync(int id, CancellationToken cancellationToken)
    {
        var resultado = await _producaoService.CancelarOrdemAsync(id, cancellationToken);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
            return RedirectToPage();
        }

        MensagemSucesso = "Ordem cancelada com sucesso.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostVerificarDisponibilidadeAsync(int id, CancellationToken cancellationToken)
    {
        Ordens = await _producaoService.ListarOrdensAsync(cancellationToken);
        Disponibilidade = await _producaoService.VerificarDisponibilidadeAsync(id, null, cancellationToken);
        OrdemDisponibilidadeId = id;

        if (Disponibilidade.Disponivel)
        {
            MensagemSucesso = "Há insumos suficientes para executar esta ordem.";
        }
        else
        {
            MensagemErro = "Há falta de insumos para executar esta ordem.";
        }

        return Page();
    }
}
