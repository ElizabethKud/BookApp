namespace BookApp.Models;

public class Book
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Author { get; set; }
    public int? PublicationYear { get; set; }
    public int? PagesCount { get; set; }
    public string Language { get; set; }
    public string FilePath { get; set; }

    // Навигационные свойства
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    public virtual ICollection<FavoriteBook> FavoriteBooks { get; set; } = new List<FavoriteBook>();
    public virtual ICollection<ReadingHistory> ReadingHistory { get; set; } = new List<ReadingHistory>();
    public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
    public virtual ICollection<BookGenre> BookGenres { get; set; } = new List<BookGenre>();
}