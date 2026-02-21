namespace gestao_producao.Models;

public class HistoricoAuditoria : EntidadeBase
{
    public int Id { get; set; }
    public string Entidade { get; set; } = string.Empty;
    public string EntidadeId { get; set; } = string.Empty;
    public string Acao { get; set; } = string.Empty;
    public string? ValoresAnteriores { get; set; }
    public string? ValoresNovos { get; set; }
    public string? UsuarioId { get; set; }
}
