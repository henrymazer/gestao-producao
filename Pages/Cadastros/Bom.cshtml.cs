using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Cadastros;

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

    public async Task OnGetAsync(int? idEdicao)
    {
        await CarregarDadosTelaAsync();

        if (idEdicao.HasValue)
        {
            var item = await _context.ProdutoInsumos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == idEdicao.Value);

            if (item is not null)
            {
                ProdutoInsumo = item;
            }
        }
    }

    public async Task<IActionResult> OnPostSalvarAsync()
    {
        await CarregarDadosTelaAsync();

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

            itemDb.ProdutoId = ProdutoInsumo.ProdutoId;
            itemDb.InsumoId = ProdutoInsumo.InsumoId;
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

        _context.ProdutoInsumos.Remove(item);
        await _context.SaveChangesAsync();

        TempData["MensagemSucesso"] = "Composição removida com sucesso.";
        return RedirectToPage();
    }

    private async Task CarregarDadosTelaAsync()
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
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync();

        InsumosOptions = await _context.Insumos
            .AsNoTracking()
            .Where(x => x.Ativo)
            .OrderBy(x => x.Nome)
            .Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Nome
            })
            .ToListAsync();
    }
}
