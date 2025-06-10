using System;
using System.Threading.Tasks;
using InfoSystem.Services;
using InfoSystem.Models;
using System.Collections.Generic;

namespace InfoSystem
{
    class Program
    {
        // Укажи здесь свои параметры подключения и ключи, если есть
        private const string MongoConnectionString = "mongodb://localhost:27017";
        private const string DatabaseName = "InfoSystemDb";

        // Если есть Google API ключи, укажи их
        private const string GoogleApiKey = null; // "YOUR_GOOGLE_API_KEY";
        private const string GoogleCseId = null;  // "YOUR_GOOGLE_CSE_ID";

        // Или Bing API ключ
        private const string BingApiKey = "8878d112f3aec3f402ffc464941ed9f1fa6d931f41a2c26409f6921d4c71d35d";   // "YOUR_BING_API_KEY";

        static async Task Main(string[] args)
        {
            var infoService = new InformationSystemService(MongoConnectionString, DatabaseName, GoogleApiKey, GoogleCseId, BingApiKey);

            bool exit = false;

            while (!exit)
            {
                Console.WriteLine("\n=== МЕНЮ ===");
                Console.WriteLine("1. Собрать статьи по теме");
                Console.WriteLine("2. Поиск по базе");
                Console.WriteLine("3. Показать статистику");
                Console.WriteLine("4. Очистить базу данных");
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
                        await infoService.ClearDatabaseAsync();
                        break;
                    case "0":
                        exit = true;
                        break;
                    default:
                        Console.WriteLine("Неверный ввод, попробуйте снова.");
                        break;
                }
            }

            Console.WriteLine("До свидания!");
        }

        private static async Task CollectArticlesAsync(InformationSystemService service)
        {
            Console.Write("Введите тему для сбора статей: ");
            var topic = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(topic))
            {
                Console.WriteLine("Тема не может быть пустой.");
                return;
            }

            Console.Write("Введите максимальное количество статей (по умолчанию 5): ");
            var maxInput = Console.ReadLine();

            int maxArticles = 5;
            if (!string.IsNullOrEmpty(maxInput) && int.TryParse(maxInput, out int parsedMax) && parsedMax > 0)
            {
                maxArticles = parsedMax;
            }

            await service.CollectArticlesAsync(topic, maxArticles);
        }

        private static async Task SearchArticlesAsync(InformationSystemService service)
        {
            Console.Write("Введите поисковый запрос: ");
            var query = Console.ReadLine()?.Trim();

            Console.Write("Введите тему для фильтрации (необязательно): ");
            var topic = Console.ReadLine()?.Trim();

            int limit = 10;

            var results = await service.SearchInternalAsync(query, string.IsNullOrEmpty(topic) ? null : topic, limit);

            service.DisplaySearchResults(results);
        }
    }
}
