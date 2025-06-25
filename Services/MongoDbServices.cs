using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Threading.Tasks;
using System.Linq;
using InfoSystem.Models;

namespace InfoSystem.Services
{
    public class MongoDbService
    {
        private readonly IMongoCollection<ArticleDocument> _articlesCollection;
        private readonly IMongoCollection<ArticleAnalysis> _analysisCollection;
        private readonly IMongoCollection<QuestionAnswer> _qaCollection;

        public MongoDbService(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);

            _articlesCollection = database.GetCollection<ArticleDocument>("articles");
            _analysisCollection = database.GetCollection<ArticleAnalysis>("analyses");
            _qaCollection = database.GetCollection<QuestionAnswer>("questions_answers");

            CreateIndexes();
        }

        private void CreateIndexes()
        {
            try
            {
                // Текстовый индекс для статей
                var articleIndexKeys = Builders<ArticleDocument>.IndexKeys
                    .Text(x => x.Title)
                    .Text(x => x.Content)
                    .Text(x => x.Metadata.Keywords);
                _articlesCollection.Indexes.CreateOne(new CreateIndexModel<ArticleDocument>(articleIndexKeys));

                // Индекс для анализов
                var analysisIndexKeys = Builders<ArticleAnalysis>.IndexKeys
                    .Ascending(x => x.ArticleId)
                    .Text(x => x.Summary)
                    .Text(x => x.KeyTopics);
                _analysisCollection.Indexes.CreateOne(new CreateIndexModel<ArticleAnalysis>(analysisIndexKeys));

                // Индекс для вопросов-ответов
                var qaIndexKeys = Builders<QuestionAnswer>.IndexKeys
                    .Ascending(x => x.ArticleId)
                    .Text(x => x.Question)
                    .Text(x => x.Answer);
                _qaCollection.Indexes.CreateOne(new CreateIndexModel<QuestionAnswer>(qaIndexKeys));

                Console.WriteLine("✅ Индексы созданы");
            }
            catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict")
            {
                Console.WriteLine("✅ Индексы уже существуют");
            }
        }

        // Методы для статей (существующие)
        public async Task SaveArticleAsync(ArticleDocument article)
        {
            var existing = await _articlesCollection.Find(x => x.Url == article.Url).FirstOrDefaultAsync();
            if (existing == null)
            {
                await _articlesCollection.InsertOneAsync(article);
                Console.WriteLine($"💾 Сохранена: {article.Title}");
            }
            else
            {
                Console.WriteLine($"⚠️ Уже есть: {article.Title}");
            }
        }

