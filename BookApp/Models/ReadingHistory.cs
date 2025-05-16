namespace BookApp.Models;

public class ReadingHistory
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int? LastReadPage { get; set; }
    public string LastReadPosition { get; set; }
    public DateTime LastReadDate { get; set; }
    public bool IsRead { get; set; }

    public virtual User User { get; set; }
    public virtual Book Book { get; set; }
}