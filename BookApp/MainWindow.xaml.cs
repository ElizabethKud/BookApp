using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using BookApp.Data;
using BookApp.Models;
using BookApp.Services;
using BookApp.Views;
using Microsoft.EntityFrameworkCore;

namespace BookApp
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _dbService = new DatabaseService();
        private readonly BookParserService _parserService = new BookParserService();
        private readonly FileDialogService _fileDialogService = new FileDialogService();
        private string _currentUser;
        private int _currentUserId;
        private Book _currentBook;
        private List<(bool isChapterStart, Paragraph paragraph)> _bookContent = new();
        private int _currentPageIndex = 0;

        public MainWindow(string username)
        {
            InitializeComponent();
            _currentUser = username;
            NicknameTextBox.Text = _currentUser;

            // Получаем ID пользователя
            using var db = CreateDbContext();
            _currentUserId = db.Users.First(u => u.Username == _currentUser).Id;

            // Инициализация базовых книг
            _dbService.InitializeDefaultBooks();

            // Загрузка настроек пользователя
            LoadUserSettings();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A)
            {
                PrevPage_Click(sender, e);
            }
            else if (e.Key == Key.D)
            {
                NextPage_Click(sender, e);
            }
            else if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                OpenBook_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void OpenBook_Click(object sender, RoutedEventArgs e)
        {
            var filePath = _fileDialogService.OpenFileDialog();
            if (string.IsNullOrEmpty(filePath)) return;
            OpenBook(filePath);
        }

        public void OpenBook(string filePath)
        {
            try
            {
                using var db = CreateDbContext();
                var book = db.Books.FirstOrDefault(b => b.FilePath == filePath);
                if (book == null)
                {
                    book = new Book
                    {
                        Title = Path.GetFileNameWithoutExtension(filePath),
                        FilePath = filePath,
                        Language = "Unknown",
                        PublicationYear = null,
                        PagesCount = 0
                    };
                    db.Books.Add(book);
                    db.SaveChanges();

                    var author = db.Authors.FirstOrDefault(a => a.LastName == "Unknown");
                    if (author == null)
                    {
                        author = new Author
                        {
                            LastName = "Unknown",
                            FirstName = "",
                            MiddleName = "",
                            BirthYear = null,
                            Country = "Unknown"
                        };
                        db.Authors.Add(author);
                        db.SaveChanges();
                    }

                    var bookAuthor = new BookAuthor
                    {
                        BookId = book.Id,
                        AuthorId = author.Id
                    };
                    db.BookAuthors.Add(bookAuthor);
                    db.SaveChanges();
                }

                _currentBook = book;

                var content = _parserService.ParseBook(filePath);
                if (!content.Any())
                {
                    MessageBox.Show("Не удалось прочитать содержимое книги.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var history = db.ReadingHistory
                    .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == book.Id);

                _currentPageIndex = history?.LastReadPage ?? 0;

                DisplayBookContent(content);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии книги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseBook_Click(object sender, RoutedEventArgs e)
        {
            CloseBook();
        }

        private void CloseBook()
        {
            if (_currentBook != null)
            {
                SaveReadingProgress(_currentPageIndex);
            }
            _currentBook = null;
            _bookContent.Clear();
            _currentPageIndex = 0;
            LeftPageDocument.Blocks.Clear();
            RightPageDocument.Blocks.Clear();
        }

        private void SaveReadingProgress(int pageNumber)
        {
            if (_currentBook == null) return;

            using var db = CreateDbContext();
            var history = db.ReadingHistory
                .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == _currentBook.Id);

            if (history == null)
            {
                history = new ReadingHistory
                {
                    UserId = _currentUserId,
                    BookId = _currentBook.Id,
                    LastReadPage = pageNumber,
                    LastReadDate = DateTime.UtcNow
                };
                db.ReadingHistory.Add(history);
            }
            else
            {
                history.LastReadPage = pageNumber;
                history.LastReadDate = DateTime.UtcNow;
            }
            db.SaveChanges();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_currentBook != null)
            {
                SaveReadingProgress(_currentPageIndex);
            }
            base.OnClosing(e);
        }

        private void LoadUserSettings()
        {
            try
            {
                var settings = _dbService.GetUserSettings(_currentUserId);
                if (settings != null)
                {
                    try
                    {
                        Resources["PageBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BackgroundColor));
                        Resources["PageForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FontColor));
                        Resources["PageFontSize"] = (double)settings.FontSize;
                        Resources["PageFontFamily"] = new FontFamily(settings.FontFamily ?? "Arial");
                    }
                    catch (FormatException)
                    {
                        Resources["PageBackground"] = new SolidColorBrush(Colors.White);
                        Resources["PageForeground"] = new SolidColorBrush(Colors.Black);
                        Resources["PageFontSize"] = 16.0;
                        Resources["PageFontFamily"] = new FontFamily("Arial");
                    }

                    BackgroundColorComboBox.SelectedItem = BackgroundColorComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(i => i.Tag.ToString() == settings.BackgroundColor);
                    FontColorComboBox.SelectedItem = FontColorComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(i => i.Tag.ToString() == settings.FontColor);
                    FontSizeSlider.Value = settings.FontSize;
                    FontFamilyComboBox.SelectedItem = FontFamilyComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(i => i.Content.ToString() == settings.FontFamily);
                }
                else
                {
                    Resources["PageBackground"] = new SolidColorBrush(Colors.White);
                    Resources["PageForeground"] = new SolidColorBrush(Colors.Black);
                    Resources["PageFontSize"] = 16.0;
                    Resources["PageFontFamily"] = new FontFamily("Arial");

                    BackgroundColorComboBox.SelectedIndex = 0;
                    FontColorComboBox.SelectedIndex = 0;
                    FontSizeSlider.Value = 16;
                    FontFamilyComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDisplaySettings()
        {
            if (_currentUserId == 0)
            {
                MessageBox.Show("Ошибка: Пользователь не идентифицирован!");
                return;
            }

            try
            {
                var backgroundColor = ((SolidColorBrush)Resources["PageBackground"]).Color.ToString();
                var fontColor = ((SolidColorBrush)Resources["PageForeground"]).Color.ToString();
                var fontSize = (int)(double)Resources["PageFontSize"];
                var fontFamily = Resources["PageFontFamily"].ToString();

                var settings = new DisplaySetting
                {
                    UserId = _currentUserId,
                    BackgroundColor = backgroundColor,
                    FontColor = fontColor,
                    FontSize = fontSize,
                    FontFamily = fontFamily
                };
                _dbService.SaveDisplaySettings(settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackgroundColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BackgroundColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag.ToString();
                Resources["PageBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
                UpdateCurrentPage();
            }
        }

        private void FontColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag.ToString();
                Resources["PageForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
                UpdateCurrentPage();
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Resources["PageFontSize"] = FontSizeSlider.Value;
            SaveDisplaySettings();
            UpdateCurrentPage();
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem item)
            {
                Resources["PageFontFamily"] = new FontFamily(item.Content.ToString());
                SaveDisplaySettings();
                UpdateCurrentPage();
            }
        }

        private void DisplayBookContent(List<(bool isChapterStart, Paragraph paragraph)> content)
        {
            _bookContent = content ?? new List<(bool isChapterStart, Paragraph paragraph)>();
            _currentPageIndex = 0;
            ShowCurrentPage();
        }

        private void ShowCurrentPage()
        {
            LeftPageDocument.Blocks.Clear();
            RightPageDocument.Blocks.Clear();

            if (_bookContent == null || !_bookContent.Any())
            {
                return;
            }

            double pageHeight = ActualHeight - 100;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerPage = (int)(pageHeight / lineHeight);

            int leftPageStartIndex = _currentPageIndex * 2;
            int rightPageStartIndex = leftPageStartIndex + 1;

            if (leftPageStartIndex < CalculateTotalPages())
            {
                int currentIndex = FindPageStartIndex(leftPageStartIndex);
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, paragraph) = _bookContent[currentIndex];
                    if (isChapterStart && linesAdded > 0)
                    {
                        break;
                    }
                    LeftPageDocument.Blocks.Add(paragraph);
                    linesAdded += EstimateLineCount(paragraph, fontSize);
                    currentIndex++;
                }
            }

            if (rightPageStartIndex < CalculateTotalPages())
            {
                int currentIndex = FindPageStartIndex(rightPageStartIndex);
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, paragraph) = _bookContent[currentIndex];
                    if (isChapterStart && linesAdded > 0)
                    {
                        break;
                    }
                    RightPageDocument.Blocks.Add(paragraph);
                    linesAdded += EstimateLineCount(paragraph, fontSize);
                    currentIndex++;
                }
            }
        }

        private int FindPageStartIndex(int pageIndex)
        {
            int totalPages = 0;
            int currentIndex = 0;
            double pageHeight = ActualHeight - 100;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerPage = (int)(pageHeight / lineHeight);

            while (currentIndex < _bookContent.Count && totalPages < pageIndex)
            {
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, paragraph) = _bookContent[currentIndex];
                    if (isChapterStart && linesAdded > 0)
                    {
                        break;
                    }
                    linesAdded += EstimateLineCount(paragraph, fontSize);
                    currentIndex++;
                }
                totalPages++;
            }

            return currentIndex;
        }

        private int CalculateTotalPages()
        {
            if (_bookContent == null || !_bookContent.Any())
            {
                return 0;
            }

            double pageHeight = ActualHeight - 100;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerPage = (int)(pageHeight / lineHeight);

            int totalPages = 0;
            int currentIndex = 0;
            while (currentIndex < _bookContent.Count)
            {
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, paragraph) = _bookContent[currentIndex];
                    if (isChapterStart && linesAdded > 0)
                    {
                        break;
                    }
                    linesAdded += EstimateLineCount(paragraph, fontSize);
                    currentIndex++;
                }
                totalPages++;
            }

            return totalPages;
        }

        private int EstimateLineCount(Paragraph paragraph, double fontSize)
        {
            var run = paragraph.Inlines.OfType<Run>().FirstOrDefault();
            if (run == null || string.IsNullOrEmpty(run.Text))
            {
                return 1;
            }
            string text = run.Text;
            int avgCharsPerLine = (int)(ActualWidth / (fontSize * 0.6));
            int lines = (int)Math.Ceiling((double)text.Length / avgCharsPerLine);
            return Math.Max(lines, 1);
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                ShowCurrentPage();
                SaveReadingProgress(_currentPageIndex);
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if ((_currentPageIndex + 1) * 2 < CalculateTotalPages())
            {
                _currentPageIndex++;
                ShowCurrentPage();
                SaveReadingProgress(_currentPageIndex);
            }
        }

        private void UpdateCurrentPage()
        {
            if (_bookContent != null && _bookContent.Any())
            {
                ShowCurrentPage();
            }
        }

        private void DefaultBooks_Click(object sender, RoutedEventArgs e)
        {
            var defaultBooksWindow = new DefaultBooksWindow(_currentUserId, this);
            defaultBooksWindow.ShowDialog();
        }

        private void Favorites_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Избранные книги...");
            // TODO: Реализовать отображение избранных книг
        }

        private void SaveNickname_Click(object sender, RoutedEventArgs e)
        {
            _currentUser = NicknameTextBox.Text;
            using var db = CreateDbContext();
            var user = db.Users.First(u => u.Id == _currentUserId);
            user.Username = _currentUser;
            db.SaveChanges();
            MessageBox.Show("Ник сохранен: " + _currentUser);
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