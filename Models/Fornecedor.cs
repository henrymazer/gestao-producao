using System.ComponentModel.DataAnnotations;

namespace gestao_producao.Models;

public class Fornecedor : EntidadeBase
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(18)]
    public string Cnpj { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Contato { get; set; }

    [MaxLength(300)]
    public string? Endereco { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<Insumo> Insumos { get; set; } = new List<Insumo>();
}
