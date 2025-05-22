using BookApp.Models;
using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace BookApp.Services
{
    public class GenresConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Collections.Generic.ICollection<BookGenre> bookGenres && bookGenres.Any())
            {
                return string.Join(", ", bookGenres.Select(bg => bg.Genre.Name));
            }
            return "Не указаны";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}