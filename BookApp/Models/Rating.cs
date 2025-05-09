namespace BookApp.Models;

public class Rating
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public int RatingValue { get; set; }
    public DateTime RatingDate { get; set; }

    // Навигационные свойства
    public virtual User User { get; set; }
    public virtual Book Book { get; set; }
}