        public async Task<List<ArticleDocument>> SearchArticlesAsync(string query, string topic = null, int limit = 20)
        {
            var filterBuilder = Builders<ArticleDocument>.Filter;
            var filters = new List<FilterDefinition<ArticleDocument>>();

            if (!string.IsNullOrEmpty(query))
            {
                filters.Add(filterBuilder.Text(query));
            }

            if (!string.IsNullOrEmpty(topic))
            {
                filters.Add(filterBuilder.Eq(x => x.Metadata.Topic, topic));
            }

            var combinedFilter = filters.Count > 0
                ? filterBuilder.And(filters)
                : filterBuilder.Empty;

            return await _articlesCollection.Find(combinedFilter)
                .SortByDescending(x => x.Metadata.DateAdded)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<ArticleDocument> GetArticleByIdAsync(ObjectId id)
        {
            return await _articlesCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<string>> GetAvailableTopicsAsync()
        {
            return await _articlesCollection.Distinct<string>("metadata.topic", FilterDefinition<ArticleDocument>.Empty)
                .ToListAsync();
        }

        public async Task<long> GetArticlesCountAsync(string topic = null)
        {
            var filter = string.IsNullOrEmpty(topic)
                ? FilterDefinition<ArticleDocument>.Empty
                : Builders<ArticleDocument>.Filter.Eq(x => x.Metadata.Topic, topic);
            return await _articlesCollection.CountDocumentsAsync(filter);
        }

        // Методы для анализов статей
        public async Task SaveAnalysisAsync(ArticleAnalysis analysis)
        {
            var existing = await _analysisCollection.Find(x => x.ArticleId == analysis.ArticleId).FirstOrDefaultAsync();
            if (existing == null)
            {
                await _analysisCollection.InsertOneAsync(analysis);
                Console.WriteLine($"💾 Сохранен анализ для статьи ID: {analysis.ArticleId}");
            }
            else
            {
                // Обновляем существующий анализ
                await _analysisCollection.ReplaceOneAsync(x => x.ArticleId == analysis.ArticleId, analysis);
                Console.WriteLine($"🔄 Обновлен анализ для статьи ID: {analysis.ArticleId}");
            }
        }

        public async Task<ArticleAnalysis> GetAnalysisAsync(ObjectId articleId)
        {
            return await _analysisCollection.Find(x => x.ArticleId == articleId).FirstOrDefaultAsync();
        }

        public async Task<List<ArticleAnalysis>> GetAnalysesByTopicAsync(string topic, int limit = 10)
        {
            // Сначала находим статьи по теме
            var articles = await SearchArticlesAsync("", topic, limit);
            var articleIds = articles.Select(a => a.Id).ToList();

            // Затем находим анализы для этих статей
            var filter = Builders<ArticleAnalysis>.Filter.In(x => x.ArticleId, articleIds);
            return await _analysisCollection.Find(filter)
                .SortByDescending(x => x.AnalysisDate)
                .ToListAsync();
        }

        public async Task<List<ArticleAnalysis>> SearchAnalysesAsync(string query, int limit = 10)
        {
            var filter = Builders<ArticleAnalysis>.Filter.Text(query);
            return await _analysisCollection.Find(filter)
                .SortByDescending(x => x.AnalysisDate)
                .Limit(limit)
                .ToListAsync();
        }

        // Методы для вопросов-ответов
        public async Task SaveQuestionAnswerAsync(QuestionAnswer qa)
        {
            await _qaCollection.InsertOneAsync(qa);
            Console.WriteLine($"💾 Сохранен Q&A для статьи ID: {qa.ArticleId}");
        }

        public async Task<List<QuestionAnswer>> GetQuestionAnswersAsync(ObjectId articleId, int limit = 10)
        {
            return await _qaCollection.Find(x => x.ArticleId == articleId)
                .SortByDescending(x => x.AskedDate)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<QuestionAnswer>> SearchQuestionAnswersAsync(string query, int limit = 10)
        {
            var filter = Builders<QuestionAnswer>.Filter.Text(query);
            return await _qaCollection.Find(filter)
                .SortByDescending(x => x.AskedDate)
                .Limit(limit)
                .ToListAsync();
        }

        // Статистика
        public async Task<AnalysisStatistics> GetAnalysisStatisticsAsync()
        {
            var totalArticles = await GetArticlesCountAsync();
            var totalAnalyses = await _analysisCollection.CountDocumentsAsync(FilterDefinition<ArticleAnalysis>.Empty);
            var totalQuestions = await _qaCollection.CountDocumentsAsync(FilterDefinition<QuestionAnswer>.Empty);

            var recentAnalyses = await _analysisCollection.Find(FilterDefinition<ArticleAnalysis>.Empty)
                .SortByDescending(x => x.AnalysisDate)
                .Limit(5)
                .ToListAsync();

            return new AnalysisStatistics
            {
                TotalArticles = totalArticles,
                TotalAnalyses = totalAnalyses,
                TotalQuestions = totalQuestions,
                AnalysisPercentage = totalArticles > 0 ? (double)totalAnalyses / totalArticles * 100 : 0,
                RecentAnalyses = recentAnalyses
            };
        }

        // Очистка базы данных
        public async Task ClearDatabaseAsync()
        {
            await _articlesCollection.DeleteManyAsync(FilterDefinition<ArticleDocument>.Empty);
            await _analysisCollection.DeleteManyAsync(FilterDefinition<ArticleAnalysis>.Empty);
            await _qaCollection.DeleteManyAsync(FilterDefinition<QuestionAnswer>.Empty);
            Console.WriteLine("🗑️ База данных очищена");
        }
    }

    public class AnalysisStatistics
    {
        public long TotalArticles { get; set; }
        public long TotalAnalyses { get; set; }
        public long TotalQuestions { get; set; }
        public double AnalysisPercentage { get; set; }
        public List<ArticleAnalysis> RecentAnalyses { get; set; } = new List<ArticleAnalysis>();
    }
}