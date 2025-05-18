using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class BookAuthorTests
    {
        [Fact]
        public void BookAuthor_Properties_CanBeSetAndGet()
        {
            var bookAuthor = new BookAuthor
            {
                Id = 1,
                BookId = 1,
                AuthorId = 1
            };

            Assert.Equal(1, bookAuthor.Id);
            Assert.Equal(1, bookAuthor.BookId);
            Assert.Equal(1, bookAuthor.AuthorId);
        }
    }
}