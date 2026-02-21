using System.ComponentModel.DataAnnotations;
using gestao_producao.Models.Enums;

namespace gestao_producao.Models;

public class OrdemProducao : EntidadeBase
{
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Codigo { get; set; } = string.Empty;

    public int ProdutoId { get; set; }
    public Produto? Produto { get; set; }

    public decimal QuantidadePlanejada { get; set; }
    public decimal QuantidadeProduzida { get; set; }

    public StatusOrdemProducao Status { get; set; } = StatusOrdemProducao.Planejada;

    public DateTime DataInicioPrevista { get; set; }
    public DateTime DataFimPrevista { get; set; }
    public DateTime? DataInicioReal { get; set; }
    public DateTime? DataFimReal { get; set; }

    public int? EquipamentoId { get; set; }
    public Equipamento? Equipamento { get; set; }

    [MaxLength(500)]
    public string? Observacoes { get; set; }

    public ICollection<PlanoProducaoItem> PlanosProducao { get; set; } = new List<PlanoProducaoItem>();
}
