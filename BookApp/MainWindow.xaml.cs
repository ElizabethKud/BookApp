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
        private List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)> _bookContent = new();
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

            // Обновление при изменении размера окна
            SizeChanged += (s, e) => UpdateCurrentPage();
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

        private void DisplayBookContent(List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)> content)
        {
            _bookContent = content ?? new List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)>();
            _currentPageIndex = 0;
            ShowCurrentPage();
        }

        private void ShowCurrentPage()
        {
            LeftPageDocument.Blocks.Clear();
            RightPageDocument.Blocks.Clear();

            if (_bookContent == null || !_bookContent.Any())
            {
                System.Diagnostics.Debug.WriteLine("ShowCurrentPage: _bookContent пуст");
                return;
            }

            // Применяем текущие настройки к параграфам
            foreach (var (_, _, _, paragraph) in _bookContent)
            {
                paragraph.FontFamily = (FontFamily)Resources["PageFontFamily"];
                paragraph.Foreground = (SolidColorBrush)Resources["PageForeground"];
            }

            double pageHeight = LeftPageViewer.ActualHeight - 40; // Учитываем PagePadding
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerColumn = (int)(pageHeight / lineHeight);

            int startIndex = FindPageStartIndex(_currentPageIndex);
            int currentIndex = startIndex;
            int linesAdded = 0;
            bool isLeftColumn = true;

            System.Diagnostics.Debug.WriteLine($"ShowCurrentPage: Начало страницы {_currentPageIndex}, startIndex={startIndex}");

            while (currentIndex < _bookContent.Count)
            {
                var (isChapterStart, isTitle, isToc, paragraph) = _bookContent[currentIndex];
                var clonedParagraph = CloneParagraph(paragraph);

                // Если это заголовок главы (не в оглавлении), начинаем новую страницу
                if (isChapterStart && !isToc && currentIndex > startIndex && linesAdded > 0)
                {
                    System.Diagnostics.Debug.WriteLine("Прерываем страницу для новой главы");
                    break; // Новая страница для главы
                }

                int lineCount = EstimateLineCount(clonedParagraph, fontSize);

                if (isLeftColumn)
                {
                    if (linesAdded + lineCount <= maxLinesPerColumn || isTitle || isToc)
                    {
                        LeftPageDocument.Blocks.Add(clonedParagraph);
                        linesAdded += lineCount;
                        currentIndex++;
                        System.Diagnostics.Debug.WriteLine($"Добавлен параграф в левую колонку: {clonedParagraph.Inlines.OfType<Run>().FirstOrDefault()?.Text.Substring(0, Math.Min(50, clonedParagraph.Inlines.OfType<Run>().FirstOrDefault()?.Text.Length ?? 0))}... (lines={lineCount})");
                    }
                    else
                    {
                        isLeftColumn = false; // Переходим к правой колонке
                        System.Diagnostics.Debug.WriteLine("Переход к правой колонке");
                    }
                }
                else
                {
                    if (linesAdded + lineCount <= maxLinesPerColumn && !isTitle && !isToc)
                    {
                        RightPageDocument.Blocks.Add(clonedParagraph);
                        linesAdded += lineCount;
                        currentIndex++;
                        System.Diagnostics.Debug.WriteLine($"Добавлен параграф в правую колонку: {clonedParagraph.Inlines.OfType<Run>().FirstOrDefault()?.Text.Substring(0, Math.Min(50, clonedParagraph.Inlines.OfType<Run>().FirstOrDefault()?.Text.Length ?? 0))}... (lines={lineCount})");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Страница заполнена, прерываем");
                        break; // Страница заполнена
                    }
                }

                // Название и оглавление не перетекают в правую колонку
                if ((isTitle || isToc) && linesAdded > 0)
                {
                    isLeftColumn = false;
                    System.Diagnostics.Debug.WriteLine("Прерываем для названия или оглавления");
                    break;
                }
            }
        }

        private int FindPageStartIndex(int pageIndex)
        {
            if (pageIndex == 0) return 0;

            double pageHeight = LeftPageViewer.ActualHeight - 40;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerColumn = (int)(pageHeight / lineHeight);

            int currentIndex = 0;
            int currentPage = 0;

            System.Diagnostics.Debug.WriteLine($"FindPageStartIndex: Ищем индекс для страницы {pageIndex}");

            while (currentIndex < _bookContent.Count && currentPage < pageIndex)
            {
                int linesAdded = 0;
                bool isLeftColumn = true;

                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerColumn)
                {
                    var (isChapterStart, isTitle, isToc, paragraph) = _bookContent[currentIndex];
                    int lineCount = EstimateLineCount(paragraph, fontSize);

                    if (isChapterStart && !isToc && currentIndex > 0 && linesAdded > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Прерываем страницу {currentPage} для новой главы на индексе {currentIndex}");
                        break; // Новая страница для главы
                    }

                    if (isLeftColumn)
                    {
                        linesAdded += lineCount;
                        currentIndex++;
                        System.Diagnostics.Debug.WriteLine($"Левая колонка: добавлено {lineCount} строк, индекс={currentIndex}");
                    }
                    else
                    {
                        if (linesAdded + lineCount <= maxLinesPerColumn && !isTitle && !isToc)
                        {
                            linesAdded += lineCount;
                            currentIndex++;
                            System.Diagnostics.Debug.WriteLine($"Правая колонка: добавлено {lineCount} строк, индекс={currentIndex}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Правая колонка заполнена или isTitle/isToc, прерываем");
                            break;
                        }
                    }

                    if (linesAdded >= maxLinesPerColumn && isLeftColumn && !isTitle && !isToc)
                    {
                        isLeftColumn = false;
                        linesAdded = 0; // Сбрасываем для правой колонки
                        System.Diagnostics.Debug.WriteLine("Сброс строк для правой колонки");
                    }

                    if ((isTitle || isToc) && linesAdded > 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Прерываем для названия или оглавления");
                        break;
                    }
                }
                currentPage++;
                System.Diagnostics.Debug.WriteLine($"Завершена страница {currentPage}, текущий индекс={currentIndex}");
            }

            System.Diagnostics.Debug.WriteLine($"Найден стартовый индекс {currentIndex} для страницы {pageIndex}");
            return currentIndex;
        }

        private int CalculateTotalPages()
        {
            if (_bookContent == null || !_bookContent.Any())
            {
                return 0;
            }

            double pageHeight = LeftPageViewer.ActualHeight - 40;
            double fontSize = (double)Resources["PageFontSize"];
            double lineHeight = fontSize * 1.5;
            int maxLinesPerColumn = (int)(pageHeight / lineHeight);

            int totalPages = 0;
            int currentIndex = 0;

            System.Diagnostics.Debug.WriteLine("CalculateTotalPages: Подсчёт страниц");

            while (currentIndex < _bookContent.Count)
            {
                int linesAdded = 0;
                bool isLeftColumn = true;

                while (currentIndex < _bookContent.Count && linesAdded < maxLinesPerColumn)
                {
                    var (isChapterStart, isTitle, isToc, paragraph) = _bookContent[currentIndex];
                    int lineCount = EstimateLineCount(paragraph, fontSize);

                    if (isChapterStart && !isToc && currentIndex > 0 && linesAdded > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"Прерываем страницу {totalPages} для новой главы");
                        break; // Новая страница для главы
                    }

                    if (isLeftColumn)
                    {
                        linesAdded += lineCount;
                        currentIndex++;
                    }
                    else
                    {
                        if (linesAdded + lineCount <= maxLinesPerColumn && !isTitle && !isToc)
                        {
                            linesAdded += lineCount;
                            currentIndex++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (linesAdded >= maxLinesPerColumn && isLeftColumn && !isTitle && !isToc)
                    {
                        isLeftColumn = false;
                        linesAdded = 0; // Сбрасываем для правой колонки
                    }

                    if ((isTitle || isToc) && linesAdded > 0)
                    {
                        break; // Название и оглавление не перетекают
                    }
                }
                totalPages++;
                System.Diagnostics.Debug.WriteLine($"Страница {totalPages} завершена, индекс={currentIndex}");
            }

            System.Diagnostics.Debug.WriteLine($"Всего страниц: {totalPages}");
            return totalPages;
        }

        private int EstimateLineCount(Paragraph paragraph, double fontSize)
        {
            var run = paragraph.Inlines.OfType<Run>().FirstOrDefault();
            if (run == null || string.IsNullOrEmpty(run.Text))
            {
                System.Diagnostics.Debug.WriteLine("EstimateLineCount: Параграф пуст, возвращаем 1 строку");
                return 1;
            }

            string text = run.Text;
            double columnWidth = LeftPageViewer.ActualWidth - 40; // Учитываем PagePadding
            double avgCharWidth = fontSize * 0.5; // Уменьшили ширину символа для точности
            int avgCharsPerLine = (int)(columnWidth / avgCharWidth);
            int lines = (int)Math.Ceiling((double)text.Length / avgCharsPerLine);
            int estimatedLines = Math.Max(lines, 1);
            System.Diagnostics.Debug.WriteLine($"EstimateLineCount: Текст='{text.Substring(0, Math.Min(50, text.Length))}...', строк={estimatedLines}, символов={text.Length}, avgCharsPerLine={avgCharsPerLine}");
            return estimatedLines;
        }

        private Paragraph CloneParagraph(Paragraph original)
        {
            var newParagraph = new Paragraph();
            foreach (var inline in original.Inlines)
            {
                if (inline is Run run)
                {
                    newParagraph.Inlines.Add(new Run(run.Text)
                    {
                        FontWeight = run.FontWeight,
                        FontSize = run.FontSize,
                        FontFamily = run.FontFamily,
                        Foreground = run.Foreground
                    });
                }
            }
            newParagraph.Margin = original.Margin;
            newParagraph.FontWeight = original.FontWeight;
            newParagraph.FontSize = original.FontSize;
            newParagraph.FontFamily = original.FontFamily;
            newParagraph.Foreground = original.Foreground;
            newParagraph.TextAlignment = original.TextAlignment;
            return newParagraph;
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
            if (_currentPageIndex < CalculateTotalPages() - 1)
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