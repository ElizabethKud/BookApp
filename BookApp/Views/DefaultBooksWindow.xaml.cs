using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BookApp.Data;
using BookApp.Models;
using BookApp.Services;
using Microsoft.EntityFrameworkCore;

namespace BookApp.Views
{
    public partial class DefaultBooksWindow : Window
    {
        private readonly DatabaseService _dbService = new DatabaseService();
        private readonly int _userId;
        private readonly MainWindow _mainWindow;

        public static int CurrentUserId { get; private set; }

        public DefaultBooksWindow(int userId, MainWindow mainWindow)
        {
            InitializeComponent();
            _userId = userId;
            CurrentUserId = userId;
            _mainWindow = mainWindow;
            LoadBooks();
        }

        private void LoadBooks()
        {
            try
            {
                using var db = CreateDbContext();
                var books = db.Books
                    .Include(b => b.BookAuthors).ThenInclude(ba => ba.Author)
                    .Include(b => b.Ratings)
                    .Include(b => b.ReadingHistory)
                    .Where(b => b.IsDefault)
                    .ToList();
                BooksGrid.ItemsSource = books;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки книг: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is Book book)
            {
                UpdateReadingStatus(book.Id, true);
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is Book book)
            {
                UpdateReadingStatus(book.Id, false);
            }
        }

        private void UpdateReadingStatus(int bookId, bool isRead)
        {
            try
            {
                using var db = CreateDbContext();
                var history = db.ReadingHistory
                    .FirstOrDefault(rh => rh.UserId == _userId && rh.BookId == bookId);

                if (history == null)
                {
                    history = new ReadingHistory
                    {
                        UserId = _userId,
                        BookId = bookId,
                        LastReadPage = 1,
                        LastReadPosition = "0",
                        LastReadDate = DateTime.UtcNow,
                        IsRead = isRead
                    };
                    db.ReadingHistory.Add(history);
                }
                else
                {
                    history.IsRead = isRead;
                    history.LastReadDate = DateTime.UtcNow;
                }
                db.SaveChanges();
                Debug.WriteLine($"Статус 'Прочитано' для книги {bookId} обновлён: {isRead}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления статуса 'Прочитано': {ex.Message}");
                MessageBox.Show($"Ошибка обновления статуса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Book book)
            {
                _mainWindow.OpenBook(book.FilePath);
                Close();
            }
        }

        private void RateBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Book book)
            {
                var rateWindow = new RateBookWindow(_userId, book.Id, book.Title);
                rateWindow.ShowDialog();
                LoadBooks();
            }
        }

        private void AddToFavorites_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Book book)
            {
                try
                {
                    using var db = CreateDbContext();
                    var existingFavorite = db.FavoriteBooks
                        .FirstOrDefault(f => f.UserId == _userId && f.BookId == book.Id);
                    if (existingFavorite == null)
                    {
                        var favorite = new FavoriteBook
                        {
                            UserId = _userId,
                            BookId = book.Id,
                            DateAdded = DateTime.UtcNow
                        };
                        db.FavoriteBooks.Add(favorite);
                        db.SaveChanges();
                        MessageBox.Show("Книга добавлена в избранное!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Книга уже в избранном.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении в избранное: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BooksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Оставляем пустым
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
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