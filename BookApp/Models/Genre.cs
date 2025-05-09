namespace BookApp.Models;

public class Genre
{
    public int Id { get; set; }
    public string Name { get; set; }

    // Навигационное свойство
    public virtual ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
}