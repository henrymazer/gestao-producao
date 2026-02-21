namespace gestao_producao.Models;

public class ProdutoInsumo : EntidadeBase
{
    public int Id { get; set; }
    public int ProdutoId { get; set; }
    public Produto? Produto { get; set; }
    public int InsumoId { get; set; }
    public Insumo? Insumo { get; set; }
    public decimal QuantidadeNecessaria { get; set; }
}
