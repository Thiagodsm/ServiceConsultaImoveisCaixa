using ConsultaImoveisLeilaoCaixa.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Repository
{
    public interface IImoveisLeilaoCaixaRepository
    {
        bool TestConnection(string connectionString, string databaseName);
        Task CreateAsync(DadosImovel imoveis);
        Task<DadosImovel> GetByIdAsync(string id);
        Task<List<DadosImovel>> GetAllAsync();
        Task UpdateAsync(string id, DadosImovel imovel);
        Task DeleteAsync(string id);
    }
}
