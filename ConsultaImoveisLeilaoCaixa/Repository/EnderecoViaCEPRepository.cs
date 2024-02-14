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
    public class EnderecoViaCEPRepository : IEnderecoViaCEPRepository
    {
        #region ctor
        private readonly IMongoCollection<EnderecoViaCEP> _collection;
        public EnderecoViaCEPRepository(string connectionString, string databaseName, string collectionName)
        {
            try
            {
                var client = new MongoClient(connectionString);
                var database = client.GetDatabase(databaseName);
                _collection = database.GetCollection<EnderecoViaCEP>(collectionName);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion

        #region CreateAsync
        public async Task CreateAsync(EnderecoViaCEP endereco)
        {
            try
            {
                await _collection.InsertOneAsync(endereco);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion

        #region DeleteAsync
        public async Task DeleteAsync(string cep)
        {
            try
            {
                await _collection.DeleteOneAsync(enderecoFilter => enderecoFilter.cep == cep);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion DeleteAsync

        #region GetAllAsync
        public async Task<List<EnderecoViaCEP>> GetAllAsync()
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
        public async Task<EnderecoViaCEP> GetByIdAsync(string cep)
        {
            try
            {
                EnderecoViaCEP endereco = await _collection.Find(endereco => endereco.cep == cep).FirstOrDefaultAsync();
                return endereco;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion GetByIdAsync

        #region TestConnection
        public bool TestConnection(string connectionString, string databaseName)
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
        public async Task UpdateAsync(string cep, EnderecoViaCEP endereco)
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

                await _collection.ReplaceOneAsync(enderecoFilter => enderecoFilter.cep == cep, endereco);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion UpdateAsync
    }
}
