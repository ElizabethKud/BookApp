using BookApp.Models;
using BookApp.Services;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace BookApp.Tests
{
    public class AverageRatingConverterTests
    {
        private readonly AverageRatingConverter _converter;

        public AverageRatingConverterTests()
        {
            _converter = new AverageRatingConverter();
        }

        [Fact]
        public void Convert_ReturnsDash_WhenRatingsIsNull()
        {
            var result = _converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("—", result);
        }

        [Fact]
        public void Convert_ReturnsDash_WhenRatingsIsEmpty()
        {
            var result = _converter.Convert(new List<Rating>(), typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("—", result);
        }

        [Fact]
        public void Convert_ReturnsAverage_WhenRatingsExist()
        {
            var ratings = new List<Rating>
            {
                new Rating { RatingValue = 8 },
                new Rating { RatingValue = 6 },
                new Rating { RatingValue = 7 }
            };
            var result = _converter.Convert(ratings, typeof(string), null, CultureInfo.InvariantCulture);
            Assert.Equal("7.0", result);
        }

        [Fact]
        public void ConvertBack_ThrowsNotImplementedException()
        {
            Assert.Throws<NotImplementedException>(() => _converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture));
        }
    }
}