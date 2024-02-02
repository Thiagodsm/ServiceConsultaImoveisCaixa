using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsultaImoveisLeilaoCaixa.Model
{
    public class TelegramApiResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}
