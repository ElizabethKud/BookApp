using System.Windows;
using System.Windows.Controls;
using BookApp.Models;
using BookApp.Services;
using BookApp; // Добавлено для RateBookWindow

namespace BookApp.Views
{
    public partial class DefaultBooksWindow
    {
        private readonly DatabaseService _dbService = new DatabaseService();
        private readonly int _userId;
        private readonly MainWindow _mainWindow;

        public DefaultBooksWindow(int userId, MainWindow mainWindow)
        {
            InitializeComponent();
            _userId = userId;
            _mainWindow = mainWindow;
            LoadBooks();
        }

        private void LoadBooks()
        {
            BooksGrid.ItemsSource = _dbService.GetDefaultBooks();
        }

        private void OpenBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Book book)
            {
                _mainWindow.OpenBook(book.FilePath);
                Close();
            }
        }

        private void RateBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Book book)
            {
                var rateWindow = new RateBookWindow(_userId, book.Id, book.Title);
                rateWindow.ShowDialog();
                LoadBooks(); // Обновляем список, если рейтинг изменился
            }
        }

        private void BooksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Пустой обработчик для предотвращения ошибок
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}