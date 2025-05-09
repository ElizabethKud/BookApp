using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BookApp.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private object _currentView;
        private int _currentUserId;

        public object CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            CurrentView = new LoginRegisterViewModel(OnLoginSuccess);
        }

        private void OnLoginSuccess(int userId)
        {
            _currentUserId = userId;
            CurrentView = new ReaderViewModel(_currentUserId);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}