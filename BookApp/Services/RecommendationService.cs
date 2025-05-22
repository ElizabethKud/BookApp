using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BookApp.Services
{
    public class RecommendationService
    {
        public List<Book> GetRecommendations(int userId)
        {
            try
            {
                using var db = CreateDbContext();
                
                System.Diagnostics.Debug.WriteLine($"Получение рекомендаций для UserId: {userId}");

                var highRatedBooks = db.Ratings
                    .Where(r => r.UserId == userId && r.RatingValue >= 8)
                    .Select(r => r.BookId)
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"Найдено высокооцененных книг: {highRatedBooks.Count}");

                var favoriteBooks = db.FavoriteBooks
                    .Where(f => f.UserId == userId)
                    .Select(f => f.BookId)
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"Найдено избранных книг: {favoriteBooks.Count}");

                var preferredBookIds = highRatedBooks
                    .Union(favoriteBooks)
                    .Distinct()
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"Всего предпочтительных книг: {preferredBookIds.Count}");
                
                var preferredGenres = db.BookGenres
                    .Where(bg => preferredBookIds.Contains(bg.BookId))
                    .Select(bg => bg.GenreId)
                    .Distinct()
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"Найдено жанров: {preferredGenres.Count}");
                
                var readBookIds = db.ReadingHistory
                    .Where(rh => rh.UserId == userId)
                    .Select(rh => rh.BookId)
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"Прочитанных книг: {readBookIds.Count}");

                var candidateBooks = db.Books
                    .Where(b => !readBookIds.Contains(b.Id) && !favoriteBooks.Contains(b.Id))
                    .Include(b => b.BookGenres)
                    .Include(b => b.Ratings)
                    .Where(b => b.Ratings.Any()) // Только книги с рейтингами
                    .ToList();

                if (preferredGenres.Any())
                {
                    candidateBooks = candidateBooks
                        .Where(b => b.BookGenres.Any(bg => preferredGenres.Contains(bg.GenreId)))
                        .ToList();
                }
                System.Diagnostics.Debug.WriteLine($"Кандидатов на рекомендацию: {candidateBooks.Count}");
                
                var recommendedBooks = candidateBooks
                    .Select(b => new
                    {
                        Book = b,
                        AverageRating = b.Ratings.Average(r => (float)r.RatingValue)
                    })
                    .Where(b => b.AverageRating >= 7)
                    .OrderByDescending(b => b.AverageRating)
                    .Take(10)
                    .Select(b => b.Book)
                    .ToList();
                System.Diagnostics.Debug.WriteLine($"Рекомендованных книг: {recommendedBooks.Count}");
                
                if (!recommendedBooks.Any())
                {
                    recommendedBooks = db.Books
                        .Where(b => !readBookIds.Contains(b.Id) && !favoriteBooks.Contains(b.Id))
                        .Include(b => b.Ratings)
                        .Where(b => b.Ratings.Any() && b.Ratings.Average(r => (float)r.RatingValue) >= 7)
                        .OrderByDescending(b => b.Ratings.Average(r => (float)r.RatingValue))
                        .Take(10)
                        .ToList();
                    System.Diagnostics.Debug.WriteLine($"Резервных рекомендаций: {recommendedBooks.Count}");
                }

                foreach (var book in recommendedBooks)
                {
                    var avgRating = book.Ratings.Any() ? book.Ratings.Average(r => (float)r.RatingValue) : 0f;
                    System.Diagnostics.Debug.WriteLine($"Книга: {book.Title}, Средний рейтинг: {avgRating}");
                }

                return recommendedBooks;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка получения рекомендаций: {ex.Message}");
                return new List<Book>();
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