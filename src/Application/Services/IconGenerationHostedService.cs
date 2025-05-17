using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Data;
using Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class IconGenerationHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IIconGenerationQueue _queue;
    private readonly ILogger<IconGenerationHostedService> _logger;
    private readonly TimeSpan _throttleDelay = TimeSpan.FromSeconds(2);

    public IconGenerationHostedService(
        IServiceProvider services,
        IIconGenerationQueue queue,
        ILogger<IconGenerationHostedService> logger)
    {
        _services = services;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IconGenerationHostedService запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // ждём следующий файл из очереди
                var file = await _queue.DequeueAsync(stoppingToken);

                _logger.LogInformation("Генерация иконки для FileId={FileId}", file.Id);

                using var scope = _services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var iconService = scope.ServiceProvider.GetRequiredService<IconService>();

                var iconId = await iconService.GetOrCreateIconAsync(file);

                // сохраняем результат
                file.IconId = iconId;
                dbContext.Files.Update(file);
                await dbContext.SaveChangesAsync(stoppingToken);

                // даём паузу перед следующим
                await Task.Delay(_throttleDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // сервис отключается
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации иконки");
            }
        }

        _logger.LogInformation("IconGenerationHostedService остановлен.");
    }
}
