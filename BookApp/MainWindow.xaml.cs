using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private int _currentSpreadIndex = 0; // Индекс разворота (каждая пара страниц)
        private List<(string title, int paragraphIndex)> _tableOfContents = new(); // Оглавление

        public MainWindow(string username)
        {
            InitializeComponent();
            _currentUser = username;
            NicknameTextBox.Text = _currentUser;

            using var db = CreateDbContext();
            _currentUserId = db.Users.First(u => u.Username == _currentUser).Id;

            _dbService.InitializeDefaultBooks();
            LoadUserSettings();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A)
            {
                PrevSpread_Click(sender, e);
            }
            else if (e.Key == Key.D)
            {
                NextSpread_Click(sender, e);
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

                Debug.WriteLine($"Book parsed. Total paragraphs: {content.Count}");

                // Формируем оглавление
                _tableOfContents.Clear();
                for (int i = 0; i < content.Count; i++)
                {
                    var (isChapterStart, paragraph) = content[i];
                    if (isChapterStart)
                    {
                        var run = paragraph.Inlines.OfType<Run>().FirstOrDefault();
                        if (run != null && !string.IsNullOrEmpty(run.Text))
                        {
                            _tableOfContents.Add((run.Text, i));
                        }
                    }
                }

                var history = db.ReadingHistory
                    .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == book.Id);

                _currentSpreadIndex = history?.LastReadPage ?? 0;
                Debug.WriteLine($"Opening book at spread index: {_currentSpreadIndex}");

                DisplayBookContent(content);

                // Показываем название книги
                BookTitleTextBlock.Text = book.Title;
                BookTitleTextBlock.Visibility = Visibility.Visible;

                // Дополнительная проверка: если разворот пустой, попробуем перейти на следующий
                if (LeftPageDocument.Blocks.Count == 0 && _currentSpreadIndex < CalculateTotalSpreads() - 1)
                {
                    Debug.WriteLine("Initial spread is empty, trying next spread...");
                    _currentSpreadIndex++;
                    ShowCurrentSpread();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening book: {ex.Message}");
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
                SaveReadingProgress(_currentSpreadIndex);
            }
            _currentBook = null;
            _bookContent.Clear();
            _tableOfContents.Clear();
            _currentSpreadIndex = 0;
            LeftPageDocument.Blocks.Clear();
            BookTitleTextBlock.Visibility = Visibility.Collapsed;
        }

        private void SaveReadingProgress(int spreadNumber)
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
                    LastReadPage = spreadNumber,
                    LastReadDate = DateTime.UtcNow
                };
                db.ReadingHistory.Add(history);
            }
            else
            {
                history.LastReadPage = spreadNumber;
                history.LastReadDate = DateTime.UtcNow;
            }
            db.SaveChanges();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_currentBook != null)
            {
                SaveReadingProgress(_currentSpreadIndex);
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
                UpdateCurrentSpread();
            }
        }

        private void FontColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag.ToString();
                Resources["PageForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
                UpdateCurrentSpread();
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Resources["PageFontSize"] = FontSizeSlider.Value;
            SaveDisplaySettings();
            UpdateCurrentSpread();
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem item)
            {
                Resources["PageFontFamily"] = new FontFamily(item.Content.ToString());
                SaveDisplaySettings();
                UpdateCurrentSpread();
            }
        }

        private void DisplayBookContent(List<(bool isChapterStart, Paragraph paragraph)> content)
        {
            _bookContent = content ?? new List<(bool isChapterStart, Paragraph paragraph)>();
            Debug.WriteLine($"Displaying book content. Total paragraphs: {_bookContent.Count}, Current spread index: {_currentSpreadIndex}");
            _currentSpreadIndex = Math.Max(0, Math.Min(_currentSpreadIndex, CalculateTotalSpreads() - 1));
            ShowCurrentSpread();
        }

        private void ShowCurrentSpread()
        {
            LeftPageDocument.Blocks.Clear();

            if (_bookContent == null || !_bookContent.Any())
            {
                Debug.WriteLine("No content to display.");
                return;
            }

            double pageHeight = ActualHeight - 150; // Учитываем отступы, панель и заголовок
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerPage = (int)(pageHeight / lineHeight);

            int spreadStartIndex = FindSpreadStartIndex(_currentSpreadIndex);

            Debug.WriteLine($"Showing spread {_currentSpreadIndex}. Start index: {spreadStartIndex}, Max lines per page: {maxLinesPerPage}");

            if (spreadStartIndex >= _bookContent.Count)
            {
                Debug.WriteLine("Spread start index exceeds content length. Adjusting...");
                _currentSpreadIndex = Math.Max(0, CalculateTotalSpreads() - 1);
                spreadStartIndex = FindSpreadStartIndex(_currentSpreadIndex);
            }

            // Отображаем левую страницу
            int linesAdded = 0;
            int currentIndex = spreadStartIndex;
            while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
            {
                var (isChapterStart, paragraph) = _bookContent[currentIndex];
                if (isChapterStart && linesAdded > 0)
                {
                    Debug.WriteLine("Chapter start detected on left page, breaking to start new page.");
                    break;
                }
                int linesForParagraph = EstimateLineCount(paragraph, fontSize, LeftPageViewer);
                if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0)
                {
                    Debug.WriteLine("Paragraph exceeds left page height, breaking.");
                    break;
                }
                LeftPageDocument.Blocks.Add(CloneParagraph(paragraph));
                linesAdded += linesForParagraph;
                currentIndex++;
            }

            Debug.WriteLine($"Left page of spread {_currentSpreadIndex} displayed. Paragraphs added: {LeftPageDocument.Blocks.Count}, Lines added: {linesAdded}");
        }

        private Paragraph CloneParagraph(Paragraph original)
        {
            var paragraph = new Paragraph();
            foreach (var inline in original.Inlines)
            {
                if (inline is Run run)
                {
                    var newRun = new Run(run.Text)
                    {
                        FontWeight = run.FontWeight,
                        FontStyle = run.FontStyle,
                        Foreground = run.Foreground
                    };
                    paragraph.Inlines.Add(newRun);
                }
            }
            return paragraph;
        }

        private int FindSpreadStartIndex(int spreadIndex)
        {
            if (_bookContent == null || !_bookContent.Any())
            {
                Debug.WriteLine("No content for spread indexing.");
                return 0;
            }

            int totalSpreads = 0;
            int currentIndex = 0;
            double pageHeight = ActualHeight - 150;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerPage = (int)(pageHeight / lineHeight);

            while (currentIndex < _bookContent.Count && totalSpreads < spreadIndex)
            {
                // Левая страница
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, paragraph) = _bookContent[currentIndex];
                    if (isChapterStart && linesAdded > 0)
                    {
                        break;
                    }
                    int linesForParagraph = EstimateLineCount(paragraph, fontSize, LeftPageViewer);
                    if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0)
                    {
                        break;
                    }
                    linesAdded += linesForParagraph;
                    currentIndex++;
                }
                totalSpreads++;
            }

            Debug.WriteLine($"Spread {spreadIndex} starts at index {currentIndex}. Total spreads so far: {totalSpreads}");
            return currentIndex;
        }

        private int CalculateTotalSpreads()
        {
            if (_bookContent == null || !_bookContent.Any())
            {
                Debug.WriteLine("No content to calculate spreads.");
                return 0;
            }

            double pageHeight = ActualHeight - 150;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerPage = (int)(pageHeight / lineHeight);

            int totalSpreads = 0;
            int currentIndex = 0;
            while (currentIndex < _bookContent.Count)
            {
                // Левая страница
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, paragraph) = _bookContent[currentIndex];
                    if (isChapterStart && linesAdded > 0)
                    {
                        break;
                    }
                    int linesForParagraph = EstimateLineCount(paragraph, fontSize, LeftPageViewer);
                    if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0)
                    {
                        break;
                    }
                    linesAdded += linesForParagraph;
                    currentIndex++;
                }
                
                totalSpreads++;
            }

            Debug.WriteLine($"Total spreads calculated: {totalSpreads}");
            return totalSpreads;
        }

        private int EstimateLineCount(Paragraph paragraph, double fontSize, FlowDocumentReader viewer)
        {
            var run = paragraph.Inlines.OfType<Run>().FirstOrDefault();
            if (run == null || string.IsNullOrEmpty(run.Text))
            {
                Debug.WriteLine("Empty paragraph detected.");
                return 1;
            }
            string text = run.Text;

            double viewerWidth = viewer.ActualWidth > 0 ? viewer.ActualWidth : 400; // Половина ширины окна по умолчанию
            double charWidth = fontSize * 0.6; // Примерная ширина символа
            int avgCharsPerLine = (int)((viewerWidth - 20) / charWidth); // 20 — PagePadding
            if (avgCharsPerLine <= 0)
            {
                avgCharsPerLine = 50; // Минимальное значение для безопасности
            }

            // Учитываем переносы слов
            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int lines = 1;
            int currentLineChars = 0;

            foreach (var word in words)
            {
                int wordLength = word.Length + 1; // +1 для пробела
                if (currentLineChars + wordLength > avgCharsPerLine)
                {
                    lines++;
                    currentLineChars = wordLength;
                }
                else
                {
                    currentLineChars += wordLength;
                }
            }

            int estimatedLines = Math.Max(lines, 1);
            Debug.WriteLine($"Estimating lines for paragraph: '{text.Substring(0, Math.Min(50, text.Length))}'... Viewer width: {viewerWidth}, Avg chars per line: {avgCharsPerLine}, Lines: {estimatedLines}");
            return estimatedLines;
        }

        private void PrevSpread_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSpreadIndex > 0)
            {
                _currentSpreadIndex--;
                ShowCurrentSpread();
                SaveReadingProgress(_currentSpreadIndex);
            }
        }

        private void NextSpread_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSpreadIndex + 1 < CalculateTotalSpreads())
            {
                _currentSpreadIndex++;
                ShowCurrentSpread();
                SaveReadingProgress(_currentSpreadIndex);
            }
        }

        private void PageViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("PageViewer loaded, updating display...");
                ShowCurrentSpread();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("Window size changed, updating display...");
                ShowCurrentSpread();
            }
        }

        private void UpdateCurrentSpread()
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("Updating current spread...");
                ShowCurrentSpread();
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

        private void TableOfContents_Click(object sender, RoutedEventArgs e)
        {
            if (_tableOfContents == null || !_tableOfContents.Any())
            {
                MessageBox.Show("Оглавление отсутствует.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tocWindow = new Window
            {
                Title = "Оглавление",
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var listBox = new ListBox { Margin = new Thickness(10) };
            foreach (var (title, index) in _tableOfContents)
            {
                var item = new ListBoxItem
                {
                    Content = title,
                    Tag = index
                };
                item.Selected += (s, args) =>
                {
                    int paragraphIndex = (int)((ListBoxItem)s).Tag;
                    _currentSpreadIndex = FindSpreadIndexForParagraph(paragraphIndex);
                    ShowCurrentSpread();
                    SaveReadingProgress(_currentSpreadIndex);
                    tocWindow.Close();
                };
                listBox.Items.Add(item);
            }

            tocWindow.Content = new ScrollViewer { Content = listBox };
            tocWindow.ShowDialog();
        }

        private int FindSpreadIndexForParagraph(int paragraphIndex)
        {
            int spreadIndex = 0;
            int currentIndex = 0;
            double pageHeight = ActualHeight - 150;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerPage = (int)(pageHeight / lineHeight);

            while (currentIndex < _bookContent.Count && currentIndex <= paragraphIndex)
            {
                // Левая страница
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage && currentIndex <= paragraphIndex)
                {
                    var (isChapterStart, paragraph) = _bookContent[currentIndex];
                    if (isChapterStart && linesAdded > 0)
                    {
                        break;
                    }
                    int linesForParagraph = EstimateLineCount(paragraph, fontSize, LeftPageViewer);
                    if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0)
                    {
                        break;
                    }
                    linesAdded += linesForParagraph;
                    currentIndex++;
                }
                spreadIndex++;
            }

            return spreadIndex - 1;
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