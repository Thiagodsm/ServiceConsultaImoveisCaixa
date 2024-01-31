using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Model
{
    public class ImoveisLeilaoCaixa
    {
        public DateTime dataProcessamento { get; set; }
        public int totalImoveis { get; set; }
        public List<DadosImovel> imoveis { get; set; }
    }
}
