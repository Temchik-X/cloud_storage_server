using Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class StreamService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StreamService> _logger;

        public StreamService(ApplicationDbContext context, ILogger<StreamService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Генерирует одноразовую ссылку для доступа к файлу и сохраняет её в БД.
        /// </summary>
        public async Task<string> GetStreamUrlAsync(int userId, int fileId)
        {
            var file = await _context.Files
                .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted);

            if (file == null)
            {
                _logger.LogWarning("Попытка сгенерировать ссылку для несуществующего файла: {FileId}", fileId);
                throw new FileNotFoundException($"File with ID {fileId} not found");
            }

            // 2) формируем токен на основе имени и текущего времени
            //    например: myvideo.mp4_20250518153000_3f1d2e4a6b7c8d9e
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var guidPart = Guid.NewGuid().ToString("N");
            var fileHash = file.FileName.GetHashCode();
            var url = $"{fileHash}_{timestamp}_{guidPart}";

            // 3) сохраняем в БД
            var link = new FileAccessLink
            {
                Url = url,
                CreatedAt = DateTime.UtcNow,
                FileId = fileId,
                UserId = userId
            };
            _context.FileAccessLinks.Add(link);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Сгенерирована ссылка для FileId={FileId}: {Url}", fileId, url);
            return url;
        }

        /// <summary>
        /// Удаляет из БД запись FileAccessLink по точному URL.
        /// Бросает InvalidOperationException, если не найдено.
        /// </summary>
        public async Task DeleteFileAccessLinkAsync(string url, CancellationToken ct = default)
        {
            var link = await _context.FileAccessLinks
                                     .FirstOrDefaultAsync(l => l.Url == url, ct);

            if (link == null)
            {
                _logger.LogWarning("Попытка удалить несуществующую ссылку: {Url}", url);
                throw new InvalidOperationException("Ссылка не найдена.");
            }

            _context.FileAccessLinks.Remove(link);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("Ссылка удалена: {Url}", url);
        }

        public async Task<int> GetFileIdByUrl(string stringUrl)
        {
            var link = await _context.FileAccessLinks
                .FirstOrDefaultAsync(l => l.Url == stringUrl && l.File.IsDeleted != true);

            if (link == null)
            {
                _logger.LogWarning("Попытка получения Id для несуществующего файла: {FileId}", link.FileId);
                throw new FileNotFoundException($"File with ID {link.FileId} not found");
            }
            return link.FileId;
        }
        public async Task<int> GetUserIdByUrl(string stringUrl)
        {
            var link = await _context.FileAccessLinks
                .FirstOrDefaultAsync(l => l.Url == stringUrl && l.File.IsDeleted != true);

            if (link == null)
            {
                _logger.LogWarning("Попытка получения Id для несуществующего файла: {FileId}", link.FileId);
                throw new FileNotFoundException($"File with ID {link.FileId} not found");
            }
            return link.UserId;
        }
    }
}
