using System.ComponentModel.DataAnnotations;
using gestao_producao.Models.Enums;

namespace gestao_producao.Models;

public class PlanoProducao : EntidadeBase
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public StatusPlano Status { get; set; } = StatusPlano.Rascunho;

    public ICollection<PlanoProducaoItem> Itens { get; set; } = new List<PlanoProducaoItem>();
}
