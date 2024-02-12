using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace ConsultaImoveisLeilaoCaixa.Model
{
    public class DadosImovel
    {
        [BsonId]
        public string id { get; set; }
        public DateTime dataProcessamento { get; set; }
        public bool visivelCaixaImoveis { get; set; }
        public string nomeLoteamento { get; set; }
        public DadosVendaImovel dadosVendaImovel { get; set; }
        public string valorAvaliacao { get; set; }
        public string valorMinimoVenda { get; set; }
        public string desconto { get; set; }
        public string valorMinimoPrimeiraVenda { get; set; }
        public string valorMinimoSegundaVenda { get; set; }
        public string tipoImovel { get; set; }
        public string quartos { get; set; }
        public string garagem { get; set; }
        public string numeroImovel { get; set; }
        public string matricula { get; set; }
        public string comarca { get; set; }
        public string oficio { get; set; }
        public string inscricaoImobiliaria { get; set; }
        public string averbacaoLeilaoNegativos { get; set; }
        public string areaTotal { get; set; }
        public string areaPrivativa { get; set; }
        public string areaTerreno { get; set; }
        public string situacao { get; set; }

    }
}
