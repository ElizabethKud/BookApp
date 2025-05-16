using BookApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BookApp.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Rating> Ratings { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<DisplaySetting> DisplaySettings { get; set; }
        public DbSet<FavoriteBook> FavoriteBooks { get; set; }
        public DbSet<ReadingHistory> ReadingHistory { get; set; }
        public DbSet<BookAuthor> BookAuthors { get; set; }
        public DbSet<BookGenre> BookGenres { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql("Host=localhost;Database=BookApp;Username=postgres;Include Error Detail=true");
                optionsBuilder.UseLazyLoadingProxies();
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Настройка имен таблиц
            modelBuilder.Entity<User>().ToTable("users");
            modelBuilder.Entity<Book>().ToTable("books");
            modelBuilder.Entity<Author>().ToTable("authors");
            modelBuilder.Entity<Rating>().ToTable("ratings");
            modelBuilder.Entity<Genre>().ToTable("genres");
            modelBuilder.Entity<Bookmark>().ToTable("bookmarks");
            modelBuilder.Entity<DisplaySetting>().ToTable("display_settings");
            modelBuilder.Entity<FavoriteBook>().ToTable("favorite_books");
            modelBuilder.Entity<ReadingHistory>().ToTable("reading_history");
            modelBuilder.Entity<BookAuthor>().ToTable("book_author");
            modelBuilder.Entity<BookGenre>().ToTable("book_genre");

            // Конфигурация User
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Id).HasColumnName("id");
                entity.Property(u => u.Username).HasColumnName("username");
                entity.Property(u => u.PasswordHash).HasColumnName("password_hash");
                entity.Property(u => u.Email).HasColumnName("email");
                entity.Property(u => u.RegistrationDate).HasColumnName("registration_date");
                
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.RegistrationDate).HasDefaultValueSql("NOW()");
            });

            // Конфигурация Book
            modelBuilder.Entity<Book>(entity =>
            {
                entity.Property(b => b.Id).HasColumnName("id");
                entity.Property(b => b.Title).HasColumnName("title");
                entity.Property(b => b.PublicationYear).HasColumnName("publication_year");
                entity.Property(b => b.PagesCount).HasColumnName("pages_count");
                entity.Property(b => b.Language).HasColumnName("language");
                entity.Property(b => b.FilePath).HasColumnName("file_path");
                entity.Property(b => b.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
                
                entity.Property(b => b.PublicationYear).IsRequired(false);
                entity.Property(b => b.PagesCount).IsRequired(false);
                entity.HasCheckConstraint("CK_Book_PublicationYear", "publication_year > 0");
            });

            // Конфигурация Author
            modelBuilder.Entity<Author>(entity =>
            {
                entity.Property(a => a.Id).HasColumnName("id");
                entity.Property(a => a.LastName).HasColumnName("last_name");
                entity.Property(a => a.FirstName).HasColumnName("first_name");
                entity.Property(a => a.MiddleName).HasColumnName("middle_name");
                entity.Property(a => a.BirthYear).HasColumnName("birth_year");
                entity.Property(a => a.Country).HasColumnName("country");
            });

            // Конфигурация Rating
            modelBuilder.Entity<Rating>(entity =>
            {
                entity.Property(r => r.Id).HasColumnName("id");
                entity.Property(r => r.UserId).HasColumnName("user_id");
                entity.Property(r => r.BookId).HasColumnName("book_id");
                entity.Property(r => r.RatingValue).HasColumnName("rating");
                entity.Property(r => r.RatingDate).HasColumnName("rating_date");
                
                entity.HasOne(r => r.User)
                    .WithMany(u => u.Ratings)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(r => r.Book)
                    .WithMany(b => b.Ratings)
                    .HasForeignKey(r => r.BookId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(r => new { r.UserId, r.BookId }).IsUnique();
                entity.HasCheckConstraint("CK_Rating_Rating", "rating BETWEEN 1 AND 10");
                entity.Property(r => r.RatingDate).HasDefaultValueSql("NOW()");
            });

            // Конфигурация Genre
            modelBuilder.Entity<Genre>(entity =>
            {
                entity.Property(g => g.Id).HasColumnName("id");
                entity.Property(g => g.Name).HasColumnName("name");
                
                entity.HasIndex(g => g.Name).IsUnique();
            });

            // Конфигурация Bookmark
            modelBuilder.Entity<Bookmark>(entity =>
            {
                entity.Property(b => b.Id).HasColumnName("id");
                entity.Property(b => b.UserId).HasColumnName("user_id");
                entity.Property(b => b.BookId).HasColumnName("book_id");
                entity.Property(b => b.PageNumber).HasColumnName("page_number");
                entity.Property(b => b.Name).HasColumnName("name");
                entity.Property(b => b.DateAdded).HasColumnName("date_added");
                
                entity.HasOne(b => b.User)
                    .WithMany(u => u.Bookmarks)
                    .HasForeignKey(b => b.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(b => b.Book)
                    .WithMany(b => b.Bookmarks)
                    .HasForeignKey(b => b.BookId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.Property(b => b.DateAdded).HasDefaultValueSql("NOW()");
            });

            // Конфигурация DisplaySetting
            modelBuilder.Entity<DisplaySetting>(entity =>
            {
                entity.Property(d => d.Id).HasColumnName("id");
                entity.Property(d => d.UserId).HasColumnName("user_id");
                entity.Property(d => d.BackgroundColor).HasColumnName("background_color");
                entity.Property(d => d.FontColor).HasColumnName("font_color");
                entity.Property(d => d.FontSize).HasColumnName("font_size");
                entity.Property(d => d.FontFamily).HasColumnName("font_family");
                
                entity.HasOne(d => d.User)
                    .WithMany(u => u.DisplaySettings)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.Property(d => d.FontSize).HasDefaultValue(16);
                entity.Property(d => d.FontFamily).HasDefaultValue("Arial");
            });

            // Конфигурация FavoriteBook
            modelBuilder.Entity<FavoriteBook>(entity =>
            {
                entity.Property(f => f.Id).HasColumnName("id");
                entity.Property(f => f.UserId).HasColumnName("user_id");
                entity.Property(f => f.BookId).HasColumnName("book_id");
                entity.Property(f => f.DateAdded).HasColumnName("date_added");
                
                entity.HasOne(f => f.User)
                    .WithMany(u => u.FavoriteBooks)
                    .HasForeignKey(f => f.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(f => f.Book)
                    .WithMany(b => b.FavoriteBooks)
                    .HasForeignKey(f => f.BookId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(f => new { f.UserId, f.BookId }).IsUnique();
                entity.Property(f => f.DateAdded).HasDefaultValueSql("NOW()");
            });

            // Конфигурация ReadingHistory
            modelBuilder.Entity<ReadingHistory>(entity =>
            {
                entity.Property(r => r.Id).HasColumnName("id");
                entity.Property(r => r.UserId).HasColumnName("user_id");
                entity.Property(r => r.BookId).HasColumnName("book_id");
                entity.Property(r => r.LastReadPage).HasColumnName("last_read_page");
                entity.Property(r => r.LastReadPosition).HasColumnName("last_read_position");
                entity.Property(r => r.LastReadDate).HasColumnName("last_read_date");
                entity.Property(r => r.IsRead).HasColumnName("is_read"); // Явное сопоставление с is_read
                
                entity.HasOne(r => r.User)
                    .WithMany(u => u.ReadingHistory)
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(r => r.Book)
                    .WithMany(b => b.ReadingHistory)
                    .HasForeignKey(r => r.BookId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasIndex(r => new { r.UserId, r.BookId }).IsUnique();
                entity.Property(r => r.LastReadDate).HasDefaultValueSql("NOW()");
            });

            // Конфигурация BookAuthor
            modelBuilder.Entity<BookAuthor>(entity =>
            {
                entity.Property(b => b.Id).HasColumnName("id");
                entity.Property(b => b.BookId).HasColumnName("book_id");
                entity.Property(b => b.AuthorId).HasColumnName("author_id");
                
                entity.HasOne(b => b.Book)
                    .WithMany(b => b.BookAuthors)
                    .HasForeignKey(b => b.BookId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(b => b.Author)
                    .WithMany(a => a.BookAuthors)
                    .HasForeignKey(b => b.AuthorId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Конфигурация BookGenre
            modelBuilder.Entity<BookGenre>(entity =>
            {
                entity.Property(b => b.Id).HasColumnName("id");
                entity.Property(b => b.BookId).HasColumnName("book_id");
                entity.Property(b => b.GenreId).HasColumnName("genre_id");
                
                entity.HasOne(b => b.Book)
                    .WithMany(b => b.BookGenres)
                    .HasForeignKey(b => b.BookId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(b => b.Genre)
                    .WithMany(g => g.BookGenres)
                    .HasForeignKey(b => b.GenreId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}