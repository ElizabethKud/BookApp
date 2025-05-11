using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private System.Collections.Generic.List<string> _bookPages = new();
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

                DisplayBookContent(content);
                // Показываем кнопки навигации
                PrevPageButton.Visibility = Visibility.Visible;
                NextPageButton.Visibility = Visibility.Visible;
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
            _bookPages.Clear();
            _currentPageIndex = 0;
            LeftPageTextBlock.Text = "";
            RightPageTextBlock.Text = "";
            PrevPageButton.Visibility = Visibility.Collapsed;
            NextPageButton.Visibility = Visibility.Collapsed;
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
                        var background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BackgroundColor));
                        var foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FontColor));
                        LeftPageTextBlock.Background = RightPageTextBlock.Background = background;
                        LeftPageTextBlock.Foreground = RightPageTextBlock.Foreground = foreground;
                    }
                    catch (FormatException)
                    {
                        // Устанавливаем значения по умолчанию, если цвет невалиден
                        LeftPageTextBlock.Background = RightPageTextBlock.Background = new SolidColorBrush(Colors.White);
                        LeftPageTextBlock.Foreground = RightPageTextBlock.Foreground = new SolidColorBrush(Colors.Black);
                    }
                    double fontSize = settings.FontSize;
                    var fontFamily = new FontFamily(settings.FontFamily ?? "Arial");
                    LeftPageTextBlock.FontSize = RightPageTextBlock.FontSize = fontSize;
                    LeftPageTextBlock.FontFamily = RightPageTextBlock.FontFamily = fontFamily;

                    // Устанавливаем значения в элементах управления
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
                    // Устанавливаем значения по умолчанию, если настроек нет
                    LeftPageTextBlock.Background = RightPageTextBlock.Background = new SolidColorBrush(Colors.White);
                    LeftPageTextBlock.Foreground = RightPageTextBlock.Foreground = new SolidColorBrush(Colors.Black);
                    LeftPageTextBlock.FontSize = RightPageTextBlock.FontSize = 16;
                    LeftPageTextBlock.FontFamily = RightPageTextBlock.FontFamily = new FontFamily("Arial");

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
                var backgroundColor = ((SolidColorBrush)LeftPageTextBlock.Background).Color.ToString();
                var fontColor = ((SolidColorBrush)LeftPageTextBlock.Foreground).Color.ToString();
                var fontSize = (int)LeftPageTextBlock.FontSize;
                var fontFamily = LeftPageTextBlock.FontFamily.ToString();

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
            if (LeftPageTextBlock != null)
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
            int charsPerPage = 2000;
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
                SaveReadingProgress(_currentPageIndex);
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPageIndex + 2 < _bookPages.Count)
            {
                _currentPageIndex += 2;
                ShowCurrentPages();
                SaveReadingProgress(_currentPageIndex);
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