using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace gestao_producao.Pages.Funcionarios;

public class FormModel : PageModel
{
    private readonly AppDbContext _context;

    public FormModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Funcionario Funcionario { get; set; } = new();

    public bool EhEdicao => Funcionario.Id > 0;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!id.HasValue)
        {
            return Page();
        }

        var funcionario = await _context.Funcionarios.FindAsync(id.Value);
        if (funcionario is null)
        {
            return NotFound();
        }

        Funcionario = funcionario;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Funcionario.Id == 0)
        {
            _context.Funcionarios.Add(Funcionario);
            TempData["MensagemSucesso"] = "Funcionário cadastrado com sucesso.";
        }
        else
        {
            var funcionarioDb = await _context.Funcionarios.FindAsync(Funcionario.Id);
            if (funcionarioDb is null)
            {
                return NotFound();
            }

            funcionarioDb.Nome = Funcionario.Nome;
            funcionarioDb.Cargo = Funcionario.Cargo;
            funcionarioDb.Setor = Funcionario.Setor;
            funcionarioDb.Disponivel = Funcionario.Disponivel;
            funcionarioDb.Ativo = Funcionario.Ativo;

            TempData["MensagemSucesso"] = "Funcionário atualizado com sucesso.";
        }

        await _context.SaveChangesAsync();
        return RedirectToPage("/Funcionarios/Index");
    }
}
