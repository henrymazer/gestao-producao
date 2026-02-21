using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Equipamentos;

public class IndexModel : BasePageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Equipamento> Equipamentos { get; private set; } = new List<Equipamento>();

    public async Task OnGetAsync()
    {
        Equipamentos = await _context.Equipamentos
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var equipamento = await _context.Equipamentos.FindAsync(id);
        if (equipamento is null)
        {
            return NotFound();
        }

        var possuiOrdens = await _context.OrdensProducao.AnyAsync(x => x.EquipamentoId == id);
        if (possuiOrdens)
        {
            TempData["MensagemErro"] = "Equipamento não pode ser excluído porque possui ordens vinculadas.";
            return RedirectToPage();
        }

        _context.Equipamentos.Remove(equipamento);
        await _context.SaveChangesAsync();

        TempData["MensagemSucesso"] = "Equipamento excluído com sucesso.";
        return RedirectToPage();
    }
}
