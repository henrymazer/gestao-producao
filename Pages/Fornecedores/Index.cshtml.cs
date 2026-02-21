using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Fornecedores;

public class IndexModel : BasePageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Fornecedor> Fornecedores { get; private set; } = new List<Fornecedor>();

    public async Task OnGetAsync()
    {
        Fornecedores = await _context.Fornecedores
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(id);
        if (fornecedor is null)
        {
            return NotFound();
        }

        var possuiInsumos = await _context.Insumos.AnyAsync(x => x.FornecedorId == id);
        if (possuiInsumos)
        {
            TempData["MensagemErro"] = "Fornecedor não pode ser excluído porque possui insumos vinculados.";
            return RedirectToPage();
        }

        _context.Fornecedores.Remove(fornecedor);
        await _context.SaveChangesAsync();

        TempData["MensagemSucesso"] = "Fornecedor excluído com sucesso.";
        return RedirectToPage();
    }
}
