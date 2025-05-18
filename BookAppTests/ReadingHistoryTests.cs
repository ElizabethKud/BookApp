using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class ReadingHistoryTests
    {
        [Fact]
        public void ReadingHistory_Properties_CanBeSetAndGet()
        {
            var history = new ReadingHistory
            {
                Id = 1,
                UserId = 1,
                BookId = 1,
                LastReadPage = 10,
                LastReadPosition = "50",
                LastReadDate = DateTime.UtcNow,
                IsRead = true
            };

            Assert.Equal(1, history.Id);
            Assert.Equal(1, history.UserId);
            Assert.Equal(1, history.BookId);
            Assert.Equal(10, history.LastReadPage);
            Assert.Equal("50", history.LastReadPosition);
            Assert.Equal(DateTime.UtcNow.Date, history.LastReadDate.Date); // Сравниваем только дату
            Assert.True(history.IsRead);
        }
    }
}