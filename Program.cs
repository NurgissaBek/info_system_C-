using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using InfoSystem.Services;
using InfoSystem.Models;

namespace InfoSystem
{
    class Program
    {
        // Настройки подключения
        private const string MongoConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "InfoSystemDb";
        private const string GoogleApiKey = null;
        private const string GoogleCseId = null;
        private const string serpApiKey = null;

        private static string currentSearchEngine = "auto";
        private static List<ArticleDocument> lastSearchResults = new List<ArticleDocument>();

        static async Task Main(string[] args)
        {
            InformationSystemService infoService;

            try
            {
                infoService = new InformationSystemService(
                    MongoConnectionString,
                    DatabaseName,
                    GoogleApiKey,
                    GoogleCseId,
                    serpApiKey);
                Console.WriteLine("✅ Подключение к базе данных успешно.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка подключения к базе данных: {ex.Message}");
                return;
            }

            Console.WriteLine("🔍 Проверяем подключение к Ollama...");
            var ollamaAvailable = await infoService.CheckOllamaConnectionAsync();

            bool exit = false;
            bool usingOllama = false;

            while (!exit)
            {
                if (!usingOllama)
                {
                    ShowMainMenu(currentSearchEngine, ollamaAvailable);
                    var input = Console.ReadLine();

                    switch (input)
                    {
                        case "1":
                            await CollectArticlesAsync(infoService);
                            break;
                        case "2":
                            await SearchArticlesAsync(infoService);
                            break;
                        case "3":
                            await infoService.ShowStatisticsAsync();
                            break;
                        case "4":
                            await ConfirmAndClearDatabaseAsync(infoService);
                            break;
                        case "5":
                            ChooseSearchEngine();
                            break;
                        case "6":
                            if (ollamaAvailable)
                            {
                                usingOllama = true;
                                ShowAIHelp();
                            }
                            else
                            {
                                Console.WriteLine("❌ Ollama недоступна. Проверяем подключение...");
                                ollamaAvailable = await infoService.CheckOllamaConnectionAsync();
                            }
                            break;
                        case "0":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("⚠️ Неверный ввод, попробуйте снова.");
                            break;
                    }
                }
                else
                {
                    ShowAIMenu();
                    var userInput = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(userInput))
                    {
                        Console.WriteLine("⚠️ Пожалуйста, введите запрос.");
                        continue;
                    }

                    if (userInput.ToLower() == "выход" || userInput.ToLower() == "exit")
                    {
                        usingOllama = false;
                        Console.WriteLine("👋 Возвращаемся в главное меню...");
                        continue;
                    }

                    await ProcessUserQuery(infoService, userInput);
                }
            }

            Console.WriteLine("👋 До свидания!");
            infoService.Dispose();
        }

        private static void ShowMainMenu(string searchEngine, bool ollamaAvailable)
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("🤖 ИНФОРМАЦИОННАЯ СИСТЕМА");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine("1. Собрать статьи по теме");
            Console.WriteLine("2. Поиск по базе");
            Console.WriteLine("3. Показать статистику");
            Console.WriteLine("4. Очистить базу данных");
            Console.WriteLine("5. Выбрать тип поиска (сейчас: " + searchEngine + ")");

            if (ollamaAvailable)
            {
                Console.WriteLine("6. 🤖 Запустить AI помощника (Ollama)");
            }
            else
            {
                Console.WriteLine("6. ❌ AI помощник недоступен (Ollama не подключена)");
            }

