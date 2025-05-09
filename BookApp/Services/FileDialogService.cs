using Microsoft.Win32;
using System;
using System.Windows;

namespace BookApp.Services
{
    public class FileDialogService
    {
        public string OpenFileDialog()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Книги (*.epub, *.fb2, *.txt)|*.epub;*.fb2;*.txt|Все файлы (*.*)|*.*",
                    Multiselect = false
                };
                return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии диалога: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}