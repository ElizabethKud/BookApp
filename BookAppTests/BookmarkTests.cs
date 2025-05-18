using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class BookmarkTests
    {
        [Fact]
        public void Bookmark_Properties_CanBeSetAndGet()
        {
            var bookmark = new Bookmark
            {
                Id = 1,
                UserId = 1,
                BookId = 1,
                PageNumber = 10,
                Name = "Chapter 1",
                DateAdded = DateTime.UtcNow
            };

            Assert.Equal(1, bookmark.Id);
            Assert.Equal(1, bookmark.UserId);
            Assert.Equal(1, bookmark.BookId);
            Assert.Equal(10, bookmark.PageNumber);
            Assert.Equal("Chapter 1", bookmark.Name);
            Assert.Equal(DateTime.UtcNow.Date, bookmark.DateAdded.Date); // Сравниваем только дату
        }
    }
}