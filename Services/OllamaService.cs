using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using InfoSystem.Models;

namespace InfoSystem.Services
{
    public class OllamaService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private const string DefaultModel = "mistral";

        public OllamaService(string baseUrl = "http://localhost:11434")
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(3);
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> AskAsync(string prompt, string model = null)
        {
            try
            {
                var request = new
                {
                    model = model ?? DefaultModel,
                    prompt = prompt,
                    stream = false
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/api/generate", content);

                if (!response.IsSuccessStatusCode)
                    return $"Ошибка API: {response.StatusCode}";

                var responseText = await response.Content.ReadAsStringAsync();
                var responseJson = JsonSerializer.Deserialize<JsonElement>(responseText);

                return responseJson.TryGetProperty("response", out var resp)
                    ? resp.GetString()
                    : "Не удалось получить ответ";
            }
            catch (Exception ex)
            {
                return $"Ошибка: {ex.Message}";
            }
        }

        // Анализ статьи - упрощенная версия
        public async Task<ArticleAnalysis> AnalyzeArticleAsync(ArticleDocument article)
        {
            var prompt = $@"Кратко проанализируй статью:

Заголовок: {article.Title}
Содержание: {article.Content.Substring(0, Math.Min(article.Content.Length, 3000))}

Ответь в формате:
КРАТКОЕ СОДЕРЖАНИЕ: [2-3 предложения]
КЛЮЧЕВЫЕ ТЕМЫ: [через запятую]
ОСНОВНЫЕ ВЫВОДЫ: [по пунктам через ;]";

            var response = await AskAsync(prompt);

            return new ArticleAnalysis
            {
                ArticleId = article.Id,
                Summary = ExtractSection(response, "КРАТКОЕ СОДЕРЖАНИЕ"),
                KeyTopics = ExtractListSection(response, "КЛЮЧЕВЫЕ ТЕМЫ"),
                MainConclusions = ExtractListSection(response, "ОСНОВНЫЕ ВЫВОДЫ", ';'),
                AnalysisDate = DateTime.UtcNow,
                AIModel = DefaultModel,
                Confidence = 0.8
            };
        }

        // Ответ на вопрос по нескольким статьям
        public async Task<string> AnswerQuestionAboutMultipleArticlesAsync(List<ArticleDocument> articles, string question)
        {
            var articlesText = string.Join("\n\n", articles.Take(3).Select((a, i) =>
                $"СТАТЬЯ {i + 1}: {a.Title}\n{a.Content.Substring(0, Math.Min(a.Content.Length, 1500))}"
            ));

            var prompt = $@"На основе статей ответь на вопрос:

{articlesText}

ВОПРОС: {question}

Дай развернутый ответ, ссылаясь на информацию из статей.";

            return await AskAsync(prompt);
        }

        // Создание обзора по теме
        public async Task<string> GenerateTopicSummaryAsync(List<ArticleDocument> articles, string topic)
        {
            var articlesText = string.Join("\n\n", articles.Take(5).Select(a =>
                $"• {a.Title}: {a.Content.Substring(0, Math.Min(a.Content.Length, 800))}"
            ));

            var prompt = $@"Создай структурированный обзор по теме '{topic}':

{articlesText}

Структура:
1. Введение
2. Основные аспекты
3. Ключевые выводы
4. Заключение";

            return await AskAsync(prompt);
        }

        // Вспомогательные методы
        private string ExtractSection(string text, string sectionName)
        {
            var lines = text.Split('\n');
            var sectionLine = lines.FirstOrDefault(l => l.ToUpper().Contains(sectionName));
            if (sectionLine != null)
            {
                var colonIndex = sectionLine.IndexOf(':');
                if (colonIndex >= 0 && colonIndex < sectionLine.Length - 1)
                {
                    return sectionLine.Substring(colonIndex + 1).Trim();
                }
            }
            return "Не удалось извлечь информацию";
        }

        private List<string> ExtractListSection(string text, string sectionName, char separator = ',')
        {
            var section = ExtractSection(text, sectionName);
            if (section == "Не удалось извлечь информацию")
                return new List<string>();

            return section.Split(separator, StringSplitOptions.RemoveEmptyEntries)
                         .Select(s => s.Trim())
                         .Where(s => !string.IsNullOrEmpty(s))
                         .ToList();
        }

        public void Dispose() => _httpClient?.Dispose();
    }
}