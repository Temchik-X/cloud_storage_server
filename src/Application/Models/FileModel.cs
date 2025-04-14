using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Application.Models
{
    public class FileModel
    {
        public int Id { get; set; } // Уникальный идентификатор файла
        public int DiskId {  get; set; }
        [ForeignKey("DiskId")]
        public DiskModel Disk { get; set; }

        public string FileName { get; set; } // Имя файла

        public string FilePath { get; set; } // Полный путь к файлу на диске

        public long Size { get; set; } // Размер файла в байтах

        public DateTime CreatedAt { get; set; } // Дата создания файла

        public DateTime UpdatedAt { get; set; } // Дата последнего обновления

        public string FileType { get; set; } // Тип файла (например, "image/png", "video/mp4")

        public string Hash { get; set; } // Хеш файла (для проверки целостности)
                                         // Связь с папкой (необязательная)
        public int? FolderId { get; set; }
        [ForeignKey("FolderId")]
        [JsonIgnore]
        public FolderModel? Folder { get; set; }

        public int? IconId { get; set; }
        [ForeignKey("IconId")]
        public FileIcon? Icon { get; set; }
    }
}
