using BookApp.Data;
using BookApp.Models;
using BookApp.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace BookApp.Views
{
    public partial class RecommendationsWindow : Window
    {
        private readonly int _userId;
        private readonly MainWindow _mainWindow;
        private readonly RecommendationService _recommendationService;

        public RecommendationsWindow(int userId, MainWindow mainWindow)
        {
            InitializeComponent();
            _userId = userId;
            _mainWindow = mainWindow;
            _recommendationService = new RecommendationService();
            LoadRecommendations();
        }

        private void LoadRecommendations()
        {
            try
            {
                var recommendations = _recommendationService.GetRecommendations(_userId);
                RecommendationsGrid.ItemsSource = recommendations;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки рекомендаций: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenBook_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Book book)
            {
                _mainWindow.OpenBook(book.FilePath);
                Close();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}