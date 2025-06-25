using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;

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
        private readonly string _serpApiKey;

        public WebScrapingService(string googleApiKey = null, string googleCseId = null, string serpApiKey = null)
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            _googleApiKey = googleApiKey;
            _googleCseId = googleCseId;
            _serpApiKey = serpApiKey;
        }

        public async Task<List<ArticleDocument>> SearchAndParseArticlesAsync(string topic, int maxResults = 5, string searchEngine = "auto")
        {
            var articles = new List<ArticleDocument>();

            Console.WriteLine($"\n🔍 Поиск статей по теме: '{topic}'");

            List<SearchResult> searchResults = new List<SearchResult>();

            try
            {
                switch (searchEngine.ToLower())
                {
                    case "google":
                        if (!string.IsNullOrEmpty(_googleApiKey) && !string.IsNullOrEmpty(_googleCseId))
                        {
                            Console.WriteLine("🌐 Поисковик: Google");
                            searchResults = await GoogleSearchAsync(topic, maxResults);
                        }
                        else
                        {
                            throw new Exception("Отсутствуют ключи для Google Custom Search API.");
                        }
                        break;

                    case "serp":
                        if (!string.IsNullOrEmpty(_serpApiKey))
                        {
                            Console.WriteLine("🌐 Поисковик: serp (SerpAPI)");
                            searchResults = await SerpApiSearchAsync(topic, maxResults);
                        }
                        else
                        {
                            throw new Exception("Отсутствует ключ для SerpAPI.");
                        }
                        break;

                    case "duckduckgo":
                        Console.WriteLine("🌐 Поисковик: DuckDuckGo");
                        searchResults = await DuckDuckGoSearchAsync(topic, maxResults);
                        break;

                    // Добавить в switch case метода SearchAndParseArticlesAsync:
                    case "sciencedirect":
                        Console.WriteLine("🌐 Поисковик: ScienceDirect");
                        searchResults = await ScienceDirectSearchAsync(topic, maxResults);
                        break;

                    case "scholar":
                        Console.WriteLine("🌐 Поисковик: Google Scholar");
                        searchResults = await GoogleScholarSearchAsync(topic, maxResults);
                        break;

                    case "auto":
                    default:
                        // текущая логика по умолчанию
                        if (!string.IsNullOrEmpty(_googleApiKey) && !string.IsNullOrEmpty(_googleCseId))
                        {
                            Console.WriteLine("🌐 По умолчанию: Google");
                            searchResults = await GoogleSearchAsync(topic, maxResults);
                        }
                        else if (!string.IsNullOrEmpty(_serpApiKey))
                        {
                            Console.WriteLine("🌐 По умолчанию: serp");
                            searchResults = await SerpApiSearchAsync(topic, maxResults);
                        }
                        else
                        {
                            Console.WriteLine("🌐 По умолчанию: DuckDuckGo");
                            searchResults = await DuckDuckGoSearchAsync(topic, maxResults);
                        }
                        break;
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
                var encodedQuery = HttpUtility.UrlEncode(query);
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
        private async Task<List<SearchResult>> SerpApiSearchAsync(string query, int maxResults)
        {
            try
            {
                var url = $"https://serpapi.com/search.json?q={HttpUtility.UrlEncode(query)}&engine=google&num={maxResults}&api_key={_serpApiKey}";

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
        private async Task<List<SearchResult>> ScienceDirectSearchAsync(string query, int maxResults)
        {
            try
            {
                var encodedQuery = HttpUtility.UrlEncode(query);
                var url = $"https://www.sciencedirect.com/search?qs={encodedQuery}&show=25";

                var response = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                var results = new List<SearchResult>();
                var articles = doc.DocumentNode.SelectNodes("//h2[@class='result-list-title-link']/a");

                if (articles != null)
                {
                    foreach (var article in articles.Take(maxResults))
                    {
                        var title = article.InnerText?.Trim();
                        var href = article.GetAttributeValue("href", "");
                        var fullUrl = href.StartsWith("http") ? href : "https://www.sciencedirect.com" + href;

                        results.Add(new SearchResult
                        {
                            Title = title,
                            Url = fullUrl,
                            Snippet = "Научная статья из ScienceDirect"
                        });
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ScienceDirect ошибка: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        private async Task<List<SearchResult>> GoogleScholarSearchAsync(string query, int maxResults)
        {
            try
            {
                var encodedQuery = HttpUtility.UrlEncode(query);
                var url = $"https://scholar.google.com/scholar?q={encodedQuery}&num={maxResults}";

                // Добавляем заголовки для Scholar
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await client.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                var results = new List<SearchResult>();
                var articles = doc.DocumentNode.SelectNodes("//div[@class='gs_r gs_or gs_scl']");

                if (articles != null)
                {
                    foreach (var article in articles.Take(maxResults))
                    {
                        var titleNode = article.SelectSingleNode(".//h3[@class='gs_rt']/a");
                        var title = titleNode?.InnerText?.Trim();
                        var articleUrl = titleNode?.GetAttributeValue("href", "");
                        var snippet = article.SelectSingleNode(".//div[@class='gs_rs']")?.InnerText?.Trim();

                        if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(articleUrl))
                        {
                            results.Add(new SearchResult
                            {
                                Title = CleanText(title),
                                Url = articleUrl,
                                Snippet = CleanText(snippet) ?? "Научная публикация из Google Scholar"
                            });
                        }
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google Scholar ошибка: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        private async Task<List<SearchResult>> DuckDuckGoSearchAsync(string query, int maxResults)
        {
            try
            {
                Console.WriteLine("🦆 Используем улучшенный DuckDuckGo поиск");
                Console.WriteLine($"🔍 Запрос: {query}");

                var results = new List<SearchResult>();

                // Используем комбинированный подход для получения более качественных результатов
                await TryImprovedHtmlParsing(query, results, maxResults);

                if (results.Count < maxResults)
                {
                    // Дополнительный поиск через альтернативные источники
                    await TryAlternativeSearchEngines(query, results, maxResults);
                }

                Console.WriteLine($"🦆 DuckDuckGo: найдено {results.Count} качественных результатов");
                return results.Take(maxResults).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Критическая ошибка DuckDuckGo поиска: {ex.Message}");
                return new List<SearchResult>();
            }
        }

        private async Task TryImprovedHtmlParsing(string query, List<SearchResult> results, int maxResults)
        {
            try
            {
                Console.WriteLine("🔄 Улучшенный HTML парсинг...");

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // Более реалистичные заголовки
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edg/122.0.0.0");
                client.DefaultRequestHeaders.Add("Accept",
                    "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
                client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
                client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                client.DefaultRequestHeaders.Add("DNT", "1");
                client.DefaultRequestHeaders.Add("Connection", "keep-alive");
                client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");

                // Задержка перед запросом
                await Task.Delay(Random.Shared.Next(1000, 3000));

                // Используем прямой поиск без редиректов
                var searchUrl = $"https://duckduckgo.com/lite/?q={HttpUtility.UrlEncode(query + " site:edu OR site:org OR site:com")}&s=0&o=json&vqd=&l=us-en&p=1&ex=-1";

                Console.WriteLine($"🌐 Запрос к: {searchUrl}");

                var response = await client.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ HTTP ошибка: {response.StatusCode}");
                    return;
                }

                var html = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"📄 Получено: {html.Length} символов");

                if (html.Length < 1000)
                {
                    Console.WriteLine("⚠️ Слишком короткий ответ, возможно блокировка");
                    return;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Улучшенные селекторы для DuckDuckGo Lite
                var resultNodes = doc.DocumentNode.SelectNodes("//table[@class='results']//tr") ??
                                 doc.DocumentNode.SelectNodes("//div[@class='result']") ??
                                 doc.DocumentNode.SelectNodes("//div[contains(@class, 'web-result')]");

                if (resultNodes != null)
                {
                    Console.WriteLine($"✅ Найдено узлов: {resultNodes.Count}");

                    foreach (var node in resultNodes.Take(maxResults * 2))
                    {
                        try
                        {
                            // Для DuckDuckGo Lite формат другой
                            var linkNode = node.SelectSingleNode(".//a[@class='result-link']") ??
                                          node.SelectSingleNode(".//a[contains(@href, 'http')]") ??
                                          node.SelectSingleNode(".//a[not(contains(@href, 'duckduckgo.com'))]");

                            if (linkNode == null) continue;

                            var title = linkNode.InnerText?.Trim();
                            var href = linkNode.GetAttributeValue("href", "");

                            // Очищаем URL от DuckDuckGo редиректов
                            var realUrl = CleanDuckDuckGoUrl(href);

                            // Ищем описание в соседних узлах
                            var snippetNode = node.SelectSingleNode(".//td[@class='result-snippet']") ??
                                             node.SelectSingleNode(".//span[@class='result-snippet']") ??
                                             node.SelectSingleNode(".//*[contains(text(), '.') and string-length(text()) > 30]");

                            var snippet = snippetNode?.InnerText?.Trim() ??
                                         GenerateSnippetFromTitle(title);

                            // Валидация результата
                            if (IsValidSearchResult(title, realUrl, snippet))
                            {
                                var searchResult = new SearchResult
                                {
                                    Title = CleanText(title),
                                    Url = realUrl,
                                    Snippet = CleanText(snippet)
                                };

                                // Проверяем на дубликаты
                                if (!results.Any(r => r.Url == searchResult.Url ||
                                                     LevenshteinDistance(r.Title, searchResult.Title) < 3))
                                {
                                    results.Add(searchResult);
                                    Console.WriteLine($"✅ Добавлен: {title}");

                                    if (results.Count >= maxResults) break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Ошибка обработки узла: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("❌ Не найдено результирующих узлов");

                    // Попробуем извлечь любые валидные ссылки
                    await ExtractAnyValidLinks(html, query, results, maxResults);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка улучшенного HTML парсинга: {ex.Message}");
            }
        }

        private async Task TryAlternativeSearchEngines(string query, List<SearchResult> results, int maxResults)
        {
            if (results.Count >= maxResults) return;

            try
            {
                Console.WriteLine("🔄 Поиск через альтернативные источники...");

                // Список альтернативных поисковых движков
                var alternativeEngines = new[]
                {
            new { Name = "Startpage", Url = $"https://www.startpage.com/sp/search?query={HttpUtility.UrlEncode(query)}" },
            new { Name = "Searx", Url = $"https://searx.be/search?q={HttpUtility.UrlEncode(query)}&format=html" },
            new { Name = "Yandex", Url = $"https://yandex.com/search/?text={HttpUtility.UrlEncode(query)}&lr=84" }
        };

                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                foreach (var engine in alternativeEngines)
                {
                    if (results.Count >= maxResults) break;

                    try
                    {
                        Console.WriteLine($"🌐 Пробуем {engine.Name}...");

                        await Task.Delay(2000); // Задержка между запросами

                        var html = await client.GetStringAsync(engine.Url);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(html);

                        // Универсальные селекторы для поисковых результатов
                        var linkNodes = doc.DocumentNode.SelectNodes("//a[contains(@href, 'http') and not(contains(@href, 'google.com')) and not(contains(@href, 'yandex.com')) and not(contains(@href, 'startpage.com'))]")
                            ?.Where(n => !string.IsNullOrWhiteSpace(n.InnerText))
                            ?.Where(n => n.InnerText.Length > 10 && n.InnerText.Length < 200)
                            ?.Take(5);

                        if (linkNodes != null)
                        {
                            foreach (var link in linkNodes)
                            {
                                var title = link.InnerText.Trim();
                                var url = link.GetAttributeValue("href", "");

                                if (IsValidSearchResult(title, url, title) &&
                                    !results.Any(r => r.Url == url))
                                {
                                    results.Add(new SearchResult
                                    {
                                        Title = CleanText(title),
                                        Url = url,
                                        Snippet = $"Найдено через {engine.Name}"
                                    });

                                    Console.WriteLine($"✅ {engine.Name}: {title}");

                                    if (results.Count >= maxResults) break;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️ Ошибка {engine.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка альтернативного поиска: {ex.Message}");
            }
        }

        private async Task ExtractAnyValidLinks(string html, string query, List<SearchResult> results, int maxResults)
        {
            try
            {
                Console.WriteLine("🔄 Извлечение любых валидных ссылок...");

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Ищем все ссылки
                var allLinks = doc.DocumentNode.SelectNodes("//a[@href]")
                    ?.Where(n => !string.IsNullOrWhiteSpace(n.InnerText))
                    ?.Where(n => n.GetAttributeValue("href", "").StartsWith("http"))
                    ?.Where(n => !n.GetAttributeValue("href", "").Contains("duckduckgo.com"))
                    ?.Take(20);

                if (allLinks != null)
                {
                    foreach (var link in allLinks)
                    {
                        var title = link.InnerText.Trim();
                        var url = link.GetAttributeValue("href", "");

                        if (IsValidSearchResult(title, url, title) &&
                            !results.Any(r => r.Url == url) &&
                            IsRelevantToQuery(title, query))
                        {
                            results.Add(new SearchResult
                            {
                                Title = CleanText(title),
                                Url = url,
                                Snippet = GenerateSnippetFromTitle(title)
                            });

                            Console.WriteLine($"✅ Извлечена ссылка: {title}");

                            if (results.Count >= maxResults) break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Ошибка извлечения ссылок: {ex.Message}");
            }
        }

        private string CleanDuckDuckGoUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            // Убираем DuckDuckGo редиректы
            if (url.Contains("duckduckgo.com/l/?uddg="))
            {
                var uddgIndex = url.IndexOf("uddg=") + 5;
                if (uddgIndex < url.Length)
                {
                    var encodedUrl = url.Substring(uddgIndex);
                    var ampIndex = encodedUrl.IndexOf("&");
                    if (ampIndex > 0)
                        encodedUrl = encodedUrl.Substring(0, ampIndex);

                    return HttpUtility.UrlDecode(encodedUrl);
                }
            }

            return url.StartsWith("//") ? "https:" + url : url;
        }

        private bool IsValidSearchResult(string title, string url, string snippet)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url))
                return false;

            if (title.Length < 5 || title.Length > 200)
                return false;

            if (!url.StartsWith("http"))
                return false;

            // Исключаем нежелательные сайты
            var excludeDomains = new[] { "duckduckgo.com", "google.com", "serp.com", "facebook.com", "twitter.com" };
            if (excludeDomains.Any(domain => url.Contains(domain)))
                return false;

            // Исключаем слишком общие заголовки
            var genericTitles = new[] { "home", "main", "index", "login", "search", "404", "error" };
            if (genericTitles.Any(generic => title.ToLower().Contains(generic)))
                return false;

            return true;
        }

        private bool IsRelevantToQuery(string title, string query)
        {
            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var titleWords = title.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Проверяем, содержит ли заголовок хотя бы одно слово из запроса
            return queryWords.Any(qw => titleWords.Any(tw => tw.Contains(qw) || qw.Contains(tw)));
        }

        private string GenerateSnippetFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Описание недоступно";

            return title.Length > 100 ? title.Substring(0, 100) + "..." : title;
        }

        private int LevenshteinDistance(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1)) return string.IsNullOrEmpty(s2) ? 0 : s2.Length;
            if (string.IsNullOrEmpty(s2)) return s1.Length;

            var matrix = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= s2.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(Math.Min(
                        matrix[i - 1, j] + 1,
                        matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[s1.Length, s2.Length];
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
