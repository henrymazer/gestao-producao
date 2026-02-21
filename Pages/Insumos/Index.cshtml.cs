using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Insumos;

public class IndexModel : PageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Insumo> Insumos { get; private set; } = new List<Insumo>();

    [TempData]
    public string? MensagemSucesso { get; set; }

    [TempData]
    public string? MensagemErro { get; set; }

    public async Task OnGetAsync()
    {
        Insumos = await _context.Insumos
            .AsNoTracking()
            .Include(x => x.Fornecedor)
            .OrderBy(x => x.Nome)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var insumo = await _context.Insumos.FindAsync(id);
        if (insumo is null)
        {
            return NotFound();
        }

        var possuiDependencias = await _context.ProdutoInsumos.AnyAsync(x => x.InsumoId == id)
            || await _context.Estoques.AnyAsync(x => x.InsumoId == id);

        if (possuiDependencias)
        {
            TempData["MensagemErro"] = "Insumo não pode ser excluído porque possui vínculos no sistema.";
            return RedirectToPage();
        }

        _context.Insumos.Remove(insumo);
        await _context.SaveChangesAsync();

        TempData["MensagemSucesso"] = "Insumo excluído com sucesso.";
        return RedirectToPage();
    }
}
