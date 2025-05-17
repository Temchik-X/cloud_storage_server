using Application.Data;
using Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Security.Cryptography;

namespace Application.Services
{
    public class FileService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FileService> _logger;
        private readonly DiskSpaceService _diskSpaceService;
        private readonly IconService _iconService;

        public FileService(ApplicationDbContext context, ILogger<FileService> logger, DiskSpaceService diskSpaceService, IconService iconService)
        {
            _context = context;
            _logger = logger;
            _diskSpaceService = diskSpaceService;
            _iconService = iconService;
        }

        public async Task<object> GetFilesList(int userId, bool? isDeleted = false)
        {
            try
            {
                var files = await _context.UserFiles
                    .Where(uf => uf.UserId == userId && uf.File.IsDeleted == isDeleted) // Фильтруем по UserId и IsDeleted
                    .Select(uf => new FileDto
                    {
                        Id = uf.File.Id,
                        FileName = uf.File.FileName,
                        Size = uf.File.Size,
                        FileType = uf.File.FileType,
                        CreatedAt = uf.File.CreatedAt,
                        IconId = uf.File.IconId
                    })
                    .ToListAsync();

                if (!files.Any())
                {
                    _logger.LogWarning("Файлы не найдены.");
                    throw new InvalidOperationException("Файлы не найдены.");
                }

                _logger.LogInformation("Список файлов успешно получен.");
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка файлов.");
                throw;
            }
        }
        public async Task<StreamVideoResult> GetStreamVideoAsync(int userId, int id)
        {
            // Проверяем, что файл принадлежит пользователю и не удалён
            var file = await _context.UserFiles
                .Include(uf => uf.File)
                .Where(uf => uf.UserId == userId && uf.File.Id == id && !uf.File.IsDeleted)
                .Select(uf => uf.File)
                .FirstOrDefaultAsync();

            if (file == null)
                throw new InvalidOperationException("Файл не найден или недоступен.");

            // Предполагаем, что FilePath хранит полный путь
            return new StreamVideoResult
            {
                FilePath = file.FilePath,
                ContentType = file.FileType,   // например "video/mp4"
                FileName = file.FileName
            };
        }
        public async Task RestoreManyAsync(int userId, List<int> ids)
        {
            try
            {
                var files = await _context.UserFiles
                    .Include(uf => uf.File)
                    .Where(uf => uf.UserId == userId
                                 && ids.Contains(uf.File.Id)
                                 && uf.File.IsDeleted)
                    .ToListAsync();

                if (!files.Any())
                {
                    _logger.LogWarning("Не найдено удалённых файлов для восстановления. UserId={UserId}, Ids=[{Ids}]",
                            userId, string.Join(",", ids));
                    throw new InvalidOperationException("Нет удалённых файлов для восстановления.");
                }

                foreach (var uf in files)
                {
                    uf.File.IsDeleted = false;
                    uf.File.FolderId = null;
                    _context.Files.Update(uf.File);
                }
                await _context.SaveChangesAsync();

                _logger.LogInformation("Восстановлено {Count} файлов для UserId={UserId}.", files.Count, userId);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка файлов.");
                throw;
            }
        }
        public async Task PermanentlyDeleteManyAsync(int userId, List<int> ids)
        {
            try
            {
                var files = await _context.UserFiles
                    .Include(uf => uf.File)
                    .Where(uf => uf.UserId == userId
                                 && ids.Contains(uf.File.Id)
                                 && uf.File.IsDeleted)
                    .ToListAsync();

                if (!files.Any())
                {
                    _logger.LogWarning("Не найдено удалённых файлов для уничтожения. UserId={UserId}, Ids=[{Ids}]",
                            userId, string.Join(",", ids));
                    throw new InvalidOperationException("Нет удалённых файлов для уничтожения.");
                }

                foreach (var uf in files)
                {
                    if (System.IO.File.Exists(uf.File.FilePath))
                    {
                        System.IO.File.Delete(uf.File.FilePath);
                        _logger.LogInformation($"Файл с ID {uf.File.Id} успешно удален с диска.");
                    }
                    _context.Files.Remove(uf.File);
                }
                await _context.SaveChangesAsync();

                _logger.LogInformation("Уничтожено {Count} файлов для UserId={UserId}.", files.Count, userId);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка файлов.");
                throw;
            }
        }
        public async Task<JsonRecord> DTOGetJsonRecordAsync(int userId, int? parentFolderId)
        {
            try
            {
                _logger.LogInformation($"Начало получения директории пользователя с ID {userId}.");
                var folderIconId = _iconService.GetFolderIconIdAsync();
                var folderName = _context.Folders
                    .Where(f => f.UserId == userId && f.Id == parentFolderId)
                    .Select(f => f.Name)
                    .FirstOrDefault();
                // Загружаем корневые папки пользователя
                var folders = await _context.Folders
                    .Where(f => f.UserId == userId && f.ParentFolderId == parentFolderId)
                    .Select(f => new FolderInfoDto
                    {
                        Id = f.Id,
                        Name = f.Name,
                        ParentFolderId = f.ParentFolderId,
                        IconId = folderIconId
                        
                    })
                    .ToListAsync();
                var files = await _context.UserFiles
                    .Where(uf => uf.UserId == userId && uf.File.FolderId == parentFolderId && uf.File.IsDeleted == false) // Фильтруем по UserId и IsDeleted
                    .Select(uf => new FileDto
                    {
                        Id = uf.File.Id,
                        FileName = uf.File.FileName,
                        Size = uf.File.Size,
                        FileType = uf.File.FileType,
                        CreatedAt = uf.File.CreatedAt,
                        IconId = uf.File.IconId
                    })
                    .ToListAsync();
                var jsonRecord = new JsonRecord(folderName, folders, files);

                _logger.LogInformation($"Директория пользователя с ID {userId} успешно загружена.");
                return jsonRecord;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении директории пользователя с ID {userId}.");
                throw;
            }
        }


