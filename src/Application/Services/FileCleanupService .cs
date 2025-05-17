using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class FileCleanupService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<FileCleanupService> _logger;

    public FileCleanupService(IServiceProvider services, ILogger<FileCleanupService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileCleanupService запущен.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1);
                var delay = nextRun - now;
                if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

                _logger.LogInformation("Следующий прогон очистки: {NextRunUtc}", nextRun);
                await Task.Delay(delay, stoppingToken);

                await DoCleanup(stoppingToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в цикле FileCleanupService");
            }
        }

        _logger.LogInformation("FileCleanupService остановлен.");
    }

    private async Task DoCleanup(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var threshold = DateTime.UtcNow.AddDays(-30);
        var toDelete = await db.Files
            .Where(f => f.IsDeleted && f.UpdatedAt < threshold)
            .ToListAsync(ct);

        if (!toDelete.Any())
        {
            _logger.LogInformation("Нет файлов для окончательного удаления.");
            return;
        }

        foreach (var file in toDelete)
        {
            if (System.IO.File.Exists(file.FilePath))
            {
                System.IO.File.Delete(file.FilePath);
                _logger.LogInformation("Файл с ID {FileId} удалён с диска.", file.Id);
            }
        }

        db.Files.RemoveRange(toDelete);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Удалено {Count} файлов.", toDelete.Count);
    }
}
