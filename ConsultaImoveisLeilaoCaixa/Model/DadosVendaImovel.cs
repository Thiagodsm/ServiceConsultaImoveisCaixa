namespace ConsultaImoveisLeilaoCaixa.Model
{
    public class DadosVendaImovel
    {
        public string edital { get; set; }
        public string numeroItem { get; set; }
        public string leiloeiro { get; set; }
        public DateTime? dataLicitacao { get; set; }
        public DateTime? dataPrimeiroLeilao { get; set; }
        public DateTime? dataSegundoLeilao { get; set; }
        public string endereco { get; set; }
        public string descricao { get; set; }
        public List<string> formasDePagamento { get; set; }
        public string linkMatriculaImovel { get; set; }
        public string linkEditalImovel { get; set; }
    }
}
