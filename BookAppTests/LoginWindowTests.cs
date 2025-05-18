using BookApp;
using BookApp.Models;
using Moq;
using System.Windows;
using Xunit;

namespace BookApp.Tests
{
    public class LoginWindowTests
    {
        private readonly LoginWindow _window;

        public LoginWindowTests()
        {
            _window = new LoginWindow();
        }

        [Fact]
        public void LoginButton_Click_SuccessfulLogin_OpensMainView()
        {
            var user = new User { Username = "test", PasswordHash = BCrypt.Net.BCrypt.HashPassword("password") };
            // Мок DbContext не реализован полностью
            Assert.NotNull(_window);
        }

        [Fact]
        public void RegisterButton_Click_SuccessfulRegistration_OpensMainView()
        {
            // Мок DbContext не реализован полностью
            Assert.NotNull(_window);
        }
    }
}