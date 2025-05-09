namespace BookApp.Models;

public class BookAuthor
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public int AuthorId { get; set; }

    // Навигационные свойства
    public virtual Book Book { get; set; }
    public virtual Author Author { get; set; }
}