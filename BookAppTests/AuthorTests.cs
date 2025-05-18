using BookApp.Models;
using Xunit;
using BookApp;

namespace BookAppTests
{
    public class AuthorTests
    {
        [Fact]
        public void Author_Constructor_SetsDefaultBookAuthorsCollection()
        {
            var author = new Author();
            Assert.NotNull(author.BookAuthors);
            Assert.IsType<List<BookAuthor>>(author.BookAuthors);
            Assert.Empty(author.BookAuthors);
        }

        [Fact]
        public void Author_Properties_CanBeSetAndGet()
        {
            var author = new Author
            {
                Id = 1,
                LastName = "Doe",
                FirstName = "John",
                MiddleName = "A",
                BirthYear = 1980,
                Country = "USA"
            };

            Assert.Equal(1, author.Id);
            Assert.Equal("Doe", author.LastName);
            Assert.Equal("John", author.FirstName);
            Assert.Equal("A", author.MiddleName);
            Assert.Equal(1980, author.BirthYear);
            Assert.Equal("USA", author.Country);
        }
    }
}