using System.ComponentModel.DataAnnotations;

namespace gestao_producao.Models;

public class Produto : EntidadeBase
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    [Required, MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string UnidadeMedida { get; set; } = "un";

    public decimal PrecoVenda { get; set; }
    public bool Ativo { get; set; } = true;

    public ICollection<ProdutoInsumo> Insumos { get; set; } = new List<ProdutoInsumo>();
    public ICollection<Estoque> Estoques { get; set; } = new List<Estoque>();
    public ICollection<OrdemProducao> OrdensProducao { get; set; } = new List<OrdemProducao>();
}
