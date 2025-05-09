using System.Windows.Input;
using System.Windows.Media;
using BookApp.Services;

namespace BookApp.ViewModels
{
    public class BookContentViewModel : BaseViewModel
    {
        private readonly BookParserService _bookParserService;
        private readonly DatabaseService _databaseService;
        private readonly FileDialogService _fileDialogService;
        private readonly int _userId;
        private string _bookContent;
        private string _selectedFont;
        private int _fontSize;
        private Brush _backgroundColor;
        private Brush _fontColor;

        public string BookContent
        {
            get => _bookContent;
            set { _bookContent = value; OnPropertyChanged(); }
        }

        public string SelectedFont
        {
            get => _selectedFont;
            set { _selectedFont = value; OnPropertyChanged(); }
        }

        public int FontSize
        {
            get => _fontSize;
            set { _fontSize = value; OnPropertyChanged(); }
        }

        public Brush BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(); }
        }

        public Brush FontColor
        {
            get => _fontColor;
            set { _fontColor = value; OnPropertyChanged(); }
        }

        public ICommand OpenBookCommand { get; }

        public BookContentViewModel(int userId)
        {
            _userId = userId;
            _bookParserService = new BookParserService();
            _databaseService = new DatabaseService();
            _fileDialogService = new FileDialogService();

            OpenBookCommand = new RelayCommand(OpenBook);

            InitializeSettings();
        }

        private void InitializeSettings()
        {
            var userSettings = _databaseService.GetUserSettings(_userId);
            SelectedFont = userSettings?.FontFamily ?? "Arial";
            FontSize = userSettings?.FontSize ?? 16;
            BackgroundColor = userSettings?.BackgroundColor != null
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(userSettings.BackgroundColor))
                : Brushes.White;
            FontColor = userSettings?.FontColor != null
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString(userSettings.FontColor))
                : Brushes.Black;
        }

        private void OpenBook()
        {
            var filePath = _fileDialogService.OpenFileDialog();
            if (string.IsNullOrEmpty(filePath)) return;

            BookContent = _bookParserService.ParseBook(filePath);

            _databaseService.SaveBook(new Models.Book
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath
            });
        }
    }
}