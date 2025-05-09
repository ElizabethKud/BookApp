using System.Collections.Generic;
using System.Linq;
using BookApp.Data;
using BookApp.Models;
using BookApp.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace BookApp.Services
{
    public class RecommendationService
    {
        public List<RecommendedBookViewModel> GetRecommendations(int userId)
        {
            using var db = CreateDbContext();

            // Получаем жанры из избранных книг и истории чтения
            var favoriteGenres = db.FavoriteBooks
                .Where(f => f.UserId == userId && f.Book != null)
                .SelectMany(f => f.Book.BookGenres)
                .Select(bg => bg.GenreId)
                .ToList();

            var historyGenres = db.ReadingHistory
                .Where(h => h.UserId == userId && h.Book != null)
                .SelectMany(h => h.Book.BookGenres)
                .Select(bg => bg.GenreId)
                .ToList();

            var preferredGenres = favoriteGenres.Union(historyGenres).Distinct().ToList();

            // Если есть предпочтительные жанры, рекомендуем книги этих жанров
            if (preferredGenres.Any())
            {
                return db.BookGenres
                    .Where(bg => preferredGenres.Contains(bg.GenreId) && bg.Book != null && bg.Genre != null)
                    .Select(bg => new RecommendedBookViewModel
                    {
                        Title = bg.Book.Title ?? "Без названия",
                        Author = bg.Book.Author ?? "Неизвестен",
                        Genre = bg.Genre.Name ?? "Неизвестно",
                        AverageRating = bg.Book.Ratings.Any() ? bg.Book.Ratings.Average(r => r.RatingValue) : 0
                    })
                    .Take(10)
                    .ToList();
            }

            // Иначе рекомендуем популярные книги (по рейтингу)
            return db.Books
                .Where(b => b != null)
                .Select(b => new RecommendedBookViewModel
                {
                    Title = b.Title ?? "Без названия",
                    Author = b.Author ?? "Неизвестен",
                    Genre = b.BookGenres.FirstOrDefault().Genre.Name ?? "Неизвестно",
                    AverageRating = b.Ratings.Any() ? b.Ratings.Average(r => r.RatingValue) : 0
                })
                .OrderByDescending(b => b.AverageRating)
                .Take(10)
                .ToList();
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
