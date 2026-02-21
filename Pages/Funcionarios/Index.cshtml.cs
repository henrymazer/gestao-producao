using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Pages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Funcionarios;

public class IndexModel : BasePageModel
{
    private readonly AppDbContext _context;

    public IndexModel(AppDbContext context)
    {
        _context = context;
    }

    public IList<Funcionario> Funcionarios { get; private set; } = new List<Funcionario>();

    public async Task OnGetAsync()
    {
        Funcionarios = await _context.Funcionarios
            .AsNoTracking()
            .OrderBy(x => x.Nome)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var funcionario = await _context.Funcionarios.FindAsync(id);
        if (funcionario is null)
        {
            return NotFound();
        }

        _context.Funcionarios.Remove(funcionario);
        await _context.SaveChangesAsync();

        MensagemSucesso = "Funcionário excluído com sucesso.";
        return RedirectToPage();
    }
}
