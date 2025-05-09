using System.Windows.Input;
using BookApp.Services;

namespace BookApp.ViewModels
{
    public class ReaderViewModel : BaseViewModel
    {
        private object _currentContent;
        private readonly int _userId;
        private readonly BookContentViewModel _bookContentViewModel;
        private readonly SettingsViewModel _settingsViewModel;
        private readonly FavoritesViewModel _favoritesViewModel;
        private readonly HistoryViewModel _historyViewModel;
        private readonly RecommendationsViewModel _recommendationsViewModel;

        public object CurrentContent
        {
            get => _currentContent;
            set { _currentContent = value; OnPropertyChanged(); }
        }

        public ICommand OpenBookCommand { get; }
        public ICommand ShowSettingsCommand { get; }
        public ICommand ShowFavoritesCommand { get; }
        public ICommand ShowHistoryCommand { get; }
        public ICommand ShowRecommendationsCommand { get; }

        public ReaderViewModel(int userId)
        {
            _userId = userId;
            _bookContentViewModel = new BookContentViewModel(_userId);
            _settingsViewModel = new SettingsViewModel(_userId);
            _favoritesViewModel = new FavoritesViewModel(_userId);
            _historyViewModel = new HistoryViewModel(_userId);
            _recommendationsViewModel = new RecommendationsViewModel(_userId);

            OpenBookCommand = new RelayCommand(() => CurrentContent = _bookContentViewModel);
            ShowSettingsCommand = new RelayCommand(() => CurrentContent = _settingsViewModel);
            ShowFavoritesCommand = new RelayCommand(() => CurrentContent = _favoritesViewModel);
            ShowHistoryCommand = new RelayCommand(() => CurrentContent = _historyViewModel);
            ShowRecommendationsCommand = new RelayCommand(() => CurrentContent = _recommendationsViewModel);

            CurrentContent = _bookContentViewModel;
        }
    }
}