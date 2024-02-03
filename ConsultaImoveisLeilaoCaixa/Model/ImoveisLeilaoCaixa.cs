namespace ConsultaImoveisLeilaoCaixa.Model
{
    public class ImoveisLeilaoCaixa
    {
        public DateTime dataProcessamento { get; set; }
        public int totalImoveis { get; set; }
        public List<DadosImovel> imoveis { get; set; }
    }
}
