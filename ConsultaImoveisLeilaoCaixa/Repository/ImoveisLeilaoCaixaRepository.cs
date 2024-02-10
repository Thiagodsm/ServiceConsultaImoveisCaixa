using ConsultaImoveisLeilaoCaixa.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ConsultaImoveisLeilaoCaixa.Repository
{
    public class ImoveisLeilaoCaixaRepository
    {
        private readonly IMongoCollection<DadosImovel> _collection;
        public ImoveisLeilaoCaixaRepository(string connectionString, string databaseName, string collectionName)
        {
            try
            {
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                _collection = database.GetCollection<DadosImovel>(collectionName);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task CreateAsync(DadosImovel imoveis)
        {
            await _collection.InsertOneAsync(imoveis);
        }

        public async Task<DadosImovel> GetByIdAsync(string id)
        {
            try
            {
                var objectId = new ObjectId(id);
                var filter = Builders<DadosImovel>.Filter.Eq("_id", objectId);
                return await _collection.Find(filter).FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<IEnumerable<DadosImovel>> GetAllAsync()
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

        public async Task UpdateAsync(string id, DadosImovel imoveis)
        {
            try
            {
                var objectId = new ObjectId(id);
                var filter = Builders<DadosImovel>.Filter.Eq("_id", objectId);
                await _collection.ReplaceOneAsync(filter, imoveis);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task DeleteAsync(string id)
        {
            try
            {
                var objectId = new ObjectId(id);
                var filter = Builders<DadosImovel>.Filter.Eq("_id", objectId);
                await _collection.DeleteOneAsync(filter);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
