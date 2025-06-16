using System;

using InfoSystem.Services;

namespace InfoSystem
{
    class Program
    {
        // Настройки подключения и API ключей
        private const string MongoConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "InfoSystemDb";

        private const string GoogleApiKey = null; // "YOUR_GOOGLE_API_KEY";
        private const string GoogleCseId = null;  // "YOUR_GOOGLE_CSE_ID";

        private const string serpApiKey = "8878d112f3aec3f402ffc464941ed9f1fa6d931f41a2c26409f6921d4c71d35d";
        private static string currentSearchEngine = "auto";

        static async Task Main(string[] args)
        {
            InformationSystemService infoService;

            // Проверка подключения
            try
            {
                infoService = new InformationSystemService(MongoConnectionString, DatabaseName, GoogleApiKey, GoogleCseId, serpApiKey);
                Console.WriteLine("✅ Подключение к базе данных успешно.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка подключения к базе данных: {ex.Message}");
                return;
            }

            bool exit = false;

            while (!exit)
            {
                Console.WriteLine("\n=== МЕНЮ ===");
                Console.WriteLine("1. Собрать статьи по теме");
                Console.WriteLine("2. Поиск по базе");
                Console.WriteLine("3. Показать статистику");
                Console.WriteLine("4. Очистить базу данных");
                Console.WriteLine("5. Выбрать тип поиска (сейчас: " + currentSearchEngine + ")");
                Console.WriteLine("0. Выход");
                Console.Write("Выберите действие: ");

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
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("⚠️ Неверный ввод, попробуйте снова.");
                        break;
                }
            }

            Console.WriteLine("👋 До свидания!");
        }

        private static async Task CollectArticlesAsync(InformationSystemService service)
        {
            Console.Write("🔎 Введите тему для сбора статей: ");
            var topic = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(topic))
            {
                Console.WriteLine("⚠️ Тема не может быть пустой.");
                return;
            }

            int maxArticles = 5;
            while (true)
            {
                Console.Write("🔢 Введите максимальное количество статей (по умолчанию 5): ");
                var input = Console.ReadLine();

                if (string.IsNullOrEmpty(input))
                    break;

                if (int.TryParse(input, out int parsed) && parsed > 0)
                {
                    maxArticles = parsed;
                    break;
                }
                Console.WriteLine("⚠️ Некорректное значение. Попробуйте снова.");
            }

            await service.CollectArticlesAsync(topic, maxArticles);
        }

        private static void ChooseSearchEngine()
        {
            Console.WriteLine("\n=== ВЫБОР ПОИСКОВОГО ДВИЖКА ===");
            Console.WriteLine("1. Google Custom Search (нужны API ключи)");
            Console.WriteLine("2. SerpAPI (использует serp, нужен ключ)");
            Console.WriteLine("3. DuckDuckGo (без ключей)");
            Console.WriteLine("4. Автоматический выбор (по наличию ключей)");
            Console.Write("Введите номер: ");

            var input = Console.ReadLine();
            switch (input)
            {
                case "1":
                    currentSearchEngine = "google";
                    break;
                case "2":
                    currentSearchEngine = "serp";
                    break;
                case "3":
                    currentSearchEngine = "duckduckgo";
                    break;
                case "4":
                    currentSearchEngine = "auto";
                    break;
                default:
                    Console.WriteLine("⚠️ Неверный выбор. Оставлен текущий тип.");
                    return;
            }

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

            Console.Write("🎯 Введите тему для фильтрации (необязательно): ");
            var topic = Console.ReadLine()?.Trim();

            int limit = 10;

            var results = await service.SearchInternalAsync(query, string.IsNullOrEmpty(topic) ? null : topic, limit);

            service.DisplaySearchResults(results);
        }

        private static async Task ConfirmAndClearDatabaseAsync(InformationSystemService service)
        {
            Console.Write("⚠️ Вы уверены, что хотите удалить все данные? (yes/no): ");
            var confirm = Console.ReadLine()?.Trim().ToLower();

            if (confirm == "yes" || confirm == "y")
            {
                await service.ClearDatabaseAsync();
                Console.WriteLine("🗑️ База данных очищена.");
            }
            else
            {
                Console.WriteLine("❌ Отмена очистки базы данных.");
            }
        }
    }
}
