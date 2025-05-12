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
        public List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> ParseBook(string filePath)
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
                var textNodes = doc.DocumentNode.SelectNodes("//text()[normalize-space() and not(parent::script or parent::style)]") ?? new HtmlNodeCollection(null);
                var text = string.Join(" ", textNodes.Select(n => n.InnerText.Trim()));
                var decodedText = System.Net.WebUtility.HtmlDecode(text).Trim();
                System.Diagnostics.Debug.WriteLine($"StripHtml: обработано {decodedText.Length} символов, текст: {decodedText.Substring(0, Math.Min(50, decodedText.Length))}...");
                return decodedText;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в StripHtml: {ex.Message}");
                return string.Empty;
            }
        }

        private List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> ParseEpub(string filePath)
        {
            var content = new List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)>();
            try
            {
                var epubBook = EpubReader.ReadBook(filePath);
                System.Diagnostics.Debug.WriteLine($"EPUB: Загружено {epubBook.ReadingOrder.Count} файлов содержимого");

                // Название книги
                if (!string.IsNullOrWhiteSpace(epubBook.Title))
                {
                    var titleParagraph = new Paragraph(new Run(epubBook.Title.Trim()))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 24,
                        Margin = new Thickness(0, 20, 0, 20),
                        TextAlignment = TextAlignment.Center
                    };
                    content.Add((false, true, false, false, titleParagraph));
                    content.Add((false, false, false, true, new Paragraph()));
                    System.Diagnostics.Debug.WriteLine($"EPUB: Добавлено название: {epubBook.Title}");
                }

                // Оглавление
                var tocItems = new List<string>();
                var processedTitles = new HashSet<string>();

                void CollectTocItems(EpubNavigationItem navItem, int depth = 0)
                {
                    if (!string.IsNullOrWhiteSpace(navItem.Title) && !processedTitles.Contains(navItem.Title + depth))
                    {
                        tocItems.Add($"{new string(' ', depth * 2)}{navItem.Title.Trim()}");
                        processedTitles.Add(navItem.Title + depth);
                    }
                    foreach (var child in navItem.NestedItems)
                        CollectTocItems(child, depth + 1);
                }

                foreach (var navItem in epubBook.Navigation)
                    CollectTocItems(navItem);

                if (tocItems.Any())
                {
                    var tocHeader = new Paragraph(new Run("Оглавление"))
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = 20,
                        Margin = new Thickness(0, 10, 0, 10)
                    };
                    content.Add((false, false, true, false, tocHeader));
                    System.Diagnostics.Debug.WriteLine("EPUB: Добавлен заголовок оглавления");

                    foreach (var item in tocItems)
                    {
                        var tocParagraph = new Paragraph(new Run(item))
                        {
                            FontSize = 16,
                            Margin = new Thickness(0, 0, 0, 5)
                        };
                        content.Add((false, false, true, false, tocParagraph));
                        System.Diagnostics.Debug.WriteLine($"EPUB: Добавлен пункт оглавления: {item}");
                    }

                    content.Add((false, false, false, true, new Paragraph()));
                    System.Diagnostics.Debug.WriteLine("EPUB: Добавлен разрыв страницы после оглавления");
                }

                // Содержимое книги
                processedTitles.Clear();
                foreach (var textFile in epubBook.ReadingOrder)
                {
                    //System.Diagnostics.Debug.WriteLine($"EPUB: Обработка файла содержимого: {textFile.FileName}");
                    var doc = new HtmlDocument();
                    doc.LoadHtml(textFile.Content);

                    // Попытка 1: Выбор всех узлов с текстом
                    var nodes = doc.DocumentNode.SelectNodes("//*[not(self::script or self::style)]") ?? new HtmlNodeCollection(null);
                    //System.Diagnostics.Debug.WriteLine($"EPUB: Найдено {nodes.Count} узлов в файле {textFile.FileName} (попытка 1)");

                    if (!nodes.Any())
                    {
                        // Попытка 2: Выбор родительских узлов текстовых узлов
                        nodes = doc.DocumentNode.SelectNodes("//text()[normalize-space() and not(parent::script or parent::style)]/..") ?? new HtmlNodeCollection(null);
                        //System.Diagnostics.Debug.WriteLine($"EPUB: Найдено {nodes.Count} узлов в файле {textFile.FileName} (попытка 2)");
                    }

                    if (!nodes.Any())
                    {
                        // Попытка 3: Извлечение всего текста из файла
                        var rawText = StripHtml(doc.DocumentNode.OuterHtml);
                        if (!string.IsNullOrWhiteSpace(rawText))
                        {
                            var paragraph = new Paragraph(new Run(rawText.Trim()))
                            {
                                FontSize = 16,
                                Margin = new Thickness(0, 0, 0, 10)
                            };
                            content.Add((false, false, false, false, paragraph));
                            System.Diagnostics.Debug.WriteLine($"EPUB: Добавлен текст из файла (попытка 3): {rawText.Substring(0, Math.Min(50, rawText.Length))}...");
                        }
                        else
                        {
                            //System.Diagnostics.Debug.WriteLine($"EPUB: Текст не найден в файле {textFile.FileName}");
                        }
                        continue;
                    }

                    foreach (var node in nodes)
                    {
                        var text = StripHtml(node.OuterHtml);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            System.Diagnostics.Debug.WriteLine($"EPUB: Пропущен пустой узел: {node.Name}");
                            continue;
                        }

                        if (processedTitles.Contains(text))
                        {
                            System.Diagnostics.Debug.WriteLine($"EPUB: Пропущен дубликат текста: {text.Substring(0, Math.Min(50, text.Length))}...");
                            continue;
                        }

                        var isChapter = node.Name is "h1" or "h2" or "h3" || Regex.IsMatch(text, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                        var paragraph = new Paragraph(new Run(text.Trim()))
                        {
                            FontWeight = isChapter ? FontWeights.Bold : FontWeights.Normal,
                            FontSize = isChapter ? 20 : 16,
                            Margin = new Thickness(0, isChapter ? 10 : 0, 0, 10)
                        };
                        content.Add((isChapter, false, false, false, paragraph));

                        if (isChapter)
                        {
                            content.Add((false, false, false, true, new Paragraph()));
                            System.Diagnostics.Debug.WriteLine($"EPUB: Добавлен разрыв страницы после главы: {text.Substring(0, Math.Min(50, text.Length))}...");
                        }

                        processedTitles.Add(text);
                        System.Diagnostics.Debug.WriteLine($"EPUB: Добавлен {(isChapter ? "заголовок главы" : "текст")}: {text.Substring(0, Math.Min(50, text.Length))}...");
                    }
                }

                if (content.Count <= tocItems.Count + 2)
                {
                    System.Diagnostics.Debug.WriteLine("EPUB: Текст не извлечён, добавляем заглушку");
                    var placeholder = new Paragraph(new Run("Содержимое книги не удалось извлечь. Проверьте файл."))
                    {
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    content.Add((false, false, false, false, placeholder));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга EPUB: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"EPUB: Всего добавлено {content.Count} параграфов");
            return content;
        }

        private List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> ParseFb2(string filePath)
        {
            var content = new List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)>();
            try
            {
                var doc = XDocument.Load(filePath);

                // Название книги
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
                        content.Add((false, true, false, false, titleParagraph));
                        content.Add((false, false, false, true, new Paragraph()));
                        System.Diagnostics.Debug.WriteLine($"FB2: Добавлено название: {bookTitle}");
                    }
                }

                // Оглавление
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
                    content.Add((false, false, true, false, tocHeader));
                    System.Diagnostics.Debug.WriteLine("FB2: Добавлен заголовок оглавления");

                    foreach (var item in tocItems)
                    {
                        var tocParagraph = new Paragraph(new Run(item))
                        {
                            FontSize = 16,
                            Margin = new Thickness(0, 0, 0, 5)
                        };
                        content.Add((false, false, true, false, tocParagraph));
                        System.Diagnostics.Debug.WriteLine($"FB2: Добавлен пункт оглавления: {item}");
                    }

                    content.Add((false, false, false, true, new Paragraph()));
                    System.Diagnostics.Debug.WriteLine("FB2: Добавлен разрыв страницы после оглавления");
                }

                // Содержимое
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
                                content.Add((true, false, false, false, titleParagraph));
                                content.Add((false, false, false, true, new Paragraph()));
                                System.Diagnostics.Debug.WriteLine($"FB2: Добавлен заголовок главы: {titleText}");
                            }
                        }

                        var sectionElements = section.Descendants()
                            .Where(e => e.Name.LocalName is "p" or "v" or "empty-line" or "subtitle" or "epigraph" or "cite" or "annotation");
                        foreach (var element in sectionElements)
                        {
                            var text = element.Name.LocalName == "empty-line" ? "" : StripHtml(element.Value);
                            if (element.Name.LocalName == "empty-line" || !string.IsNullOrWhiteSpace(text))
                            {
                                var paragraph = new Paragraph(new Run(text.Trim()))
                                {
                                    FontSize = 16,
                                    Margin = new Thickness(0, 0, 0, 10)
                                };
                                content.Add((false, false, false, false, paragraph));
                                System.Diagnostics.Debug.WriteLine($"FB2: Добавлен текст: {text.Substring(0, Math.Min(50, text.Length))}...");
                            }
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("FB2: Тег <body> не найден");
                }

                if (content.Count <= tocItems.Count + 2)
                {
                    System.Diagnostics.Debug.WriteLine("FB2: Текст не извлечён, добавляем заглушку");
                    var placeholder = new Paragraph(new Run("Содержимое книги не удалось извлечь. Проверьте файл."))
                    {
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    content.Add((false, false, false, false, placeholder));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка парсинга FB2: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine($"FB2: Всего добавлено {content.Count} параграфов");
            return content;
        }

        private List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)> ParsePdf(string filePath)
        {
            var content = new List<(bool isChapterStart, bool isTitle, bool isToc, bool isPageBreak, Paragraph paragraph)>();
            try
            {
                using var document = PdfDocument.Load(filePath);

                // Название книги
                var bookTitle = Path.GetFileNameWithoutExtension(filePath);
                var titleParagraph = new Paragraph(new Run(bookTitle.Trim()))
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 24,
                    Margin = new Thickness(0, 20, 0, 20),
                    TextAlignment = TextAlignment.Center
                };
                content.Add((false, true, false, false, titleParagraph));
                content.Add((false, false, false, true, new Paragraph()));
                System.Diagnostics.Debug.WriteLine($"PDF: Добавлено название: {bookTitle}");

                // Оглавление
                var tocHeader = new Paragraph(new Run("Оглавление"))
                {
                    FontWeight = FontWeights.Bold,
                    FontSize = 20,
                    Margin = new Thickness(0, 10, 0, 10)
                };
                content.Add((false, false, true, false, tocHeader));
                System.Diagnostics.Debug.WriteLine("PDF: Добавлен заголовок оглавления");

                for (int i = 0; i < Math.Min(document.PageCount, 5); i++)
                {
                    var text = document.GetPdfText(i);
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (Regex.IsMatch(trimmedLine, @"^\d+\.\s|^Chapter\s", RegexOptions.IgnoreCase))
                        {
                            var tocParagraph = new Paragraph(new Run(trimmedLine))
                            {
                                FontSize = 16,
                                Margin = new Thickness(0, 0, 0, 5)
                            };
                            content.Add((false, false, true, false, tocParagraph));
                            System.Diagnostics.Debug.WriteLine($"PDF: Добавлен пункт оглавления: {trimmedLine}");
                        }
                    }
                }

                content.Add((false, false, false, true, new Paragraph()));
                System.Diagnostics.Debug.WriteLine("PDF: Добавлен разрыв страницы после оглавления");

                // Содержимое
                var currentParagraphText = new StringBuilder();
                for (int i = 0; i < document.PageCount; i++)
                {
                    var text = document.GetPdfText(i);
                    var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                    foreach (var line in lines)
                    {
                        var trimmedLine = line.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedLine))
                        {
                            if (currentParagraphText.Length > 0)
                            {
                                var textContent = currentParagraphText.ToString().Trim();
                                var isChapter = Regex.IsMatch(textContent, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                                var paragraph = new Paragraph(new Run(textContent))
                                {
                                    FontWeight = isChapter ? FontWeights.Bold : FontWeights.Normal,
                                    FontSize = isChapter ? 20 : 16,
                                    Margin = new Thickness(0, isChapter ? 10 : 0, 0, 10)
                                };
                                content.Add((isChapter, false, false, false, paragraph));
                                if (isChapter)
                                {
                                    content.Add((false, false, false, true, new Paragraph()));
                                    System.Diagnostics.Debug.WriteLine($"PDF: Добавлен разрыв страницы после главы: {textContent.Substring(0, Math.Min(50, textContent.Length))}...");
                                }
                                System.Diagnostics.Debug.WriteLine($"PDF: Добавлен {(isChapter ? "заголовок главы" : "текст")}: {textContent.Substring(0, Math.Min(50, textContent.Length))}...");
                                currentParagraphText.Clear();
                            }
                        }
                        else
                        {
                            currentParagraphText.AppendLine(trimmedLine);
                        }
                    }
                }

                if (currentParagraphText.Length > 0)
                {
                    var textContent = currentParagraphText.ToString().Trim();
                    var isChapter = Regex.IsMatch(textContent, @"^(Глава|Chapter)\s", RegexOptions.IgnoreCase);
                    var paragraph = new Paragraph(new Run(textContent))
                    {
                        FontWeight = isChapter ? FontWeights.Bold : FontWeights.Normal,
                        FontSize = isChapter ? 20 : 16,
                        Margin = new Thickness(0, isChapter ? 10 : 0, 0, 10)
                    };
                    content.Add((isChapter, false, false, false, paragraph));
                    if (isChapter)
                    {
                        content.Add((false, false, false, true, new Paragraph()));
                    }
                    System.Diagnostics.Debug.WriteLine($"PDF: Добавлен {(isChapter ? "заголовок главы" : "текст")}: {textContent.Substring(0, Math.Min(50, textContent.Length))}...");
                }

                if (content.Count <= 2)
                {
                    System.Diagnostics.Debug.WriteLine("PDF: Текст не извлечён, добавляем заглушку");
                    var placeholder = new Paragraph(new Run("Содержимое книги не удалось извлечь. Возможно, PDF содержит отсканированные страницы."))
                    {
                        FontSize = 16,
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    content.Add((false, false, false, false, placeholder));
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