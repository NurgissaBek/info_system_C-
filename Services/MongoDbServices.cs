using System;
using System.Collections.Generic;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Threading.Tasks;

using InfoSystem.Models;

namespace InfoSystem.Services
{
    public class MongoDbService
    {
        private readonly IMongoCollection<ArticleDocument> _collection;

        public MongoDbService(string connectionString, string databaseName)
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            _collection = database.GetCollection<ArticleDocument>("articles");

            CreateTextIndex();
        }

        private void CreateTextIndex()
        {
            try
            {
                var indexKeys = Builders<ArticleDocument>.IndexKeys
                    .Text(x => x.Title)
                    .Text(x => x.Content)
                    .Text(x => x.Metadata.Keywords);

                _collection.Indexes.CreateOne(new CreateIndexModel<ArticleDocument>(indexKeys));
                Console.WriteLine("✅ Текстовый индекс создан");
            }
            catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict")
            {
                Console.WriteLine("✅ Текстовый индекс уже существует");
            }
        }

        public async Task SaveArticleAsync(ArticleDocument article)
        {
            var existing = await _collection.Find(x => x.Url == article.Url).FirstOrDefaultAsync();
            if (existing == null)
            {
                await _collection.InsertOneAsync(article);
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

            return await _collection.Find(combinedFilter)
                .SortByDescending(x => x.Metadata.DateAdded)
                .Limit(limit)
                .ToListAsync();
        }

        public async Task<List<string>> GetAvailableTopicsAsync()
        {
            return await _collection.Distinct<string>("metadata.topic", FilterDefinition<ArticleDocument>.Empty)
                .ToListAsync();
        }

        public async Task<long> GetArticlesCountAsync(string topic = null)
        {
            var filter = string.IsNullOrEmpty(topic)
                ? FilterDefinition<ArticleDocument>.Empty
                : Builders<ArticleDocument>.Filter.Eq(x => x.Metadata.Topic, topic);

            return await _collection.CountDocumentsAsync(filter);
        }

        public async Task ClearDatabaseAsync()
        {
            await _collection.DeleteManyAsync(FilterDefinition<ArticleDocument>.Empty);
            Console.WriteLine("🗑️ База данных очищена");
        }
    }
}
