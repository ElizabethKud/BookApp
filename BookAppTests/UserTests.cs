using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class UserTests
    {
        [Fact]
        public void User_Constructor_SetsDefaultCollections()
        {
            var user = new User();
            Assert.NotNull(user.Ratings);
            Assert.IsType<List<Rating>>(user.Ratings);
            Assert.Empty(user.Ratings);

            Assert.NotNull(user.Bookmarks);
            Assert.IsType<List<Bookmark>>(user.Bookmarks);
            Assert.Empty(user.Bookmarks);

            Assert.NotNull(user.DisplaySettings);
            Assert.IsType<List<DisplaySetting>>(user.DisplaySettings);
            Assert.Empty(user.DisplaySettings);

            Assert.NotNull(user.FavoriteBooks);
            Assert.IsType<List<FavoriteBook>>(user.FavoriteBooks);
            Assert.Empty(user.FavoriteBooks);

            Assert.NotNull(user.ReadingHistory);
            Assert.IsType<List<ReadingHistory>>(user.ReadingHistory);
            Assert.Empty(user.ReadingHistory);
        }

        [Fact]
        public void User_Properties_CanBeSetAndGet()
        {
            var user = new User
            {
                Id = 1,
                Username = "testuser",
                PasswordHash = "hashedpassword",
                Email = "test@example.com",
                RegistrationDate = DateTime.UtcNow
            };

            Assert.Equal(1, user.Id);
            Assert.Equal("testuser", user.Username);
            Assert.Equal("hashedpassword", user.PasswordHash);
            Assert.Equal("test@example.com", user.Email);
            Assert.Equal(DateTime.UtcNow.Date, user.RegistrationDate.Date); // Сравниваем только дату
        }
    }
}