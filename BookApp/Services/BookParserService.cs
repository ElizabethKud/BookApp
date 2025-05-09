using System.IO;
using System.Text;
using System.Xml.Linq;
using VersOne.Epub;

namespace BookApp.Services
{
    public class BookParserService
    {
        public string ParseBook(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".epub" => ParseEpub(filePath),
                ".fb2" => ParseFb2(filePath),
                ".txt" => ParseTxt(filePath),
                _ => throw new NotSupportedException("Unsupported file format")
            };
        }

        private string ParseEpub(string filePath)
        {
            var epubBook = EpubReader.ReadBook(filePath);
            var content = new StringBuilder();
            foreach (var textFile in epubBook.ReadingOrder)
            {
                content.AppendLine(textFile.Content);
            }
            return content.ToString();
        }

        private string ParseFb2(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
            return body?.Value ?? string.Empty;
        }

        private string ParseTxt(string filePath)
        {
            return File.ReadAllText(filePath);
        }
    }
}