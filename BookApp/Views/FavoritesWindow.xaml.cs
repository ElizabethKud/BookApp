using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BookApp.Views
{
    public partial class FavoritesWindow : Window
    {
        private readonly int _userId;

        public FavoritesWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadFavorites();
        }

        private void LoadFavorites()
        {
            try
            {
                using var db = CreateDbContext();
                var favorites = db.FavoriteBooks
                    .Include(f => f.Book)
                        .ThenInclude(b => b.BookAuthors)
                        .ThenInclude(ba => ba.Author)
                    .Where(f => f.UserId == _userId)
                    .OrderBy(f => f.Book.Title)
                    .ToList();
                FavoritesGrid.ItemsSource = favorites;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки избранных книг: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveFromFavorites_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.Tag is FavoriteBook favorite)
            {
                try
                {
                    using var db = CreateDbContext();
                    var favoriteToRemove = db.FavoriteBooks
                        .FirstOrDefault(f => f.Id == favorite.Id && f.UserId == _userId);
                    if (favoriteToRemove != null)
                    {
                        db.FavoriteBooks.Remove(favoriteToRemove);
                        db.SaveChanges();
                        LoadFavorites(); // Перезагружаем список
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления книги из избранного: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    checkBox.IsChecked = true; // Возвращаем чекбокс в исходное состояние
                }
            }
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