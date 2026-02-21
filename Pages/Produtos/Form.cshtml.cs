using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Produtos;

public class FormModel : PageModel
{
    private readonly AppDbContext _context;

    public FormModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Produto Produto { get; set; } = new();

    public bool EhEdicao => Produto.Id > 0;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (!id.HasValue)
        {
            return Page();
        }

        var produto = await _context.Produtos.FindAsync(id.Value);
        if (produto is null)
        {
            return NotFound();
        }

        Produto = produto;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var codigoExistente = await _context.Produtos
            .AsNoTracking()
            .AnyAsync(x => x.Codigo == Produto.Codigo && x.Id != Produto.Id);

        if (codigoExistente)
        {
            ModelState.AddModelError("Produto.Codigo", "Já existe um produto com este código.");
            return Page();
        }

        if (Produto.Id == 0)
        {
            _context.Produtos.Add(Produto);
            TempData["MensagemSucesso"] = "Produto cadastrado com sucesso.";
        }
        else
        {
            var produtoDb = await _context.Produtos.FindAsync(Produto.Id);
            if (produtoDb is null)
            {
                return NotFound();
            }

            produtoDb.Nome = Produto.Nome;
            produtoDb.Codigo = Produto.Codigo;
            produtoDb.UnidadeMedida = Produto.UnidadeMedida;
            produtoDb.PrecoVenda = Produto.PrecoVenda;
            produtoDb.Descricao = Produto.Descricao;
            produtoDb.Ativo = Produto.Ativo;

            TempData["MensagemSucesso"] = "Produto atualizado com sucesso.";
        }

        await _context.SaveChangesAsync();
        return RedirectToPage("/Produtos/Index");
    }
}
