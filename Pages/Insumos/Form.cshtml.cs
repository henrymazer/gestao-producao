using gestao_producao.Data;
using gestao_producao.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace gestao_producao.Pages.Insumos;

public class FormModel : BasePageModel
{
    private readonly AppDbContext _context;

    public FormModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Insumo Insumo { get; set; } = new();

    public bool EhEdicao => Insumo.Id > 0;

    public List<SelectListItem> FornecedoresOptions { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        await CarregarFornecedoresOptionsAsync();

        if (!id.HasValue)
        {
            return Page();
        }

        var insumo = await _context.Insumos.FindAsync(id.Value);
        if (insumo is null)
        {
            return NotFound();
        }

        Insumo = insumo;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await CarregarFornecedoresOptionsAsync();

        if (Insumo.EstoqueMaximo < Insumo.EstoqueMinimo)
        {
            ModelState.AddModelError("Insumo.EstoqueMaximo", "Estoque máximo deve ser maior ou igual ao estoque mínimo.");
        }

        var fornecedorExiste = await _context.Fornecedores
            .AsNoTracking()
            .AnyAsync(x => x.Id == Insumo.FornecedorId);

        if (!fornecedorExiste)
        {
            ModelState.AddModelError("Insumo.FornecedorId", "Selecione um fornecedor válido.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Insumo.Id == 0)
        {
            _context.Insumos.Add(Insumo);
            MensagemSucesso = "Insumo cadastrado com sucesso.";
        }
        else
        {
            var insumoDb = await _context.Insumos.FindAsync(Insumo.Id);
            if (insumoDb is null)
            {
                return NotFound();
            }

            insumoDb.Nome = Insumo.Nome;
            insumoDb.Descricao = Insumo.Descricao;
            insumoDb.UnidadeMedida = Insumo.UnidadeMedida;
            insumoDb.FornecedorId = Insumo.FornecedorId;
            insumoDb.EstoqueMinimo = Insumo.EstoqueMinimo;
            insumoDb.EstoqueMaximo = Insumo.EstoqueMaximo;
            insumoDb.DataValidade = Insumo.DataValidade;
            insumoDb.PrecoUnitario = Insumo.PrecoUnitario;
            insumoDb.Ativo = Insumo.Ativo;

            MensagemSucesso = "Insumo atualizado com sucesso.";
        }

        await _context.SaveChangesAsync();
        return RedirectToPage("/Insumos/Index");
    }

    private async Task CarregarFornecedoresOptionsAsync()
    {
        FornecedoresOptions = await _context.Fornecedores
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
