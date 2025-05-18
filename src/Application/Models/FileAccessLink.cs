using Application.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

public class FileAccessLink
{
    public int Id { get; set; }
    public string Url { get; set; }    // единственная условная «ссылка» (token + путь)
    public DateTime CreatedAt { get; set; }  // когда сгенерирована
    public int UserId { get; set; }  // кто сгенерировал

    // связь на файл
    public int FileId { get; set; }
    [ForeignKey(nameof(FileId))]
    public FileModel File { get; set; }
}