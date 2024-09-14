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

        #region UpdateAsync
        public async Task UpdateAsync(string id, DadosImovel imovel)
        {
            try
            {
                await _collection.ReplaceOneAsync(imovelFilter => imovelFilter.id == id, imovel);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion UpdateAsync

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

        #region DeleteByTituloAsync
        public async Task DeleteByTituloAsync(string tituloEdital)
        {
            try
            {
                await _collection.DeleteManyAsync(imovelFilter => imovelFilter.tituloEditalImovel == tituloEdital);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion DeleteAsync

        #region DeleteByTituloEditalImovelAsync
        public async Task DeleteByTituloEditalImovelAsync(string tituloEditalImovel)
        {
            try
            {
                // Define o filtro para encontrar documentos com o título de edital especificado
                var filter = Builders<DadosImovel>.Filter.Eq(x => x.tituloEditalImovel, tituloEditalImovel);

                // Executa a exclusão dos documentos correspondentes ao filtro
                await _collection.DeleteManyAsync(filter);
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion DeleteByTituloEditalImovelAsync


        /*#######################################
         ### METODOS UTILIZADOS PELO TELEGRAM ###
        #########################################*/

        public async Task<List<DadosImovel>> GetByCidades(string uf, string cidades)
        {
            var cidadesArray = cidades.Split(';');
            var filtros = new List<FilterDefinition<DadosImovel>>();

            foreach (var cidade in cidadesArray)
            {
                filtros.Add(
                    Builders<DadosImovel>.Filter.And(
                        Builders<DadosImovel>.Filter.Regex("informacoesComplementares.uf", new BsonRegularExpression(uf, "i")),
                        Builders<DadosImovel>.Filter.Regex("informacoesComplementares.localidade", new BsonRegularExpression(cidade.Trim(), "i"))
                    )
                );
            }

            var filtroFinal = Builders<DadosImovel>.Filter.Or(filtros);
            return await _collection.Find(filtroFinal).ToListAsync();
        }


        // VERIFICAR OS METODOS DE VENDA DIRETA E LICITAÇÃO ABERTA - PORQUE O LIKE NAO TA FUNCIONANDO; 
        // VERIFICAR OS METODOS QUE RETORNAM OS TOP 5 IMOVEIS NAS CATEGORIAS SELECIONADAS
        public async Task<List<DadosImovel>> GetByVendaDireta(string uf, string cidades)
        {
            try
            {
                var listaCidades = cidades.Split(';').Select(c => c.Trim()).ToList();

                // Filtro para "Venda Direta" ou "Venda Online"
                var filter = Builders<DadosImovel>.Filter.And(
                    Builders<DadosImovel>.Filter.Regex("informacoesComplementares.uf", new BsonRegularExpression(uf, "i")),
                    Builders<DadosImovel>.Filter.In("informacoesComplementares.localidade", listaCidades),
                    Builders<DadosImovel>.Filter.Or(
                        Builders<DadosImovel>.Filter.Regex("tituloEditalImovel", new BsonRegularExpression(".*Venda Direta.*", "i")),
                        Builders<DadosImovel>.Filter.Regex("tituloEditalImovel", new BsonRegularExpression(".*Venda Online.*", "i"))
                    )
                );

                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<DadosImovel>> GetByLicitacaoAberta(string uf, string cidades)
        {
            try
            {
                var listaCidades = cidades.Split(';').Select(c => c.Trim()).ToList();

                // Filtro para "Licitação Aberta", utilizando expressões regulares com 'contains' (similar a '%text%')
                var filter = Builders<DadosImovel>.Filter.And(
                    Builders<DadosImovel>.Filter.Regex("informacoesComplementares.uf", new BsonRegularExpression(uf, "i")),
                    Builders<DadosImovel>.Filter.In("informacoesComplementares.localidade", listaCidades),
                    Builders<DadosImovel>.Filter.Regex("tituloEditalImovel", new BsonRegularExpression(".*Licitação Aberta.*", "i"))
                );

                return await _collection.Find(filter).ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<DadosImovel>> GetTop5ByDataVenda(string uf, string cidades)
        {
            try
            {
                var listaCidades = cidades.Split(';').Select(c => c.Trim()).ToList();

                // Filtro para UF e cidades, ordenado pela data de venda mais próxima
                var filter = Builders<DadosImovel>.Filter.And(
                    Builders<DadosImovel>.Filter.Regex("informacoesComplementares.uf", new BsonRegularExpression(uf, "i")),
                    Builders<DadosImovel>.Filter.In("informacoesComplementares.localidade", listaCidades)
                );

                return await _collection.Find(filter)
                    .Sort(Builders<DadosImovel>.Sort.Ascending("dataLicitacao"))
                    .Limit(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<DadosImovel>> GetTop5ByValor(string uf, string cidades)
        {
            try
            {
                var listaCidades = cidades.Split(';').Select(c => c.Trim()).ToList();

                // Filtro para UF e cidades, ordenado pelo menor valor de venda
                var filter = Builders<DadosImovel>.Filter.And(
                    Builders<DadosImovel>.Filter.Regex("informacoesComplementares.uf", new BsonRegularExpression(uf, "i")),
                    Builders<DadosImovel>.Filter.In("informacoesComplementares.localidade", listaCidades)
                );

                return await _collection.Find(filter)
                    .Sort(Builders<DadosImovel>.Sort.Ascending("valorMinimoVenda"))
                    .Limit(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<DadosImovel>> GetTop5ByDesconto(string uf, string cidades)
        {
            try
            {
                var listaCidades = cidades.Split(';').Select(c => c.Trim()).ToList();

                // Filtro para UF e cidades, ordenado pelo maior desconto
                var filter = Builders<DadosImovel>.Filter.And(
                    Builders<DadosImovel>.Filter.Regex("informacoesComplementares.uf", new BsonRegularExpression(uf, "i")),
                    Builders<DadosImovel>.Filter.In("informacoesComplementares.localidade", listaCidades),
                    Builders<DadosImovel>.Filter.Gt("desconto", 0)
                );

                return await _collection.Find(filter)
                    .Sort(Builders<DadosImovel>.Sort.Descending("desconto"))
                    .Limit(5)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
