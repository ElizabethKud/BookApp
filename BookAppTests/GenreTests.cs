using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class GenreTests
    {
        [Fact]
        public void Genre_Constructor_SetsDefaultBookGenresCollection()
        {
            var genre = new Genre();
            Assert.NotNull(genre.BookGenres);
            Assert.IsType<List<BookGenre>>(genre.BookGenres);
            Assert.Empty(genre.BookGenres);
        }

        [Fact]
        public void Genre_Properties_CanBeSetAndGet()
        {
            var genre = new Genre
            {
                Id = 1,
                Name = "Fiction"
            };

            Assert.Equal(1, genre.Id);
            Assert.Equal("Fiction", genre.Name);
        }
    }
}