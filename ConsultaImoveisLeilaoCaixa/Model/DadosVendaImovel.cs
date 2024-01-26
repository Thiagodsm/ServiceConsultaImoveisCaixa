using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Model
{
    public class DadosVendaImovel
    {
        public string edital { get; set; }
        public string numeroItem { get; set; }
        public string leiloeiro { get; set; }
        public string dataLicitacao { get; set; }
        public string dataPrimeiroLeilao { get; set; }
        public string dataSegundoLeilao { get; set; }
        public string endereco { get; set; }
        public string descricao { get; set; }
        public List<string> formasDePagamento { get; set; }
    }
}
