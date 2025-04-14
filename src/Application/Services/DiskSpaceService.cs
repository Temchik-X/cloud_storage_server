using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Application.Data;
using Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Application.Services
{
    public class DiskSpaceService
    {
        private readonly ILogger<DiskSpaceService> _logger;
        private readonly ApplicationDbContext _context;

        public DiskSpaceService(ILogger<DiskSpaceService> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }
        public async Task<(int DiskId, int SubFolderId, string DiskPath)> SelectOptimalDirectoryAsync(long requiredSpace)
        {
            try
            {
                // Получаем список доступных дисков из базы данных
                var disks = await _context.Disks.ToListAsync();

                // Выбираем оптимальный диск для загрузки файла
                var (optimalDiskId, optimalDiskPath) = await SelectOptimalDiskAsync(disks, requiredSpace);

                if (optimalDiskId == -1 || optimalDiskPath == null)
                {
                    _logger.LogWarning("Не удалось выбрать оптимальный диск.");
                    return (-1, -1, null);
                }

                // Выбираем оптимальную подпапку на выбранном диске
                var (optimalSubFolderId, optimalSubFolder) = await SelectOptimalSubFolderAsync(optimalDiskId, optimalDiskPath);

                if (optimalSubFolder == null)
                {
                    _logger.LogWarning("Не удалось выбрать оптимальную подпапку на диске {optimalDiskPath}.", optimalDiskPath);
                    return (-1, -1, null);
                }

                return (optimalDiskId, optimalSubFolderId, optimalSubFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выборе оптимальной директории.");
                throw;
            }
        }

        /// Выбирает оптимальный диск для загрузки файла.
        /// param disks - Список доступных дисков.
        /// param requiredSpace - Требуемое место (в байтах).
        /// return Путь к выбранному диску или null, если подходящий диск не найден.
        public async Task<(int DiskId, string DiskPath)> SelectOptimalDiskAsync(IEnumerable<DiskModel> disks, long requiredSpace)
        {
            try
            {
                const double minFreeSpacePercentage = 0.12;

                // Фильтруем диски, которые существуют на сервере
                var validDisks = await Task.Run(() => disks
                    .Where(d => Directory.Exists(d.Name))
                    .Select(d =>
                    {
                        var driveInfo = new DriveInfo(d.Name);
                        return new
                        {
                            d.Id,
                            Path = d.Name,
                            FreeSpace = driveInfo.AvailableFreeSpace,
                            TotalSpace = driveInfo.TotalSize,
                            FreeSpacePercentage = (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize
                        };
                    })
                    .ToList());

                if (!validDisks.Any())
                {
                    _logger.LogWarning("Нет доступных дисков для загрузки файла.");
                    return (-1, null);
                }

                // Ищем диск с достаточным свободным местом (>= 12%)
                var suitableDisk = validDisks
                    .FirstOrDefault(d => d.FreeSpacePercentage >= minFreeSpacePercentage && d.FreeSpace >= requiredSpace);

                if (suitableDisk != null)
                {
                    _logger.LogInformation($"Выбран диск с достаточным свободным местом: {suitableDisk.Path}");
                    return (suitableDisk.Id, suitableDisk.Path);
                }

                // Если подходящего диска нет, выбираем диск с наибольшим свободным местом
                var fallbackDisk = validDisks
                    .OrderByDescending(d => d.FreeSpace)
                    .FirstOrDefault();

                if (fallbackDisk == null || fallbackDisk.FreeSpace < requiredSpace)
                {
                    _logger.LogError("Недостаточно места на всех доступных дисках.");
                    return (-1, null);
                }

                _logger.LogInformation($"Выбран диск с наибольшим свободным местом: {fallbackDisk.Path}");
                return (fallbackDisk.Id, fallbackDisk.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выборе диска.");
                throw;
            }
        }
        /// <summary>
        /// Выбирает оптимальную подпапку для загрузки файла на указанном диске.
        /// </summary>
        /// param diskPath - Путь к выбранному диску.</param>
        /// return Путь к выбранной подпапке или null, если подходящая папка не найдена.</returns>
        public async Task<(int directoryId, string directoryPath)> SelectOptimalSubFolderAsync(int diskId, string diskPath)
        {
            try
            {
                // Получаем список подпапок на диске из базы данных
                var subFolders = await _context.SubFolders
                    .Where(sf => sf.Disk.Id == diskId) // Фильтруем по диску
                    .ToListAsync();

                // Если подпапок нет, создаем первую папку
                if (!subFolders.Any())
                {
                    var newFolderPath = Path.Combine(diskPath, "folder_1");
                    Directory.CreateDirectory(newFolderPath);

                    // Добавляем новую папку в базу данных
                    var newSubFolder = new ServiceFolderModel
                    {
                        DiskId = diskId,
                        FolderName = "folder_1",
                        Count = 0
                    };
                    _context.SubFolders.Add(newSubFolder);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Создана новая папка: {newFolderPath}");
                    return (1, newFolderPath);
                }

                // Ищем папку с количеством файлов меньше 1000
                var suitableFolder = subFolders
                    .FirstOrDefault(sf => sf.Count < 1000);

                if (suitableFolder != null)
                {
                    _logger.LogInformation($"Выбрана папка с количеством файлов < 1000: {suitableFolder.FolderName}");
                    return (suitableFolder.Id, Path.Combine(diskPath, suitableFolder.FolderName));
                }

                // Если нет папок с количеством файлов < 1000, проверяем общее количество папок
                var totalFolders = subFolders.Count;

                if (totalFolders < 256)
                {
                    // Создаем новую папку
                    var newFolderName = $"folder_{totalFolders + 1}";
                    var newFolderPath = Path.Combine(diskPath, newFolderName);
                    Directory.CreateDirectory(newFolderPath);

                    // Добавляем новую папку в базу данных
                    var newSubFolder = new ServiceFolderModel
                    {
                        DiskId = diskId,
                        FolderName = newFolderName,
                        Count = 0
                    };
                    _context.SubFolders.Add(newSubFolder);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Создана новая папка: {newFolderPath}");
                    return (newSubFolder.Id, newFolderPath);
                }

                // Если папок уже 256, ищем папку с количеством файлов < 5000
                suitableFolder = subFolders
                    .FirstOrDefault(sf => sf.Count < 5000);

                if (suitableFolder != null)
                {
                    _logger.LogInformation($"Выбрана папка с количеством файлов < 5000: {suitableFolder.FolderName}");
                    return (suitableFolder.Id, Path.Combine(diskPath, suitableFolder.FolderName));
                }

                // Если подходящей папки нет, выбираем папку с минимальным количеством файлов
                var folderWithMinFiles = subFolders
                    .OrderBy(sf => sf.Count)
                    .FirstOrDefault();

                if (folderWithMinFiles != null)
                {
                    _logger.LogInformation($"Выбрана папка с минимальным количеством файлов: {folderWithMinFiles.FolderName}");
                    return (folderWithMinFiles.Id, Path.Combine(diskPath, folderWithMinFiles.FolderName));
                }

                // Если даже папка с минимальным количеством файлов не найдена (теоретически невозможный случай)
                _logger.LogError("Нет подходящих папок для загрузки файла.");
                return (-1, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при выборе подпапки.");
                throw;
            }
        }
        public async Task<List<DirectoryDto>> GetDirectoriesAsync()
        {

           try
            {
                return  await _context.SubFolders.Select(sf => new DirectoryDto
                                                    {
                                                        Path = sf.Disk.Name + sf.FolderName,
                                                        DiskId = sf.Disk.Id
                                                    })
                                                    .ToListAsync();

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка подпапок.");
                throw;
            }
        }
        // Генерация хеша для файла с использованием потоковой передачи
        public async Task<string> GenerateFileHashAsync(string filePath)
        {
            try
            {
                using (var sha256 = SHA256.Create())
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        var hashBytes = await sha256.ComputeHashAsync(stream); // Асинхронное вычисление хеша
                        var hash = BitConverter.ToString(hashBytes).Replace("-", string.Empty);
                        _logger.LogInformation($"Хеш файла успешно сгенерирован: {hash}");
                        return hash;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при генерации хеша файла.");
                throw;
            }
        }

        /// <summary>
        /// Генерирует уникальное имя файла, если файл с таким именем уже существует.
        /// </summary>
        public string GetUniqueFileName(string directoryPath, string fileName)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = Path.GetExtension(fileName);
            var uniqueFileName = fileName;
            int counter = 1;

            while (System.IO.File.Exists(Path.Combine(directoryPath, uniqueFileName)))
            {
                uniqueFileName = $"{fileNameWithoutExtension}({counter}){extension}";
                counter++;
            }

            return uniqueFileName;
        }
    }
}