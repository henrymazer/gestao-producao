using System.ComponentModel.DataAnnotations;
using gestao_producao.Models.Enums;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;

namespace gestao_producao.Pages.Producao.Planos;

public class FormModel : BasePageModel
{
    private readonly ProducaoService _producaoService;

    public FormModel(ProducaoService producaoService)
    {
        _producaoService = producaoService;
    }

    [BindProperty]
    public PlanoInput Plano { get; set; } = new();

    [BindProperty]
    public List<OrdemVinculoInput> Ordens { get; set; } = new();

    public bool EhEdicao => Plano.Id > 0;

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (id.HasValue)
        {
            var detalhe = await _producaoService.ObterPlanoAsync(id.Value, cancellationToken);
            if (detalhe is null)
            {
                return NotFound();
            }

            Plano = new PlanoInput
            {
                Id = detalhe.Id,
                Nome = detalhe.Nome,
                Descricao = detalhe.Descricao,
                DataInicio = detalhe.DataInicio,
                DataFim = detalhe.DataFim,
                Status = detalhe.Status
            };
        }

        await CarregarOrdensAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Plano.DataFim < Plano.DataInicio)
        {
            ModelState.AddModelError("Plano.DataFim", "A data de fim deve ser maior ou igual à data de início.");
        }

        foreach (var ordem in Ordens.Where(x => x.Selecionada && x.Prioridade <= 0))
        {
            ModelState.AddModelError(string.Empty, $"Prioridade inválida para a ordem {ordem.Codigo}. Use valor maior que zero.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new SalvarPlanoRequest(
            Plano.Id,
            Plano.Nome,
            Plano.Descricao,
            Plano.DataInicio,
            Plano.DataFim,
            Plano.Status,
            Ordens
                .Where(x => x.Selecionada)
                .Select(x => new SalvarPlanoItemRequest(x.OrdemId, x.Prioridade))
                .ToList());

        var resultado = await _producaoService.SalvarPlanoAsync(request, cancellationToken);
        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro ?? "Não foi possível salvar o plano.");
            return Page();
        }

        MensagemSucesso = Plano.Id == 0
            ? "Plano de produção cadastrado com sucesso."
            : "Plano de produção atualizado com sucesso.";

        return RedirectToPage("/Producao/Planos/Index");
    }

    private async Task CarregarOrdensAsync(CancellationToken cancellationToken)
    {
        var ordensDisponiveis = await _producaoService.ListarOrdensAsync(cancellationToken);

        var vinculadas = await _producaoService.ObterPlanoAsync(Plano.Id, cancellationToken);
        var prioridadePorOrdem = vinculadas?.Ordens.ToDictionary(x => x.OrdemProducaoId, x => x.Prioridade)
            ?? new Dictionary<int, int>();

        var statusValidos = new[] { StatusOrdemProducao.Planejada, StatusOrdemProducao.EmAndamento };

        Ordens = ordensDisponiveis
            .Where(x => statusValidos.Contains(x.Status) || prioridadePorOrdem.ContainsKey(x.Id))
            .Select(x => new OrdemVinculoInput
            {
                OrdemId = x.Id,
                Codigo = x.Codigo,
                ProdutoNome = x.ProdutoNome,
                Status = x.Status,
                Selecionada = prioridadePorOrdem.ContainsKey(x.Id),
                Prioridade = prioridadePorOrdem.TryGetValue(x.Id, out var prioridade) ? prioridade : 1
            })
            .OrderBy(x => x.Prioridade)
            .ThenBy(x => x.Codigo)
            .ToList();
    }

    public class PlanoInput
    {
        public int Id { get; set; }

        [Required, MaxLength(150)]
        public string Nome { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Descricao { get; set; }

        [DataType(DataType.Date)]
        public DateTime DataInicio { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime DataFim { get; set; } = DateTime.Today.AddDays(7);

        public StatusPlano Status { get; set; } = StatusPlano.Rascunho;
    }

    public class OrdemVinculoInput
    {
        public int OrdemId { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string ProdutoNome { get; set; } = string.Empty;
        public StatusOrdemProducao Status { get; set; }
        public bool Selecionada { get; set; }
        public int Prioridade { get; set; } = 1;
    }
}
