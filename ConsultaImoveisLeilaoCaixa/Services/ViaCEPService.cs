using ConsultaImoveisLeilaoCaixa.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Services
{
    public class ViaCEPService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<EnderecoViaCEP> ConsultarCep(string cep)
        {
            // Validação do formato do CEP
            if (!ValidarFormatoCep(cep))
            {
                throw new ArgumentException("Formato de CEP inválido.");
            }

            // Construção da URL de consulta
            string url = $"https://viacep.com.br/ws/{cep}/json/";

            // Consulta o serviço ViaCEP
            HttpResponseMessage response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                // Retorna o conteúdo da resposta
                string enderecoString = await response.Content.ReadAsStringAsync();
                EnderecoViaCEP endereco = JsonSerializer.Deserialize<EnderecoViaCEP>(enderecoString);
            }
            return new EnderecoViaCEP();
        }

        private bool ValidarFormatoCep(string cep)
        {
            // Verifica se o CEP possui exatamente 8 dígitos
            return cep.Length == 8 && int.TryParse(cep, out _);
        }
    }
}
