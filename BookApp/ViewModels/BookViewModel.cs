using BookApp.Models;

namespace BookApp.ViewModels;

public class BookViewModel
{
    public Book Book { get; set; }
    public bool IsRead { get; set; }
}