using ConsultaImoveisLeilaoCaixa.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Services.Interface
{
    public interface IViaCEPService 
    {
        Task<EnderecoViaCEP> ConsultarCep(string cep);
    }
}
