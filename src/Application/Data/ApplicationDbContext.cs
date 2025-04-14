using Microsoft.EntityFrameworkCore;
using Application.Models;
namespace Application.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        
        public DbSet<DiskModel> Disks { get; set; }
        public DbSet<UserModel> Users { get; set; }
        public DbSet<UserFileModel> UserFiles { get; set; }
        public DbSet<FolderModel> Folders { get; set; }
        public DbSet<ServiceFolderModel> SubFolders { get; set; }
        public DbSet<FileIcon> FileIcons { get; set; }
        public DbSet<FileModel> Files { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Настройки таблицы Files
            modelBuilder.Entity<FileModel>(entity =>
            {
                entity.HasKey(f => f.Id); // Уникальный ключ

                entity.Property(f => f.FileName)
                    .IsRequired()
                    .HasMaxLength(255); // Ограничение длины имени файла

                entity.Property(f => f.FilePath)
                    .IsRequired();

                entity.Property(f => f.Size)
                    .IsRequired();

                entity.Property(f => f.FileType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(f => f.CreatedAt)
                    .IsRequired();
                // Связь файла с папкой
                entity.HasOne(f => f.Folder)
                      .WithMany(folder => folder.Files)
                      .HasForeignKey(f => f.FolderId)
                      .OnDelete(DeleteBehavior.Cascade);
                // Связь файла с иконкой
                entity.HasOne(f => f.Icon)
                      .WithMany()
                      .HasForeignKey(f => f.IconId)
                      .OnDelete(DeleteBehavior.Restrict);

            });
            // Настройки для таблицы Disk
            modelBuilder.Entity<DiskModel>(entity =>
            {
                entity.HasKey(d => d.Id); // Уникальный ключ
                entity.Property(d => d.Name).IsRequired().HasMaxLength(255); // Название диска
            });
            modelBuilder.Entity<UserModel>(entity => 
            { 
                entity.HasKey(u => u.Id); 
            });
            modelBuilder.Entity<UserFileModel>(entity =>
                entity.HasKey(u => u.Id));

            modelBuilder.Entity<ServiceFolderModel>(entity =>
                entity.HasKey(sf => sf.Id));
            // Настройка связи между SubFolderModel и DiskModel
            modelBuilder.Entity<ServiceFolderModel>()
                .HasOne(sf => sf.Disk)
                .WithMany()
                .HasForeignKey(sf => sf.DiskId)
                .OnDelete(DeleteBehavior.Cascade);
            // Настройка связи между UserFile и FileModel
            modelBuilder.Entity<UserFileModel>()
                .HasOne(uf => uf.File)
                .WithMany()
                .HasForeignKey(uf => uf.FileId)
                .OnDelete(DeleteBehavior.Cascade);

            // Настройка связи между UserFile и UserModel
            modelBuilder.Entity<UserFileModel>()
                .HasOne(uf => uf.User)
                .WithMany() // У пользователя может быть много связанных файлов
                .HasForeignKey(uf => uf.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Запрещаем каскадное удаление

            // Настройка связи между FileModel и DiskModel
            modelBuilder.Entity<FileModel>()
                .HasOne(f => f.Disk)
                .WithMany()
                .HasForeignKey(f => f.DiskId)
                .OnDelete(DeleteBehavior.Cascade); // Разрешаем каскадное удаление

            // Конфигурация папки
            modelBuilder.Entity<FolderModel>(entity =>
            {
                entity.ToTable("Folders");

                entity.HasKey(f => f.Id);

                entity.Property(f => f.Name)
                      .IsRequired()
                      .HasMaxLength(255);

                // Связь папки с пользователем
                entity.HasOne(f => f.User)
                      .WithMany() // Если у UserModel нет ICollection<FolderModel> Folders
                      .HasForeignKey(f => f.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Рекурсивная связь - родительская папка
                entity.HasOne(f => f.ParentFolder)
                      .WithMany(f => f.SubFolders)
                      .HasForeignKey(f => f.ParentFolderId)
                      .OnDelete(DeleteBehavior.Restrict); // Защита от каскадного удаления

                // Связь папки с файлами
                entity.HasMany(f => f.Files)
                      .WithOne(file => file.Folder)
                      .HasForeignKey(file => file.FolderId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<FileIcon>().HasIndex(i => i.FileType);

        }
    }
}
