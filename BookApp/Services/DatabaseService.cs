    using System.IO;
    using BookApp.Data;
    using BookApp.Models;
    using Microsoft.EntityFrameworkCore;

    namespace BookApp.Services
    {
        public class DatabaseService
        {
            private readonly string _defaultBooksPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Books", "Default");
            
            public void InitializeDefaultBooks()
            {
                using var db = CreateDbContext();
                if (!db.Books.Any())
                {
                    var defaultBooks = Directory.GetFiles(_defaultBooksPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".epub") || f.EndsWith(".fb2") || f.EndsWith(".pdf"))
                        .Select(f => new Book
                        {
                            Title = Path.GetFileNameWithoutExtension(f),
                            FilePath = f,
                            Language = "Unknown",
                            PublicationYear = null,
                            PagesCount = 0 // Можно добавить логику для извлечения метаданных
                        });

                    db.Books.AddRange(defaultBooks);
                    db.SaveChanges();
                }
            }
            
            private AppDbContext CreateDbContext()
            {
                var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
                optionsBuilder.UseNpgsql("Host=localhost;Database=BookApp;Username=postgres;Include Error Detail=true");
                optionsBuilder.UseLazyLoadingProxies();
                return new AppDbContext(optionsBuilder.Options);
            }
            
            public void SaveBook(Book book)
            {
                using var db = CreateDbContext();
                db.Books.Add(book);
                db.SaveChanges();
            }

            public DisplaySetting GetUserSettings(int userId)
            {
                using var db = CreateDbContext();
                return db.DisplaySettings.FirstOrDefault(ds => ds.UserId == userId);
            }

            public void SaveDisplaySettings(DisplaySetting settings)
            {
                using var db = CreateDbContext();
        
                // Проверяем существование пользователя
                var userExists = db.Users.Any(u => u.Id == settings.UserId);
                if (!userExists)
                {
                    throw new ArgumentException("Пользователь не существует");
                }

                var existingSettings = db.DisplaySettings
                    .FirstOrDefault(ds => ds.UserId == settings.UserId);

                if (existingSettings == null)
                {
                    db.DisplaySettings.Add(settings);
                }
                else
                {
                    existingSettings.BackgroundColor = settings.BackgroundColor;
                    existingSettings.FontColor = settings.FontColor;
                    existingSettings.FontSize = settings.FontSize;
                    existingSettings.FontFamily = settings.FontFamily;
                }
        
                db.SaveChanges();
            }
        }
    }