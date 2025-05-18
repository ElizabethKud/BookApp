using BookApp.Models;
using BookApp.Services;
using Moq;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using BookApp;

namespace BookAppTests
{
    public class AuthorNameConverterTests
    {
        private readonly AuthorNameConverter _converter;

        public AuthorNameConverterTests()
        {
            _converter = new AuthorNameConverter();
        }

        [Fact]
        public void Convert_ReturnsUnknownAuthor_WhenValueIsNull()
        {
            var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("Неизвестный автор", result);
        }

        [Fact]
        public void Convert_ReturnsUnknownAuthor_WhenBookAuthorsIsEmpty()
        {
            var result = _converter.Convert(new List<BookAuthor>(), typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("Неизвестный автор", result);
        }

        [Fact]
        public void Convert_ReturnsAuthorNames_WhenBookAuthorsExist()
        {
            var bookAuthors = new List<BookAuthor>
            {
                new BookAuthor { Author = new Author { LastName = "Doe", FirstName = "John" } },
                new BookAuthor { Author = new Author { LastName = "Smith", FirstName = "Jane" } }
            };
            var result = _converter.Convert(bookAuthors, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("Doe John, Smith Jane", result);
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
        }
    }
}