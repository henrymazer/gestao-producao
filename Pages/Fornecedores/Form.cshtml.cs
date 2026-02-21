using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Fornecedores;

public class FormModel : PageModel
{
    private readonly AppDbContext _context;

    public FormModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Fornecedor Fornecedor { get; set; } = new();

    public bool EhEdicao => Fornecedor.Id > 0;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!id.HasValue)
        {
            return Page();
        }

        var fornecedor = await _context.Fornecedores.FindAsync(id.Value);
        if (fornecedor is null)
        {
            return NotFound();
        }

        Fornecedor = fornecedor;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var cnpjExistente = await _context.Fornecedores
            .AsNoTracking()
            .AnyAsync(x => x.Cnpj == Fornecedor.Cnpj && x.Id != Fornecedor.Id);

        if (cnpjExistente)
        {
            ModelState.AddModelError("Fornecedor.Cnpj", "Já existe um fornecedor com este CNPJ.");
            return Page();
        }

        if (Fornecedor.Id == 0)
        {
            _context.Fornecedores.Add(Fornecedor);
            TempData["MensagemSucesso"] = "Fornecedor cadastrado com sucesso.";
        }
        else
        {
            var fornecedorDb = await _context.Fornecedores.FindAsync(Fornecedor.Id);
            if (fornecedorDb is null)
            {
                return NotFound();
            }

            fornecedorDb.Nome = Fornecedor.Nome;
            fornecedorDb.Cnpj = Fornecedor.Cnpj;
            fornecedorDb.Contato = Fornecedor.Contato;
            fornecedorDb.Endereco = Fornecedor.Endereco;
            fornecedorDb.Ativo = Fornecedor.Ativo;

            TempData["MensagemSucesso"] = "Fornecedor atualizado com sucesso.";
        }

        await _context.SaveChangesAsync();
        return RedirectToPage("/Fornecedores/Index");
    }
}
