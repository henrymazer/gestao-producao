using System.ComponentModel.DataAnnotations;
using gestao_producao.Models.Enums;

namespace gestao_producao.Models;

public class Estoque : EntidadeBase
{
    public int Id { get; set; }

    public int? InsumoId { get; set; }
    public Insumo? Insumo { get; set; }

    public int? ProdutoId { get; set; }
    public Produto? Produto { get; set; }

    public TipoItem TipoItem { get; set; }
    public decimal QuantidadeAtual { get; set; }

    [MaxLength(80)]
    public string? Lote { get; set; }

    public DateTime? DataValidade { get; set; }

    [MaxLength(120)]
    public string? Localizacao { get; set; }

    public ICollection<MovimentacaoEstoque> Movimentacoes { get; set; } = new List<MovimentacaoEstoque>();
    public ICollection<AlertaEstoque> Alertas { get; set; } = new List<AlertaEstoque>();
}
