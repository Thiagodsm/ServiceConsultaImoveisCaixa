using ConsultaImoveisLeilaoCaixa.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Repository.Interface
{
    public interface ITituloEditalRepository
    {
        Task<bool> TestConnection(string connectionString, string databaseName);
        Task CreateAsync(TituloEditalLeilao edital);
        Task<TituloEditalLeilao> GetByIdAsync(string titulo);
        Task<List<TituloEditalLeilao>> GetAllAsync();
        Task<List<TituloEditalLeilao>> GetAllProcessedAsync();
        Task UpdateAsync(string titulo, TituloEditalLeilao edital);
        Task DeleteAsync(string titulo);
    }
}
