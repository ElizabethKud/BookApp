using BookApp.Services;
using System.Globalization;
using Xunit;

namespace BookApp.Tests
{
    public class BooleanToStringConverterTests
    {
        private readonly BooleanToStringConverter _converter;

        public BooleanToStringConverterTests()
        {
            _converter = new BooleanToStringConverter();
        }

        [Theory]
        [InlineData(true, "Да")]
        [InlineData(false, "Нет")]
        [InlineData(null, "Нет")]
        public void Convert_ReturnsCorrectString(bool? value, string expected)
        {
            var result = _converter.Convert(value, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
        }
    }
}