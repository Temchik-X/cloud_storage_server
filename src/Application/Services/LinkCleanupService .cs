using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class LinkCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<LinkCleanupService> _logger;

    public LinkCleanupService(IServiceProvider services, ILogger<LinkCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LinkCleanupService запущен.");

        // Пускай чистка идёт раз в полчаса
        var interval = TimeSpan.FromMinutes(30);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await CleanUpAsync(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в LinkCleanupService");
            }
        }

        _logger.LogInformation("LinkCleanupService остановлен.");
    }

    private async Task CleanUpAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow.AddHours(-3);
        var oldLinks = db.FileAccessLinks
            .Where(l => l.CreatedAt < cutoff).ToList();

        if (!oldLinks.Any())
        {
            _logger.LogInformation("Нет устаревших ссылок для удаления.");
            return;
        }

        db.FileAccessLinks.RemoveRange(oldLinks);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Удалено {Count} ссылок старше 3 часов.", oldLinks.Count);
    }
}
