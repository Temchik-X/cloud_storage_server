using Application.Data;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace Application.Services
{
    public class DatabaseInitializer
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;

        public DatabaseInitializer(ApplicationDbContext context, ILogger<AuthService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Применение миграций, если они есть");
                // Применяем миграции, если они есть
                await _context.Database.MigrateAsync();

                // Проверяем, есть ли пользователи в таблице
                var usersCount = await _context.Users.CountAsync();
                if (usersCount == 0)
                {
                    // Создаем пользователя по умолчанию
                    var defaultUser = new UserModel
                    {
                        Username = "admin",
                        PasswordHash = PasswordHasher.HashPassword("root"),
                        IsAdmin = true,
                        Email = "admin@admin.ru",
                        FreeSpace = -1,
                        DateRegistration = DateTime.Now

                    };

                    _context.Users.Add(defaultUser);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Пользователь по умолчанию создан: admin/root");
                }
                _logger.LogInformation("Инициализация завершена");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации БД.");
                throw;
            }

        }
        public async Task LoadDefaultIconsAsync()
        {
            try
            {
                _logger.LogInformation("Загрузка стандартных иконок, если они не были загружены");
                string iconsPath = _configuration["DefaultIconsPath"];
                if (!Directory.Exists(iconsPath))
                {
                    throw new DirectoryNotFoundException($"Icons directory not found: {iconsPath}");
                }

                var iconFiles = Directory.GetFiles(iconsPath, "*.png");
                foreach (var iconFile in iconFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(iconFile); // Например: pdf, docx, mp4
                    string fileType = "." + fileName;

                    // Проверяем, есть ли уже такая иконка
                    bool exists = await _context.FileIcons.AnyAsync(i => !i.IsGenerated && i.FileType == fileType);
                    if (exists) continue;

                    using var image = await SixLabors.ImageSharp.Image.LoadAsync(iconFile);
                    image.Mutate(x => x.Resize(128, 128));

                    using var ms = new MemoryStream();
                    await image.SaveAsPngAsync(ms);
                    var iconData = ms.ToArray();

                    var icon = new FileIcon
                    {
                        IconData = iconData,
                        IsGenerated = false,
                        FileType = fileType
                    };

                    _context.FileIcons.Add(icon);
                }
                _logger.LogInformation("Стандартные икноки загружены успешно");
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации свтандартных иконок.");
                throw;
            }
            
        }
    }
}
