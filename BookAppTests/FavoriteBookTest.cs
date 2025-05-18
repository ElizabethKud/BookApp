using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class FavoriteBookTests
    {
        [Fact]
        public void FavoriteBook_Properties_CanBeSetAndGet()
        {
            var favorite = new FavoriteBook
            {
                Id = 1,
                UserId = 1,
                BookId = 1,
                DateAdded = DateTime.UtcNow
            };

            Assert.Equal(1, favorite.Id);
            Assert.Equal(1, favorite.UserId);
            Assert.Equal(1, favorite.BookId);
            Assert.Equal(DateTime.UtcNow.Date, favorite.DateAdded.Date); // Сравниваем только дату
        }
    }
}