using BookApp.Data;
using BookApp.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace BookApp.Views
{
    public partial class BookmarksWindow : Window
    {
        private readonly int _userId;
        private readonly int _bookId;
        private readonly MainWindow _mainWindow;

        public BookmarksWindow(int userId, int bookId, MainWindow mainWindow)
        {
            InitializeComponent();
            _userId = userId;
            _bookId = bookId;
            _mainWindow = mainWindow;
            LoadBookmarks();
        }

        private void LoadBookmarks()
        {
            try
            {
                using var db = CreateDbContext();
                var bookmarks = db.Bookmarks
                    .Where(b => b.UserId == _userId && b.BookId == _bookId)
                    .OrderBy(b => b.PageNumber)
                    .ToList();
                BookmarksGrid.ItemsSource = bookmarks;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки закладок: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GoToBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Bookmark bookmark)
            {
                try
                {
                    // Вычисляем примерный offset для страницы
                    var totalPages = _mainWindow._currentBook?.PagesCount ?? 1;
                    var document = _mainWindow.LeftPageDocument;
                    var documentStart = document.ContentStart;
                    var documentEnd = document.ContentEnd;
                    var totalLength = documentEnd.GetOffsetToPosition(documentStart);
                    var pageProgress = Math.Min(1.0, (double)bookmark.PageNumber / totalPages);
                    var targetOffset = (int)(totalLength * pageProgress);

                    var position = documentStart.GetPositionAtOffset(targetOffset);
                    if (position != null)
                    {
                        position.Paragraph?.BringIntoView();
                        var viewer = _mainWindow.FindScrollViewer(_mainWindow.LeftPageViewer);
                        if (viewer != null)
                        {
                            var rect = position.GetCharacterRect(LogicalDirection.Forward);
                            viewer.ScrollToVerticalOffset(rect.Top);
                        }
                        _mainWindow.SaveReadingPosition(position);
                        _mainWindow.UpdateProgressDisplay();
                        Close();
                    }
                    else
                    {
                        MessageBox.Show("Не удалось перейти к закладке.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка перехода к закладке: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
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