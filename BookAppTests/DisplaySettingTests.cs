using BookApp.Models;
using Xunit;

namespace BookApp.Tests
{
    public class DisplaySettingTests
    {
        [Fact]
        public void DisplaySetting_Properties_CanBeSetAndGet()
        {
            var setting = new DisplaySetting
            {
                Id = 1,
                UserId = 1,
                BackgroundColor = "#FFFFFF",
                FontColor = "#000000",
                FontSize = 16,
                FontFamily = "Arial"
            };

            Assert.Equal(1, setting.Id);
            Assert.Equal(1, setting.UserId);
            Assert.Equal("#FFFFFF", setting.BackgroundColor);
            Assert.Equal("#000000", setting.FontColor);
            Assert.Equal(16, setting.FontSize);
            Assert.Equal("Arial", setting.FontFamily);
        }
    }
}