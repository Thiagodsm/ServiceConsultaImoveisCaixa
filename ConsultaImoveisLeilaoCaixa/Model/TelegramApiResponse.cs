using Newtonsoft.Json;

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
