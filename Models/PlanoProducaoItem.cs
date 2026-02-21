namespace gestao_producao.Models;

public class PlanoProducaoItem : EntidadeBase
{
    public int Id { get; set; }

    public int PlanoProducaoId { get; set; }
    public PlanoProducao? PlanoProducao { get; set; }

    public int OrdemProducaoId { get; set; }
    public OrdemProducao? OrdemProducao { get; set; }

    public int Prioridade { get; set; }
}
