using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using BookApp.Models;

namespace BookApp.Services
{
    public class AuthorNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<BookAuthor> bookAuthors && bookAuthors.Any())
            {
                var authorNames = bookAuthors
                    .Select(ba => ba.Author)
                    .Where(a => a != null)
                    .Select(a => string.Join(" ", new[] { a.LastName, a.FirstName, a.MiddleName }.Where(s => !string.IsNullOrEmpty(s))))
                    .Where(name => !string.IsNullOrEmpty(name));
                return string.Join(", ", authorNames) is string result && !string.IsNullOrEmpty(result) ? result : "Неизвестный автор";
            }
            return "Неизвестный автор";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}