using ConsultaImoveisLeilaoCaixa.Model;
using ConsultaImoveisLeilaoCaixa.Repository.Interface;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Repository
{
    public class TituloEditalRepository : ITituloEditalRepository
    {
        #region ctor
        private readonly IMongoCollection<TituloEditalLeilao> _collection;

        public TituloEditalRepository(string connectionString, string databaseName, string collectionName)
        {
            try
            {
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                _collection = database.GetCollection<TituloEditalLeilao>(collectionName);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion ctor

        #region CreateAsync
        public async Task CreateAsync(TituloEditalLeilao edital)
        {
            try
            {
                await _collection.InsertOneAsync(edital);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion CreateAsync

        #region DeleteAsync
        public async Task DeleteAsync(string titulo)
        {
            try
            {
                await _collection.DeleteOneAsync(editalFilter => editalFilter.titulo == titulo);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion DeleteAsync

        #region GetAllAsync
        public async Task<List<TituloEditalLeilao>> GetAllAsync()
        {
            try
            {
                return await _collection.Find(new BsonDocument()).ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion GetAllAsync

        #region GetByIdAsync
        public async Task<TituloEditalLeilao> GetByIdAsync(string titulo)
        {
            try
            {
                TituloEditalLeilao edital = await _collection.Find(edital => edital.titulo == titulo).FirstOrDefaultAsync();
                return edital;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion GetByIdAsync

        #region TestConnection
        public async Task<bool> TestConnection(string connectionString, string databaseName)
        {
            try
            {
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                var collections = database.ListCollections().ToList();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion TestConnection

        #region UpdateAsync
        public async Task UpdateAsync(string titulo, TituloEditalLeilao edital)
        {
            try
            {
                await _collection.ReplaceOneAsync(editalFilter => editalFilter.titulo == titulo, edital);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion UpdateAsync
    }
}
