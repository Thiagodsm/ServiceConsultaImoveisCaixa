using ConsultaImoveisLeilaoCaixa.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Repository.Interface
{
    public interface IImoveisLeilaoCaixaRepository
    {
        Task<bool> TestConnection(string connectionString, string databaseName);
        Task CreateAsync(DadosImovel imoveis);
        Task<DadosImovel> GetByIdAsync(string id);
        Task<List<DadosImovel>> GetAllAsync();
        Task UpdateAsync(string id, DadosImovel imovel);
        Task DeleteAsync(string id);
        Task DeleteByTituloAsync(string tituloEdital);

        Task<List<DadosImovel>> GetByCidades(string uf, string cidades);
        Task<List<DadosImovel>> GetByVendaDireta(string uf, string cidades);
        Task<List<DadosImovel>> GetByLicitacaoAberta(string uf, string cidades);
        Task<List<DadosImovel>> GetTop5ByDataVenda(string uf, string cidades);       
        Task<List<DadosImovel>> GetTop5ByValor(string uf, string cidades);
        Task<List<DadosImovel>> GetTop5ByDesconto(string uf, string cidades);
    }
}
