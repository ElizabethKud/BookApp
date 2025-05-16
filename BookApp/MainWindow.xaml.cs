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
        private List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> _bookContent = new();
        private int _currentSpreadIndex = 0;
        private List<(string title, int paragraphIndex)> _tableOfContents = new();
        private const double PAGE_WIDTH = 1460;
        private const double PAGE_HEIGHT = 650;
        private const double FONT_SIZE = 16;

        public MainWindow(string username)
        {
            InitializeComponent();
            _currentUser = username ?? "Guest";
            InitializeUser();
            _dbService.InitializeDefaultBooks();
            LoadUserSettings();
        }

        private void InitializeUser()
        {
            try
            {
                using var db = CreateDbContext();
                var user = db.Users.FirstOrDefault(u => u.Username == _currentUser);
                if (user == null)
                {
                    user = new User { Username = _currentUser };
                    db.Users.Add(user);
                    db.SaveChanges();
                }
                _currentUserId = user.Id;
                Debug.WriteLine($"User initialized with ID: {_currentUserId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing user: {ex.Message}");
                MessageBox.Show($"Ошибка инициализации пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
            else if (e.Key == Key.B && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                DefaultBooks_Click(sender, e);
            }
            else if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                LeftPageViewer.Focus();
                LeftPageViewer.Find();
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

                _bookContent.Clear();
                foreach (var (isChapterStart, paragraph) in content)
                {
                    bool isTitle = _bookContent.Count == 0;
                    bool isToc = false;
                    bool isPageBreak = false;
                    _bookContent.Add((isChapterStart, isTitle, isToc, isPageBreak, paragraph));
                }

                _tableOfContents.Clear();
                for (int i = 0; i < _bookContent.Count; i++)
                {
                    var (isChapterStart, _, _, _, paragraph) = _bookContent[i];
                    if (isChapterStart)
                    {
                        var run = paragraph.Inlines.OfType<Run>().FirstOrDefault();
                        if (run != null && !string.IsNullOrEmpty(run.Text))
                        {
                            _tableOfContents.Add((run.Text, i));
                        }
                    }
                }

                if (_tableOfContents.Any())
                {
                    var tocParagraph = new Paragraph(new Run("Оглавление"))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = FONT_SIZE + 6,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 20)
                    };
                    _bookContent.Insert(0, (false, false, true, false, tocParagraph));

                    int tocIndex = 1;
                    foreach (var (title, paragraphIndex) in _tableOfContents)
                    {
                        var tocEntry = new Paragraph(new Run($"{title}"))
                        {
                            FontSize = FONT_SIZE,
                            Margin = new Thickness(20, 5, 0, 5),
                            Tag = paragraphIndex
                        };
                        _bookContent.Insert(tocIndex++, (false, false, true, false, tocEntry));
                    }
                }

                var history = db.ReadingHistory
                    .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == book.Id);

                _currentSpreadIndex = history?.LastReadPage ?? 0;
                Debug.WriteLine($"Opening book at spread index: {_currentSpreadIndex}");

                DisplayBookContent(_bookContent);

                BookTitleTextBlock.Text = book.Title;
                BookTitleTextBlock.Visibility = Visibility.Visible;

                if (LeftPageDocument.Blocks.Count == 0 && _currentSpreadIndex < CalculateTotalSpreads() - 1)
                {
                    Debug.WriteLine("Initial spread is empty, trying next spread...");
                    _currentSpreadIndex++;
                    ShowCurrentSpread();
                }

                UpdateProgressDisplay();
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
            UpdateProgressDisplay();
        }

        private void SaveReadingProgress(int spreadNumber)
        {
            if (_currentBook == null || _currentUserId == 0) return;

            try
            {
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
                Debug.WriteLine($"Reading progress saved for user {_currentUserId}, book {_currentBook.Id}, page {spreadNumber}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving reading progress: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения прогресса чтения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                using var db = CreateDbContext();
                var settings = _dbService.GetUserSettings(_currentUserId);
                if (settings != null)
                {
                    try
                    {
                        Resources["PageBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BackgroundColor));
                        Resources["PageForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FontColor));
                        Resources["PageFontFamily"] = new FontFamily(settings.FontFamily ?? "Arial");
                    }
                    catch (FormatException)
                    {
                        Resources["PageBackground"] = new SolidColorBrush(Colors.White);
                        Resources["PageForeground"] = new SolidColorBrush(Colors.Black);
                        Resources["PageFontFamily"] = new FontFamily("Arial");
                    }
                }
                else
                {
                    Resources["PageBackground"] = new SolidColorBrush(Colors.White);
                    Resources["PageForeground"] = new SolidColorBrush(Colors.Black);
                    Resources["PageFontFamily"] = new FontFamily("Arial");
                }
                UpdateCurrentSpread();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading user settings: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDisplaySettings()
        {
            if (_currentUserId == 0)
            {
                Debug.WriteLine("User not identified, skipping save settings.");
                return;
            }

            try
            {
                var backgroundColor = ((SolidColorBrush)Resources["PageBackground"]).Color.ToString();
                var fontColor = ((SolidColorBrush)Resources["PageForeground"]).Color.ToString();
                var fontFamily = Resources["PageFontFamily"].ToString();

                var settings = new DisplaySetting
                {
                    UserId = _currentUserId,
                    BackgroundColor = backgroundColor,
                    FontColor = fontColor,
                    FontSize = (int)FONT_SIZE,
                    FontFamily = fontFamily
                };
                _dbService.SaveDisplaySettings(settings);
                Debug.WriteLine($"Display settings saved for user {_currentUserId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving display settings: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BackgroundColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var color = item.Tag.ToString();
                Resources["PageBackground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
                UpdateCurrentSpread();
            }
        }

        private void FontColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var color = item.Tag.ToString();
                Resources["PageForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
                UpdateCurrentSpread();
            }
        }

        private void FontFamilyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                Resources["PageFontFamily"] = new FontFamily(item.Header.ToString());
                SaveDisplaySettings();
                UpdateCurrentSpread();
            }
        }

        private void ChangeNickname_Click(object sender, RoutedEventArgs e)
        {
            var nicknameWindow = new Window
            {
                Title = "Смена ника",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stackPanel = new StackPanel { Margin = new Thickness(10) };
            var textBox = new TextBox { Width = 200, Text = _currentUser };
            var saveButton = new Button { Content = "Сохранить", Margin = new Thickness(0, 10, 0, 0) };
            saveButton.Click += (s, args) =>
            {
                _currentUser = textBox.Text;
                using var db = CreateDbContext();
                var user = db.Users.First(u => u.Id == _currentUserId);
                user.Username = _currentUser;
                db.SaveChanges();
                MessageBox.Show("Ник сохранен: " + _currentUser);
                nicknameWindow.Close();
            };

            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(saveButton);
            nicknameWindow.Content = stackPanel;
            nicknameWindow.ShowDialog();
        }

        private void DisplayBookContent(List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> content)
        {
            _bookContent = content ?? new List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)>();
            Debug.WriteLine($"Displaying book content. Total paragraphs: {_bookContent.Count}, Current spread index: {_currentSpreadIndex}");
            _currentSpreadIndex = Math.Max(0, Math.Min(_currentSpreadIndex, CalculateTotalSpreads() - 1));
            ShowCurrentSpread();
            UpdateProgressDisplay();
        }

        private void ShowCurrentSpread()
        {
            LeftPageDocument.Blocks.Clear();

            if (_bookContent == null || !_bookContent.Any())
            {
                Debug.WriteLine("No content to display.");
                return;
            }

            double lineHeight = FONT_SIZE * 1.5;
            int maxLinesPerPage = (int)(PAGE_HEIGHT / lineHeight);

            int spreadStartIndex = FindSpreadStartIndex(_currentSpreadIndex);

            Debug.WriteLine($"Showing spread {_currentSpreadIndex}. Start index: {spreadStartIndex}, Max lines per page: {maxLinesPerPage}");

            if (spreadStartIndex >= _bookContent.Count)
            {
                Debug.WriteLine("Spread start index exceeds content length. Adjusting...");
                _currentSpreadIndex = Math.Max(0, CalculateTotalSpreads() - 1);
                spreadStartIndex = FindSpreadStartIndex(_currentSpreadIndex);
            }

            int linesAdded = 0;
            int currentIndex = spreadStartIndex;
            bool hasContent = false;

            while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
            {
                var (isChapterStart, isTitle, isToc, isPageBreak, paragraph) = _bookContent[currentIndex];
                var clonedParagraph = CloneParagraph(paragraph);

                if (isTitle && _currentSpreadIndex != 0 && !_bookContent[currentIndex].isToc)
                {
                    Debug.WriteLine($"Skipping title on spread {_currentSpreadIndex}");
                    currentIndex++;
                    continue;
                }

                if (isPageBreak && linesAdded > 0)
                {
                    Debug.WriteLine($"Breaking spread {_currentSpreadIndex} due to page break");
                    break;
                }

                int linesForParagraph = EstimateLineCount(clonedParagraph, FONT_SIZE, LeftPageViewer);
                if (isChapterStart && linesAdded > 0 && !_bookContent[currentIndex].isToc)
                {
                    Debug.WriteLine("Chapter start detected, breaking to start new spread.");
                    break;
                }

                if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0 && !_bookContent[currentIndex].isToc)
                {
                    Debug.WriteLine("Paragraph exceeds page height, breaking.");
                    break;
                }

                if (isChapterStart && !isToc)
                {
                    if (!LeftPageDocument.Blocks.Any(p => GetParagraphText(p as Paragraph).Equals(GetParagraphText(clonedParagraph))))
                    {
                        clonedParagraph.FontWeight = FontWeights.Bold;
                        clonedParagraph.FontSize = FONT_SIZE + 4;
                        clonedParagraph.Margin = new Thickness(0, 20, 0, 10);
                        LeftPageDocument.Blocks.Add(clonedParagraph);
                        linesAdded += linesForParagraph;
                        hasContent = true;
                        Debug.WriteLine($"Added chapter heading on spread {_currentSpreadIndex}");
                    }
                    currentIndex++;
                    continue;
                }

                if (isTitle)
                {
                    clonedParagraph.FontWeight = FontWeights.Bold;
                    clonedParagraph.FontSize = FONT_SIZE + 6;
                    clonedParagraph.TextAlignment = TextAlignment.Center;
                    LeftPageDocument.Blocks.Add(clonedParagraph);
                    linesAdded += linesForParagraph;
                    hasContent = true;
                    Debug.WriteLine("Added title on spread 0");
                    currentIndex++;
                    continue;
                }

                if (isToc)
                {
                    clonedParagraph.FontSize = FONT_SIZE;
                    clonedParagraph.TextAlignment = TextAlignment.Left;
                    LeftPageDocument.Blocks.Add(clonedParagraph);
                    linesAdded += linesForParagraph;
                    hasContent = true;
                    Debug.WriteLine($"Added TOC entry on spread {_currentSpreadIndex}");
                    currentIndex++;
                    continue;
                }

                LeftPageDocument.Blocks.Add(clonedParagraph);
                linesAdded += linesForParagraph;
                hasContent = true;
                Debug.WriteLine($"Added paragraph to spread {_currentSpreadIndex}. Lines: {linesForParagraph}, Total: {linesAdded}");
                currentIndex++;
            }

            if (!hasContent && _currentSpreadIndex < CalculateTotalSpreads() - 1)
            {
                Debug.WriteLine($"Spread {_currentSpreadIndex} is empty, moving to next...");
                _currentSpreadIndex++;
                ShowCurrentSpread();
            }

            UpdateProgressDisplay();
        }

        private void UpdateProgressDisplay()
        {
            if (_currentBook == null || _bookContent == null || !_bookContent.Any())
            {
                BookProgressText.Text = "";
                return;
            }

            int totalSpreads = CalculateTotalSpreads();
            double bookProgress = totalSpreads > 0 ? (_currentSpreadIndex + 1) / (double)totalSpreads * 100 : 0;
            BookProgressText.Text = $"Страница: {_currentSpreadIndex + 1}/{totalSpreads} ({bookProgress:F1}%)";
        }

        private Paragraph CloneParagraph(Paragraph original)
        {
            if (original == null)
            {
                Debug.WriteLine("CloneParagraph: original paragraph is null");
                return new Paragraph();
            }

            var paragraph = new Paragraph();
            foreach (var inline in original.Inlines)
            {
                if (inline is Run run)
                {
                    var newRun = new Run(run.Text)
                    {
                        FontWeight = run.FontWeight,
                        FontStyle = run.FontStyle,
                        FontSize = run.FontSize,
                        FontFamily = (FontFamily)Resources["PageFontFamily"],
                        Foreground = (SolidColorBrush)Resources["PageForeground"]
                    };
                    paragraph.Inlines.Add(newRun);
                }
            }
            paragraph.FontWeight = original.FontWeight;
            paragraph.FontSize = original.FontSize;
            paragraph.FontFamily = (FontFamily)Resources["PageFontFamily"];
            paragraph.Foreground = (SolidColorBrush)Resources["PageForeground"];
            paragraph.Margin = original.Margin;
            paragraph.TextAlignment = original.TextAlignment;
            paragraph.TextIndent = original.TextIndent; // Сохраняем отступ (красная строка)
            return paragraph;
        }

        private int FindSpreadStartIndex(int spreadIndex)
        {
            if (_bookContent == null || !_bookContent.Any())
            {
                Debug.WriteLine("No content for spread indexing.");
                return 0;
            }

            int currentSpread = 0;
            int currentIndex = 0;
            double lineHeight = FONT_SIZE * 1.5;
            int maxLinesPerPage = (int)(PAGE_HEIGHT / lineHeight);

            while (currentIndex < _bookContent.Count && currentSpread < spreadIndex)
            {
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, isTitle, isToc, isPageBreak, paragraph) = _bookContent[currentIndex];
                    if (isTitle && !isToc)
                    {
                        currentIndex++;
                        continue;
                    }
                    if (isPageBreak && linesAdded > 0)
                    {
                        break;
                    }
                    int linesForParagraph = EstimateLineCount(paragraph, FONT_SIZE, LeftPageViewer);
                    if (isChapterStart && linesAdded > 0 && !isToc)
                    {
                        break;
                    }
                    if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0 && !isToc)
                    {
                        break;
                    }
                    linesAdded += linesForParagraph;
                    currentIndex++;
                }
                currentSpread++;
            }

            Debug.WriteLine($"Spread {spreadIndex} starts at index {currentIndex}. Total spreads so far: {currentSpread}");
            return currentIndex;
        }

        private int CalculateTotalSpreads()
        {
            if (_bookContent == null || !_bookContent.Any())
            {
                Debug.WriteLine("No content to calculate spreads.");
                return 0;
            }

            double lineHeight = FONT_SIZE * 1.5;
            int maxLinesPerPage = (int)(PAGE_HEIGHT / lineHeight);

            int totalSpreads = 0;
            int currentIndex = 0;
            while (currentIndex < _bookContent.Count)
            {
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage)
                {
                    var (isChapterStart, isTitle, isToc, isPageBreak, paragraph) = _bookContent[currentIndex];
                    if (isTitle && !isToc)
                    {
                        currentIndex++;
                        continue;
                    }
                    if (isPageBreak && linesAdded > 0)
                    {
                        break;
                    }
                    int linesForParagraph = EstimateLineCount(paragraph, FONT_SIZE, LeftPageViewer);
                    if (isChapterStart && linesAdded > 0 && !isToc)
                    {
                        break;
                    }
                    if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0 && !isToc)
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

            double charWidth = fontSize * 0.6;
            int avgCharsPerLine = (int)((PAGE_WIDTH - 20) / charWidth);
            if (avgCharsPerLine <= 0)
            {
                avgCharsPerLine = 50;
            }

            string[] words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int lines = 1;
            int currentLineChars = 0;

            foreach (var word in words)
            {
                int wordLength = word.Length + 1;
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
            Debug.WriteLine($"Estimating lines for paragraph: '{text.Substring(0, Math.Min(50, text.Length))}'... Avg chars per line: {avgCharsPerLine}, Lines: {estimatedLines}");
            return estimatedLines;
        }

        private string GetParagraphText(Paragraph paragraph)
        {
            if (paragraph == null)
                return string.Empty;
            return string.Join("", paragraph.Inlines.OfType<Run>().Select(r => r.Text));
        }

        private void PrevSpread_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSpreadIndex > 0)
            {
                _currentSpreadIndex--;
                NavigationCommands.PreviousPage.Execute(null, LeftPageViewer);
                ShowCurrentSpread();
                SaveReadingProgress(_currentSpreadIndex);
                UpdateProgressDisplay();
            }
        }

        private void NextSpread_Click(object sender, RoutedEventArgs e)
        {
            int totalSpreads = CalculateTotalSpreads();
            if (_currentSpreadIndex < totalSpreads - 1)
            {
                _currentSpreadIndex++;
                NavigationCommands.NextPage.Execute(null, LeftPageViewer);
                ShowCurrentSpread();
                SaveReadingProgress(_currentSpreadIndex);
                UpdateProgressDisplay();
            }
        }

        private void PageViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("PageViewer loaded, updating display...");
                ShowCurrentSpread();
                UpdateProgressDisplay();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("Window size changed, updating display...");
                ShowCurrentSpread();
                UpdateProgressDisplay();
            }
        }

        private void UpdateCurrentSpread()
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("Updating current spread...");
                ShowCurrentSpread();
                UpdateProgressDisplay();
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
        }

        private void TableOfContents_Click(object sender, RoutedEventArgs e)
        {
            if (_tableOfContents == null || !_tableOfContents.Any())
            {
                MessageBox.Show("Оглавление отсутствует.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _currentSpreadIndex = 0;
            ShowCurrentSpread();
            UpdateProgressDisplay();
        }

        private int FindSpreadIndexForParagraph(int paragraphIndex)
        {
            int spreadIndex = 0;
            int currentIndex = 0;
            double lineHeight = FONT_SIZE * 1.5;
            int maxLinesPerPage = (int)(PAGE_HEIGHT / lineHeight);

            while (currentIndex < _bookContent.Count && currentIndex <= paragraphIndex)
            {
                int linesAdded = 0;
                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerPage && currentIndex <= paragraphIndex)
                {
                    var (isChapterStart, isTitle, isToc, isPageBreak, paragraph) = _bookContent[currentIndex];
                    if (isTitle && !isToc)
                    {
                        currentIndex++;
                        continue;
                    }
                    if (isPageBreak && linesAdded > 0)
                    {
                        break;
                    }
                    int linesForParagraph = EstimateLineCount(paragraph, FONT_SIZE, LeftPageViewer);
                    if (isChapterStart && linesAdded > 0 && !isToc)
                    {
                        break;
                    }
                    if (linesAdded + linesForParagraph > maxLinesPerPage && linesAdded > 0 && !isToc)
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

        private AppDbContext CreateDbContext()
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=BookApp;Username=postgres;Include Error Detail=true");
            optionsBuilder.UseLazyLoadingProxies();
            return new AppDbContext(optionsBuilder.Options);
        }
    }
}