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
            if (value is ICollection<BookAuthor> bookAuthors && bookAuthors.Any())
            {
                var authorNames = bookAuthors
                    .Select(ba => ba.Author)
                    .Where(a => a != null)
                    .Select(a => $"{a.LastName} {a.FirstName} {a.MiddleName}".Trim());
                return string.Join(", ", authorNames);
            }
            return "Неизвестный автор";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}