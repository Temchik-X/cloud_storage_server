using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Models
{
    public class UserFileModel
    {
        [Key]
        public int Id { get; set; }
        // Связь с файлом
        public int FileId { get; set; }
        [ForeignKey("FileId")]
        public FileModel File { get; set; }
        // Связь с файлом
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public UserModel User { get; set; }
        // Флаг для будущего использования
        public bool Share { get; set; }
    }
}
