using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace InfoSystem.Models
{
    public class ArticleDocument
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("title")]
        public string Title { get; set; }

        [BsonElement("content")]
        public string Content { get; set; }

        [BsonElement("url")]
        public string Url { get; set; }

        [BsonElement("metadata")]
        public ArticleMetadata Metadata { get; set; }
    }
}
