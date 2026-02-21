using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Produtos;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Produto> Produtos { get; private set; } = new List<Produto>();

    [TempData]
    public string? MensagemSucesso { get; set; }

    public async Task OnGetAsync()
    {
        Produtos = await _context.Produtos
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var produto = await _context.Produtos.FindAsync(id);
        if (produto is null)
        {
            return NotFound();
        }

        var possuiDependencias = await _context.ProdutoInsumos.AnyAsync(x => x.ProdutoId == id)
            || await _context.OrdensProducao.AnyAsync(x => x.ProdutoId == id)
            || await _context.Estoques.AnyAsync(x => x.ProdutoId == id);

        if (possuiDependencias)
        {
            TempData["MensagemSucesso"] = "Produto não pode ser excluído porque possui vínculos no sistema.";
            return RedirectToPage();
        }

        _context.Produtos.Remove(produto);
        await _context.SaveChangesAsync();

        TempData["MensagemSucesso"] = "Produto excluído com sucesso.";
        return RedirectToPage();
    }
}
