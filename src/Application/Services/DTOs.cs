using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class FolderDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentFolderId { get; set; }
        public ICollection<FolderDto> SubFolders { get; set; } = new List<FolderDto>();
        public ICollection<FileDto> Files { get; set; } = new List<FileDto>();
    }
    public class FolderInfoDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? ParentFolderId { get; set; }
        public int? IconId { get; set; }
    }

    public class FileDto
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public long Size { get; set; }
        public string FileType { get; set; }
        public int? IconId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    // DTO для запроса добавления папки(FolderController.cs)
    public class FolderRequest
    {
        public string Name { get; set; }
        public int? ParentFolderId { get; set; }
    }

    public class DirectoryDto
    {
        public string Path { get; set; }
        public int DiskId { get; set; }
    }
    /// <summary>
    /// Модель запроса для добавления пользователя
    /// </summary>
    public class UserRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public bool IsAdmin { get; set; } = false; // По умолчанию роль "user"
        public string Email { get; set; }
        public int? FreeSpace { get; set; }
    }

    /// <summary>
    /// Модель запроса для изменения пароля
    /// </summary>
    public class ChangePasswordRequest
    {
        public string NewPassword { get; set; }
    }
    public class GeneralDiskInfo
    {
        public int DiskCount { get; set; }
        public double GeneralFreeSpace { get; set; }
        public int GeneralFileCount { get; set; }
        public double GeneralDiskSpace { get; set; }
        public GeneralDiskInfo()
        {
            DiskCount = 0;
            GeneralFreeSpace = 0;   
            GeneralFileCount = 0;
            GeneralDiskSpace = 0;
        }
    }
    public class JsonRecord
    {
        public string Name { get; set; } = string.Empty;
        public ICollection<FolderInfoDto> Folders { get; set; } = new List<FolderInfoDto>();
        public ICollection<FileDto> Files { get; set; } = new List<FileDto>();
        public JsonRecord(string folderName, List<FolderInfoDto> folders, List<FileDto> files) {
            Name = folderName;
            Folders = folders; 
            Files = files;
        }

    }
    public class StreamVideoResult
    {
        public string FilePath { get; set; }    // полный путь к файлу на диске
        public string ContentType { get; set; } // например "video/mp4"
        public string FileName { get; set; }    // название файла для заголовка Content-Disposition
    }
}
