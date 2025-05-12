using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using VersOne.Epub;
using PdfiumViewer;
using System.Xml.Linq;

namespace BookApp.Services
{
    public class BookParserService
    {
        public List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)> ParseBook(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLower();
                var content = extension switch
                {
                    ".epub" => ParseEpub(filePath),
                    ".fb2" => ParseFb2(filePath),
                    ".pdf" => ParsePdf(filePath),
                    _ => throw new NotSupportedException($"Неподдерживаемый формат файла: {extension}")
                };
                System.Diagnostics.Debug.WriteLine($"Парсер вернул {content.Count} параграфов для файла {filePath}");
                return content;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга книги {filePath}: {ex.Message}");
                throw;
            }
        }

        private string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                System.Diagnostics.Debug.WriteLine("StripHtml: входная строка пуста");
                return string.Empty;
            }

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var text = doc.DocumentNode.InnerText;
                var decodedText = System.Net.WebUtility.HtmlDecode(text).Trim();
                System.Diagnostics.Debug.WriteLine($"StripHtml: обработано {decodedText.Length} символов");
                return decodedText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в StripHtml: {ex.Message}");
                return string.Empty;
            }
        }

        private List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)> ParseEpub(string filePath)
        {
            var content = new List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)>();
            try
            {
                var epubBook = EpubReader.ReadBook(filePath);

                // Добавляем название книги
                if (!string.IsNullOrWhiteSpace(epubBook.Title))
                {
                    var titleParagraph = new Paragraph(new Run(epubBook.Title.Trim()))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 24,
                        Margin = new Thickness(0, 20, 0, 20),
                        TextAlignment = TextAlignment.Center
                    };
                    content.Add((false, true, false, titleParagraph));
                    System.Diagnostics.Debug.WriteLine($"EPUB: Добавлено название: {epubBook.Title}");
                }

                // Создаём оглавление
                var tocItems = new List<string>();
                var processedTitles = new HashSet<string>();

                void CollectTocItems(EpubNavigationItem navItem, int depth = 0)
                {
                    if (!string.IsNullOrWhiteSpace(navItem.Title) && !processedTitles.Contains(navItem.Title + depth))
                    {
                        tocItems.Add($"{new string(' ', depth * 2)}{navItem.Title.Trim()}");
                        processedTitles.Add(navItem.Title + depth);
                    }
                    foreach (var childItem in navItem.NestedItems)
                    {
                        CollectTocItems(childItem, depth + 1);
                    }
                }

                foreach (var navItem in epubBook.Navigation)
                {
                    CollectTocItems(navItem);
                }

                if (tocItems.Any())
                {
                    var tocHeader = new Paragraph(new Run("Оглавление"))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 20,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    content.Add((false, false, true, tocHeader));
                    System.Diagnostics.Debug.WriteLine("EPUB: Добавлен заголовок оглавления");

                    foreach (var tocItem in tocItems)
                    {
                        var tocParagraph = new Paragraph(new Run(tocItem))
                        {
                            FontSize = 16,
                            Margin = new Thickness(0, 0, 0, 5)
                        };
                        content.Add((false, false, true, tocParagraph));
                        System.Diagnostics.Debug.WriteLine($"EPUB: Добавлен пункт оглавления: {tocItem}");
                    }
                }

                // Обрабатываем содержимое книги
                processedTitles.Clear(); // Сбрасываем для обработки текста
                void ProcessNavigationItem(EpubNavigationItem navItem, int depth = 0)
                {
                    if (!string.IsNullOrWhiteSpace(navItem.Title) && !processedTitles.Contains(navItem.Title + depth))
                    {
                        var chapterTitle = new Paragraph(new Run(navItem.Title.Trim()))
                        {
                            FontWeight = FontWeights.Bold,
                            FontSize = 20,
                            Margin = new Thickness(0, 10, 0, 10)
                        };
                        content.Add((true, false, false, chapterTitle));
                        processedTitles.Add(navItem.Title + depth);
                        System.Diagnostics.Debug.WriteLine($"EPUB: Добавлен заголовок главы: {navItem.Title}");
                    }

                    if (navItem.HtmlContentFile != null)
                    {
                        try
                        {
                            var text = StripHtml(navItem.HtmlContentFile.Content);
                            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                            foreach (var line in lines)
                            {
                                var trimmedLine = line.Trim();
                                if (!string.IsNullOrWhiteSpace(trimmedLine) && !processedTitles.Contains(trimmedLine + depth))
                                {
                                    var isChapterStart = Regex.IsMatch(trimmedLine, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                                    var paragraph = new Paragraph(new Run(trimmedLine))
                                    {
                                        FontWeight = isChapterStart ? FontWeights.Bold : FontWeights.Normal,
                                        FontSize = isChapterStart ? 20 : 16,
                                        Margin = new Thickness(0, isChapterStart ? 10 : 0, 0, 10)
                                    };
                                    content.Add((isChapterStart, false, false, paragraph));
                                    System.Diagnostics.Debug.WriteLine($"EPUB: Добавлен текст: {trimmedLine.Substring(0, Math.Min(50, trimmedLine.Length))}...");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"EPUB: Ошибка чтения содержимого HTML: {ex.Message}");
                        }
                    }

                    foreach (var childItem in navItem.NestedItems)
                    {
                        ProcessNavigationItem(childItem, depth + 1);
                    }
                }

                foreach (var navItem in epubBook.Navigation)
                {
                    ProcessNavigationItem(navItem);
                }

                // Резервный проход по ReadingOrder
                if (content.Count <= (tocItems.Any() ? tocItems.Count + 1 : 0) + (string.IsNullOrWhiteSpace(epubBook.Title) ? 0 : 1))
                {
                    System.Diagnostics.Debug.WriteLine("EPUB: Навигация пуста или мало контента, обрабатываем ReadingOrder");
                    foreach (var textFile in epubBook.ReadingOrder)
                    {
                        var text = StripHtml(textFile.Content);
                        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var line in lines)
                        {
                            var trimmedLine = line.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmedLine))
                            {
                                var isChapterStart = Regex.IsMatch(trimmedLine, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                                var paragraph = new Paragraph(new Run(trimmedLine))
                                {
                                    FontWeight = isChapterStart ? FontWeights.Bold : FontWeights.Normal,
                                    FontSize = isChapterStart ? 20 : 16,
                                    Margin = new Thickness(0, isChapterStart ? 10 : 0, 0, 10)
                                };
                                content.Add((isChapterStart, false, false, paragraph));
                                System.Diagnostics.Debug.WriteLine($"EPUB (резерв): Добавлен текст: {trimmedLine.Substring(0, Math.Min(50, trimmedLine.Length))}...");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга EPUB: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"EPUB: Всего добавлено {content.Count} параграфов");
            return content;
        }

        private List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)> ParseFb2(string filePath)
        {
            var content = new List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)>();
            try
            {
                var doc = XDocument.Load(filePath);

                // Извлекаем название книги
                var titleInfo = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "title-info");
                if (titleInfo != null)
                {
                    var bookTitle = titleInfo.Descendants().FirstOrDefault(e => e.Name.LocalName == "book-title")?.Value;
                    if (!string.IsNullOrWhiteSpace(bookTitle))
                    {
                        var titleParagraph = new Paragraph(new Run(bookTitle.Trim()))
                        {
                            FontWeight = FontWeights.Bold,
                            FontSize = 24,
                            Margin = new Thickness(0, 20, 0, 20),
                            TextAlignment = TextAlignment.Center
                        };
                        content.Add((false, true, false, titleParagraph));
                        System.Diagnostics.Debug.WriteLine($"FB2: Добавлено название: {bookTitle}");
                    }
                }

                // Создаём оглавление
                var tocItems = new List<string>();
                var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
                if (body != null)
                {
                    var sections = body.Descendants().Where(e => e.Name.LocalName == "section");
                    foreach (var section in sections)
                    {
                        var title = section.Descendants().FirstOrDefault(e => e.Name.LocalName == "title");
                        if (title != null)
                        {
                            var titleText = StripHtml(title.Value);
                            if (!string.IsNullOrWhiteSpace(titleText))
                            {
                                tocItems.Add(titleText.Trim());
                            }
                        }
                    }
                }

                if (tocItems.Any())
                {
                    var tocHeader = new Paragraph(new Run("Оглавление"))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 20,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    content.Add((false, false, true, tocHeader));
                    System.Diagnostics.Debug.WriteLine("FB2: Добавлен заголовок оглавления");

                    foreach (var tocItem in tocItems)
                    {
                        var tocParagraph = new Paragraph(new Run(tocItem))
                        {
                            FontSize = 16,
                            Margin = new Thickness(0, 0, 0, 5)
                        };
                        content.Add((false, false, true, tocParagraph));
                        System.Diagnostics.Debug.WriteLine($"FB2: Добавлен пункт оглавления: {tocItem}");
                    }
                }

                // Обрабатываем содержимое
                if (body != null)
                {
                    var sections = body.Descendants().Where(e => e.Name.LocalName == "section");
                    foreach (var section in sections)
                    {
                        var title = section.Descendants().FirstOrDefault(e => e.Name.LocalName == "title");
                        if (title != null)
                        {
                            var titleText = StripHtml(title.Value);
                            if (!string.IsNullOrWhiteSpace(titleText))
                            {
                                var titleParagraph = new Paragraph(new Run(titleText.Trim()))
                                {
                                    FontWeight = FontWeights.Bold,
                                    FontSize = 20,
                                    Margin = new Thickness(0, 10, 0, 10)
                                };
                                content.Add((true, false, false, titleParagraph));
                                System.Diagnostics.Debug.WriteLine($"FB2: Добавлен заголовок главы: {titleText}");
                            }
                        }

                        var sectionParagraphs = section.Descendants().Where(e => e.Name.LocalName == "p");
                        foreach (var p in sectionParagraphs)
                        {
                            var text = StripHtml(p.Value);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var paragraph = new Paragraph(new Run(text.Trim()))
                                {
                                    FontSize = 16,
                                    Margin = new Thickness(0, 0, 0, 10)
                                };
                                content.Add((false, false, false, paragraph));
                                System.Diagnostics.Debug.WriteLine($"FB2: Добавлен текст: {text.Substring(0, Math.Min(50, text.Length))}...");
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("FB2: Тег <body> не найден");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга FB2: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"FB2: Всего добавлено {content.Count} параграфов");
            return content;
        }

        private List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)> ParsePdf(string filePath)
        {
            var content = new List<(bool isChapterStart, bool isTitle, bool isToc, Paragraph paragraph)>();
            try
            {
                using var document = PdfDocument.Load(filePath);

                // Название книги (имя файла)
                var bookTitle = Path.GetFileNameWithoutExtension(filePath);
                var titleParagraph = new Paragraph(new Run(bookTitle.Trim()))
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 24,
                    Margin = new Thickness(0, 20, 0, 20),
                    TextAlignment = TextAlignment.Center
                };
                content.Add((false, true, false, titleParagraph));
                System.Diagnostics.Debug.WriteLine($"PDF: Добавлено название: {bookTitle}");

                // Оглавление (заглушка)
                var tocHeader = new Paragraph(new Run("Оглавление"))
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 20,
                    Margin = new Thickness(0, 10, 0, 10)
                };
                content.Add((false, false, true, tocHeader));
                System.Diagnostics.Debug.WriteLine("PDF: Добавлен заголовок оглавления");

                // Содержимое
                for (int i = 0; i < document.PageCount; i++)
                {
                    var text = document.GetPdfText(i);
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                    var currentParagraphText = new StringBuilder();
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            if (currentParagraphText.Length > 0)
                            {
                                var textContent = currentParagraphText.ToString().Trim();
                                var isChapterStart = Regex.IsMatch(textContent, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                                var paragraph = new Paragraph(new Run(textContent))
                                {
                                    FontWeight = isChapterStart ? FontWeights.Bold : FontWeights.Normal,
                                    FontSize = isChapterStart ? 20 : 16,
                                    Margin = new Thickness(0, isChapterStart ? 10 : 0, 0, 10)
                                };
                                content.Add((isChapterStart, false, false, paragraph));
                                System.Diagnostics.Debug.WriteLine($"PDF: Добавлен текст: {textContent.Substring(0, Math.Min(50, textContent.Length))}...");
                                currentParagraphText.Clear();
                            }
                        }
                        else
                        {
                            currentParagraphText.AppendLine(trimmedLine);
                        }
                    }

                    if (currentParagraphText.Length > 0)
                    {
                        var textContent = currentParagraphText.ToString().Trim();
                        var isChapterStart = Regex.IsMatch(textContent, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                        var paragraph = new Paragraph(new Run(textContent))
                        {
                            FontWeight = isChapterStart ? FontWeights.Bold : FontWeights.Normal,
                            FontSize = isChapterStart ? 20 : 16,
                            Margin = new Thickness(0, isChapterStart ? 10 : 0, 0, 10)
                        };
                        content.Add((isChapterStart, false, false, paragraph));
                        System.Diagnostics.Debug.WriteLine($"PDF: Добавлен текст: {textContent.Substring(0, Math.Min(50, textContent.Length))}...");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга PDF: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"PDF: Всего добавлено {content.Count} параграфов");
            return content;
        }
    }
}