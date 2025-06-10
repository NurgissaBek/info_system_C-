using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using HtmlAgilityPack;
using InfoSystem.Models;

namespace InfoSystem.Services
{
    public class WebScrapingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _googleApiKey;
        private readonly string _googleCseId;
        private readonly string _bingApiKey;

        // Настройки для разных источников поиска
        private readonly string[] _rssSources = {
            "https://lenta.ru/rss",
            "https://ria.ru/export/rss2/archive/index.xml",
            "https://tass.ru/rss/v2.xml"
        };

        public WebScrapingService(string googleApiKey = null, string googleCseId = null, string bingApiKey = null)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _googleApiKey = googleApiKey;
            _googleCseId = googleCseId;
            _bingApiKey = bingApiKey;
        }

        public async Task<List<ArticleDocument>> SearchAndParseArticlesAsync(string topic, int maxResults = 5)
        {
            var articles = new List<ArticleDocument>();

            Console.WriteLine($"\n🔍 Поиск статей по теме: '{topic}'");

            List<SearchResult> searchResults = new List<SearchResult>();

            // Пробуем разные источники поиска по приоритету
            try
            {
                // 1. Google Custom Search (если есть API ключи)
                if (!string.IsNullOrEmpty(_googleApiKey) && !string.IsNullOrEmpty(_googleCseId))
                {
                    Console.WriteLine("🌐 Используем Google Custom Search API");
                    searchResults = await GoogleSearchAsync(topic, maxResults);
                }
                // 2. Bing Search API (если есть ключ)
                else if (!string.IsNullOrEmpty(_bingApiKey))
                {
                    Console.WriteLine("🌐 Используем Bing Search API");
                    searchResults = await SerpApiSearchAsync(topic, maxResults);
                }
                // 3. RSS поиск (бесплатный)
                else
                {
                    Console.WriteLine("📡 Используем RSS поиск");
                    searchResults = await RssSearchAsync(topic, maxResults);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка поиска: {ex.Message}");
                return articles;
            }

            if (!searchResults.Any())
            {
                Console.WriteLine("😞 Статьи по данной теме не найдены");
                return articles;
            }

            Console.WriteLine($"📋 Найдено {searchResults.Count} потенциальных статей");

            // Парсим найденные статьи
            foreach (var result in searchResults)
            {
                try
                {
                    Console.WriteLine($"⏳ Парсинг: {result.Title}");

                    var content = await ParseArticleContentAsync(result.Url);
                    if (!string.IsNullOrEmpty(content) && content.Length > 200)
                    {
                        var article = new ArticleDocument
                        {
                            Title = CleanText(result.Title),
                            Content = content,
                            Url = result.Url,
                            Metadata = new ArticleMetadata
                            {
                                Topic = topic,
                                Source = ExtractDomain(result.Url),
                                DateAdded = DateTime.UtcNow,
                                WordCount = CountWords(content),
                                Keywords = ExtractKeywords(content, topic),
                                Summary = result.Snippet ?? GenerateSummary(content),
                                Language = DetectLanguage(content)
                            }
                        };

                        articles.Add(article);
                        Console.WriteLine($"✅ Успешно: {article.Title} ({article.Metadata.WordCount} слов)");
                    }
                    else
                    {
                        Console.WriteLine($"❌ Недостаточно контента: {result.Title}");
                    }

                    // Пауза между запросами
                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка парсинга {result.Url}: {ex.Message}");
                }
            }

            Console.WriteLine($"🎯 Успешно обработано: {articles.Count} из {searchResults.Count} статей");
            return articles;
        }

        // Google Custom Search API
        private async Task<List<SearchResult>> GoogleSearchAsync(string query, int maxResults)
        {
            try
            {
                var encodedQuery = HttpUtility.UrlEncode(query + " site:lenta.ru OR site:ria.ru OR site:tass.ru OR site:rbc.ru");
                var url = $"https://www.googleapis.com/customsearch/v1" +
                         $"?key={_googleApiKey}" +
                         $"&cx={_googleCseId}" +
                         $"&q={encodedQuery}" +
                         $"&num={Math.Min(maxResults, 10)}" +
                         $"&lr=lang_ru";

                var response = await _httpClient.GetStringAsync(url);
                var searchResponse = JsonSerializer.Deserialize<GoogleSearchResponse>(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return searchResponse.Items.Select(item => new SearchResult
                {
                    Title = item.Title,
                    Url = item.Link,
                    Snippet = item.Snippet
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Search ошибка: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        // Bing Search API
        /*private async Task<List<SearchResult>> BingSearchAsync(string query, int maxResults)
        {
            try
            {
                var encodedQuery = HttpUtility.UrlEncode(query + " site:lenta.ru OR site:ria.ru OR site:tass.ru");
                var url = $"https://api.bing.microsoft.com/v7.0/search?q={encodedQuery}&count={maxResults}&mkt=ru-RU";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", _bingApiKey);

                var response = await _httpClient.GetStringAsync(url);
                var searchResponse = JsonSerializer.Deserialize<BingSearchResponse>(response, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return searchResponse.WebPages.Value.Select(item => new SearchResult
                {
                    Title = item.Name,
                    Url = item.Url,
                    Snippet = item.Snippet
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bing Search ошибка: {ex.Message}");
                return new List<SearchResult>();
            }
        }*/
        private async Task<List<SearchResult>> SerpApiSearchAsync(string query, int maxResults)
        {
            try
            {
                var url = $"https://serpapi.com/search.json?q={HttpUtility.UrlEncode(query)}&engine=google&num={maxResults}&hl=ru&gl=ru&api_key={_bingApiKey}";

                var response = await _httpClient.GetStringAsync(url);
                var json = JsonSerializer.Deserialize<JsonElement>(response);

                if (!json.TryGetProperty("organic_results", out var results))
                    return new List<SearchResult>();

                return results.EnumerateArray()
                    .Select(item => new SearchResult
                    {
                        Title = item.GetProperty("title").GetString(),
                        Url = item.GetProperty("link").GetString(),
                        Snippet = item.TryGetProperty("snippet", out var snippet) ? snippet.GetString() : null
                    })
                    .Take(maxResults)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SerpAPI ошибка: {ex.Message}");
                return new List<SearchResult>();
            }
        }


        // RSS поиск (бесплатный вариант)
        private async Task<List<SearchResult>> RssSearchAsync(string topic, int maxResults)
        {
            var results = new List<SearchResult>();
            var topicLower = topic.ToLower();

            foreach (var rssUrl in _rssSources)
            {
                try
                {
                    Console.WriteLine($"📡 Проверяем RSS: {rssUrl}");

                    var rssContent = await _httpClient.GetStringAsync(rssUrl);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(rssContent);

                    // Парсим RSS
                    var items = doc.DocumentNode.SelectNodes("//item");
                    if (items != null)
                    {
                        foreach (var item in items.Take(20))
                        {
                            var title = item.SelectSingleNode(".//title")?.InnerText?.Trim();
                            var link = item.SelectSingleNode(".//link")?.InnerText?.Trim();
                            var description = item.SelectSingleNode(".//description")?.InnerText?.Trim();

                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                            {
                                // Проверяем, содержит ли заголовок или описание искомую тему
                                var titleLower = title.ToLower();
                                var descLower = (description ?? "").ToLower();

                                var keywords = topicLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (keywords.Any(k => titleLower.Contains(k) || descLower.Contains(k)))
                                {
                                    results.Add(new SearchResult
                                    {
                                        Title = CleanText(title),
                                        Url = link,
                                        Snippet = CleanText(description) ?? "Описание недоступно"
                                    });

                                    Console.WriteLine($"✅ Найдено в RSS: {title}");
                                }
                            }
                        }
                    }

                    await Task.Delay(500); // Пауза между RSS источниками
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Ошибка RSS {rssUrl}: {ex.Message}");
                }

                if (results.Count >= maxResults) break;
            }

            return results.Take(maxResults).ToList();
        }

        private async Task<string> ParseArticleContentAsync(string url)
        {
            try
            {
                var html = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Удаляем ненужные элементы
                var scriptsAndStyles = doc.DocumentNode.SelectNodes("//script | //style | //nav | //header | //footer | //aside");
                if (scriptsAndStyles != null)
                {
                    foreach (var node in scriptsAndStyles)
                        node.Remove();
                }

                // Ищем основной контент в разных местах
                var contentSelectors = new[]
                {
                    "//article//p",
                    "//div[contains(@class, 'content')]//p",
                    "//div[contains(@class, 'article')]//p",
                    "//div[contains(@class, 'text')]//p",
                    "//div[contains(@class, 'body')]//p",
                    "//main//p",
                    "//p[string-length(text()) > 50]"
                };

                string bestContent = "";

                foreach (var selector in contentSelectors)
                {
                    var nodes = doc.DocumentNode.SelectNodes(selector);
                    if (nodes != null && nodes.Any())
                    {
                        var content = string.Join(" ", nodes
                            .Select(n => n.InnerText)
                            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 30))
                            .Trim();

                        if (content.Length > bestContent.Length)
                        {
                            bestContent = content;
                        }
                    }
                }

                if (string.IsNullOrEmpty(bestContent))
                {
                    // Последняя попытка - весь текст
                    bestContent = doc.DocumentNode.InnerText;
                }

                return CleanContent(bestContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка парсинга {url}: {ex.Message}");
                return null;
            }
        }

        private string CleanContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return "";

            // Убираем лишние пробелы и переносы
            content = Regex.Replace(content, @"\s+", " ");
            content = Regex.Replace(content, @"[\r\n\t]+", " ");

            // Убираем HTML entities
            content = HttpUtility.HtmlDecode(content);

            return content.Trim();
        }

        private List<string> ExtractKeywords(string content, string topic)
        {
            var keywords = new HashSet<string>();

            // Добавляем слова из темы
            var topicWords = topic.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2);
            foreach (var word in topicWords)
            {
                keywords.Add(word.ToLower());
            }

            // Ищем наиболее частые слова в тексте
            var words = Regex.Matches(content.ToLower(), @"\b[а-яё]{4,}\b")
                .Cast<Match>()
                .Select(m => m.Value)
                .Where(w => !IsStopWord(w))
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key);

            foreach (var word in words)
            {
                keywords.Add(word);
            }

            return keywords.Take(10).ToList();
        }

        private bool IsStopWord(string word)
        {
            var stopWords = new[] { "который", "которая", "которые", "этого", "этом", "была", "было", "были", "есть", "для", "как", "что", "чтобы", "или", "также", "если", "когда", "где", "так" };
            return stopWords.Contains(word);
        }

        private string GenerateSummary(string content)
        {
            if (string.IsNullOrEmpty(content)) return "";

            var sentences = content.Split('.', '!', '?')
                .Where(s => s.Trim().Length > 50)
                .Take(2);

            var summary = string.Join(". ", sentences).Trim();
            return summary.Length > 300 ? summary.Substring(0, 300) + "..." : summary;
        }

        private string DetectLanguage(string content)
        {
            var russianChars = Regex.Matches(content, @"[а-яё]", RegexOptions.IgnoreCase).Count;
            var totalChars = content.Where(char.IsLetter).Count();

            return totalChars > 0 && (double)russianChars / totalChars > 0.5 ? "ru" : "en";
        }

        private int CountWords(string text)
        {
            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private string ExtractDomain(string url)
        {
            try
            {
                var uri = new Uri(url);
                return uri.Host.Replace("www.", "");
            }
            catch
            {
                return "unknown";
            }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return HttpUtility.HtmlDecode(Regex.Replace(text, @"\s+", " ").Trim());
        }
    }
}
