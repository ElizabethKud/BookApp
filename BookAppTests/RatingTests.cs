using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class RatingTests
    {
        [Fact]
        public void Rating_Properties_CanBeSetAndGet()
        {
            var rating = new Rating
            {
                Id = 1,
                UserId = 1,
                BookId = 1,
                RatingValue = 8,
                RatingDate = DateTime.UtcNow
            };

            Assert.Equal(1, rating.Id);
            Assert.Equal(1, rating.UserId);
            Assert.Equal(1, rating.BookId);
            Assert.Equal(8, rating.RatingValue);
            Assert.Equal(DateTime.UtcNow.Date, rating.RatingDate.Date); // Сравниваем только дату
        }
    }
}