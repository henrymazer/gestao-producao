using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Cadastros;

[Authorize]
public class BomModel : PageModel
{
    private readonly AppDbContext _context;

    public BomModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public ProdutoInsumo ProdutoInsumo { get; set; } = new();

    public IList<ProdutoInsumo> Itens { get; private set; } = new List<ProdutoInsumo>();
    public List<SelectListItem> ProdutosOptions { get; private set; } = new();
    public List<SelectListItem> InsumosOptions { get; private set; } = new();

    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    public async Task OnGetAsync(int? idEdicao)
    {
        int? produtoIdSelecionado = null;
        int? insumoIdSelecionado = null;

        if (idEdicao.HasValue)
        {
            var item = await _context.ProdutoInsumos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == idEdicao.Value);

            if (item is not null)
            {
                ProdutoInsumo = item;
                produtoIdSelecionado = item.ProdutoId;
                insumoIdSelecionado = item.InsumoId;
            }
        }

        await CarregarDadosTelaAsync(produtoIdSelecionado, insumoIdSelecionado);
    }

    public async Task<IActionResult> OnPostSalvarAsync()
    {
        await CarregarDadosTelaAsync(ProdutoInsumo.ProdutoId, ProdutoInsumo.InsumoId);

        if (ProdutoInsumo.QuantidadeNecessaria <= 0)
        {
            ModelState.AddModelError("ProdutoInsumo.QuantidadeNecessaria", "A quantidade necessária deve ser maior que zero.");
        }

        var produtoExiste = await _context.Produtos
            .AsNoTracking()
            .AnyAsync(x => x.Id == ProdutoInsumo.ProdutoId && x.Ativo);

        if (!produtoExiste)
        {
            ModelState.AddModelError("ProdutoInsumo.ProdutoId", "Selecione um produto válido.");
        }

        var insumoExiste = await _context.Insumos
            .AsNoTracking()
            .AnyAsync(x => x.Id == ProdutoInsumo.InsumoId && x.Ativo);

        if (!insumoExiste)
        {
            ModelState.AddModelError("ProdutoInsumo.InsumoId", "Selecione um insumo válido.");
        }

        var combinacaoDuplicada = await _context.ProdutoInsumos
            .AsNoTracking()
            .AnyAsync(x => x.ProdutoId == ProdutoInsumo.ProdutoId
                        && x.InsumoId == ProdutoInsumo.InsumoId
                        && x.Id != ProdutoInsumo.Id);

        if (combinacaoDuplicada)
        {
            ModelState.AddModelError(string.Empty, "Já existe uma composição com este produto e insumo.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (ProdutoInsumo.Id == 0)
        {
            _context.ProdutoInsumos.Add(ProdutoInsumo);
            TempData["MensagemSucesso"] = "Composição cadastrada com sucesso.";
        }
        else
        {
            var itemDb = await _context.ProdutoInsumos.FindAsync(ProdutoInsumo.Id);
            if (itemDb is null)
            {
                return NotFound();
            }

            if (itemDb.ProdutoId != ProdutoInsumo.ProdutoId || itemDb.InsumoId != ProdutoInsumo.InsumoId)
            {
                ModelState.AddModelError(string.Empty, "Para trocar produto ou insumo, exclua a composição atual e cadastre uma nova.");
                ProdutoInsumo.ProdutoId = itemDb.ProdutoId;
                ProdutoInsumo.InsumoId = itemDb.InsumoId;
                return Page();
            }

            itemDb.QuantidadeNecessaria = ProdutoInsumo.QuantidadeNecessaria;

            TempData["MensagemSucesso"] = "Composição atualizada com sucesso.";
        }

        await _context.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostExcluirAsync(int id)
    {
        var item = await _context.ProdutoInsumos.FindAsync(id);
        if (item is null)
        {
            return NotFound();
        }

        var possuiOrdensAbertas = await _context.OrdensProducao
            .AsNoTracking()
            .AnyAsync(x => x.ProdutoId == item.ProdutoId
                        && (x.Status == StatusOrdemProducao.Planejada || x.Status == StatusOrdemProducao.EmAndamento));

        if (possuiOrdensAbertas)
        {
            TempData["MensagemErro"] = "Composição não pode ser excluída porque existem ordens de produção planejadas ou em andamento para este produto.";
            return RedirectToPage();
        }

        _context.ProdutoInsumos.Remove(item);
        await _context.SaveChangesAsync();

        TempData["MensagemSucesso"] = "Composição removida com sucesso.";
        return RedirectToPage();
    }

    private async Task CarregarDadosTelaAsync(int? produtoIdSelecionado = null, int? insumoIdSelecionado = null)
    {
        Itens = await _context.ProdutoInsumos
            .AsNoTracking()
            .Include(x => x.Produto)
            .Include(x => x.Insumo)
            .OrderBy(x => x.Produto!.Nome)
            .ThenBy(x => x.Insumo!.Nome)
            .ToListAsync();

        ProdutosOptions = await _context.Produtos
            .AsNoTracking()
            .Where(x => x.Ativo || (produtoIdSelecionado.HasValue && x.Id == produtoIdSelecionado.Value))
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Ativo ? x.Nome : $"{x.Nome} (inativo)"
            })
            .ToListAsync();

        InsumosOptions = await _context.Insumos
            .AsNoTracking()
            .Where(x => x.Ativo || (insumoIdSelecionado.HasValue && x.Id == insumoIdSelecionado.Value))
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Ativo ? x.Nome : $"{x.Nome} (inativo)"
            })
            .ToListAsync();
    }
}
