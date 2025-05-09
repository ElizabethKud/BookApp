namespace BookApp.Models;

public class BookGenre
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int GenreId { get; set; }

    // Навигационные свойства
    public virtual Book Book { get; set; }
    public virtual Genre Genre { get; set; }
}