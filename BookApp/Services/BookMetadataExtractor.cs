using System.IO;
using System.Xml.Linq;
using VersOne.Epub;

namespace BookApp.Services
{
    public class BookMetadataExtractor
    {
        public BookApp.Models.Book ExtractMetadata(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".epub" => ExtractEpub(filePath),
                ".fb2" => ExtractFb2(filePath),
                ".txt" => ExtractTxt(filePath),
                _ => throw new NotSupportedException("Unsupported file format")
            };
        }

        private BookApp.Models.Book ExtractEpub(string filePath)
        {
            var epub = EpubReader.ReadBook(filePath);
            return new BookApp.Models.Book
            {
                Title = epub.Title,
                Author = epub.Author,
                FilePath = filePath
            };
        }

        private BookApp.Models.Book ExtractFb2(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var title = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "book-title")?.Value;
            var author = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "author")?.Value;
            return new BookApp.Models.Book
            {
                Title = title,
                Author = author,
                FilePath = filePath
            };
        }

        private BookApp.Models.Book ExtractTxt(string filePath)
        {
            return new BookApp.Models.Book
            {
                Title = Path.GetFileNameWithoutExtension(filePath),
                Author = "Unknown",
                FilePath = filePath
            };
        }
    }
}