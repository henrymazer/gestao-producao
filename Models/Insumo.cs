using System.ComponentModel.DataAnnotations;

namespace gestao_producao.Models;

public class Insumo : EntidadeBase
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Required, MaxLength(30)]
    public string UnidadeMedida { get; set; } = "un";

    public int FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    public decimal EstoqueMinimo { get; set; }
    public decimal EstoqueMaximo { get; set; }
    public DateTime? DataValidade { get; set; }
    public decimal PrecoUnitario { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<ProdutoInsumo> Produtos { get; set; } = new List<ProdutoInsumo>();
    public ICollection<Estoque> Estoques { get; set; } = new List<Estoque>();
}
