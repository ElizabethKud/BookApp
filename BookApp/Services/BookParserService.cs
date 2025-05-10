using System.IO;
using System.Text;
using System.Xml.Linq;
using VersOne.Epub;
using PdfiumViewer; 

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
                ".pdf" => ParsePdf(filePath),
                _ => throw new NotSupportedException("Unsupported file format")
            };
        }
        
        private string StripHtml(string html)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(text);
        }

        private string ParseEpub(string filePath)
        {
            var epubBook = EpubReader.ReadBook(filePath);
            var content = new StringBuilder();
            foreach (var textFile in epubBook.ReadingOrder)
            {
                content.AppendLine(StripHtml(textFile.Content));
            }
            return content.ToString();
        }

        private string ParseFb2(string filePath)
        {
            var doc = XDocument.Load(filePath);
            var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
            return StripHtml(body?.Value ?? string.Empty);
        }

        private string ParsePdf(string filePath)
        {
            using var document = PdfDocument.Load(filePath);
            var content = new StringBuilder();

            for (int i = 0; i < document.PageCount; i++)
            {
                using var page = document.Render(i, 300, 300, true); // изображение
                var text = document.GetPdfText(i); // вот рабочая альтернатива
                content.AppendLine(text);
            }

            return content.ToString();
        }
    }
}