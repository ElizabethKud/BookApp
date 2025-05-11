using System.Windows;
using System.Windows.Controls;
using BookApp.Models;
using BookApp.Services;

namespace BookApp.Views;

public partial class RateBookWindow 
{
    private readonly DatabaseService _dbService = new DatabaseService();
    private readonly int _userId;
    private readonly int _bookId;

    public RateBookWindow(int userId, int bookId, string bookTitle)
    {
        InitializeComponent();
        _userId = userId;
        _bookId = bookId;
        BookTitleLabel.Content = bookTitle;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (RatingComboBox.SelectedItem is ComboBoxItem selectedItem &&
            int.TryParse(selectedItem.Content.ToString(), out int ratingValue))
        {
            try
            {
                var rating = new Rating
                {
                    UserId = _userId,
                    BookId = _bookId,
                    RatingValue = ratingValue,
                    RatingDate = System.DateTime.UtcNow
                };
                _dbService.SaveRating(rating);
                MessageBox.Show("Оценка сохранена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении оценки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            MessageBox.Show("Пожалуйста, выберите оценку.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}