namespace BookApp.Models;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Email { get; set; }
    public DateTime RegistrationDate { get; set; }

    // Навигационные свойства
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    public virtual ICollection<DisplaySetting> DisplaySettings { get; set; } = new List<DisplaySetting>();
    public virtual ICollection<FavoriteBook> FavoriteBooks { get; set; } = new List<FavoriteBook>();
    public virtual ICollection<ReadingHistory> ReadingHistory { get; set; } = new List<ReadingHistory>();
}