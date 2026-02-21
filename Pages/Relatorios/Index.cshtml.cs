using System.Globalization;
using System.Text;
using System.Text.Json;
using gestao_producao.Services;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace gestao_producao.Pages.Relatorios;

public class IndexModel : BasePageModel
{
    private readonly RelatorioService _relatorioService;

    public IndexModel(RelatorioService relatorioService)
    {
        _relatorioService = relatorioService;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? Inicio { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? Fim { get; set; }

    public RelatoriosResultado? Relatorios { get; private set; }

    public string DashboardLabelsJson { get; private set; } = "[]";
    public string DashboardProducaoJson { get; private set; } = "[]";
    public string DashboardSaidasJson { get; private set; } = "[]";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await CarregarAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostExportCsvAsync(string relatorio, DateTime? inicio, DateTime? fim, CancellationToken cancellationToken)
    {
        var resultado = await _relatorioService.ObterRelatoriosAsync(inicio, fim, cancellationToken);
        var conteudo = GerarCsv(relatorio, resultado);
        var arquivo = $"relatorio-{relatorio}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(conteudo), "text/csv; charset=utf-8", arquivo);
    }

    public async Task<IActionResult> OnPostExportPdfAsync(string relatorio, DateTime? inicio, DateTime? fim, CancellationToken cancellationToken)
    {
        var resultado = await _relatorioService.ObterRelatoriosAsync(inicio, fim, cancellationToken);
        var titulo = ObterTituloRelatorio(relatorio);

        QuestPDF.Settings.License = LicenseType.Community;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Size(PageSizes.A4);

                page.Header()
                    .Text($"{titulo} ({resultado.InicioUtc:dd/MM/yyyy} a {resultado.FimUtc:dd/MM/yyyy})")
                    .FontSize(14)
                    .SemiBold();

                page.Content().PaddingVertical(12).Element(content =>
                {
                    content.Column(column =>
                    {
                        foreach (var linha in ObterLinhasPdf(relatorio, resultado))
                        {
                            column.Item().Text(linha).FontSize(10);
                        }
                    });
                });

                page.Footer()
                    .AlignRight()
                    .Text(DateTime.UtcNow.ToString("'Gerado em' dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture))
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken2);
            });
        }).GeneratePdf();

