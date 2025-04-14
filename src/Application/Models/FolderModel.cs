using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Application.Models
{
    public class FolderModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public UserModel User { get; set; }
        public int? ParentFolderId { get; set; }
        [ForeignKey("ParentFolderId")]
        [JsonIgnore]
        public FolderModel? ParentFolder { get; set; }
        public ICollection<FolderModel> SubFolders { get; set; } = new List<FolderModel>();
        public ICollection<FileModel> Files { get; set; } = new List<FileModel>();
    }
}
