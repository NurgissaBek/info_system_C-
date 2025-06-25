using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MongoDB.Bson;
using InfoSystem.Models;

namespace InfoSystem.Services
{
    public class InformationSystemService
    {
        private readonly MongoDbService _mongoService;
        private readonly WebScrapingService _scrapingService;
        private readonly OllamaService _ollamaService;

        public InformationSystemService(string connectionString, string databaseName,
            string googleApiKey = null, string googleCseId = null, string serpApiKey = null,
            string ollamaBaseUrl = "http://localhost:11434", string defaultModel = "mistral")
        {
            _mongoService = new MongoDbService(connectionString, databaseName);
            _scrapingService = new WebScrapingService(googleApiKey, googleCseId, serpApiKey);
            _ollamaService = new OllamaService(ollamaBaseUrl);
        }

        #region Сбор и поиск статей (существующие методы)
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
            var analysisStats = await _mongoService.GetAnalysisStatisticsAsync();

            Console.WriteLine($"\n📈 СТАТИСТИКА БАЗЫ ДАННЫХ");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"Всего статей: {totalCount}");
            Console.WriteLine($"Проанализировано: {analysisStats.TotalAnalyses} ({analysisStats.AnalysisPercentage:F1}%)");
            Console.WriteLine($"Задано вопросов: {analysisStats.TotalQuestions}");
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

