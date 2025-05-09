namespace BookApp.Models;

public class FavoriteBook
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public DateTime DateAdded { get; set; }

    // Навигационные свойства
    public virtual User User { get; set; }
    public virtual Book Book { get; set; }
}