using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using BookApp.Data;
using BookApp.Models;
using BookApp.Services;
using Microsoft.Win32;
using VersOne.Epub;
using System.Xml.Linq;

namespace BookApp.ViewModels
{
    public class ReaderViewModel : BaseViewModel
    {
        private readonly BookParserService _bookParserService;
        private readonly DatabaseService _databaseService;
        private readonly FileDialogService _fileDialogService;

        private string _bookContent;
        private string _selectedFont;
        private int _fontSize;
        private Brush _backgroundColor;
        private Brush _fontColor;
        private ObservableCollection<string> _availableFonts;
        private ObservableCollection<Brush> _availableColors;

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

        public ObservableCollection<string> AvailableFonts
        {
            get => _availableFonts;
            set { _availableFonts = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Brush> AvailableColors
        {
            get => _availableColors;
            set { _availableColors = value; OnPropertyChanged(); }
        }

        public ICommand OpenBookCommand { get; }
        public ICommand ApplySettingsCommand { get; }

        public ReaderViewModel()
        {
            _bookParserService = new BookParserService();
            _databaseService = new DatabaseService();
            _fileDialogService = new FileDialogService();

            OpenBookCommand = new RelayCommand(OpenBook);
            ApplySettingsCommand = new RelayCommand(ApplySettings);

            InitializeSettings();
        }

        private void InitializeSettings()
        {
            AvailableFonts = new ObservableCollection<string> { "Arial", "Times New Roman", "Calibri" };
            AvailableColors = new ObservableCollection<Brush>
            {
                Brushes.White, Brushes.Black, Brushes.LightGray, Brushes.DarkBlue
            };

            var userSettings = _databaseService.GetUserSettings(1); // Assuming user ID 1 for demo
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

            // Save book to database
            _databaseService.SaveBook(new Book
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                FilePath = filePath
            });
        }

        private void ApplySettings()
        {
            _databaseService.SaveDisplaySettings(new DisplaySetting
            {
                UserId = 1, // Assuming user ID 1 for demo
                FontFamily = SelectedFont,
                FontSize = FontSize,
                BackgroundColor = ((SolidColorBrush)BackgroundColor).Color.ToString(),
                FontColor = ((SolidColorBrush)FontColor).Color.ToString()
            });
        }
    }
}