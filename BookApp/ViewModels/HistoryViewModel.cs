using System.Collections.ObjectModel;
using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookApp.ViewModels
{
    public class HistoryViewModel : BaseViewModel
    {
        private readonly int _userId;
        private ObservableCollection<ReadingHistory> _readingHistory;

        public ObservableCollection<ReadingHistory> ReadingHistory
        {
            get => _readingHistory;
            set { _readingHistory = value; OnPropertyChanged(); }
        }

        public HistoryViewModel(int userId)
        {
            _userId = userId;
            LoadHistory();
        }

        private void LoadHistory()
        {
            using var db = CreateDbContext();
            ReadingHistory = new ObservableCollection<ReadingHistory>(
                db.ReadingHistory
                    .Where(h => h.UserId == _userId)
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