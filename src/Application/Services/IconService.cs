using Application.Data;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Application.Services
{
    public class IconService
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<IconService> _logger;
        private int folderIconId { get; set; } 

        public IconService(ApplicationDbContext context, IConfiguration configuration, IServiceProvider serviceProvider, ILogger<IconService> logger)
        {
            _context = context;
            _configuration = configuration;
            _serviceProvider = serviceProvider;
            _logger = logger;
            folderIconId = _context.FileIcons.FirstOrDefault(i => i.FileType == ".folder")?.Id ?? 0; // Получаем ID иконки для папки
        }
        public int GetFolderIconIdAsync()
        {
            // Получаем ID иконки для папки
            return folderIconId;
        }
        public async Task<int> GetOrCreateIconAsync(FileModel file)
        {
            // Для стандартных типов ищем иконку
            var existingIcon = await _context.FileIcons
                .FirstOrDefaultAsync(i => i.FileType == file.FileType && !i.IsGenerated);

            if (existingIcon != null)
                return existingIcon.Id;
            // Определяем, является ли файл изображением
            var supportedImageFormats = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif", ".webp", ".heic" };
            var supportedVideoFormats = new[] { ".mp4", ".avi", ".mov", ".mkv", ".flv" };
            if (supportedImageFormats.Contains(file.FileType.ToLower()))
            {
                // Для изображений генерируем
                var generatedIcon = new FileIcon
                {
                    FileType = file.FileType,
                    IconData = await GenerateImageIconAsync(file.FilePath),
                    IsGenerated = true
                };
                _context.FileIcons.Add(generatedIcon);
                await _context.SaveChangesAsync();
                return generatedIcon.Id;
            }
            if (supportedVideoFormats.Contains(file.FileType.ToLower()))
            {
                // Для видео генерируем (не реализовано)
                /*var generatedIcon = new FileIcon
                {
                    FileType = file.FileType,
                    IconData = await GenerateImageIconAsync(file.FilePath),
                    IsGenerated = true
                };
                _context.FileIcons.Add(generatedIcon);
                await _context.SaveChangesAsync();
                return generatedIcon.Id;*/
                return await GenerateVideoIconAsync(file.FilePath);
            }
            // Для остальных типов возвращаем стандартную иконку
            
            return await GetStandartFileIconAsync();

        }
        // Перевести на загрузки по id файла РЕФЕРЕНС У QWEN чат разделение контроллера
        public async Task<(byte[], string fileName)> DownloadIcon(int fileId, int iconId, int userId)
        {
            /*try
            {
                var file = await _context.Files
                                        .Include(f => f.Icon)
                                        .FirstOrDefaultAsync(f => f.Id == fileId);
                if (file == null || file.Icon == null || file.Icon.IconData == null)
                {
                    _logger.LogWarning($"Иконка для файла с ID {fileId} не найдена.");
                    throw new KeyNotFoundException("Иконка не найдена.");
                }
                // Логируем успешное получение иконки
                _logger.LogInformation($"Иконка для файла с ID {fileId} получена.");
                if (file.Icon.IsGenerated)
                {
                    var access = await _context.UserFiles.FirstOrDefaultAsync(
                                                                        uf => uf.UserId == userId &&
                                                                        uf.FileId == fileId); // Добавляем условие для FileIcons
                    if (access == null)
                    {
                        _logger.LogWarning($"Нессанкционированный доступ к файлу: ID {fileId}, UserId {userId}");
                        throw new AccessViolationException("Нет доступа к файлу.");
                    }
                }
                // Возвращаем данные иконки и имя файла
                return (file.Icon.IconData, $"{Path.GetFileNameWithoutExtension(file.FileName)}_icon.jpg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрeзке иконки.");
                throw;
            }*/
            try
            {
                var icon = await _context.FileIcons
                    .Where(f => f.Id == iconId)  
                    .FirstOrDefaultAsync();
                if (icon == null)
                {
                    _logger.LogWarning($"Иконка для файла с ID {fileId} не найдена.");
                    throw new KeyNotFoundException("Иконка не найдена.");
                }
                // Логируем успешное получение иконки
                _logger.LogInformation($"Иконка для файла с ID {fileId} получена.");
                if (icon.IsGenerated)
                {
                    var access = await _context.UserFiles.FirstOrDefaultAsync(
                                                                        uf => uf.UserId == userId &&
                                                                        uf.FileId == fileId); // Добавляем условие для FileIcons
                    if (access == null)
                    {
                        _logger.LogWarning($"Нессанкционированный доступ к файлу: ID {fileId}, UserId {userId}");
                        throw new AccessViolationException("Нет доступа к файлу.");
                    }
                }
                // Возвращаем данные иконки и имя файла
                return (icon.IconData, $"{fileId}_icon.jpg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрeзке иконки.");
                throw;
            }
        }

        private async Task<byte[]> GenerateImageIconAsync(string filePath)
        {
            // Максимальный размер иконки
            const int maxIconSize = 128;

            try
            {
                if (Path.GetExtension(filePath).ToLower() == ".heic")
                {
                    filePath = await ConvertToHeic(filePath);
                }
                    using (var image = await SixLabors.ImageSharp.Image.LoadAsync(filePath))
                {
                    // Изменяем размер изображения с сохранением пропорций
                    image.Mutate(ctx => ctx.Resize(new ResizeOptions
                    {
                        Size = new Size(maxIconSize, maxIconSize),
                        Mode = ResizeMode.Max // Сохраняет пропорции, масштабирует до максимального размера
                    }));

                    // Создаем новый холст размером 128x128 с белым фоном (или другим цветом)
                    using (var resizedImage = new Image<Rgba32>(maxIconSize, maxIconSize, Color.Transparent))
                    {
                        // Вычисляем позицию для центрирования изображения
                        int offsetX = (maxIconSize - image.Width) / 2;
                        int offsetY = (maxIconSize - image.Height) / 2;

                        // Накладываем измененное изображение на холст
                        resizedImage.Mutate(ctx => ctx.DrawImage(image, new Point(offsetX, offsetY), 1f));

                        // Сохраняем результат в поток в формате PNG
                        using (var memoryStream = new MemoryStream())
                        {
                            await resizedImage.SaveAsPngAsync(memoryStream);
                            return memoryStream.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при генерации иконки для файла: {filePath}");
                throw new InvalidOperationException("Не удалось сгенерировать иконку.");
            }
        }
        private async Task<int> GenerateVideoIconAsync(string filePath)
        {
            var icon =  await _context.FileIcons.FirstOrDefaultAsync(i => i.FileType == ".video");
            return icon.Id;
        }

        private async Task<int> GetStandartFileIconAsync()
        {
            var icon = await _context.FileIcons.FirstOrDefaultAsync(i => i.FileType == ".file");
            return icon.Id;
        }
        public void ScheduleIconGeneration(FileModel file)
        {
            _ = Task.Run(async () =>
            {
                _logger.LogInformation($"Поиск или создание иконки для {file.Id}");
                using var scope = _serviceProvider.CreateScope();
                var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var scopedIconService = scope.ServiceProvider.GetRequiredService<IconService>();

                var iconId = await scopedIconService.GetOrCreateIconAsync(file);

                if (file != null)
                {
                    file.IconId = iconId;
                    scopedContext.Files.Update(file);
                    await scopedContext.SaveChangesAsync();
                }
            });
        }

        public async Task<string> ConvertToHeic(string filePath)
        {
            // Конвертируем HEIC в JPEG
            using (var image = new MagickImage(filePath))
            {
                image.Format = MagickFormat.Jpeg; // Конвертируем в JPEG

                // Сохраняем конвертированное изображение во временный файл
                var tempFilePath = Path.GetTempFileName();
                await image.WriteAsync(tempFilePath);

                // Заменяем исходный путь на временный
                filePath = tempFilePath;
            }
            return filePath;
        }
    }
}
