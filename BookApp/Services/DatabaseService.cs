using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookApp.Services
{
    public class DatabaseService
    {
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
            var existing = db.DisplaySettings.FirstOrDefault(ds => ds.UserId == settings.UserId);
            if (existing != null)
            {
                existing.FontFamily = settings.FontFamily;
                existing.FontSize = settings.FontSize;
                existing.BackgroundColor = settings.BackgroundColor;
                existing.FontColor = settings.FontColor;
            }
            else
            {
                db.DisplaySettings.Add(settings);
            }
            db.SaveChanges();
        }

        private AppDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=BookApp;Username=postgres;Include Error Detail=true");
            optionsBuilder.UseLazyLoadingProxies();
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}