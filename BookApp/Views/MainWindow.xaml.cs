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
using System.Windows.Navigation;
using BookApp.Data;
using BookApp.Models;
using BookApp.Services;
using BookApp.Views;
using Microsoft.EntityFrameworkCore;
using System.Windows.Threading;

namespace BookApp
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _dbService = new DatabaseService();
        private readonly BookParserService _parserService = new BookParserService();
        private readonly FileDialogService _fileDialogService = new FileDialogService();
        private readonly RecommendationService _recommendationService = new RecommendationService();
        private string _currentUser;
        private int _currentUserId;
        public Book _currentBook;
        private ReadingHistory _currentReadingHistory;
        private List<(bool isChapterStart, bool isTitle, bool isToc, Block block)> _bookContent = new();
        private List<(string title, string anchorName)> _tableOfContents = new();
        private const double FONT_SIZE = 16;
        private DispatcherTimer _positionSaveTimer;

        public MainWindow(string username)
        {
            InitializeComponent();
            _currentUser = username ?? "Guest";
            InitializeUser();
            _dbService.InitializeDefaultBooks();
            LoadUserSettings();

            // Инициализация таймера для периодического сохранения позиции
            _positionSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _positionSaveTimer.Tick += (s, e) => SaveReadingPosition(GetCurrentPosition());
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
                Debug.WriteLine($"Пользователь инициализирован с ID: {_currentUserId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка инициализации пользователя: {ex.Message}");
                MessageBox.Show($"Ошибка инициализации пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.O && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
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
                        PagesCount = null // Оставляем как null, будем оценивать позже
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

                // Оценка количества страниц на основе количества параграфов или секций
                int estimatedPages = Math.Max(1, content.Count / 10); // Примерная оценка: 10 параграфов на страницу
                if (!_currentBook.PagesCount.HasValue)
                {
                    _currentBook.PagesCount = estimatedPages;
                    db.SaveChanges();
                }

                Debug.WriteLine($"Книга разобрана. Всего параграфов: {content.Count}, оценочное количество страниц: {estimatedPages}");

                _bookContent.Clear();
                _tableOfContents.Clear();
                int chapterCount = 0;

                var titleParagraph = new Paragraph(new Run(book.Title))
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = FONT_SIZE + 6,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                };
                _bookContent.Add((false, true, false, titleParagraph));

                Section tocSection = null;
                if (content.Any(c => c.isChapterStart))
                {
                    tocSection = new Section { BreakPageBefore = false };
                    var tocParagraph = new Paragraph(new Run("Оглавление"))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = FONT_SIZE + 6,
                        TextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 20)
                    };
                    tocSection.Blocks.Add(tocParagraph);
                    _bookContent.Add((false, false, true, tocParagraph));
                }

                Section currentSection = null;
                foreach (var (isChapterStart, paragraph) in content)
                {
                    bool isTitle = _bookContent.Count == 1;
                    bool isToc = false;

                    if (isChapterStart)
                    {
                        if (currentSection != null)
                        {
                            _bookContent.Add((false, false, false, currentSection));
                        }

                        currentSection = new Section { BreakPageBefore = true };
                        currentSection.Blocks.Add(paragraph);

                        var run = paragraph.Inlines.OfType<Run>().FirstOrDefault();
                        if (run != null && !string.IsNullOrEmpty(run.Text))
                        {
                            string anchorName = $"chapter_{chapterCount}";
                            paragraph.Name = anchorName;
                            _tableOfContents.Add((run.Text, anchorName));

                            var tocEntry = new Paragraph
                            {
                                FontSize = FONT_SIZE,
                                Margin = new Thickness(20, 5, 0, 5)
                            };
                            var hyperlink = new Hyperlink(new Run(run.Text))
                            {
                                NavigateUri = new Uri($"#{anchorName}", UriKind.Relative),
                                ToolTip = $"Перейти к {run.Text}"
                            };
                            hyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                            tocEntry.Inlines.Add(hyperlink);
                            tocSection?.Blocks.Add(tocEntry);
                        }

                        chapterCount++;
                    }
                    else
                    {
                        if (currentSection == null)
                        {
                            currentSection = new Section { BreakPageBefore = true };
                        }
                        currentSection.Blocks.Add(paragraph);
                    }
                }

                if (currentSection != null)
                {
                    _bookContent.Add((false, false, false, currentSection));
                }

                if (tocSection != null && tocSection.Blocks.Count > 1)
                {
                    _bookContent.Insert(1, (false, false, true, tocSection));
                }

                Debug.WriteLine("Перед отображением книги...");
                DisplayBookContent();

                BookTitleTextBlock.Text = book.Title;
                BookTitleTextBlock.Visibility = Visibility.Visible;

                _currentReadingHistory = db.ReadingHistory.FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == book.Id);
                if (_currentReadingHistory != null && !string.IsNullOrEmpty(_currentReadingHistory.LastReadPosition))
                {
                    Debug.WriteLine($"Найдена сохраненная позиция чтения: {_currentReadingHistory.LastReadPosition}");
                    RestoreReadingPosition(_currentReadingHistory.LastReadPosition);
                }
                else
                {
                    Debug.WriteLine("Сохраненная позиция чтения отсутствует, книга должна открыться с начала.");
                    ScrollToStartWithRetries();
                }

                UpdateProgressDisplay();
                _positionSaveTimer.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка открытия книги: {ex.Message}");
                MessageBox.Show($"Ошибка при открытия книги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ScrollToStartWithRetries(int maxRetries = 3, int delayMs = 200)
        {
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    var firstBlock = LeftPageDocument.Blocks.FirstOrDefault();
                    if (firstBlock != null)
                    {
                        firstBlock.BringIntoView();
                        var viewer = FindScrollViewer(LeftPageViewer);
                        if (viewer != null)
                        {
                            viewer.ScrollToTop();
                            Debug.WriteLine($"Попытка {attempt}: ScrollViewer прокручен в начало. VerticalOffset = {viewer.VerticalOffset}");
                            // Проверяем, действительно ли прокрутка удалась
                            if (viewer.VerticalOffset > 0)
                            {
                                Debug.WriteLine($"Попытка {attempt}: VerticalOffset не 0, повторная прокрутка...");
                                viewer.ScrollToTop();
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"Попытка {attempt}: ScrollViewer не найден в OpenBook.");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Попытка {attempt}: Первый блок документа не найден в OpenBook.");
                    }
                }, DispatcherPriority.Render);

                // Задержка перед следующей попыткой
                if (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            if (sender is Hyperlink hyperlink)
            {
                string anchor = hyperlink.NavigateUri.ToString().TrimStart('#');
                var target = LeftPageDocument.FindName(anchor) as Paragraph;
                if (target != null)
                {
                    target.BringIntoView();
                    SaveReadingPosition(target.ContentStart);
                }
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
                SaveReadingPosition(GetCurrentPosition());
                _positionSaveTimer.Stop();
            }
            _currentBook = null;
            _currentReadingHistory = null;
            _bookContent.Clear();
            _tableOfContents.Clear();
            LeftPageDocument.Blocks.Clear();
            BookTitleTextBlock.Visibility = Visibility.Collapsed;
            UpdateProgressDisplay();
        }

        public void SaveReadingPosition(TextPointer position)
        {
            if (_currentBook == null || _currentUserId == 0 || position == null) return;

            try
            {
                using var db = CreateDbContext();
                var history = db.ReadingHistory
                    .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == _currentBook.Id);

                string positionStr = position.GetOffsetToPosition(LeftPageDocument.ContentStart).ToString();
                int estimatedPage = EstimateCurrentPage(position);

                var documentStart = LeftPageDocument.ContentStart;
                var documentEnd = LeftPageDocument.ContentEnd;
                int currentOffset = position.GetOffsetToPosition(documentStart);
                int totalLength = documentEnd.GetOffsetToPosition(documentStart);
                bool isRead = totalLength > 0 && currentOffset >= totalLength * 0.95;

                if (history == null)
                {
                    history = new ReadingHistory
                    {
                        UserId = _currentUserId,
                        BookId = _currentBook.Id,
                        LastReadPage = estimatedPage,
                        LastReadPosition = positionStr,
                        LastReadDate = DateTime.UtcNow,
                        IsRead = isRead
                    };
                    db.ReadingHistory.Add(history);
                }
                else
                {
                    history.LastReadPage = estimatedPage;
                    history.LastReadPosition = positionStr;
                    history.LastReadDate = DateTime.UtcNow;
                    history.IsRead = isRead;
                }
                _currentReadingHistory = history;
                db.SaveChanges();
                Debug.WriteLine($"Прогресс чтения сохранен: позиция {positionStr}, страница {estimatedPage}, прочитано: {isRead}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения прогресса чтения: {ex.Message}");
                MessageBox.Show($"Ошибка сохранения прогресса чтения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RestoreReadingPosition(string positionStr)
        {
            if (string.IsNullOrEmpty(positionStr))
            {
                Debug.WriteLine("Позиция не указана, прокрутка к началу.");
                ScrollToStartWithRetries();
                return;
            }

            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (!int.TryParse(positionStr, out int offset))
                    {
                        Debug.WriteLine("Некорректный формат позиции, прокрутка к началу.");
                        ScrollToStartWithRetries();
                        return;
                    }

                    var document = LeftPageDocument;
                    var position = document.ContentStart.GetPositionAtOffset(offset);
                    if (position == null)
                    {
                        Debug.WriteLine($"Позиция с offset {offset} не найдена, прокрутка к началу.");
                        ScrollToStartWithRetries();
                        return;
                    }

                    // Прокрутка к сохраненной позиции
                    position.Paragraph?.BringIntoView();
                    var viewer = FindScrollViewer(LeftPageViewer);
                    if (viewer != null)
                    {
                        var rect = position.GetCharacterRect(LogicalDirection.Forward);
                        viewer.ScrollToVerticalOffset(rect.Top);
                        Debug.WriteLine($"Позиция восстановлена: offset = {offset}, VerticalOffset = {viewer.VerticalOffset}");
                    }
                    else
                    {
                        Debug.WriteLine("ScrollViewer не найден, прокрутка к началу.");
                        ScrollToStartWithRetries();
                    }

                    // Перезапуск таймера для сохранения позиции
                    if (_positionSaveTimer != null)
                    {
                        _positionSaveTimer.Stop();
                        _positionSaveTimer.Start();
                        Debug.WriteLine("Таймер перезапущен для сохранения позиции.");
                    }
                    else
                    {
                        Debug.WriteLine("Таймер не инициализирован.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка восстановления позиции: {ex.Message}");
                    ScrollToStartWithRetries();
                }
            }, DispatcherPriority.Render);
        }

        private void ScrollToStart()
        {
            var firstBlock = LeftPageDocument.Blocks.FirstOrDefault();
            if (firstBlock != null)
            {
                firstBlock.BringIntoView();
                var viewer = FindScrollViewer(LeftPageViewer);
                if (viewer != null)
                {
                    viewer.ScrollToTop();
                    Debug.WriteLine("Прокрутка к началу выполнена.");
                }
                else
                {
                    Debug.WriteLine("ScrollViewer не найден для прокрутки к началу.");
                }
            }
            else
            {
                Debug.WriteLine("Первый блок документа не найден.");
            }
        }

        public ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private TextPointer GetCurrentPosition()
        {
            var viewer = FindScrollViewer(LeftPageViewer);
            if (viewer == null) return LeftPageDocument.ContentStart;

            var offset = viewer.VerticalOffset;
            var viewportHeight = viewer.ViewportHeight;

            TextPointer currentPosition = LeftPageDocument.ContentStart;
            TextPointer closestPosition = LeftPageDocument.ContentStart;
            double closestDifference = double.MaxValue;

            while (currentPosition != null && currentPosition.CompareTo(LeftPageDocument.ContentEnd) < 0)
            {
                var rect = currentPosition.GetCharacterRect(LogicalDirection.Forward);
                if (rect.IsEmpty) continue;

                double difference = Math.Abs(rect.Top - offset);
                if (difference < closestDifference)
                {
                    closestDifference = difference;
                    closestPosition = currentPosition;
                }

                if (rect.Top > offset + viewportHeight)
                    break;

                currentPosition = currentPosition.GetNextInsertionPosition(LogicalDirection.Forward);
            }

            return closestPosition;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_currentBook != null)
            {
                SaveReadingPosition(GetCurrentPosition());
                _positionSaveTimer.Stop();
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
                UpdateDocumentSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDisplaySettings()
        {
            if (_currentUserId == 0)
            {
                Debug.WriteLine("Пользователь не идентифицирован, пропуск сохранения настроек.");
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
                Debug.WriteLine($"Настройки отображения сохранены для пользователя {_currentUserId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
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
                UpdateDocumentSettings();
            }
        }

        private void FontColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                var color = item.Tag.ToString();
                Resources["PageForeground"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
                UpdateDocumentSettings();
            }
        }

        private void FontFamilyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item)
            {
                Resources["PageFontFamily"] = new FontFamily(item.Header.ToString());
                SaveDisplaySettings();
                UpdateDocumentSettings();
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

        private void DisplayBookContent()
        {
            LeftPageDocument.Blocks.Clear();

            foreach (var (isChapterStart, isTitle, isToc, block) in _bookContent)
            {
                if (block is Paragraph paragraph)
                {
                    var clonedParagraph = CloneParagraph(paragraph);
                    LeftPageDocument.Blocks.Add(clonedParagraph);
                }
                else if (block is Section section)
                {
                    var clonedSection = CloneSection(section);
                    LeftPageDocument.Blocks.Add(clonedSection);
                }
            }

            // Принудительное обновление layout перед восстановлением позиции
            LeftPageViewer.UpdateLayout();
            UpdateProgressDisplay();
        }

        private Paragraph CloneParagraph(Paragraph original)
        {
            if (original == null)
            {
                Debug.WriteLine("CloneParagraph: исходный параграф null");
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
                else if (inline is Hyperlink hyperlink)
                {
                    var newHyperlink = new Hyperlink(new Run(((Run)hyperlink.Inlines.FirstInline).Text))
                    {
                        NavigateUri = hyperlink.NavigateUri,
                        ToolTip = hyperlink.ToolTip
                    };
                    newHyperlink.RequestNavigate += Hyperlink_RequestNavigate;
                    paragraph.Inlines.Add(newHyperlink);
                }
            }
            paragraph.Name = original.Name;
            paragraph.FontWeight = original.FontWeight;
            paragraph.FontSize = original.FontSize;
            paragraph.FontFamily = (FontFamily)Resources["PageFontFamily"];
            paragraph.Foreground = (SolidColorBrush)Resources["PageForeground"];
            paragraph.Margin = original.Margin;
            paragraph.TextAlignment = original.TextAlignment;
            paragraph.TextIndent = original.TextIndent;
            return paragraph;
        }

        private Section CloneSection(Section original)
        {
            var section = new Section
            {
                BreakPageBefore = original.BreakPageBefore
            };
            foreach (var block in original.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    section.Blocks.Add(CloneParagraph(paragraph));
                }
            }
            return section;
        }

        public void UpdateProgressDisplay()
        {
            if (_currentBook == null || _bookContent == null || !_bookContent.Any())
            {
                BookProgressText.Text = "";
                Debug.WriteLine("UpdateProgressDisplay: книга или содержимое отсутствуют.");
                return;
            }

            var currentPosition = GetCurrentPosition();
            var documentStart = LeftPageDocument.ContentStart;
            var documentEnd = LeftPageDocument.ContentEnd;
            int currentOffset = currentPosition.GetOffsetToPosition(documentStart);
            int totalLength = Math.Max(1, documentEnd.GetOffsetToPosition(documentStart));
            double progress = totalLength > 0 ? (double)currentOffset / totalLength * 100 : 0;
            int estimatedPage = EstimateCurrentPage(currentPosition);
            int totalPages = _currentBook.PagesCount ?? totalLength; // Используем totalLength как резерв

            Debug.WriteLine($"UpdateProgressDisplay: currentOffset = {currentOffset}, totalLength = {totalLength}, progress = {progress:F1}%, estimatedPage = {estimatedPage}, totalPages = {totalPages}");

            BookProgressText.Text = $"Страница: {Math.Max(1, estimatedPage)}/{Math.Max(1, totalPages)} ({progress:F1}%)";
        }

        private int EstimateCurrentPage(TextPointer position)
        {
            var documentStart = LeftPageDocument.ContentStart;
            var documentEnd = LeftPageDocument.ContentEnd;
            int currentOffset = position.GetOffsetToPosition(documentStart);
            int totalLength = Math.Max(1, documentEnd.GetOffsetToPosition(documentStart)); // Избегаем нуля или отрицательного значения
            if (_currentBook.PagesCount.HasValue && _currentBook.PagesCount.Value > 0)
            {
                double progress = totalLength > 0 ? (double)currentOffset / totalLength : 0;
                return (int)Math.Max(1, Math.Round(progress * _currentBook.PagesCount.Value));
            }
            return Math.Max(1, currentOffset); // Если PagesCount неизвестен, используем текущий оффсет как примерную страницу
        }

        private void PageViewer_Loaded(object sender, RoutedEventArgs e)
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("PageViewer загружен, содержимое присутствует.");
            }
            else
            {
                Debug.WriteLine("Содержимое книги отсутствует в PageViewer_Loaded.");
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("Размер окна изменен, обновление отображения...");
                UpdateProgressDisplay();
            }
        }

        private void UpdateDocumentSettings()
        {
            if (_bookContent != null && _bookContent.Any())
            {
                Debug.WriteLine("Обновление настроек документа...");
                DisplayBookContent();
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
            var favoritesWindow = new FavoritesWindow(_currentUserId);
            favoritesWindow.ShowDialog();
        }

        private void TableOfContents_Click(object sender, RoutedEventArgs e)
        {
            if (_tableOfContents == null || !_tableOfContents.Any())
            {
                MessageBox.Show("Оглавление отсутствует.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Paragraph toc = FindParagraphWithTitle("Оглавление");
            if (toc != null)
            {
                toc.BringIntoView();
                SaveReadingPosition(toc.ContentStart);
            }
            else
            {
                MessageBox.Show("Не удалось найти оглавление в документе.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            UpdateProgressDisplay();
        }

        private Paragraph FindParagraphWithTitle(string title)
        {
            foreach (Block block in LeftPageDocument.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    string text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Trim();
                    if (text.StartsWith(title))
                    {
                        return paragraph;
                    }
                }
            }
            return null;
        }

        private void ShowBookmarks_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBook == null)
            {
                MessageBox.Show("Книга не открыта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var bookmarksWindow = new BookmarksWindow(_currentUserId, _currentBook.Id, this);
            bookmarksWindow.ShowDialog();
        }

        private void ShowRecommendations_Click(object sender, RoutedEventArgs e)
        {
            var recommendationsWindow = new RecommendationsWindow(_currentUserId, this);
            recommendationsWindow.ShowDialog();
        }

        private void MarkAsReadCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateReadingStatus(true);
        }

        private void MarkAsReadCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateReadingStatus(false);
        }

        private void UpdateReadingStatus(bool isRead)
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
                        LastReadPage = 1,
                        LastReadPosition = "0",
                        LastReadDate = DateTime.UtcNow,
                        IsRead = isRead
                    };
                    db.ReadingHistory.Add(history);
                }
                else
                {
                    history.IsRead = isRead;
                    history.LastReadDate = DateTime.UtcNow;
                }
                _currentReadingHistory = history;
                db.SaveChanges();
                Debug.WriteLine($"Статус 'Прочитано' обновлён: {isRead}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления статуса 'Прочитано': {ex.Message}");
                MessageBox.Show($"Ошибка обновления статуса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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