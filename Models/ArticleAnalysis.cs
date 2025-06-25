using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace InfoSystem.Models
{
    public class ArticleAnalysis
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("articleId")]
        public ObjectId ArticleId { get; set; }

        [BsonElement("summary")]
        public string Summary { get; set; }

        [BsonElement("keyTopics")]
        public List<string> KeyTopics { get; set; } = new List<string>();

        [BsonElement("authors")]
        public List<string> Authors { get; set; } = new List<string>();

        [BsonElement("definitions")]
        public Dictionary<string, string> Definitions { get; set; } = new Dictionary<string, string>();

        [BsonElement("mainConclusions")]
        public List<string> MainConclusions { get; set; } = new List<string>();

        [BsonElement("practicalApplications")]
        public List<string> PracticalApplications { get; set; } = new List<string>();

        [BsonElement("extractedEntities")]
        public List<string> ExtractedEntities { get; set; } = new List<string>();

        [BsonElement("analysisDate")]
        public DateTime AnalysisDate { get; set; }

        [BsonElement("aiModel")]
        public string AIModel { get; set; }

        [BsonElement("confidence")]
        public double Confidence { get; set; } = 0.8;
    }

    public class QuestionAnswer
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("articleId")]
        public ObjectId ArticleId { get; set; }

        [BsonElement("question")]
        public string Question { get; set; }

        [BsonElement("answer")]
        public string Answer { get; set; }

        [BsonElement("askedDate")]
        public DateTime AskedDate { get; set; }

        [BsonElement("aiModel")]
        public string AIModel { get; set; }

        [BsonElement("confidence")]
        public double Confidence { get; set; }
    }
}