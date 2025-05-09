using System.Collections.ObjectModel;
using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookApp.ViewModels
{
    public class FavoritesViewModel : BaseViewModel
    {
        private readonly int _userId;
        private ObservableCollection<Book> _favoriteBooks;

        public ObservableCollection<Book> FavoriteBooks
        {
            get => _favoriteBooks;
            set { _favoriteBooks = value; OnPropertyChanged(); }
        }

        public FavoritesViewModel(int userId)
        {
            _userId = userId;
            LoadFavorites();
        }

        private void LoadFavorites()
        {
            using var db = CreateDbContext();
            FavoriteBooks = new ObservableCollection<Book>(
                db.FavoriteBooks
                    .Where(f => f.UserId == _userId)
                    .Select(f => f.Book)
                    .ToList());
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