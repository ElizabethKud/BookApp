using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public List<(bool isChapterStart, Paragraph paragraph)> ParseBook(string filePath)
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

        private List<(bool isChapterStart, Paragraph paragraph)> ParseEpub(string filePath)
        {
            var content = new List<(bool isChapterStart, Paragraph paragraph)>();
            var epubBook = EpubReader.ReadBook(filePath);
            var processedTitles = new HashSet<string>();

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
                    content.Add((true, chapterTitle));
                    processedTitles.Add(navItem.Title + depth);
                }

                if (navItem.HtmlContentFile != null)
                {
                    try
                    {
                        var htmlContent = navItem.HtmlContentFile.Content;
                        var paragraphs = Regex.Split(htmlContent, @"(?<=</p>)").Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

                        foreach (var p in paragraphs)
                        {
                            var text = StripHtml(p).Trim();
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                var paragraph = new Paragraph(new Run(text))
                                {
                                    Margin = new Thickness(0, 0, 0, 10),
                                    TextIndent = 20 // Красная строка
                                };
                                content.Add((false, paragraph));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Ошибка при чтении содержимого главы: {ex.Message}");
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

            if (!content.Any() && epubBook.ReadingOrder.Any())
            {
                foreach (var textFile in epubBook.ReadingOrder)
                {
                    var htmlContent = textFile.Content;
                    var paragraphs = Regex.Split(htmlContent, @"(?<=</p>)").Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

                    foreach (var p in paragraphs)
                    {
                        var trimmedLine = StripHtml(p).Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            var isChapterStart = Regex.IsMatch(trimmedLine, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                            if (isChapterStart && processedTitles.Contains(trimmedLine))
                            {
                                continue;
                            }
                            var paragraph = new Paragraph(new Run(trimmedLine))
                            {
                                FontWeight = isChapterStart ? FontWeights.Bold : FontWeights.Normal,
                                FontSize = isChapterStart ? 20 : 16,
                                Margin = new Thickness(0, isChapterStart ? 10 : 0, 0, 10),
                                TextIndent = isChapterStart ? 0 : 20 // Красная строка для обычных абзацев
                            };
                            content.Add((isChapterStart, paragraph));
                            if (isChapterStart)
                            {
                                processedTitles.Add(trimmedLine);
                            }
                        }
                    }
                }
            }

            Debug.WriteLine($"Parsed EPUB content. Total paragraphs: {content.Count}, Chapters: {content.Count(c => c.isChapterStart)}");
            return content;
        }

        private string StripHtml(string html)
        {
            var text = Regex.Replace(html, "<.*?>", string.Empty);
            return System.Net.WebUtility.HtmlDecode(text);
        }

        private List<(bool isChapterStart, Paragraph paragraph)> ParseFb2(string filePath)
        {
            var content = new List<(bool isChapterStart, Paragraph paragraph)>();
            var doc = XDocument.Load(filePath);
            var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "body");
            var processedTitles = new HashSet<string>();

            if (body != null)
            {
                var sections = body.Descendants().Where(e => e.Name.LocalName == "section");
                foreach (var section in sections)
                {
                    var title = section.Descendants().FirstOrDefault(e => e.Name.LocalName == "title");
                    if (title != null)
                    {
                        var titleText = StripHtml(title.Value);
                        if (!string.IsNullOrWhiteSpace(titleText) && !processedTitles.Contains(titleText))
                        {
                            var titleParagraph = new Paragraph(new Run(titleText.Trim()))
                            {
                                FontWeight = FontWeights.Bold,
                                FontSize = 20,
                                Margin = new Thickness(0, 10, 0, 10)
                            };
                            content.Add((true, titleParagraph));
                            processedTitles.Add(titleText);
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
                                Margin = new Thickness(0, 0, 0, 10),
                                TextIndent = 20 // Красная строка
                            };
                            content.Add((false, paragraph));
                        }
                    }
                }
            }

            Debug.WriteLine($"Parsed FB2 content. Total paragraphs: {content.Count}, Chapters: {content.Count(c => c.isChapterStart)}");
            return content;
        }

        private List<(bool isChapterStart, Paragraph paragraph)> ParsePdf(string filePath)
        {
            var content = new List<(bool isChapterStart, Paragraph paragraph)>();
            using var document = PdfDocument.Load(filePath);
            var processedTitles = new HashSet<string>();

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
                            text = currentParagraphText.ToString().Trim();
                            var isChapterStart = Regex.IsMatch(text, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                            if (isChapterStart && processedTitles.Contains(text))
                            {
                                currentParagraphText.Clear();
                                continue;
                            }
                            var paragraph = new Paragraph(new Run(text))
                            {
                                FontWeight = isChapterStart ? FontWeights.Bold : FontWeights.Normal,
                                FontSize = isChapterStart ? 20 : 16,
                                Margin = new Thickness(0, isChapterStart ? 10 : 0, 0, 10),
                                TextIndent = isChapterStart ? 0 : 20 // Красная строка для обычных абзацев
                            };
                            content.Add((isChapterStart, paragraph));
                            if (isChapterStart)
                            {
                                processedTitles.Add(text);
                            }
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
                    text = currentParagraphText.ToString().Trim();
                    var isChapterStart = Regex.IsMatch(text, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                    if (isChapterStart && processedTitles.Contains(text))
                    {
                        continue;
                    }
                    var paragraph = new Paragraph(new Run(text))
                    {
                        FontWeight = isChapterStart ? FontWeights.Bold : FontWeights.Normal,
                        FontSize = isChapterStart ? 20 : 16,
                        Margin = new Thickness(0, isChapterStart ? 10 : 0, 0, 10),
                        TextIndent = isChapterStart ? 0 : 20 // Красная строка для обычных абзацев
                    };
                    content.Add((isChapterStart, paragraph));
                    if (isChapterStart)
                    {
                        processedTitles.Add(text);
                    }
                }
            }

            Debug.WriteLine($"Parsed PDF content. Total paragraphs: {content.Count}, Chapters: {content.Count(c => c.isChapterStart)}");
            return content;
        }
    }
}