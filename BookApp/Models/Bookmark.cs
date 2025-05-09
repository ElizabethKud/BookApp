namespace BookApp.Models;

public class Bookmark
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int PageNumber { get; set; }
    public string Name { get; set; }
    public DateTime DateAdded { get; set; }

    // Навигационные свойства
    public virtual User User { get; set; }
    public virtual Book Book { get; set; }
}