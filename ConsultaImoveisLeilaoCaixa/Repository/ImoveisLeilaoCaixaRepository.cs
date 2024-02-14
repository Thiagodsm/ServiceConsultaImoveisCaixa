using ConsultaImoveisLeilaoCaixa.Model;
using ConsultaImoveisLeilaoCaixa.Repository.Interface;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace ConsultaImoveisLeilaoCaixa.Repository
{
    public class ImoveisLeilaoCaixaRepository : IImoveisLeilaoCaixaRepository
    {
        #region ctor
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
        #endregion ctor

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

        #region CreateAsync
        public async Task CreateAsync(DadosImovel imoveis)
        {
            try
            {
                await _collection.InsertOneAsync(imoveis);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion CreateAsync

        #region GetByIdAsync
        public async Task<DadosImovel> GetByIdAsync(string id)
        {
            try
            {
                DadosImovel imovel = await _collection.Find(imovel => imovel.id == id).FirstOrDefaultAsync();
                return imovel;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion GetByIdAsync

        #region GetAllAsync
        public async Task<List<DadosImovel>> GetAllAsync()
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

        #region DeleteAsync
        public async Task DeleteAsync(string id)
        {
            try
            {
                await _collection.DeleteOneAsync(imovelFilter => imovelFilter.id == id);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion DeleteAsync

        #region UpdateAsync
        public async Task UpdateAsync(string id, DadosImovel imovel)
        {
            try
            {
                //var objectId = new ObjectId(id);
                //var filter = Builders<DadosImovel>.Filter.Eq("id", objectId);
                //await _collection.ReplaceOneAsync(filter, imoveis);

                //var filter = Builders<DadosImovel>.Filter.Eq(x => x.id, id);
                //await _collection.ReplaceOneAsync(filter, imoveis);

                //var filter = Builders<DadosImovel>.Filter.Eq(imovel => imovel.id, id);
                //var update = Builders<DadosImovel>.Update
                //    .Set(imovel => imovel.dataProcessamento, imovel.dataProcessamento)
                //    .Set(imovel => imovel.visivelCaixaImoveis, imovel.visivelCaixaImoveis)
                //    .Set(imovel => imovel.nomeLoteamento, imovel.nomeLoteamento)
                //    .Set(imovel => imovel.dadosVendaImovel, imovel.dadosVendaImovel)
                //    .Set(imovel => imovel.valorAvaliacao, imovel.valorAvaliacao)
                //    .Set(imovel => imovel.valorMinimoVenda, imovel.valorMinimoVenda)
                //    .Set(imovel => imovel.desconto, imovel.desconto)
                //    .Set(imovel => imovel.valorMinimoPrimeiraVenda, imovel.valorMinimoPrimeiraVenda)
                //    .Set(imovel => imovel.valorMinimoSegundaVenda, imovel.valorMinimoSegundaVenda)
                //    .Set(imovel => imovel.tipoImovel, imovel.tipoImovel)
                //    .Set(imovel => imovel.quartos, imovel.quartos)
                //    .Set(imovel => imovel.garagem, imovel.garagem)
                //    .Set(imovel => imovel.numeroImovel, imovel.numeroImovel)
                //    .Set(imovel => imovel.matricula, imovel.matricula)
                //    .Set(imovel => imovel.comarca, imovel.comarca)
                //    .Set(imovel => imovel.oficio, imovel.oficio)
                //    .Set(imovel => imovel.inscricaoImobiliaria, imovel.inscricaoImobiliaria)
                //    .Set(imovel => imovel.averbacaoLeilaoNegativos, imovel.averbacaoLeilaoNegativos)
                //    .Set(imovel => imovel.areaTotal, imovel.areaTotal)
                //    .Set(imovel => imovel.areaPrivativa, imovel.areaPrivativa)
                //    .Set(imovel => imovel.areaTerreno, imovel.areaTerreno)
                //    .Set(imovel => imovel.situacao, imovel.situacao);

                //await _collection.UpdateOneAsync(filter, update);

                await _collection.ReplaceOneAsync(imovelFilter => imovelFilter.id == id, imovel);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion UpdateAsync
    }
}
