using BookApp.Models;
using BookApp.Services;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace BookApp.Tests
{
    public class IsReadConverterTests
    {
        private readonly IsReadConverter _converter;

        public IsReadConverterTests()
        {
            _converter = new IsReadConverter();
        }

        [Fact]
        public void Convert_ReturnsFalse_WhenReadingHistoryIsNull()
        {
            var result = _converter.Convert(null, typeof(bool), 1, CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_ReturnsFalse_WhenNoMatchingUser()
        {
            var history = new List<ReadingHistory> { new ReadingHistory { UserId = 2 } };
            var result = _converter.Convert(history, typeof(bool), 1, CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void Convert_ReturnsTrue_WhenIsReadIsTrue()
        {
            var history = new List<ReadingHistory> { new ReadingHistory { UserId = 1, IsRead = true } };
            var result = _converter.Convert(history, typeof(bool), 1, CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void ConvertBack_ReturnsValue()
        {
            var result = _converter.ConvertBack(true, typeof(object), null, CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }
    }
}