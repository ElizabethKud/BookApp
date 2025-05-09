using System.Collections.ObjectModel;
using BookApp.Services;

namespace BookApp.ViewModels
{
    public class RecommendedBookViewModel
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Genre { get; set; }
        public double AverageRating { get; set; }
    }

    public class RecommendationsViewModel : BaseViewModel
    {
        private readonly RecommendationService _recommendationService;
        private readonly int _userId;
        private ObservableCollection<RecommendedBookViewModel> _recommendedBooks;

        public ObservableCollection<RecommendedBookViewModel> RecommendedBooks
        {
            get => _recommendedBooks;
            set { _recommendedBooks = value; OnPropertyChanged(); }
        }

        public RecommendationsViewModel(int userId)
        {
            _userId = userId;
            _recommendationService = new RecommendationService();
            LoadRecommendations();
        }

        private void LoadRecommendations()
        {
            RecommendedBooks = new ObservableCollection<RecommendedBookViewModel>(
                _recommendationService.GetRecommendations(_userId) as IEnumerable<RecommendedBookViewModel>);
        }
    }
}