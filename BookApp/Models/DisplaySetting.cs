namespace BookApp.Models;

public class DisplaySetting
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BackgroundColor { get; set; }
    public string FontColor { get; set; }
    public int FontSize { get; set; }
    public string FontFamily { get; set; }

    // Навигационное свойство
    public virtual User User { get; set; }
}