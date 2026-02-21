namespace gestao_producao.Services;

public class AlertaEstoqueOptions
{
    public const string SectionName = "AlertasEstoque";
    public int AntecedenciaValidadeDias { get; set; } = 30;
    public int IntervaloAtualizacaoSegundos { get; set; } = 900;
}
