using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class BookGenreTests
    {
        [Fact]
        public void BookGenre_Properties_CanBeSetAndGet()
        {
            var bookGenre = new BookGenre
            {
                Id = 1,
                BookId = 1,
                GenreId = 1
            };

            Assert.Equal(1, bookGenre.Id);
            Assert.Equal(1, bookGenre.BookId);
            Assert.Equal(1, bookGenre.GenreId);
        }
    }
}