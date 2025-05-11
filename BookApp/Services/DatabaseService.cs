using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BookApp.Services
{
    public class DatabaseService
    {
        private readonly string _defaultBooksPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Books", "Default");
        private readonly ILogger<DatabaseService> _logger;

        public DatabaseService()
        {
            // Для простоты используем Console Logger. В реальном приложении можно внедрить ILogger через DI.
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _logger = loggerFactory.CreateLogger<DatabaseService>();
        }

        public void InitializeDefaultBooks()
        {
            try
            {
                using var db = CreateDbContext();
                if (db.Books.Any(b => b.IsDefault))
                {
                    _logger.LogInformation("Default books already initialized.");
                    return;
                }

                if (!Directory.Exists(_defaultBooksPath))
                {
                    Directory.CreateDirectory(_defaultBooksPath);
                    _logger.LogWarning("Default books directory created at {Path}. No books found.", _defaultBooksPath);
                    return;
                }

                var defaultBooks = Directory.GetFiles(_defaultBooksPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".epub") || f.EndsWith(".fb2") || f.EndsWith(".pdf"))
                    .Select(f => new Book
                    {
                        Title = Path.GetFileNameWithoutExtension(f),
                        FilePath = f,
                        Language = "Unknown",
                        PublicationYear = null,
                        PagesCount = 0,
                        IsDefault = true
                    })
                    .ToList();

                if (!defaultBooks.Any())
                {
                    _logger.LogWarning("No default books found in {Path}.", _defaultBooksPath);
                    return;
                }

                foreach (var book in defaultBooks)
                {
                    if (db.Books.Any(b => b.FilePath == book.FilePath))
                    {
                        _logger.LogInformation("Book with path {FilePath} already exists in database.", book.FilePath);
                        continue;
                    }

                    db.Books.Add(book);

                    var author = db.Authors.FirstOrDefault(a => a.LastName == "Unknown");
                    if (author == null)
                    {
                        author = new Author
                        {
                            LastName = "Unknown",
                            FirstName = "",
                            MiddleName = "",
                            BirthYear = null,
                            Country = "Unknown"
                        };
                        db.Authors.Add(author);
                    }

                    // Связь добавляется после сохранения книги, так как Book.Id нужен
                }

                db.SaveChanges();

                // Добавляем связи BookAuthor после сохранения книг
                foreach (var book in defaultBooks)
                {
                    var author = db.Authors.First(a => a.LastName == "Unknown");
                    var bookAuthor = new BookAuthor
                    {
                        BookId = book.Id,
                        AuthorId = author.Id
                    };
                    db.BookAuthors.Add(bookAuthor);
                }

                db.SaveChanges();
                _logger.LogInformation("Successfully initialized {Count} default books.", defaultBooks.Count);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error accessing default books directory {Path}.", _defaultBooksPath);
                throw;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving default books to database.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error initializing default books.");
                throw;
            }
        }

        public List<Book> GetDefaultBooks()
        {
            try
            {
                using var db = CreateDbContext();
                return db.Books
                    .Include(b => b.BookAuthors)
                    .ThenInclude(ba => ba.Author)
                    .Where(b => b.IsDefault)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving default books.");
                throw;
            }
        }

        public void SaveBook(Book book)
        {
            try
            {
                using var db = CreateDbContext();
                db.Books.Add(book);
                db.SaveChanges();
                _logger.LogInformation("Book {Title} saved successfully.", book.Title);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving book {Title}.", book.Title);
                throw;
            }
        }

        public DisplaySetting GetUserSettings(int userId)
        {
            try
            {
                using var db = CreateDbContext();
                return db.DisplaySettings.FirstOrDefault(ds => ds.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving settings for user {UserId}.", userId);
                throw;
            }
        }

        public void SaveDisplaySettings(DisplaySetting settings)
        {
            try
            {
                using var db = CreateDbContext();
                var userExists = db.Users.Any(u => u.Id == settings.UserId);
                if (!userExists)
                {
                    _logger.LogError("User {UserId} does not exist.", settings.UserId);
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
                _logger.LogInformation("Display settings saved for user {UserId}.", settings.UserId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving display settings for user {UserId}.", settings.UserId);
                throw;
            }
        }

        public List<ReadingHistory> GetReadingHistory(int userId)
        {
            try
            {
                using var db = CreateDbContext();
                return db.ReadingHistory
                    .Include(rh => rh.Book)
                    .ThenInclude(b => b.BookAuthors)
                    .ThenInclude(ba => ba.Author)
                    .Where(rh => rh.UserId == userId)
                    .OrderByDescending(rh => rh.LastReadDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving reading history for user {UserId}.", userId);
                throw;
            }
        }

        public void SaveRating(Rating rating)
        {
            try
            {
                using var db = CreateDbContext();
                var existingRating = db.Ratings
                    .FirstOrDefault(r => r.UserId == rating.UserId && r.BookId == rating.BookId);

                if (existingRating == null)
                {
                    db.Ratings.Add(rating);
                }
                else
                {
                    existingRating.RatingValue = rating.RatingValue;
                    existingRating.RatingDate = DateTime.UtcNow;
                }

                db.SaveChanges();
                _logger.LogInformation("Rating saved for book {BookId} by user {UserId}.", rating.BookId, rating.UserId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving rating for book {BookId} by user {UserId}.", rating.BookId, rating.UserId);
                throw;
            }
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