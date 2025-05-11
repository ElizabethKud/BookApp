namespace BookApp.Models;

public class ReadingHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int? LastReadPage { get; set; }
    public DateTime LastReadDate { get; set; }

    // Навигационные свойства
    public virtual User User { get; set; }
    public virtual Book Book { get; set; }
}