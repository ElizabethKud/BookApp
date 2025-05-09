using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;

namespace BookApp.Services
{
    public class DatabaseService
    {
        public void SaveBook(Book book, List<string> genres)
        {
            using var db = CreateDbContext();
            db.Books.Add(book);
            db.SaveChanges();

            foreach (var genreName in genres)
            {
                var genre = db.Genres.FirstOrDefault(g => g.Name == genreName);
                if (genre == null)
                {
                    genre = new Genre { Name = genreName };
                    db.Genres.Add(genre);
                    db.SaveChanges();
                }
                db.BookGenres.Add(new BookGenre { BookId = book.Id, GenreId = genre.Id });
            }
            db.SaveChanges();
        }

        public void SaveRating(int userId, int bookId, int rating)
        {
            using var db = CreateDbContext();
            var existingRating = db.Ratings.FirstOrDefault(r => r.UserId == userId && r.BookId == bookId);
            if (existingRating != null)
            {
                existingRating.Rating = rating;
                existingRating.RatingDate = DateTime.UtcNow;
            }
            else
            {
                db.Ratings.Add(new Rating
                {
                    UserId = userId,
                    BookId = bookId,
                    Rating = rating,
                    RatingDate = DateTime.UtcNow
                });
            }
            db.SaveChanges();
        }

        public DisplaySetting GetUserSettings(int userId)
        {
            using var db = CreateDbContext();
            return db.DisplaySettings.FirstOrDefault(ds => ds.UserId == userId) ?? new DisplaySetting { UserId = userId };
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