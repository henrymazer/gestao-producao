using gestao_producao.Data;
using gestao_producao.Models;
using gestao_producao.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace gestao_producao.Pages.Equipamentos;

public class FormModel : PageModel
{
    private readonly AppDbContext _context;

    public FormModel(AppDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public Equipamento Equipamento { get; set; } = new();

    public bool EhEdicao => Equipamento.Id > 0;

    public List<SelectListItem> StatusOptions { get; private set; } = new();

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        CarregarStatusOptions();

        if (!id.HasValue)
        {
            return Page();
        }

        var equipamento = await _context.Equipamentos.FindAsync(id.Value);
        if (equipamento is null)
        {
            return NotFound();
        }

        Equipamento = equipamento;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        CarregarStatusOptions();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Equipamento.Id == 0)
        {
            _context.Equipamentos.Add(Equipamento);
            TempData["MensagemSucesso"] = "Equipamento cadastrado com sucesso.";
        }
        else
        {
            var equipamentoDb = await _context.Equipamentos.FindAsync(Equipamento.Id);
            if (equipamentoDb is null)
            {
                return NotFound();
            }

            equipamentoDb.Nome = Equipamento.Nome;
            equipamentoDb.Descricao = Equipamento.Descricao;
            equipamentoDb.CapacidadePorHora = Equipamento.CapacidadePorHora;
            equipamentoDb.Status = Equipamento.Status;
            equipamentoDb.Ativo = Equipamento.Ativo;

            TempData["MensagemSucesso"] = "Equipamento atualizado com sucesso.";
        }

        await _context.SaveChangesAsync();
        return RedirectToPage("/Equipamentos/Index");
    }

    private void CarregarStatusOptions()
    {
        StatusOptions = Enum.GetValues<StatusEquipamento>()
            .Select(x => new SelectListItem
            {
                Value = ((int)x).ToString(),
                Text = x.ToString()
            })
            .ToList();
    }
}
