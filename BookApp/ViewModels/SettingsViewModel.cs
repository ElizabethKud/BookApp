using System.Collections.ObjectModel;
using System.Linq; // Необходим для FirstOrDefault
using System.Windows.Media;
using BookApp.Services;
using System.ComponentModel;

namespace BookApp.ViewModels
{
    public class ColorOption
    {
        public string Name { get; set; }
        public Brush Brush { get; set; }
    }

    public class SettingsViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;
        private readonly int _userId;
        private string _selectedFont;
        private int _fontSize;
        private ColorOption _backgroundColor;
        private ColorOption _fontColor;
        private ObservableCollection<string> _availableFonts;
        private ObservableCollection<ColorOption> _availableColors;

        public string SelectedFont
        {
            get => _selectedFont;
            set
            {
                if (_selectedFont != value)
                {
                    _selectedFont = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value && value >= 8 && value <= 48)
                {
                    _fontSize = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public ColorOption BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                if (_backgroundColor != value)
                {
                    _backgroundColor = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public ColorOption FontColor
        {
            get => _fontColor;
            set
            {
                if (_fontColor != value)
                {
                    _fontColor = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public ObservableCollection<string> AvailableFonts
        {
            get => _availableFonts;
            set
            {
                _availableFonts = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ColorOption> AvailableColors
        {
            get => _availableColors;
            set
            {
                _availableColors = value;
                OnPropertyChanged();
            }
        }

        public SettingsViewModel(int userId)
        {
            _userId = userId;
            _databaseService = new DatabaseService();
            InitializeSettings();
        }

        private void InitializeSettings()
        {
            // Инициализация доступных шрифтов и цветов
            AvailableFonts = new ObservableCollection<string> { "Arial", "Times New Roman", "Calibri" };
            AvailableColors = new ObservableCollection<ColorOption>
            {
                new ColorOption { Name = "Белый", Brush = Brushes.White },
                new ColorOption { Name = "Черный", Brush = Brushes.Black },
                new ColorOption { Name = "Светло-серый", Brush = Brushes.LightGray },
                new ColorOption { Name = "Темно-синий", Brush = Brushes.DarkBlue }
            };

            // Получение настроек пользователя из БД
            var userSettings = _databaseService.GetUserSettings(_userId);
            SelectedFont = userSettings?.FontFamily ?? "Arial";
            FontSize = userSettings?.FontSize ?? 16;

            // Преобразование строки в цвет и сопоставление с доступными
            var bgColor = TryParseColor(userSettings?.BackgroundColor, Colors.White);
            var fgColor = TryParseColor(userSettings?.FontColor, Colors.Black);

            BackgroundColor = AvailableColors.FirstOrDefault(c =>
                ((SolidColorBrush)c.Brush).Color == bgColor) ?? AvailableColors[0];

            FontColor = AvailableColors.FirstOrDefault(c =>
                ((SolidColorBrush)c.Brush).Color == fgColor) ?? AvailableColors[1];
        }

        private Color TryParseColor(string colorString, Color fallback)
        {
            try
            {
                if (!string.IsNullOrEmpty(colorString))
                    return (Color)ColorConverter.ConvertFromString(colorString);
            }
            catch
            {
                // Игнорируем ошибку и возвращаем fallback
            }
            return fallback;
        }

        private void SaveSettings()
        {
            var backgroundBrush = BackgroundColor?.Brush as SolidColorBrush;
            var fontBrush = FontColor?.Brush as SolidColorBrush;

            if (backgroundBrush != null && fontBrush != null)
            {
                _databaseService.SaveDisplaySettings(new Models.DisplaySetting
                {
                    UserId = _userId,
                    FontFamily = SelectedFont,
                    FontSize = FontSize,
                    BackgroundColor = backgroundBrush.Color.ToString(),
                    FontColor = fontBrush.Color.ToString()
                });
            }
        }
    }
}
