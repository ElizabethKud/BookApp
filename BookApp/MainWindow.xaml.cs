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
                db.SaveChanges(); // Сохраняем книгу в том же контексте
            }

            _currentBook = book;
            var content = _parserService.ParseBook(filePath);
            BookTextBlock.Text = content;

            // Восстановление последней страницы
            var history = db.ReadingHistory
                .FirstOrDefault(rh => rh.UserId == _currentUserId && rh.BookId == book.Id);
            if (history != null)
            {
                MessageBox.Show($"Возвращаемся к странице {history.LastReadPage}");
                // Добавить прокрутку к странице (см. комментарий ниже)
            }

            // Переключение на вкладку "Книга"
            ((TabControl)Content).SelectedIndex = 1;
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
                SaveReadingProgress((int)(BookTextBlock.ActualHeight / 100) + 1);
            }
            base.OnClosing(e);
        }

        private void LoadUserSettings()
        {
            var settings = _dbService.GetUserSettings(_currentUserId);
            if (settings != null)
            {
                BookTextBlock.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BackgroundColor));
                BookTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.FontColor));
                BookTextBlock.FontSize = settings.FontSize;
                BookTextBlock.FontFamily = new FontFamily(settings.FontFamily);

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
        }

        private void SaveDisplaySettings()
        {
            if (_currentUserId == 0) // Добавленная проверка
            {
                MessageBox.Show("Ошибка: Пользователь не идентифицирован!");
                return;
            }
            
            var settings = new DisplaySetting
            {
                UserId = _currentUserId,
                BackgroundColor = ((SolidColorBrush)BookTextBlock.Background).Color.ToString(),
                FontColor = ((SolidColorBrush)BookTextBlock.Foreground).Color.ToString(),
                FontSize = (int)BookTextBlock.FontSize,
                FontFamily = BookTextBlock.FontFamily.ToString()
            };
            _dbService.SaveDisplaySettings(settings);
        }

        private void BackgroundColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BackgroundColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag.ToString();
                BookTextBlock.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
            }
        }

        private void FontColorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontColorComboBox.SelectedItem is ComboBoxItem item)
            {
                var color = item.Tag.ToString();
                BookTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
                SaveDisplaySettings();
            }
        }

        private void FontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (BookTextBlock != null)
            {
                BookTextBlock.FontSize = FontSizeSlider.Value;
                SaveDisplaySettings();
            }
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is ComboBoxItem item)
            {
                BookTextBlock.FontFamily = new FontFamily(item.Content.ToString());
                SaveDisplaySettings();
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