        public async Task<(Stream fileStream, string fileName)> DownloadFile(int id, int userId)
        {
            try
            {
                var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);

                if (file == null || !System.IO.File.Exists(file.FilePath))
                {
                    _logger.LogWarning($"Файл с ID {id} не найден.");
                    throw new KeyNotFoundException("Файл не найден.");
                }
                var access = await _context.UserFiles.FirstOrDefaultAsync(uf => uf.FileId == id && uf.UserId == userId);
                if (access == null)
                {
                    _logger.LogWarning($"Нессанкционированный доступ к файлу: ID {id}, UserId {userId}");
                    throw new AccessViolationException("Нет доступа к файлу.");
                }
                var fileStream = new FileStream(file.FilePath, FileMode.Open, FileAccess.Read);
                _logger.LogInformation($"Файл с ID {id} успешно подготовлен для скачивания.");

                return (fileStream, file.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загурзке файла.");
                throw;
            }
        }

        public async Task<string> UploadFile(IFormFile file, int userId, int? folderId)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("Попытка загрузки пустого файла.");
                    throw new ArgumentException("Нет файла для загрузки.");
                }
                // Выбираем оптимальный диск для загрузки файла
                var (targetDiskId, targetSubFolderId, targetDisk) = await _diskSpaceService.SelectOptimalDirectoryAsync(file.Length);
                if (string.IsNullOrEmpty(targetDisk))
                {
                    _logger.LogError("Не удалось найти подходящий диск для загрузки файла.");
                    throw new InvalidOperationException("Недостаточно места на всех доступных дисках.");
                }
                // Путь для сохранения файла
                var originalFileName = file.FileName; // Сохраняем исходное имя файла
                var uniqueFileName = _diskSpaceService.GetUniqueFileName(targetDisk, originalFileName); // Генерируем уникальное имя
                var filePath = Path.Combine(targetDisk, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    await file.CopyToAsync(stream);
                }
                var fileInfo = new FileInfo(filePath);

                var newFile = new FileModel
                {
                    FileName = file.FileName,
                    DiskId = targetDiskId,
                    FilePath = filePath,
                    Size = file.Length,
                    FileType = Path.GetExtension(file.FileName).ToLower(),
                    CreatedAt = fileInfo.CreationTime,
                    UpdatedAt = DateTime.Now,
                    Hash = await _diskSpaceService.GenerateFileHashAsync(filePath), 
                    FolderId = folderId
                };

                _context.Files.Add(newFile);
                _context.SubFolders
                    .Where(s => s.Id == targetSubFolderId)
                    .ExecuteUpdate(s => s.SetProperty(p => p.Count, p => p.Count + 1));
                await _context.SaveChangesAsync();


                // Добавляем запись в таблицу UserFiles
                var userFile = new UserFileModel
                {
                    FileId = newFile.Id,
                    UserId = userId,
                    Share = false // Флаг пока не используется
                };

                _context.UserFiles.Add(userFile);
                await _context.SaveChangesAsync();

                _iconService.ScheduleIconGeneration(newFile);

                _logger.LogInformation($"Файл успешно загружен: {filePath}");
                return "Файл успешно загружен.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке файла.");
                throw;
            }
        }
        public async Task<string> UploadManyFilesAsync(List<IFormFile> files, int userId, int? parentFolderId)
        {
            try
            {
                if (files == null || !files.Any())
                    throw new ArgumentException("Нет файлов для загрузки.");

                _logger.LogInformation($"Начало загрузки массива файлов пользователем ID {userId}.");

                var filesToAdd = new List<FileModel>();
                var userFilesToAdd = new List<UserFileModel>();

                foreach (var file in files)
                {
                    try
                    {
                        var relativePath = Path.GetDirectoryName(file.FileName);
                        var fileName = Path.GetFileName(file.FileName);
                        var (targetDiskId, targetSubFolderId, targetDiskPath) = await _diskSpaceService.SelectOptimalDirectoryAsync(file.Length);

                        if (string.IsNullOrEmpty(targetDiskPath))
                            throw new InvalidOperationException("Недостаточно места на всех доступных дисках.");

                        var fullPath = targetDiskPath;
                        var uniqueFileName = _diskSpaceService.GetUniqueFileName(fullPath, fileName);
                        var filePath = Path.Combine(fullPath, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        {
                            await file.CopyToAsync(stream);
                        }

                        var fileInfo = new FileInfo(filePath);

                        var newFile = new FileModel
                        {
                            FileName = fileName,
                            FilePath = filePath,
                            DiskId = targetDiskId,
                            Size = file.Length,
                            FileType = Path.GetExtension(fileName).ToLower(),
                            CreatedAt = fileInfo.CreationTime,
                            UpdatedAt = DateTime.Now,
                            Hash = await _diskSpaceService.GenerateFileHashAsync(filePath),
                            IconId = 7,
                        };

                        filesToAdd.Add(newFile);

                        _context.SubFolders
                            .Where(s => s.Id == targetSubFolderId)
                            .ExecuteUpdate(s => s.SetProperty(p => p.Count, p => p.Count + 1));

                        var userFile = new UserFileModel
                        {
                            UserId = userId,
                            Share = false
                        };
                        userFilesToAdd.Add(userFile);

                        if (!string.IsNullOrEmpty(relativePath))
                        {
                            var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                            var currentFolder = await _context.Folders.FirstOrDefaultAsync(f => f.Id == parentFolderId);

                            foreach (var part in pathParts)
                            {
                                var parentId = currentFolder?.Id;
                                var existingFolder = await _context.Folders
                                    .FirstOrDefaultAsync(f => f.Name == part &&
                                                              f.UserId == userId &&
                                                              f.ParentFolderId == parentId);

                                if (existingFolder == null)
                                {
                                    var newFolder = new FolderModel
                                    {
                                        Name = part,
                                        UserId = userId,
                                        ParentFolderId = currentFolder?.Id
                                    };

                                    _context.Folders.Add(newFolder);
                                    await _context.SaveChangesAsync();
                                    currentFolder = newFolder;
                                }
                                else
                                {
                                    currentFolder = existingFolder;
                                }
                            }

                            newFile.FolderId = currentFolder.Id;
                        }
                        else
                        {
                            newFile.FolderId = parentFolderId;
                        }
                        
                    }
                    catch (Exception exFile)
                    {
                        _logger.LogError(exFile, $"Ошибка при обработке файла {file.FileName}.");
                        throw;
                    }
                }

                if (filesToAdd.Any())
                {
                    _context.Files.AddRange(filesToAdd);
                    await _context.SaveChangesAsync();

                    for (int i = 0; i < filesToAdd.Count; i++)
                    {
                        userFilesToAdd[i].FileId = filesToAdd[i].Id;
                    }

                    _context.UserFiles.AddRange(userFilesToAdd);
                    await _context.SaveChangesAsync();
                }
                foreach (var fileModel in filesToAdd)
                {
                    _iconService.ScheduleIconGeneration(fileModel);
                }

                _logger.LogInformation($"Папка успешно загружена пользователем ID {userId}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при загрузке папки пользователем ID {userId}.");
                throw;
            }
            return "Файлы успешно загружены.";
        }

        // Метод для удаления файла из хранилища и БД
        public async Task<string> DeleteFile(int id, int userId)
        {
            try
            {
                var file = await _context.Files.FirstOrDefaultAsync(f => f.Id == id);

                if (file == null)
                {
                    _logger.LogWarning($"Файл с ID {id} не найден.");
                    throw new KeyNotFoundException("Файл не найден.");
                }
                var access = await _context.UserFiles.FirstOrDefaultAsync(uf => uf.FileId == id && uf.UserId == userId);
                if (access == null)
                {
                    _logger.LogWarning($"Нет прав на удаление файла: ID {id}, UserId {userId}");
                    throw new AccessViolationException("Нет прав на удаление файла.");
                }
                if (System.IO.File.Exists(file.FilePath))
                {
                    _logger.LogInformation($"Файл с ID {id} успешно удален с диска.");
                }
                file.IsDeleted = true;
                file.UpdatedAt = DateTime.Now;
                _context.Files.Update(file);
                await _context.SaveChangesAsync();

                return "Файл успешно удалён.";
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении файла с ID {id}.");
                throw;
            }
            catch (AccessViolationException ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении файла с ID {id}.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении файла с ID {id}.");
                throw;
            }
        }
        private FileDto MapToFileDto(FileModel file)
        {
            return new FileDto
            {
                Id = file.Id,
                FileName = file.FileName,
                Size = file.Size,
                FileType = file.FileType,
                CreatedAt = file.CreatedAt,
                IconId = file.IconId
            };
        }
    }
        
}