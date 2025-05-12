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
        private List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> _bookContent = new();
        private int _currentPageIndex = 0;

        public MainWindow(string username)
        {
            InitializeComponent();
            _currentUser = username;
            NicknameTextBox.Text = _currentUser;

            using var db = CreateDbContext();
            _currentUserId = db.Users.First(u => u.Username == _currentUser).Id;

            _dbService.InitializeDefaultBooks();
            LoadUserSettings();
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

                _bookContent = content;
                var history = db.ReadingHistory
                    .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == book.Id);

                _currentPageIndex = history?.LastReadPage ?? 0;
                DisplayBookContent(_bookContent);
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

        private void DisplayBookContent(List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> content)
        {
            _bookContent = content ?? new List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)>();
            _currentPageIndex = 0;
            ShowCurrentPage();
        }

        private void ShowCurrentPage()
        {
            LeftPageDocument.Blocks.Clear();
            RightPageDocument.Blocks.Clear();

            if (_bookContent == null || !_bookContent.Any())
            {
                System.Diagnostics.Debug.WriteLine("ShowCurrentPage: Контент книги отсутствует");
                return;
            }

            int currentIndex = FindPageStartIndex(_currentPageIndex);
            System.Diagnostics.Debug.WriteLine($"ShowCurrentPage: Страница {_currentPageIndex}, startIndex={currentIndex}, bookContentCount={_bookContent.Count}");

            int totalLeftLines = 0;
            int totalRightLines = 0;
            const int maxLinesPerColumn = 40; // Максимум строк на колонку
            bool isLeftColumn = true;

            while (currentIndex < _bookContent.Count)
            {
                var (isChapterStart, isTitle, isToc, isPageBreak, paragraph) = _bookContent[currentIndex];
                var clonedParagraph = CloneParagraph(paragraph);
                if (clonedParagraph == null)
                {
                    System.Diagnostics.Debug.WriteLine($"ShowCurrentPage: Параграф {currentIndex} не склонирован, пропускаем");
                    currentIndex++;
                    continue;
                }

                int lineCount = EstimateLineCount(clonedParagraph);
                System.Diagnostics.Debug.WriteLine($"Обработка параграфа {currentIndex}: isChapterStart={isChapterStart}, isTitle={isTitle}, isToc={isToc}, isPageBreak={isPageBreak}, текст={GetParagraphText(clonedParagraph).Substring(0, Math.Min(50, GetParagraphText(clonedParagraph).Length))}..., строк={lineCount}");

                // Страница 0: Название в левой колонке, оглавление в правой
                if (_currentPageIndex == 0)
                {
                    if (isTitle && isLeftColumn)
                    {
                        LeftPageDocument.Blocks.Add(clonedParagraph);
                        totalLeftLines += lineCount;
                        System.Diagnostics.Debug.WriteLine("Добавлено название в левую колонку");
                        currentIndex++;
                        continue;
                    }
                    else if (isToc && !isLeftColumn)
                    {
                        if (totalRightLines + lineCount <= maxLinesPerColumn)
                        {
                            RightPageDocument.Blocks.Add(clonedParagraph);
                            totalRightLines += lineCount;
                            System.Diagnostics.Debug.WriteLine($"Добавлен параграф оглавления в правую колонку (lines={lineCount}, total={totalRightLines})");
                            currentIndex++;
                            continue;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Оглавление не помещается, переход на следующую страницу");
                            break; // Перейти на следующую страницу
                        }
                    }
                    else if (isPageBreak && isLeftColumn && LeftPageDocument.Blocks.Any())
                    {
                        isLeftColumn = false; // Переключиться на правую колонку для оглавления
                        currentIndex++;
                        System.Diagnostics.Debug.WriteLine("Переключение на правую колонку после разрыва страницы");
                        continue;
                    }
                    else
                    {
                        currentIndex++;
                        continue; // Пропустить неподходящие параграфы
                    }
                }

                // Остальные страницы
                if (isTitle)
                {
                    System.Diagnostics.Debug.WriteLine($"Пропущено название на странице {_currentPageIndex}");
                    currentIndex++;
                    continue;
                }

                if (isPageBreak)
                {
                    if ((isLeftColumn && totalLeftLines > 0) || (!isLeftColumn && totalRightLines > 0))
                    {
                        System.Diagnostics.Debug.WriteLine($"Прерываем страницу {_currentPageIndex} из-за isPageBreak");
                        break;
                    }
                    currentIndex++;
                    continue;
                }

                if (isLeftColumn)
                {
                    if (totalLeftLines + lineCount <= maxLinesPerColumn)
                    {
                        LeftPageDocument.Blocks.Add(clonedParagraph);
                        totalLeftLines += lineCount;
                        System.Diagnostics.Debug.WriteLine($"Добавлен параграф в левую колонку (lines={lineCount}, total={totalLeftLines})");
                        currentIndex++;
                    }
                    else
                    {
                        isLeftColumn = false;
                        System.Diagnostics.Debug.WriteLine("Переключение на правую колонку");
                    }
                }
                else
                {
                    if (totalRightLines + lineCount <= maxLinesPerColumn)
                    {
                        RightPageDocument.Blocks.Add(clonedParagraph);
                        totalRightLines += lineCount;
                        System.Diagnostics.Debug.WriteLine($"Добавлен параграф в правую колонку (lines={lineCount}, total={totalRightLines})");
                        currentIndex++;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Прерываем страницу {_currentPageIndex} из-за превышения строк");
                        break;
                    }
                }

                if (isChapterStart)
                {
                    System.Diagnostics.Debug.WriteLine($"Прерываем страницу {_currentPageIndex} из-за начала главы");
                    break;
                }
            }
        }

        private int FindPageStartIndex(int pageIndex)
        {
            if (pageIndex == 0) return 0;

            int currentIndex = 0;
            int currentPage = 0;
            int totalLeftLines = 0;
            int totalRightLines = 0;
            const int maxLinesPerColumn = 40;
            bool isLeftColumn = true;

            System.Diagnostics.Debug.WriteLine($"FindPageStartIndex: Ищем индекс для страницы {pageIndex}");

            while (currentIndex < _bookContent.Count && currentPage < pageIndex)
            {
                var (isChapterStart, isTitle, isToc, isPageBreak, paragraph) = _bookContent[currentIndex];
                int lineCount = EstimateLineCount(paragraph);

                System.Diagnostics.Debug.WriteLine($"FindPageStartIndex: Параграф {currentIndex}, isChapterStart={isChapterStart}, isTitle={isTitle}, isToc={isToc}, isPageBreak={isPageBreak}, строк={lineCount}");

                // Страница 0: Название и оглавление
                if (currentPage == 0)
                {
                    if (isTitle && isLeftColumn)
                    {
                        totalLeftLines += lineCount;
                        currentIndex++;
                        continue;
                    }
                    if (isPageBreak && isLeftColumn && totalLeftLines > 0)
                    {
                        isLeftColumn = false;
                        currentIndex++;
                        continue;
                    }
                    if (isToc && !isLeftColumn)
                    {
                        if (totalRightLines + lineCount <= maxLinesPerColumn)
                        {
                            totalRightLines += lineCount;
                            currentIndex++;
                            continue;
                        }
                        else
                        {
                            currentPage++;
                            totalLeftLines = 0;
                            totalRightLines = 0;
                            isLeftColumn = true;
                            continue;
                        }
                    }
                    currentIndex++;
                    continue;
                }

                // Остальные страницы
                if (isPageBreak && ((isLeftColumn && totalLeftLines > 0) || (!isLeftColumn && totalRightLines > 0)))
                {
                    currentPage++;
                    totalLeftLines = 0;
                    totalRightLines = 0;
                    isLeftColumn = true;
                    currentIndex++;
                    continue;
                }

                if (isLeftColumn)
                {
                    if (totalLeftLines + lineCount <= maxLinesPerColumn)
                    {
                        totalLeftLines += lineCount;
                        currentIndex++;
                    }
                    else
                    {
                        isLeftColumn = false;
                    }
                }
                else
                {
                    if (totalRightLines + lineCount <= maxLinesPerColumn)
                    {
                        totalRightLines += lineCount;
                        currentIndex++;
                    }
                    else
                    {
                        currentPage++;
                        totalLeftLines = 0;
                        totalRightLines = 0;
                        isLeftColumn = true;
                    }
                }

                if (isChapterStart && (totalLeftLines > 0 || totalRightLines > 0))
                {
                    currentPage++;
                    totalLeftLines = 0;
                    totalRightLines = 0;
                    isLeftColumn = true;
                }
            }

            System.Diagnostics.Debug.WriteLine($"Найден стартовый индекс {currentIndex} для страницы {pageIndex}");
            return currentIndex;
        }

        private Paragraph CloneParagraph(Paragraph original)
        {
            if (original == null)
            {
                System.Diagnostics.Debug.WriteLine("CloneParagraph: исходный параграф null");
                return new Paragraph();
            }

            var cloned = new Paragraph();
            foreach (var inline in original.Inlines)
            {
                if (inline is Run originalRun)
                {
                    var clonedRun = new Run(originalRun.Text)
                    {
                        FontWeight = originalRun.FontWeight,
                        FontSize = originalRun.FontSize
                    };
                    cloned.Inlines.Add(clonedRun);
                }
            }
            cloned.FontWeight = original.FontWeight;
            cloned.FontSize = original.FontSize;
            cloned.Margin = original.Margin;
            cloned.TextAlignment = original.TextAlignment;
            return cloned;
        }

        private int CalculateTotalPages()
        {
            if (_bookContent == null || !_bookContent.Any())
            {
                return 0;
            }

            int totalPages = 0;
            int currentIndex = 0;
            int totalLeftLines = 0;
            int totalRightLines = 0;
            const int maxLinesPerColumn = 40;
            bool isLeftColumn = true;

            System.Diagnostics.Debug.WriteLine("CalculateTotalPages: Подсчёт страниц");

            while (currentIndex < _bookContent.Count)
            {
                var (isChapterStart, isTitle, isToc, isPageBreak, paragraph) = _bookContent[currentIndex];
                int lineCount = EstimateLineCount(paragraph);

                // Страница 0: Название и оглавление
                if (totalPages == 0)
                {
                    if (isTitle && isLeftColumn)
                    {
                        totalLeftLines += lineCount;
                        currentIndex++;
                        continue;
                    }
                    if (isPageBreak && isLeftColumn && totalLeftLines > 0)
                    {
                        isLeftColumn = false;
                        currentIndex++;
                        continue;
                    }
                    if (isToc && !isLeftColumn)
                    {
                        if (totalRightLines + lineCount <= maxLinesPerColumn)
                        {
                            totalRightLines += lineCount;
                            currentIndex++;
                            continue;
                        }
                        else
                        {
                            totalPages++;
                            totalLeftLines = 0;
                            totalRightLines = 0;
                            isLeftColumn = true;
                            continue;
                        }
                    }
                    currentIndex++;
                    continue;
                }

                // Остальные страницы
                if (isPageBreak && ((isLeftColumn && totalLeftLines > 0) || (!isLeftColumn && totalRightLines > 0)))
                {
                    totalPages++;
                    totalLeftLines = 0;
                    totalRightLines = 0;
                    isLeftColumn = true;
                    currentIndex++;
                    continue;
                }

                if (isLeftColumn)
                {
                    if (totalLeftLines + lineCount <= maxLinesPerColumn)
                    {
                        totalLeftLines += lineCount;
                        currentIndex++;
                    }
                    else
                    {
                        isLeftColumn = false;
                    }
                }
                else
                {
                    if (totalRightLines + lineCount <= maxLinesPerColumn)
                    {
                        totalRightLines += lineCount;
                        currentIndex++;
                    }
                    else
                    {
                        totalPages++;
                        totalLeftLines = 0;
                        totalRightLines = 0;
                        isLeftColumn = true;
                    }
                }

                if (isChapterStart && (totalLeftLines > 0 || totalRightLines > 0))
                {
                    totalPages++;
                    totalLeftLines = 0;
                    totalRightLines = 0;
                    isLeftColumn = true;
                }
            }

            if (totalLeftLines > 0 || totalRightLines > 0)
            {
                totalPages++;
            }

            System.Diagnostics.Debug.WriteLine($"Всего страниц: {totalPages}");
            return totalPages;
        }

        private int EstimateLineCount(Paragraph paragraph)
        {
            if (paragraph == null || !paragraph.Inlines.Any())
                return 0;

            var text = string.Join("", paragraph.Inlines.OfType<Run>().Select(r => r.Text));
            var avgCharsPerLine = 60; // Среднее количество символов в строке
            return (int)Math.Ceiling((double)text.Length / avgCharsPerLine) + 1;
        }

        private string GetParagraphText(Paragraph paragraph)
        {
            if (paragraph == null)
                return string.Empty;
            return string.Join("", paragraph.Inlines.OfType<Run>().Select(r => r.Text));
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