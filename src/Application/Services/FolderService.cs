using Application.Data;
using Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class FolderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FolderService> _logger;
        private readonly DiskSpaceService _diskSpaceService;

        public FolderService(ApplicationDbContext context, ILogger<FolderService> logger, DiskSpaceService diskSpaceService)
        {
            _context = context;
            _logger = logger;
            _diskSpaceService = diskSpaceService;
        }

        // Получение всех папок пользователя
        public async Task<List<FolderModel>> GetUserFoldersAsync(int userId)
        {
            try
            {
                _logger.LogInformation($"Начало получения папок пользователя с ID {userId}.");

                var folders = await _context.Folders
                    .Where(f => f.UserId == userId && f.ParentFolderId == null)
                    .ToListAsync();

                if (folders == null || !folders.Any())
                {
                    _logger.LogWarning($"У пользователя с ID {userId} нет папок.");
                    return new List<FolderModel>();
                }

                await LoadSubFoldersAsync(folders);
                _logger.LogInformation($"Папки пользователя с ID {userId} успешно загружены.");
                return folders;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении папок пользователя с ID {userId}.");
                throw;
            }
        }

        private async Task LoadSubFoldersAsync(ICollection<FolderModel> folders)
        {
            foreach (var folder in folders)
            {
                try
                {
                    folder.SubFolders = await _context.Folders
                        .Where(f => f.ParentFolderId == folder.Id)
                        .ToListAsync();

                    folder.Files = await _context.Files
                        .Where(f => f.FolderId == folder.Id)
                        .ToListAsync();

                    if (folder.SubFolders.Any())
                    {
                        await LoadSubFoldersAsync(folder.SubFolders);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Ошибка загрузки содержимого папки с ID {folder.Id}.");
                }
            }
        }

        public async Task<List<FolderDto>> DTOGetUserFoldersAsync(int userId)
        {
            try
            {
                _logger.LogInformation($"Начало получения папок пользователя с ID {userId}.");

                // Загружаем корневые папки пользователя
                var rootFolders = await _context.Folders
                    .Where(f => f.UserId == userId && f.ParentFolderId == null)
                    .Include(f => f.SubFolders) // Включаем подпапки
                    .Include(f => f.Files)      // Включаем файлы
                    .ToListAsync();

                if (rootFolders == null || !rootFolders.Any())
                {
                    _logger.LogWarning($"У пользователя с ID {userId} нет папок.");
                    return new List<FolderDto>();
                }

                // Преобразуем модели в DTO
                var folderDtos = rootFolders.Select(folder => MapToFolderDto(folder)).ToList();

                // Рекурсивно загружаем подпапки
                foreach (var folderDto in folderDtos)
                {
                    await DTOLoadSubFoldersAsync(folderDto);
                }

                _logger.LogInformation($"Папки пользователя с ID {userId} успешно загружены.");
                return folderDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при получении папок пользователя с ID {userId}.");
                throw;
            }
        }

        private async Task DTOLoadSubFoldersAsync(FolderDto folderDto)
        {
            try
            {
                // Загружаем подпапки
                var subFolders = await _context.Folders
                    .Where(f => f.ParentFolderId == folderDto.Id)
                    .Include(f => f.Files) // Включаем файлы для каждой подпапки
                    .ToListAsync();

                folderDto.SubFolders = subFolders.Select(subFolder => MapToFolderDto(subFolder)).ToList();

                // Загружаем файлы для текущей папки
                var files = await _context.Files
                    .Where(f => f.FolderId == folderDto.Id)
                    .ToListAsync();

                folderDto.Files = files.Select(file => MapToFileDto(file)).ToList();

                // Рекурсивно загружаем подпапки для каждой подпапки
                foreach (var subFolderDto in folderDto.SubFolders)
                {
                    await DTOLoadSubFoldersAsync(subFolderDto);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка загрузки содержимого папки с ID {folderDto.Id}.");
            }
        }

        // Добавление новой папки
        public async Task<FolderModel> AddFolderAsync(string name, int userId, int? parentFolderId = null)
        {
            try
            {
                _logger.LogInformation($"Попытка создать папку '{name}' для пользователя ID {userId}.");

                var disk = await _context.Disks.FirstOrDefaultAsync();
                if (disk == null)
                    throw new InvalidOperationException("Нет доступных дисков.");

                var folder = new FolderModel
                {
                    Name = name,
                    UserId = userId,
                    ParentFolderId = parentFolderId
                };

                if (parentFolderId.HasValue)
                {
                    var parentFolder = await _context.Folders
                        .Include(f => f.SubFolders)
                        .FirstOrDefaultAsync(f => f.Id == parentFolderId.Value && f.UserId == userId);

                    if (parentFolder == null)
                        throw new InvalidOperationException("Родительская папка не найдена.");

                    parentFolder.SubFolders.Add(folder);
                    _context.Folders.Update(parentFolder);
                }

                _context.Folders.Add(folder);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Папка '{name}' успешно создана для пользователя ID {userId}.");
                return folder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при создании папки '{name}' для пользователя ID {userId}.");
                throw;
            }
        }

        // Удаление папки с содержимым
        public async Task DeleteFolderAsync(int folderId)
        {
            try
            {
                _logger.LogInformation($"Начало удаления папки с ID {folderId}.");

                var folder = await _context.Folders
                    .Include(f => f.SubFolders)
                    .Include(f => f.Files)
                    .FirstOrDefaultAsync(f => f.Id == folderId);

                if (folder == null)
                {
                    _logger.LogWarning($"Папка с ID {folderId} не найдена.");
                    throw new KeyNotFoundException("Папка не найдена.");
                }

                foreach (var subFolder in folder.SubFolders.ToList())
                {
                    await DeleteFolderAsync(subFolder.Id);
                }

                foreach (var file in folder.Files.ToList())
                {
                    _context.Files.Remove(file);

                    if (System.IO.File.Exists(file.FilePath))
                    {
                        System.IO.File.Delete(file.FilePath);
                        _logger.LogInformation($"Файл с ID {file.Id} успешно удален с диска.");
                    }
                    else
                    {
                        _logger.LogWarning($"Файл с ID {file.Id} не найден на диске по пути {file.FilePath}.");
                    }
                }

                _context.Folders.Remove(folder);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Папка с ID {folderId} успешно удалена.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при удалении папки с ID {folderId}.");
                throw;
            }
        }
        private FolderDto MapToFolderDto(FolderModel folder)
        {
            return new FolderDto
            {
                Id = folder.Id,
                Name = folder.Name,
                ParentFolderId = folder.ParentFolderId,
                SubFolders = new List<FolderDto>(), // Подпапки будут заполнены позже
                Files = folder.Files?.Select(MapToFileDto).ToList() ?? new List<FileDto>()
            };
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
