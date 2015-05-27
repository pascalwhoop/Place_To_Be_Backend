﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace placeToBe.Model.Repositories {
    /// <summary>
    /// A MongoDB repository. Maps to a collection with the same name
    /// as type TEntity.
    /// </summary>
    /// <typeparam name="TEntity">Entity type for this repository</typeparam>
    public class MongoDbRepository<TEntity> : 
        IRepository<TEntity> where 
            TEntity : EntityBase
    {
        private IMongoDatabase _database;
        private IMongoCollection<TEntity> _collection;

        public MongoDbRepository()
        {
            GetDatabase();
            GetCollection();
        }

        public async void InsertAsync(TEntity entity)
        {
            entity.Id = Guid.NewGuid();
            await _collection.InsertOneAsync(entity);
            
            
        }

        public async void UpdateAsync(TEntity entity)
        {
                await _collection.InsertOneAsync(entity);
        }

        public async void DeleteAsync(TEntity entity) {
            var filter = Builders<TEntity>.Filter.Eq("_id", entity.Id);
            await _collection.DeleteOneAsync(filter);
            
        }

        public async Task<IList<TEntity>> 
            SearchForAsync(string filterText) {
            var filter = Builders<TEntity>.Filter.Text(filterText);
            return await _collection.Find(filter).ToListAsync();

        }

        public async Task<IList<TEntity>> GetAllAsync() {
            return await _collection.Find(new BsonDocument()).ToListAsync();
        }

        public async Task<TEntity> GetByIdAsync(Guid id)
        {
            var filter = Builders<TEntity>.Filter.Eq("_id", id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        #region Private Helper Methods
        private void GetDatabase()
        {
            var client = new MongoClient(GetConnectionString());
            _database = client.GetDatabase(GetDatabaseName());
        }

        private string GetConnectionString()
        {
            return ConfigurationManager
                .AppSettings
                .Get("MongoDBConnectionString")
                .Replace("{DB_NAME}", GetDatabaseName());
        }

        private string GetDatabaseName()
        {
            return ConfigurationManager
                .AppSettings
                .Get("MongoDBDatabaseName");
        }

        private void GetCollection()
        {
            _collection = _database
                .GetCollection<TEntity>(typeof(TEntity).Name);
        }
        #endregion
    }
}