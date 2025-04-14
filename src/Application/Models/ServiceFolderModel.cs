using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Models
{
    public class ServiceFolderModel
    {
        [Key]
        public int Id { get; set; }
        // Связь с файлом
        public int DiskId { get; set; }
        [ForeignKey("DiskId")]
        public DiskModel Disk { get; set; }
        public string FolderName { get; set; }
        // Флаг для будущего использования
        public int Count { get; set; }
    }
}
