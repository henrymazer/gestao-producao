using System.ComponentModel.DataAnnotations;
using gestao_producao.Data;
using gestao_producao.Models.Enums;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Estoque;

public class MovimentarModel : BasePageModel
{
    private readonly AppDbContext _context;
    private readonly EstoqueService _estoqueService;

    public MovimentarModel(AppDbContext context, EstoqueService estoqueService)
    {
        _context = context;
        _estoqueService = estoqueService;
    }

    [BindProperty]
    public MovimentacaoInput Input { get; set; } = new();

    public List<SelectListItem> EstoquesOptions { get; private set; } = new();
    public List<SelectListItem> InsumosOptions { get; private set; } = new();
    public List<SelectListItem> ProdutosOptions { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await CarregarOptionsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        await CarregarOptionsAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.TipoMovimentacao == TipoMovimentacao.Entrada)
        {
            if (!Input.TipoItem.HasValue)
            {
                ModelState.AddModelError("Input.TipoItem", "Selecione o tipo do item para entrada.");
            }

            if (Input.TipoItem == Models.Enums.TipoItem.MateriaPrima && !Input.InsumoId.HasValue)
            {
                ModelState.AddModelError("Input.InsumoId", "Selecione um insumo para entrada.");
            }

            if (Input.TipoItem != Models.Enums.TipoItem.MateriaPrima && !Input.ProdutoId.HasValue)
            {
                ModelState.AddModelError("Input.ProdutoId", "Selecione um produto para entrada.");
            }
        }
        else if (!Input.EstoqueId.HasValue)
        {
            ModelState.AddModelError("Input.EstoqueId", "Selecione um item de estoque para movimentar.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var request = new MovimentacaoRequest
        {
            EstoqueId = Input.EstoqueId,
            TipoMovimentacao = Input.TipoMovimentacao,
            TipoAjuste = Input.TipoAjuste,
            TipoItem = Input.TipoItem,
            InsumoId = Input.InsumoId,
            ProdutoId = Input.ProdutoId,
            Quantidade = Input.Quantidade,
            Motivo = Input.Motivo,
            DocumentoReferencia = Input.DocumentoReferencia,
            Lote = Input.Lote,
            DataValidade = Input.DataValidade,
            Localizacao = Input.Localizacao
        };

        var resultado = await _estoqueService.RegistrarMovimentacaoAsync(request, User, cancellationToken);
        if (!resultado.Sucesso)
        {
            MensagemErro = resultado.Erro;
            return Page();
        }

        MensagemSucesso = "Movimentação registrada com sucesso.";
        return RedirectToPage("/Estoque/Index");
    }

    private async Task CarregarOptionsAsync(CancellationToken cancellationToken)
    {
        EstoquesOptions = await _context.Estoques
            .AsNoTracking()
            .Include(x => x.Insumo)
            .Include(x => x.Produto)
            .OrderBy(x => x.TipoItem)
            .ThenBy(x => x.Insumo != null ? x.Insumo.Nome : x.Produto!.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = $"{(x.Insumo != null ? x.Insumo.Nome : x.Produto!.Nome)} | Saldo: {x.QuantidadeAtual:N2} | Lote: {(string.IsNullOrWhiteSpace(x.Lote) ? "-" : x.Lote)}"
            })
            .ToListAsync(cancellationToken);

        InsumosOptions = await _context.Insumos
            .AsNoTracking()
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync(cancellationToken);

        ProdutosOptions = await _context.Produtos
            .AsNoTracking()
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync(cancellationToken);
    }

    public class MovimentacaoInput
    {
        [Required]
        public TipoMovimentacao TipoMovimentacao { get; set; } = TipoMovimentacao.Entrada;

        public int? EstoqueId { get; set; }

        public Models.Enums.TipoItem? TipoItem { get; set; }

        public int? InsumoId { get; set; }

        public int? ProdutoId { get; set; }

        [Range(typeof(decimal), "0.0001", "999999999")]
        public decimal Quantidade { get; set; }

        public TipoAjusteEstoque TipoAjuste { get; set; } = TipoAjusteEstoque.Somar;

        [Required, MaxLength(300)]
        public string Motivo { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? DocumentoReferencia { get; set; }

        [MaxLength(80)]
        public string? Lote { get; set; }

        public DateTime? DataValidade { get; set; }

        [MaxLength(120)]
        public string? Localizacao { get; set; }
    }
}
