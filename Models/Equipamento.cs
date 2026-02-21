using System.ComponentModel.DataAnnotations;
using gestao_producao.Models.Enums;

namespace gestao_producao.Models;

public class Equipamento : EntidadeBase
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Nome { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Descricao { get; set; }

    public decimal CapacidadePorHora { get; set; }
    public StatusEquipamento Status { get; set; } = StatusEquipamento.Disponivel;
    public bool Ativo { get; set; } = true;

    public ICollection<OrdemProducao> OrdensProducao { get; set; } = new List<OrdemProducao>();
}
