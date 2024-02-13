using ConsultaImoveisLeilaoCaixa.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Repository.Interface
{
    public interface IEnderecoViaCEPRepository
    {
        bool TestConnection(string connectionString, string databaseName);
        Task CreateAsync(EnderecoViaCEP endereco);
        Task<EnderecoViaCEP> GetByIdAsync(string id);
        Task<List<EnderecoViaCEP>> GetAllAsync();
        Task UpdateAsync(string id, EnderecoViaCEP imovel);
        Task DeleteAsync(string id);
    }
}