        var arquivo = $"relatorio-{relatorio}-{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        return File(pdf, "application/pdf", arquivo);
    }

    private async Task CarregarAsync(CancellationToken cancellationToken)
    {
        Relatorios = await _relatorioService.ObterRelatoriosAsync(Inicio, Fim, cancellationToken);

        Inicio = Relatorios.InicioUtc.Date;
        Fim = Relatorios.FimUtc.Date;

        DashboardLabelsJson = JsonSerializer.Serialize(Relatorios.Dashboard.Serie.Select(x => x.Data.ToString("dd/MM")));
        DashboardProducaoJson = JsonSerializer.Serialize(Relatorios.Dashboard.Serie.Select(x => x.QuantidadeProduzida));
        DashboardSaidasJson = JsonSerializer.Serialize(Relatorios.Dashboard.Serie.Select(x => x.QuantidadeSaidaEstoque));
    }

    private static string GerarCsv(string relatorio, RelatoriosResultado resultado)
    {
        var csv = new StringBuilder();
        csv.AppendLine($"Relatório: {ObterTituloRelatorio(relatorio)}");
        csv.AppendLine($"Período;{resultado.InicioUtc:dd/MM/yyyy};{resultado.FimUtc:dd/MM/yyyy}");
        csv.AppendLine();

        switch (relatorio?.Trim().ToLowerInvariant())
        {
            case "desempenho":
                csv.AppendLine("Ordem;Produto;Status;Qtd Planejada;Qtd Produzida;% Atendimento;Início Previsto;Fim Previsto;Fim Real");
                foreach (var item in resultado.DesempenhoProducao)
                {
                    csv.AppendLine(string.Join(";",
                        item.CodigoOrdem,
                        item.Produto,
                        item.Status,
                        FormatarDecimal(item.QuantidadePlanejada),
                        FormatarDecimal(item.QuantidadeProduzida),
                        FormatarDecimal(item.PercentualAtendimento),
                        item.DataInicioPrevista.ToString("dd/MM/yyyy"),
                        item.DataFimPrevista.ToString("dd/MM/yyyy"),
                        item.DataFimReal?.ToString("dd/MM/yyyy") ?? "-"));
                }
                break;
            case "giro":
                csv.AppendLine("Item;Tipo;Saldo Atual;Entradas;Saídas;Saldo Médio;Giro");
                foreach (var item in resultado.GiroEstoque)
                {
                    csv.AppendLine(string.Join(";",
                        item.NomeItem,
                        item.TipoItem,
                        FormatarDecimal(item.SaldoAtual),
                        FormatarDecimal(item.EntradasPeriodo),
                        FormatarDecimal(item.SaidasPeriodo),
                        FormatarDecimal(item.SaldoMedioEstimado),
                        FormatarDecimal(item.Giro)));
                }
                break;
            case "custos":
                csv.AppendLine("Insumo;Quantidade Consumida;Custo Médio Unitário;Custo Total");
                foreach (var item in resultado.CustosInsumo)
                {
                    csv.AppendLine(string.Join(";",
                        item.NomeInsumo,
                        FormatarDecimal(item.QuantidadeConsumida),
                        FormatarDecimal(item.CustoMedioUnitario),
                        FormatarDecimal(item.CustoTotal)));
                }
                break;
            case "movimentacoes":
                csv.AppendLine("Data;Tipo;Item;Quantidade;Motivo;Documento;Usuário");
                foreach (var item in resultado.Movimentacoes)
                {
                    csv.AppendLine(string.Join(";",
                        item.DataMovimentacao.ToString("dd/MM/yyyy HH:mm"),
                        item.TipoMovimentacao,
                        item.NomeItem,
                        FormatarDecimal(item.Quantidade),
                        item.Motivo,
                        item.DocumentoReferencia ?? "-",
                        item.UsuarioId ?? "-"));
                }
                break;
            default:
                csv.AppendLine("Dashboard;Valor");
                csv.AppendLine($"Produção do dia;{FormatarDecimal(resultado.Dashboard.ProducaoDia)}");
                csv.AppendLine($"Produção da semana;{FormatarDecimal(resultado.Dashboard.ProducaoSemana)}");
                csv.AppendLine($"Produção do mês;{FormatarDecimal(resultado.Dashboard.ProducaoMes)}");
                csv.AppendLine($"Valor do estoque atual;{FormatarDecimal(resultado.Dashboard.ValorEstoqueAtual)}");
                csv.AppendLine($"Alertas ativos;{resultado.Dashboard.AlertasAtivos}");
                csv.AppendLine($"OPs em andamento;{resultado.Dashboard.OpsEmAndamento}");
                break;
        }

        return csv.ToString();
    }

    private static List<string> ObterLinhasPdf(string relatorio, RelatoriosResultado resultado)
    {
        var linhas = new List<string>();

        switch (relatorio?.Trim().ToLowerInvariant())
        {
            case "desempenho":
                linhas.Add("Ordem | Produto | Planejada | Produzida | % Atendimento | Status");
                linhas.AddRange(resultado.DesempenhoProducao.Select(x =>
                    $"{x.CodigoOrdem} | {x.Produto} | {FormatarDecimal(x.QuantidadePlanejada)} | {FormatarDecimal(x.QuantidadeProduzida)} | {FormatarDecimal(x.PercentualAtendimento)} | {x.Status}"));
                break;
            case "giro":
                linhas.Add("Item | Saldo Atual | Entradas | Saídas | Saldo Médio | Giro");
                linhas.AddRange(resultado.GiroEstoque.Select(x =>
                    $"{x.NomeItem} | {FormatarDecimal(x.SaldoAtual)} | {FormatarDecimal(x.EntradasPeriodo)} | {FormatarDecimal(x.SaidasPeriodo)} | {FormatarDecimal(x.SaldoMedioEstimado)} | {FormatarDecimal(x.Giro)}"));
                break;
            case "custos":
                linhas.Add("Insumo | Qtd Consumida | Custo Médio | Custo Total");
                linhas.AddRange(resultado.CustosInsumo.Select(x =>
                    $"{x.NomeInsumo} | {FormatarDecimal(x.QuantidadeConsumida)} | {FormatarDecimal(x.CustoMedioUnitario)} | {FormatarDecimal(x.CustoTotal)}"));
                break;
            case "movimentacoes":
                linhas.Add("Data | Tipo | Item | Quantidade | Motivo");
                linhas.AddRange(resultado.Movimentacoes.Select(x =>
                    $"{x.DataMovimentacao:dd/MM/yyyy HH:mm} | {x.TipoMovimentacao} | {x.NomeItem} | {FormatarDecimal(x.Quantidade)} | {x.Motivo}"));
                break;
            default:
                linhas.Add($"Produção do dia: {FormatarDecimal(resultado.Dashboard.ProducaoDia)}");
                linhas.Add($"Produção da semana: {FormatarDecimal(resultado.Dashboard.ProducaoSemana)}");
                linhas.Add($"Produção do mês: {FormatarDecimal(resultado.Dashboard.ProducaoMes)}");
                linhas.Add($"Valor do estoque atual: {FormatarDecimal(resultado.Dashboard.ValorEstoqueAtual)}");
                linhas.Add($"Alertas ativos: {resultado.Dashboard.AlertasAtivos}");
                linhas.Add($"OPs em andamento: {resultado.Dashboard.OpsEmAndamento}");
                break;
        }

        if (linhas.Count == 1)
        {
            linhas.Add("Sem dados para o período selecionado.");
        }

        return linhas.Take(350).ToList();
    }

    private static string ObterTituloRelatorio(string relatorio)
    {
        return relatorio?.Trim().ToLowerInvariant() switch
        {
            "desempenho" => "Desempenho de produção",
            "giro" => "Giro de estoque",
            "custos" => "Custos de insumos",
            "movimentacoes" => "Movimentações de estoque",
            _ => "Dashboard principal"
        };
    }

    private static string FormatarDecimal(decimal valor)
    {
        return valor.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
