﻿using MongoDB.Driver;

namespace placeToBe.Model.Repositories
{
    public class PageRepository: MongoDbRepository<Page>
    {
        //a constructor that makes sure we have a facebook id index over our page list. 
        public PageRepository() {
            //unique index on fb pages id
            CreateIndexOptions options = new CreateIndexOptions {Unique = true};
            _collection.Indexes.CreateOneAsync(Builders<Page>.IndexKeys.Text(_ => _.fbId), options);
            
        }

    }
}