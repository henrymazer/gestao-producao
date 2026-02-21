using System.ComponentModel.DataAnnotations;

namespace gestao_producao.Models;

public class Funcionario : EntidadeBase
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Cargo { get; set; }

    [MaxLength(120)]
    public string? Setor { get; set; }

    public bool Disponivel { get; set; } = true;
    public bool Ativo { get; set; } = true;
}
