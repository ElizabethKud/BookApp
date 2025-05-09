namespace BookApp.Models;

public class Author
{
    public int Id { get; set; }
    public string LastName { get; set; }
    public string FirstName { get; set; }
    public string MiddleName { get; set; }
    public int? BirthYear { get; set; }
    public string Country { get; set; }

    // Навигационное свойство
    public virtual ICollection<BookAuthor> BookAuthors { get; set; } = new List<BookAuthor>();
}