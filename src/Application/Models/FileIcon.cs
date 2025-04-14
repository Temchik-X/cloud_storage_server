using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Models
{
    public class FileIcon
    {
        public int Id { get; set; }
        public string FileType { get; set; } // image/png, video/mp4, application/pdf и т.д.
        public byte[] IconData { get; set; } // Данные иконки (image/png)
        public bool IsGenerated { get; set; } // true - сгенерированная, false - стандартная
    }

}
