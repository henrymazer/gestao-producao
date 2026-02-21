using System.ComponentModel.DataAnnotations;
using gestao_producao.Data;
using gestao_producao.Models.Enums;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Producao.Ordens;

public class FormModel : BasePageModel
{
    private readonly AppDbContext _context;
    private readonly ProducaoService _producaoService;

    public FormModel(AppDbContext context, ProducaoService producaoService)
    {
        _context = context;
        _producaoService = producaoService;
    }

    [BindProperty]
    public OrdemInput Ordem { get; set; } = new();

    public List<SelectListItem> ProdutosOptions { get; private set; } = new();
    public List<SelectListItem> EquipamentosOptions { get; private set; } = new();

    public bool EhEdicao => Ordem.Id > 0;

    public async Task<IActionResult> OnGetAsync(int? id, CancellationToken cancellationToken)
    {
        if (id.HasValue)
        {
            var detalhe = await _producaoService.ObterOrdemAsync(id.Value, cancellationToken);
            if (detalhe is null)
            {
                return NotFound();
            }

            Ordem = new OrdemInput
            {
                Id = detalhe.Id,
                Codigo = detalhe.Codigo,
                ProdutoId = detalhe.ProdutoId,
                QuantidadePlanejada = detalhe.QuantidadePlanejada,
                QuantidadeProduzida = detalhe.QuantidadeProduzida,
                Status = detalhe.Status,
                DataInicioPrevista = detalhe.DataInicioPrevista,
                DataFimPrevista = detalhe.DataFimPrevista,
                DataInicioReal = detalhe.DataInicioReal,
                DataFimReal = detalhe.DataFimReal,
                EquipamentoId = detalhe.EquipamentoId,
                Observacoes = detalhe.Observacoes
            };
        }

        await CarregarOptionsAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (Ordem.DataFimPrevista < Ordem.DataInicioPrevista)
        {
            ModelState.AddModelError("Ordem.DataFimPrevista", "A data de fim prevista deve ser maior ou igual à data de início prevista.");
        }

        if (Ordem.DataFimReal.HasValue && Ordem.DataInicioReal.HasValue && Ordem.DataFimReal.Value < Ordem.DataInicioReal.Value)
        {
            ModelState.AddModelError("Ordem.DataFimReal", "A data de fim real deve ser maior ou igual à data de início real.");
        }

        if (Ordem.Status == StatusOrdemProducao.Concluida)
        {
            ModelState.AddModelError("Ordem.Status", "Conclua ordens pela tela de listagem para executar a baixa automática de insumos.");
        }

        if (!ModelState.IsValid)
        {
            await CarregarOptionsAsync(cancellationToken);
            return Page();
        }

        var request = new SalvarOrdemRequest(
            Ordem.Id,
            Ordem.Codigo,
            Ordem.ProdutoId,
            Ordem.QuantidadePlanejada,
            Ordem.QuantidadeProduzida,
            Ordem.Status,
            Ordem.DataInicioPrevista,
            Ordem.DataFimPrevista,
            Ordem.DataInicioReal,
            Ordem.DataFimReal,
            Ordem.EquipamentoId,
            Ordem.Observacoes);

        var resultado = await _producaoService.SalvarOrdemAsync(request, cancellationToken);
        if (!resultado.Sucesso)
        {
            ModelState.AddModelError(string.Empty, resultado.Erro ?? "Não foi possível salvar a ordem.");
            await CarregarOptionsAsync(cancellationToken);
            return Page();
        }

        MensagemSucesso = Ordem.Id == 0
            ? "Ordem de produção cadastrada com sucesso."
            : "Ordem de produção atualizada com sucesso.";

        return RedirectToPage("/Producao/Ordens/Index");
    }

    private async Task CarregarOptionsAsync(CancellationToken cancellationToken)
    {
        ProdutosOptions = await _context.Produtos
            .AsNoTracking()
            .Where(x => x.Ativo || x.Id == Ordem.ProdutoId)
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Ativo ? x.Nome : $"{x.Nome} (inativo)"
            })
            .ToListAsync(cancellationToken);

        EquipamentosOptions = await _context.Equipamentos
            .AsNoTracking()
            .Where(x => x.Ativo || (Ordem.EquipamentoId.HasValue && x.Id == Ordem.EquipamentoId.Value))
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Ativo ? x.Nome : $"{x.Nome} (inativo)"
            })
            .ToListAsync(cancellationToken);

        EquipamentosOptions.Insert(0, new SelectListItem
        {
            Value = string.Empty,
            Text = "(Sem equipamento)"
        });
    }

    public class OrdemInput
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        public int ProdutoId { get; set; }

        [Range(typeof(decimal), "0.0001", "999999999")]
        public decimal QuantidadePlanejada { get; set; }

        [Range(typeof(decimal), "0", "999999999")]
        public decimal QuantidadeProduzida { get; set; }

        public StatusOrdemProducao Status { get; set; } = StatusOrdemProducao.Planejada;

        [DataType(DataType.Date)]
        public DateTime DataInicioPrevista { get; set; } = DateTime.Today;

        [DataType(DataType.Date)]
        public DateTime DataFimPrevista { get; set; } = DateTime.Today.AddDays(1);

        [DataType(DataType.DateTime)]
        public DateTime? DataInicioReal { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? DataFimReal { get; set; }

        public int? EquipamentoId { get; set; }

        [MaxLength(500)]
        public string? Observacoes { get; set; }
    }
}