            Console.WriteLine("0. Выход");
            Console.WriteLine(new string('=', 50));
            Console.Write("Выберите действие: ");
        }

        private static void ShowAIHelp()
        {
            Console.WriteLine("\n🤖 Добро пожаловать в AI помощника!");
            Console.WriteLine("💡 Примеры команд:");
            Console.WriteLine("   • найди статьи про машинное обучение");
            Console.WriteLine("   • спроси 2 что такое нейронные сети");
            Console.WriteLine("   • анализ 3");
            Console.WriteLine("   • сравни 1 2 3");
            Console.WriteLine("   • обзор искусственный интеллект");
            Console.WriteLine("Введите 'выход' для возврата в главное меню.");
        }

        private static void ShowAIMenu()
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("🤖 AI ПОМОЩНИК");
            Console.WriteLine(new string('=', 50));
            if (lastSearchResults.Any())
            {
                Console.WriteLine($"📚 Доступно {lastSearchResults.Count} статей из последнего поиска");
                Console.WriteLine("💡 Команды для работы со статьями:");
                Console.WriteLine("   • спроси [номер] [вопрос] - задать вопрос по статье");
                Console.WriteLine("   • анализ [номер] - краткое изложение статьи");
                Console.WriteLine("   • сравни [номера] - сравнить статьи (например: сравни 1 2 3)");
            }
            Console.WriteLine("💡 Общие команды:");
            Console.WriteLine("   • найди [запрос] - поиск статей");
            Console.WriteLine("   • обзор [тема] - создать обзор по теме");

            Console.Write("👤 Вы: ");
        }

        private static async Task ProcessUserQuery(InformationSystemService service, string query)
        {
            try
            {
                var lowerQuery = query.ToLower();

                // НОВАЯ ЛОГИКА: Работа с номерами статей
                if (TryParseArticleCommand(query, out string command, out List<int> numbers, out string remainingText))
                {
                    if (command == "спроси" && numbers.Count == 1 && !string.IsNullOrEmpty(remainingText))
                    {
                        await service.AskAboutArticleByNumberAsync(numbers[0], remainingText, lastSearchResults);
                        return;
                    }
                    else if (command == "анализ" && numbers.Count == 1)
                    {
                        await service.SummarizeArticleByNumberAsync(numbers[0], lastSearchResults);
                        return;
                    }
                    else if (command == "сравни" && numbers.Count > 1)
                    {
                        await service.CompareArticlesAsync(numbers, lastSearchResults);
                        return;
                    }
                }

                // Создание обзора по теме
                if (lowerQuery.StartsWith("обзор "))
                {
                    var topic = query.Substring(6).Trim();
                    await service.GenerateTopicSummaryAsync(topic);
                    return;
                }

                // Поиск статей
                if (lowerQuery.StartsWith("найди "))
                {
                    var searchTerm = query.Substring(6).Trim();
                    var results = await service.SearchInternalAsync(searchTerm);
                    service.DisplaySearchResults(results);
                    lastSearchResults = results; // Сохраняем результаты
                    return;
                }

                // Общий вопрос
                if (lowerQuery.Contains("что такое") || lowerQuery.Contains("расскажи") ||
                    lowerQuery.Contains("объясни") || lowerQuery.Contains("?"))
                {
                    await AnswerGeneralQuestion(service, query);
                    return;
                }

                // Если не удалось определить команду, делаем поиск
                Console.WriteLine("🔍 Ищу статьи по вашему запросу...");
                var searchResults = await service.SearchInternalAsync(query);
                service.DisplaySearchResults(searchResults);
                lastSearchResults = searchResults;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка: {ex.Message}");
            }
        }

        private static bool TryParseArticleCommand(string input, out string command, out List<int> numbers, out string remainingText)
        {
            command = "";
            numbers = new List<int>();
            remainingText = "";

            var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            var firstWord = parts[0].ToLower();
            if (firstWord != "спроси" && firstWord != "анализ" && firstWord != "сравни")
                return false;

            command = firstWord;

            // Извлекаем числа
            var numbersParsed = new List<int>();
            var textParts = new List<string>();
            bool parsingNumbers = true;

            for (int i = 1; i < parts.Length; i++)
            {
                if (parsingNumbers && int.TryParse(parts[i], out int number))
                {
                    numbersParsed.Add(number);
                }
                else
                {
                    parsingNumbers = false;
                    textParts.Add(parts[i]);
                }
            }

            if (numbersParsed.Count == 0) return false;

            numbers = numbersParsed;
            remainingText = string.Join(" ", textParts);
            return true;
        }

        private static async Task AnswerGeneralQuestion(InformationSystemService service, string question)
        {
            var keywords = ExtractKeywords(question);
            var searchQuery = string.Join(" ", keywords);

            Console.WriteLine($"🔍 Ищу информацию по теме: {searchQuery}");

            var articles = await service.SearchInternalAsync(searchQuery, limit: 5);

            if (articles.Any())
            {
                Console.WriteLine($"📚 Найдено {articles.Count} релевантных статей. Формирую ответ...");
                var topics = articles.Select(a => a.Metadata.Topic).Distinct().ToList();
                var topic = topics.FirstOrDefault() ?? searchQuery;
                await service.AskQuestionAboutTopicAsync(topic, question);
            }
            else
            {
                Console.WriteLine("😞 В базе нет статей для ответа на ваш вопрос.");
                Console.WriteLine("💡 Попробуйте сначала собрать статьи по интересующей теме.");
            }
        }

        private static List<string> ExtractKeywords(string question)
        {
            var stopWords = new[] { "что", "такое", "как", "почему", "где", "когда", "кто", "какой",
                                   "расскажи", "объясни", "о", "про", "в", "на", "с", "и", "а", "но", "или" };

            return question.ToLower()
                          .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(w => !stopWords.Contains(w) && w.Length > 2)
                          .Take(5)
                          .ToList();
        }

        #region Существующие методы (упрощенные)
        private static async Task CollectArticlesAsync(InformationSystemService service)
        {
            Console.Write("🔎 Введите тему для сбора статей: ");
            var topic = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(topic))
            {
                Console.WriteLine("⚠️ Тема не может быть пустой.");
                return;
            }

            Console.Write("🔢 Количество статей (по умолчанию 5): ");
            var input = Console.ReadLine();
            var maxArticles = string.IsNullOrEmpty(input) ? 5 : (int.TryParse(input, out int parsed) ? parsed : 5);

            await service.CollectArticlesAsync(topic, maxArticles, currentSearchEngine);
        }

        private static void ChooseSearchEngine()
        {
            Console.WriteLine("\n=== ВЫБОР ПОИСКОВОГО ДВИЖКА ===");
            Console.WriteLine("1. Google Custom Search");
            Console.WriteLine("2. SerpAPI");
            Console.WriteLine("3. DuckDuckGo");
            Console.WriteLine("4. Автоматический выбор");
            Console.Write("Введите номер: ");

            var input = Console.ReadLine();
            currentSearchEngine = input switch
            {
                "1" => "google",
                "2" => "serp",
                "3" => "duckduckgo",
                "4" => "auto",
                _ => currentSearchEngine
            };

            Console.WriteLine($"✅ Установлен тип поиска: {currentSearchEngine}");
        }

        private static async Task SearchArticlesAsync(InformationSystemService service)
        {
            Console.Write("🔍 Введите поисковый запрос: ");
            var query = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("⚠️ Поисковый запрос не может быть пустым.");
                return;
            }

            Console.Write("🎯 Фильтр по теме (необязательно): ");
            var topic = Console.ReadLine()?.Trim();

            var results = await service.SearchInternalAsync(query, string.IsNullOrEmpty(topic) ? null : topic);
            service.DisplaySearchResults(results);
            lastSearchResults = results; // Сохраняем результаты для работы с AI
        }

        private static async Task ConfirmAndClearDatabaseAsync(InformationSystemService service)
        {
            Console.Write("⚠️ Удалить все данные? (yes/no): ");
            var confirm = Console.ReadLine()?.Trim().ToLower();

            if (confirm == "yes" || confirm == "y")
            {
                await service.ClearDatabaseAsync();
                Console.WriteLine("🗑️ База данных очищена.");
            }
            else
            {
                Console.WriteLine("❌ Отмена.");
            }
        }
        #endregion
    }
}