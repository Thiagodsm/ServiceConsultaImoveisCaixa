using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Model
{
    public class TituloEditalLeilao
    {
        [BsonId]
        public string titulo { get; set; }
        public DateTime data { get; set; }
        public bool processado { get; set; }
    }
}
