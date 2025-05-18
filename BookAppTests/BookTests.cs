using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class BookTests
    {
        [Fact]
        public void Book_Constructor_SetsDefaultCollections()
        {
            var book = new Book();
            Assert.NotNull(book.Ratings);
            Assert.IsType<List<Rating>>(book.Ratings);
            Assert.Empty(book.Ratings);

            Assert.NotNull(book.Bookmarks);
            Assert.IsType<List<Bookmark>>(book.Bookmarks);
            Assert.Empty(book.Bookmarks);

            Assert.NotNull(book.FavoriteBooks);
            Assert.IsType<List<FavoriteBook>>(book.FavoriteBooks);
            Assert.Empty(book.FavoriteBooks);

            Assert.NotNull(book.ReadingHistory);
            Assert.IsType<List<ReadingHistory>>(book.ReadingHistory);
            Assert.Empty(book.ReadingHistory);

            Assert.NotNull(book.BookAuthors);
            Assert.IsType<List<BookAuthor>>(book.BookAuthors);
            Assert.Empty(book.BookAuthors);

            Assert.NotNull(book.BookGenres);
            Assert.IsType<List<BookGenre>>(book.BookGenres);
            Assert.Empty(book.BookGenres);
        }

        [Fact]
        public void Book_Properties_CanBeSetAndGet()
        {
            var book = new Book
            {
                Id = 1,
                Title = "Test Book",
                PublicationYear = 2020,
                PagesCount = 300,
                Language = "English",
                FilePath = "path/to/book",
                IsDefault = true
            };

            Assert.Equal(1, book.Id);
            Assert.Equal("Test Book", book.Title);
            Assert.Equal(2020, book.PublicationYear);
            Assert.Equal(300, book.PagesCount);
            Assert.Equal("English", book.Language);
            Assert.Equal("path/to/book", book.FilePath);
            Assert.True(book.IsDefault);
        }
    }
}