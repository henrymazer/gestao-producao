using System.ComponentModel.DataAnnotations;
using gestao_producao.Models.Enums;

namespace gestao_producao.Models;

public class MovimentacaoEstoque : EntidadeBase
{
    public int Id { get; set; }
    public int EstoqueId { get; set; }
    public Estoque? Estoque { get; set; }

    public TipoMovimentacao TipoMovimentacao { get; set; }
    public decimal Quantidade { get; set; }

    [MaxLength(300)]
    public string Motivo { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? DocumentoReferencia { get; set; }

    [MaxLength(64)]
    public string? UsuarioId { get; set; }

    public DateTime DataMovimentacao { get; set; } = DateTime.UtcNow;
}
