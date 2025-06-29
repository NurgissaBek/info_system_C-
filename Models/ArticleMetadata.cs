﻿using System;
using System.Collections.Generic;

using MongoDB.Bson.Serialization.Attributes;

namespace InfoSystem.Models
{
    public class ArticleMetadata
    {
        [BsonElement("topic")]
        public string Topic { get; set; }

        [BsonElement("source")]
        public string Source { get; set; }

        [BsonElement("dateAdded")]
        public DateTime DateAdded { get; set; }

        [BsonElement("wordCount")]
        public int WordCount { get; set; }

        [BsonElement("keywords")]
        public List<string> Keywords { get; set; }

        [BsonElement("summary")]
        public string Summary { get; set; }

        [BsonElement("language")]
        public string Language { get; set; }
    }

    public class SearchResult
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Snippet { get; set; }
    }

    // Модели для различных API поисковиков
    public class GoogleSearchResponse
    {
        public List<GoogleSearchItem> Items { get; set; } = new List<GoogleSearchItem>();
    }

    public class GoogleSearchItem
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Snippet { get; set; }
    }
}
