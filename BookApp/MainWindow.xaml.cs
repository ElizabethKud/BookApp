using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BookApp.Data;
using BookApp.Models;
using BookApp.Services;
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
        private List<string> _bookPages = new();
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

            // Заполнение комбобоксов для оформления
            BackgroundColorComboBox.Items.Add(new ComboBoxItem { Content = "Белый", Tag = "#FFFFFF" });
            BackgroundColorComboBox.Items.Add(new ComboBoxItem { Content = "Черный", Tag = "#000000" });
            BackgroundColorComboBox.Items.Add(new ComboBoxItem { Content = "Серый", Tag = "#D3D3D3" });
            BackgroundColorComboBox.Items.Add(new ComboBoxItem { Content = "Сепия", Tag = "#F4ECD8" });
            BackgroundColorComboBox.Items.Add(new ComboBoxItem { Content = "Темно-синий", Tag = "#1E3A5F" });

            FontColorComboBox.Items.Add(new ComboBoxItem { Content = "Черный", Tag = "#000000" });
            FontColorComboBox.Items.Add(new ComboBoxItem { Content = "Белый", Tag = "#FFFFFF" });
            FontColorComboBox.Items.Add(new ComboBoxItem { Content = "Серый", Tag = "#666666" });
            FontColorComboBox.Items.Add(new ComboBoxItem { Content = "Коричневый", Tag = "#5C4033" });

            FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = "Arial" });
            FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = "Times New Roman" });
            FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = "Segoe UI" });
            FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = "Calibri" });
            FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = "Georgia" });
            FontFamilyComboBox.Items.Add(new ComboBoxItem { Content = "Verdana" });

            BackgroundColorComboBox.SelectedIndex = 0;
            FontColorComboBox.SelectedIndex = 0;
            FontFamilyComboBox.SelectedIndex = 0;
            FontSizeSlider.Value = 16;

            // Загрузка настроек пользователя
            LoadUserSettings();
        }

        private void OpenBook_Click(object sender, RoutedEventArgs e)
        {
            var filePath = _fileDialogService.OpenFileDialog();
            if (string.IsNullOrEmpty(filePath)) return;

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

                    // Добавляем автора через связь BookAuthor
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
                if (string.IsNullOrEmpty(content))
                {
                    MessageBox.Show("Не удалось прочитать содержимое книги.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Загрузка истории перед отображением
                var history = db.ReadingHistory
                    .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == book.Id);

                if (history != null)
                {
                    _currentPageIndex = history.LastReadPage;
                }
                else
                {
                    _currentPageIndex = 0;
                }

                // Только после установки _currentPageIndex
                DisplayBookContent(content);

                var tabControl = (TabControl)FindName("TabControl") ?? (TabControl)((Grid)Content).Children[0];
                var bookTab = tabControl.Items.OfType<TabItem>().FirstOrDefault(t => t.Header.ToString() == "Книга");
                if (bookTab != null)
                {
                    tabControl.SelectedItem = bookTab;
                }
                else
                {
                    MessageBox.Show("Вкладка 'Книга' не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии книги: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void BookScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_currentBook == null) return;

            // Пример: считаем страницу на основе вертикального смещения
            var scrollViewer = (ScrollViewer)sender;
            int estimatedPage = (int)(scrollViewer.VerticalOffset / 100) + 1; // Примерная логика
            SaveReadingProgress(estimatedPage);
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
            // Сохраняем последнюю страницу (примерная страница, доработать для точности)
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
                        var background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BackgroundColor));
                        var foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FontColor));
                        LeftPageTextBlock.Background = RightPageTextBlock.Background = background;
                        LeftPageTextBlock.Foreground = RightPageTextBlock.Foreground = foreground;
                    }
                    catch (FormatException)
                    {
                        LeftPageTextBlock.Background = RightPageTextBlock.Background = new SolidColorBrush(Colors.White);
                        LeftPageTextBlock.Foreground = RightPageTextBlock.Foreground = new SolidColorBrush(Colors.Black);
                    }
                    double fontSize = settings.FontSize;
                    var fontFamily = new FontFamily(settings.FontFamily ?? "Arial");
                    LeftPageTextBlock.FontSize = RightPageTextBlock.FontSize = fontSize;
                    LeftPageTextBlock.FontFamily = RightPageTextBlock.FontFamily = fontFamily;

                    BackgroundColorComboBox.SelectedItem = BackgroundColorComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(i => i.Tag.ToString() == settings.BackgroundColor) ?? BackgroundColorComboBox.Items[0];
                    FontColorComboBox.SelectedItem = FontColorComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(i => i.Tag.ToString() == settings.FontColor) ?? FontColorComboBox.Items[0];
                    FontSizeSlider.Value = settings.FontSize;
                    FontFamilyComboBox.SelectedItem = FontFamilyComboBox.Items
                        .Cast<ComboBoxItem>()
                        .FirstOrDefault(i => i.Content.ToString() == settings.FontFamily) ?? FontFamilyComboBox.Items[0];
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
                var backgroundColor = ((SolidColorBrush)LeftPageTextBlock.Background).Color.ToString();
                var fontColor = ((SolidColorBrush)LeftPageTextBlock.Foreground).Color.ToString();
                var fontSize = (int)LeftPageTextBlock.FontSize;
                var fontFamily = LeftPageTextBlock.FontFamily.ToString();

                if (!IsValidHexColor(backgroundColor) || !IsValidHexColor(fontColor))
                {
                    MessageBox.Show("Недопустимый формат цвета.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (fontSize < 8 || fontSize > 30)
                {
                    MessageBox.Show("Размер шрифта должен быть от 8 до 30.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

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

        private bool IsValidHexColor(string color)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9A-Fa-f]{6}$|^#[0-9A-Fa-f]{8}$");
        }

        private void BackgroundColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BackgroundColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag.ToString();
                LeftPageTextBlock.Background = RightPageTextBlock.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
            }
        }

        private void FontColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag.ToString();
                LeftPageTextBlock.Foreground = RightPageTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LeftPageTextBlock.FontSize != null)
            {
                LeftPageTextBlock.FontSize = RightPageTextBlock.FontSize = FontSizeSlider.Value;
                SaveDisplaySettings();
            }
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem item)
            {
                var fontFamily = new FontFamily(item.Content.ToString());
                LeftPageTextBlock.FontFamily = RightPageTextBlock.FontFamily = fontFamily;

                SaveDisplaySettings();
            }
        }
        
        private void DisplayBookContent(string fullText)
        {
            int charsPerPage = 2000; // Подбирай под размер экрана
            _bookPages = Enumerable.Range(0, (fullText.Length + charsPerPage - 1) / charsPerPage)
                .Select(i => fullText.Substring(i * charsPerPage, Math.Min(charsPerPage, fullText.Length - i * charsPerPage)))
                .ToList();

            _currentPageIndex = 0;
            ShowCurrentPages();
        }
        
        private void ShowCurrentPages()
        {
            LeftPageTextBlock.Text = _bookPages.ElementAtOrDefault(_currentPageIndex) ?? "";
            RightPageTextBlock.Text = _bookPages.ElementAtOrDefault(_currentPageIndex + 1) ?? "";
        }
        
        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex >= 2)
            {
                _currentPageIndex -= 2;
                ShowCurrentPages();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex + 2 < _bookPages.Count)
            {
                _currentPageIndex += 2;
                ShowCurrentPages();
            }
        }

        private void DefaultBooks_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Загрузка базовых книг...");
            // TODO: Добавить отображение списка базовых книг
        }

        private void Favorites_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Избранные книги...");
            // TODO: Добавить отображение избранных книг
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("История чтения...");
            // TODO: Добавить отображение истории чтения
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