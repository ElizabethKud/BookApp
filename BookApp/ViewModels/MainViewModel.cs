using BookApp.Models;
using BookApp.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace BookApp.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private readonly FileDialogService _fileDialogService = new();
        private readonly BookMetadataExtractor _metadataExtractor = new();
        private readonly DatabaseService _databaseService = new();

        public ObservableCollection<Book> Books { get; set; } = new();

        public ICommand OpenBookCommand { get; }

        public MainViewModel()
        {
            OpenBookCommand = new RelayCommand(OpenBook);
        }

        private void OpenBook()
        {
            string filePath = _fileDialogService.OpenFileDialog();
            if (filePath == null) return;

            var metadata = _metadataExtractor.ExtractMetadata(filePath);
            _databaseService.SaveBook(metadata);
            Books.Add(metadata);
        }
    }
}