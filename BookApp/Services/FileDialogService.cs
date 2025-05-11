using Microsoft.Win32;

namespace BookApp.Services
{
    public class FileDialogService
    {
        public string OpenFileDialog()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Book Files (*.epub, *.fb2, *.pdf)|*.epub;*.fb2;*.pdf|All Files (*.*)|*.*"
            };
            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }
    }
}