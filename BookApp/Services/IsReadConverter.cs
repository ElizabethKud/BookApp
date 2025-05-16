using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using BookApp.Models;

namespace BookApp.Services
{
    public class IsReadConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not System.Collections.Generic.ICollection<ReadingHistory> readingHistory || parameter is not int userId)
                return false;

            return readingHistory.FirstOrDefault(rh => rh.UserId == userId)?.IsRead ?? false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value; // Для двухсторонней привязки
        }
    }
}