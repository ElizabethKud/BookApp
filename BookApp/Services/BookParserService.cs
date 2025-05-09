using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using VersOne.Epub;
using System.Linq;
using System.Collections.Generic;

namespace BookApp.Services
{
    public class BookMetadata
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public List<string> Genres { get; set; }
        public string Content { get; set; }
    }

    public class BookParserService
    {
        public BookMetadata ParseBook(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Файл не найден", filePath);

            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".epub" => ParseEpub(filePath),
                ".fb2" => ParseFb2(filePath),
                ".txt" => ParseTxt(filePath),
                _ => throw new NotSupportedException("Неподдерживаемый формат файла")
            };
        }

        private BookMetadata ParseEpub(string filePath)
        {
            try
            {
                var epubBook = EpubReader.ReadBook(filePath);
                var content = new StringBuilder();
                foreach (var textFile in epubBook.ReadingOrder)
                {
                    content.AppendLine(textFile.Content);
                }

                return new BookMetadata
                {
                    Title = epubBook.Title ?? Path.GetFileNameWithoutExtension(filePath),
                    Author = epubBook.Author ?? "Неизвестен",
                    Genres = epubBook.Description?.Split(',').Select(s => s.Trim()).ToList() ?? new List<string>(),
                    Content = content.ToString()
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при разборе EPUB файла", ex);
            }
        }

        private BookMetadata ParseFb2(string filePath)
        {
            try
            {
                var doc = XDocument.Load(filePath);
                var description = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "description");
                var titleInfo = description?.Descendants().FirstOrDefault(e => e.Name.LocalName == "title-info");

                var title = titleInfo?.Descendants().FirstOrDefault(e => e.Name.LocalName == "book-title")?.Value;
                var author = titleInfo?.Descendants().FirstOrDefault(e => e.Name.LocalName == "author")?.Value;
                var genres = titleInfo?.Descendants().Where(e => e.Name.LocalName == "genre").Select(g => g.Value).ToList();
                var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");

                return new BookMetadata
                {
                    Title = title ?? Path.GetFileNameWithoutExtension(filePath),
                    Author = author ?? "Неизвестен",
                    Genres = genres ?? new List<string>(),
                    Content = body?.Value ?? string.Empty
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при разборе FB2 файла", ex);
            }
        }

        private BookMetadata ParseTxt(string filePath)
        {
            try
            {
                return new BookMetadata
                {
                    Title = Path.GetFileNameWithoutExtension(filePath),
                    Author = "Неизвестен",
                    Genres = new List<string>(),
                    Content = File.ReadAllText(filePath)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Ошибка при разборе TXT файла", ex);
            }
        }
    }
}