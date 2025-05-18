using BookApp.Services;
using Moq;
using Xunit;

namespace BookApp.Tests
{
    public class FileDialogServiceTests
    {
        private readonly FileDialogService _service;

        public FileDialogServiceTests()
        {
            _service = new FileDialogService();
        }

        [Fact]
        public void OpenFileDialog_ReturnsNull_WhenDialogCanceled()
        {
            var result = _service.OpenFileDialog();
            Assert.Null(result); // Зависит от мокинга, но по умолчанию возвращает null
        }
    }
}