            if (analysisStats.RecentAnalyses.Any())
            {
                Console.WriteLine("\nПоследние анализы:");
                foreach (var analysis in analysisStats.RecentAnalyses.Take(3))
                {
                    var article = await _mongoService.GetArticleByIdAsync(analysis.ArticleId);
                    Console.WriteLine($"  🔍 {article?.Title ?? "Неизвестная статья"} ({analysis.AnalysisDate:dd.MM.yyyy})");
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
        #endregion

        #region Работа с AI и анализ
        public async Task<bool> CheckOllamaConnectionAsync()
        {
            var isAvailable = await _ollamaService.IsAvailableAsync();
            Console.WriteLine(isAvailable ? "✅ Ollama доступна" : "❌ Ollama недоступна");
            return isAvailable;
        }

        public async Task AnalyzeAllArticlesAsync(string topic = null)
        {
            var articles = await _mongoService.SearchArticlesAsync("", topic, 50);
            Console.WriteLine($"📊 Найдено {articles.Count} статей для анализа");

            var analyzed = 0;
            var skipped = 0;

            foreach (var article in articles)
            {
                // Проверяем, есть ли уже анализ
                var existingAnalysis = await _mongoService.GetAnalysisAsync(article.Id);
                if (existingAnalysis != null)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    Console.WriteLine($"🔍 Анализируем ({analyzed + 1}/{articles.Count - skipped}): {article.Title}");
                    var analysis = await _ollamaService.AnalyzeArticleAsync(article);
                    await _mongoService.SaveAnalysisAsync(analysis);
                    analyzed++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Ошибка анализа '{article.Title}': {ex.Message}");
                }
            }

            Console.WriteLine($"\n📊 РЕЗУЛЬТАТ: проанализировано {analyzed}, пропущено {skipped}");
        }

        public async Task AskQuestionAboutTopicAsync(string topic, string question)
        {
            var articles = await _mongoService.SearchArticlesAsync("", topic, 5);
            if (!articles.Any())
            {
                Console.WriteLine($"❌ Статьи по теме '{topic}' не найдены");
                return;
            }

            try
            {
                Console.WriteLine($"🤔 Обрабатываю вопрос по теме '{topic}'...");
                var answer = await _ollamaService.AnswerQuestionAboutMultipleArticlesAsync(articles, question);
                Console.WriteLine($"\n💬 ОТВЕТ:\n{answer}");

                // Сохраняем Q&A
                var qa = new QuestionAnswer
                {
                    ArticleId = articles.First().Id,
                    Question = $"[{topic}] {question}",
                    Answer = answer,
                    AskedDate = DateTime.UtcNow,
                    AIModel = "mistral",
                    Confidence = 0.7
                };
                await _mongoService.SaveQuestionAnswerAsync(qa);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }
        public async Task AskAboutArticleByNumberAsync(int articleNumber, string question, List<ArticleDocument> lastSearchResults, string model = null)
        {
            if (lastSearchResults == null || articleNumber < 1 || articleNumber > lastSearchResults.Count)
            {
                Console.WriteLine("❌ Неверный номер статьи. Сначала выполните поиск.");
                return;
            }

            var article = lastSearchResults[articleNumber - 1];

            var prompt = $@"На основе статьи ответь на вопрос:

Статья: {article.Title}
Содержание: {article.Content.Substring(0, Math.Min(article.Content.Length, 3000))}

Вопрос: {question}

Дай развернутый ответ на основе содержания статьи.";

            Console.WriteLine($"🤔 Ищу ответ в статье: {article.Title}");
            var answer = await _ollamaService.AskAsync(prompt, model);
            Console.WriteLine($"\n💬 ОТВЕТ:\n{answer}");

            // Сохраняем вопрос-ответ
            var qa = new QuestionAnswer
            {
                ArticleId = article.Id,
                Question = question,
                Answer = answer,
                AskedDate = DateTime.UtcNow,
                AIModel = model ?? "mistral",
                Confidence = 0.7
            };
            await _mongoService.SaveQuestionAnswerAsync(qa);
        }

        public async Task SummarizeArticleByNumberAsync(int articleNumber, List<ArticleDocument> lastSearchResults, string model = null)
        {
            if (lastSearchResults == null || articleNumber < 1 || articleNumber > lastSearchResults.Count)
            {
                Console.WriteLine("❌ Неверный номер статьи. Сначала выполните поиск.");
                return;
            }

            var article = lastSearchResults[articleNumber - 1];

            var prompt = $@"Кратко изложи основные моменты статьи:

Заголовок: {article.Title}
Содержание: {article.Content.Substring(0, Math.Min(article.Content.Length, 4000))}

Дай краткое изложение в 3-4 предложениях и выдели ключевые моменты.";

            Console.WriteLine($"📝 Анализирую статью: {article.Title}");
            var summary = await _ollamaService.AskAsync(prompt, model);
            Console.WriteLine($"\n📋 КРАТКОЕ ИЗЛОЖЕНИЕ:\n{summary}");
        }

        public async Task CompareArticlesAsync(List<int> articleNumbers, List<ArticleDocument> lastSearchResults, string model = null)
        {
            if (lastSearchResults == null || articleNumbers.Any(n => n < 1 || n > lastSearchResults.Count))
            {
                Console.WriteLine("❌ Неверные номера статей.");
                return;
            }

            var articles = articleNumbers.Select(n => lastSearchResults[n - 1]).ToList();
            var articlesText = string.Join("\n\n", articles.Select((a, i) =>
                $"СТАТЬЯ {i + 1}: {a.Title}\n{a.Content.Substring(0, Math.Min(a.Content.Length, 2000))}"
            ));

            var prompt = $@"Сравни статьи и найди общие темы, различия и выводы:

{articlesText}

Структура ответа:
1. Общие темы
2. Основные различия  
3. Выводы";

            Console.WriteLine($"🔍 Сравниваю {articles.Count} статей");
            var comparison = await _ollamaService.AskAsync(prompt, model);
            Console.WriteLine($"\n📊 СРАВНЕНИЕ:\n{comparison}");
        }

        public async Task GenerateTopicSummaryAsync(string topic)
        {
            var articles = await _mongoService.SearchArticlesAsync("", topic, 10);
            if (!articles.Any())
            {
                Console.WriteLine($"❌ Статьи по теме '{topic}' не найдены");
                return;
            }

            try
            {
                Console.WriteLine($"📋 Создаю обзор по теме '{topic}'...");
                var summary = await _ollamaService.GenerateTopicSummaryAsync(articles, topic);
                Console.WriteLine($"\n📋 ОБЗОР ПО ТЕМЕ '{topic.ToUpper()}':");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine(summary);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }
        #endregion

        #region Отображение результатов

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

            Console.WriteLine($"\n💡 Для работы со статьями используйте номера [1-{results.Count}]");
            Console.WriteLine("   Например: 'спроси 2 что такое машинное обучение'");
        }
        #endregion

        #region Dispose
        public void Dispose()
        {
            _ollamaService?.Dispose();
        }
        #endregion
    }
}