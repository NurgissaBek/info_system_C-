using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

using InfoSystem.Models;

namespace InfoSystem.Services
{
    public class InformationSystemService
    {
        private readonly MongoDbService _mongoService;
        private readonly WebScrapingService _scrapingService;

        public InformationSystemService(string connectionString, string databaseName,
            string googleApiKey = null, string googleCseId = null, string serpApiKey = null)
        {
            _mongoService = new MongoDbService(connectionString, databaseName);
            _scrapingService = new WebScrapingService(googleApiKey, googleCseId, serpApiKey);
        }

        public async Task CollectArticlesAsync(string topic, int maxArticles = 5, string searchEngine = "auto")
        {
            Console.WriteLine($"=== Сбор статей по теме '{topic}' через {searchEngine} ===");

            var startTime = DateTime.Now;
            var articles = await _scrapingService.SearchAndParseArticlesAsync(topic, maxArticles, searchEngine);
            var duration = DateTime.Now - startTime;

            foreach (var article in articles)
            {
                await _mongoService.SaveArticleAsync(article);
            }

            Console.WriteLine($"\n📊 РЕЗУЛЬТАТ:");
            Console.WriteLine($"   • Найдено и сохранено: {articles.Count} статей");
            Console.WriteLine($"   • Время выполнения: {duration:mm\\:ss}");
            Console.WriteLine($"   • Среднее время на статью: {(articles.Count > 0 ? duration.TotalSeconds / articles.Count : 0):F1} сек");
        }

        public async Task<List<ArticleDocument>> SearchInternalAsync(string query, string topic = null, int limit = 10)
        {
            Console.WriteLine($"\n🔍 ВНУТРЕННИЙ ПОИСК");
            Console.WriteLine($"   Запрос: '{query}'");
            if (topic != null) Console.WriteLine($"   Фильтр по теме: '{topic}'");

            var results = await _mongoService.SearchArticlesAsync(query, topic, limit);
            Console.WriteLine($"   Найдено: {results.Count} статей");

            return results;
        }

        public async Task<List<string>> GetAvailableTopicsAsync()
        {
            return await _mongoService.GetAvailableTopicsAsync();
        }

        public async Task ShowStatisticsAsync()
        {
            var topics = await GetAvailableTopicsAsync();
            var totalCount = await _mongoService.GetArticlesCountAsync();

            Console.WriteLine($"\n📈 СТАТИСТИКА БАЗЫ ДАННЫХ");
            Console.WriteLine(new string('=', 40));
            Console.WriteLine($"Всего статей: {totalCount}");
            Console.WriteLine($"Уникальных тем: {topics.Count}");

            if (topics.Any())
            {
                Console.WriteLine("\nРаспределение по темам:");
                foreach (var topic in topics)
                {
                    var count = await _mongoService.GetArticlesCountAsync(topic);
                    Console.WriteLine($"  📂 {topic}: {count} статей");
                }
            }
        }

        public async Task ClearDatabaseAsync()
        {
            Console.Write("⚠️ Вы уверены, что хотите очистить базу данных? (да/нет): ");
            var confirm = Console.ReadLine()?.ToLower();

            if (confirm == "да" || confirm == "yes" || confirm == "y")
            {
                await _mongoService.ClearDatabaseAsync();
            }
            else
            {
                Console.WriteLine("❌ Операция отменена");
            }
        }

        public void DisplaySearchResults(List<ArticleDocument> results)
        {
            if (!results.Any())
            {
                Console.WriteLine("\n😞 По вашему запросу ничего не найдено");
                Console.WriteLine("💡 Попробуйте:");
                Console.WriteLine("   • Изменить ключевые слова");
                Console.WriteLine("   • Убрать фильтр по теме");
                Console.WriteLine("   • Собрать больше статей по нужной теме");
                return;
            }

            Console.WriteLine($"\n📋 РЕЗУЛЬТАТЫ ПОИСКА ({results.Count} статей)");
            Console.WriteLine(new string('=', 80));

            for (int i = 0; i < results.Count; i++)
            {
                var article = results[i];
                Console.WriteLine($"\n📄 [{i + 1}] {article.Title}");
                Console.WriteLine($"🔗 {article.Url}");
                Console.WriteLine($"📂 Тема: {article.Metadata.Topic}");
                Console.WriteLine($"🌐 Источник: {article.Metadata.Source}");
                Console.WriteLine($"📊 Слов: {article.Metadata.WordCount} | Язык: {article.Metadata.Language}");
                Console.WriteLine($"🏷️ Ключевые слова: {string.Join(", ", article.Metadata.Keywords)}");
                Console.WriteLine($"📝 {article.Metadata.Summary}");
                Console.WriteLine($"📅 {article.Metadata.DateAdded:dd.MM.yyyy HH:mm}");
                Console.WriteLine(new string('-', 80));
            }
        }
    }
}
