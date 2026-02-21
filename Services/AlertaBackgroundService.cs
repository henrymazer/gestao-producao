using Microsoft.Extensions.Options;

namespace gestao_producao.Services;

public class AlertaBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AlertaBackgroundService> _logger;
    private readonly AlertaEstoqueOptions _options;

    public AlertaBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<AlertaEstoqueOptions> options,
        ILogger<AlertaBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var alertaService = scope.ServiceProvider.GetRequiredService<AlertaService>();
                await alertaService.AtualizarAlertasAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao atualizar alertas de estoque em background.");
            }

            var intervalo = Math.Max(60, _options.IntervaloAtualizacaoSegundos);
            await Task.Delay(TimeSpan.FromSeconds(intervalo), stoppingToken);
        }
    }
}
