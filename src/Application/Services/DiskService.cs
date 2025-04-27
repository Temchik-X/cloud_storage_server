using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Application.Data;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class DiskService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DiskService> _logger;
        private readonly UserService _userService;
        private readonly DiskSpaceService _diskSpaceService;
        private readonly IconService _iconService;

        public DiskService(ApplicationDbContext context, ILogger<DiskService> logger, UserService userService, DiskSpaceService diskSpaceService, IconService iconService)
        {
            _context = context;
            _logger = logger;
            _userService = userService;
            _diskSpaceService = diskSpaceService;
            _iconService = iconService;
        }

        public async Task<object> GetConnectedDirectories()
        {
            try
            {
                var disks = await _context.Disks.ToListAsync();

                _logger.LogInformation("Получен список подключенных директорий.");

                return disks.Select(d => new
                {
                    d.Id,
                    d.Name,
                    FileCount = Directory.GetFiles(d.Name).Length,
                    UsedSpace = Math.Round(GetUsedSpace(d.Name), 2),
                    TotalSpace = Math.Round(GetTotalSpace(d.Name), 2),
                    FreeSpace = Math.Round(GetFreeSpace(d.Name), 2),
                    d.Description
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка подключенных директорий.");
                throw;
            }
        }

        public async Task<string> AddDirectory(string directoryName)
        {
            string fullPath = Path.GetFullPath(directoryName);
            if (string.IsNullOrEmpty(fullPath))
            {
                _logger.LogWarning("Попытка добавить пустую директорию.");
                throw new ArgumentException("Имя папки не может быть пустым.");
            }

            if (!Directory.Exists(fullPath))
            {
                _logger.LogWarning($"Указанная директория не существует: {fullPath}");
                throw new ArgumentException("Указанная директория не существует.");
            }

            var directoryExists = await _context.Disks.AnyAsync(d => d.Name == fullPath);
            if (directoryExists)
            {
                _logger.LogWarning($"Директория уже существует в базе данных: {fullPath}");
                throw new InvalidOperationException("Директория с таким именем уже существует в базе данных.");
            }

            var directory = new DiskModel
            {
                Name = fullPath,
                Description = string.Empty
            };

            _context.Disks.Add(directory);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Директория успешно добавлена: {fullPath}");

            return "Директория успешно добавлена.";
        }

        public bool GetOS()
        {
            var isWindows = System.Runtime.InteropServices.RuntimeInformation
                .IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);

            _logger.LogInformation($"Определена операционная система: {(isWindows ? "Windows" : "Not Windows")}");

            return isWindows;
        }

        public async Task<GeneralDiskInfo> GetGeneralDirectoryInfoAsync() {
            try
            {
                var disks = await _context.Disks.ToListAsync();

                _logger.LogInformation("Получен список подключенных директорий для общей информации.");
                var generalDiskInfo = new GeneralDiskInfo();
                generalDiskInfo.DiskCount = disks.Count;
                foreach (var disk in disks) {
                    generalDiskInfo.GeneralDiskSpace = GetTotalSpace(disk.Name);
                    generalDiskInfo.GeneralFreeSpace = GetFreeSpace(disk.Name);
                }
                var fileCount = await _context.Files.CountAsync();
                generalDiskInfo.GeneralFileCount = fileCount;
                _logger.LogInformation("Общая информация о состоянии системы получена.");
                return generalDiskInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при общей информации о состоянии системы.");
                throw;
            }
        }
        public async Task<string> DeleteDirectory(int id)
        {
            var disk = await _context.Disks.FirstOrDefaultAsync(d => d.Id == id);
            if (disk == null)
            {
                _logger.LogWarning($"Диск с ID {id} не найден.");
                throw new KeyNotFoundException("Диск не найден.");
            }

            _context.Disks.Remove(disk);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Диск успешно удален. ID: {id}");

            return "Диск успешно удален.";
        }

        // Индексация файлов
        // Старый вариант, не учитывает распределение по папкам.
        // ПЕРЕДЕЛАТЬ
        public async Task<string> IndexFiles()
        {
            var disks = await _diskSpaceService.GetDirectoriesAsync();
            if (!disks.Any())
            {
                _logger.LogWarning("Нет доступных файлов для индексации.");
                throw new InvalidOperationException("Нет доступных файлов для индексации.");
            }
            var filesToAdd = new List<FileModel>();
            var userFilesToAdd = new List<UserFileModel>();
            foreach (var disk in disks)
            {
                if (!Directory.Exists(disk.Path))
                {
                    _logger.LogWarning($"Директория больше не существует: {disk.Path}");
                    continue;
                }
                // Получаем минимально требуемую информацию о файлах (путь и имя)
                var filePaths = Directory.GetFiles(disk.Path);
                foreach (var filePath in filePaths) 
                {
                    var fileName = Path.GetFileName(filePath);
                    var existingFile = await _context.Files.FirstOrDefaultAsync(f => f.FilePath == filePath);
                    if (existingFile == null)
                    {
                        // Файл отсутствует в базе данных — добавляем его
                        var fileInfo = new FileInfo(filePath);
                        var fileModel = new FileModel
                        {
                            FileName = fileName,
                            FilePath = filePath,
                            DiskId = disk.DiskId,
                            Size = fileInfo.Length,
                            FileType = Path.GetExtension(filePath).ToLower(),
                            CreatedAt = File.GetCreationTime(filePath),
                            UpdatedAt = File.GetLastWriteTime(filePath),
                            Hash = await GenerateFileHashAsync(filePath) // Асинхронное вычисление хеша
                        };

                        filesToAdd.Add(fileModel);
                        _logger.LogInformation($"Файл добавлен в список для записи в базу данных: {filePath}");

                        // Добавляем запись в таблицу UserFile (привязка к пользователю "admin")
                        var userFile = new UserFileModel
                        {
                            UserId = _userService.GetCurrentUserId(),
                            Share = false
                        };

                        userFilesToAdd.Add(userFile);
                    }
                }
            }

            // Пакетное сохранение файлов
            if (filesToAdd.Any())
            {
                _context.Files.AddRange(filesToAdd);
                await _context.SaveChangesAsync(); // Сохраняем файлы, чтобы получить Id

                // Привязываем записи UserFile к файлам
                for (int i = 0; i < filesToAdd.Count; i++)
                {
                    userFilesToAdd[i].FileId = filesToAdd[i].Id;
                }

                _context.UserFiles.AddRange(userFilesToAdd);
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Индексация файлов завершена.");
            foreach (var fileModel in filesToAdd)
            {
                _iconService.ScheduleIconGeneration(fileModel);
            }
            

            return "Индексация файлов завершена.";
        }

        // Вспомогательные методы
        private double GetUsedSpace(string drive)
        {
            var driveInfo = new DriveInfo(drive);
            return (driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / (1024.0 * 1024 * 1024);
        }

        private double GetTotalSpace(string drive)
        {
            var driveInfo = new DriveInfo(drive);
            return driveInfo.TotalSize / (1024.0 * 1024 * 1024);
        }

        private double GetFreeSpace(string drive)
        {
            var driveInfo = new DriveInfo(drive);
            return driveInfo.AvailableFreeSpace / (1024.0 * 1024 * 1024);
        }

        private async Task<string> GenerateFileHashAsync(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var hashBytes = await sha256.ComputeHashAsync(stream); // Асинхронное вычисление хеша
                    return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
                }
            }
        }
    }
}