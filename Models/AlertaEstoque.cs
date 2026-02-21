using gestao_producao.Models.Enums;

namespace gestao_producao.Models;

public class AlertaEstoque : EntidadeBase
{
    public int Id { get; set; }
    public int EstoqueId { get; set; }
    public Estoque? Estoque { get; set; }
    public TipoAlerta TipoAlerta { get; set; }
    public string Mensagem { get; set; } = string.Empty;
    public bool Lido { get; set; }
}
