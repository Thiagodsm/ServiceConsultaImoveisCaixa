using ConsultaImoveisLeilaoCaixa.Model;
using ConsultaImoveisLeilaoCaixa.Util;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot;

namespace ConsultaImoveisLeilaoCaixa
{
    public class TaskConsultaImoveisCaixa : BackgroundService
    {
        private readonly ILogger<TaskConsultaImoveisCaixa> _logger;

        public TaskConsultaImoveisCaixa(ILogger<TaskConsultaImoveisCaixa> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            TelegramPollingService telegramPollingService = new TelegramPollingService();
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await telegramPollingService.IniciarPolling();

                string edgeDriverPath = @"C:\Users\thiag\Documents\WebDriver\msedgedriver.exe";
                EdgeDriver driver = new EdgeDriver(edgeDriverPath);

                try
                {
                    #region Navegacao
                    // Captura o título da nova página
                    var newPageTitle = driver.Title;
                    Console.WriteLine($"Novo Título da Página: {newPageTitle}");

                    // Navegue para a página da Caixa
                    driver.Navigate().GoToUrl("https://venda-imoveis.caixa.gov.br/sistema/busca-imovel.asp?sltTipoBusca=imoveis");

                    // Selecione o estado (SP) no dropdown
                    var estadoDropdown = new SelectElement(driver.FindElement(By.Id("cmb_estado")));
                    estadoDropdown.SelectByText("SP");

                    // Aguarde a página carregar as cidades
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                    wait.Until(d => d.FindElement(By.Id("cmb_cidade")));

                    // Aguarde um tempo adicional (de 5 segundos) antes de selecionar a cidade
                    Thread.Sleep(5000);

                    // Selecione a cidade (Guarujá) no dropdown
                    var cidadeDropdown = new SelectElement(driver.FindElement(By.Id("cmb_cidade")));
                    cidadeDropdown.SelectByText("GUARUJA");

                    // Aguarde um tempo adicional (de 5 segundos) antes de clicar em proximo
                    Thread.Sleep(5000);

                    // Clique no botão "Próximo"
                    var btnNext = driver.FindElement(By.Id("btn_next0"));
                    btnNext.Click();

                    // Aguarde um tempo adicional (de 5 segundos) antes de clicar em proximo
                    Thread.Sleep(5000);

                    // Clique no botão "Próximo"
                    var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                    btnNext1.Click();

                    // Aguarde um tempo adicional (de 20 segundos) antes de verificar a quantidade de paginas
                    Thread.Sleep(20000);

                    // Conjunto para rastrear números de imóveis já processados
                    List<string> numerosImoveisProcessados = new List<string>();

                    // Obtém o número total de páginas
                    var totalPages = int.Parse(driver.FindElement(By.Id("hdnQtdPag")).GetAttribute("value"));
                    #endregion Navegacao

                    #region Buscando Id's dos imoveis
                    for (int currentPage = 1; currentPage <= totalPages; currentPage++)
                    {
                        // Lógica para extrair detalhes de cada imóvel na página atual
                        ReadOnlyCollection<IWebElement> detalhesLinks = driver.FindElements(By.CssSelector("a[onclick*='detalhe_imovel']"));

                        foreach (IWebElement detalhesLink in detalhesLinks)
                        {
                            string onclickValue = detalhesLink.GetAttribute("onclick");
                            string numeroImovel = ExtrairNumeroImovel(onclickValue);
                            //detalhesLink.Click();

                            // Adiciona o número do imóvel a lista
                            numerosImoveisProcessados.Add(numeroImovel);
                        }

                        // Aguarde um tempo para a próxima página carregar completamente
                        Thread.Sleep(10000);

                        // Navegue para a próxima página, se houver
                        if (currentPage < totalPages)
                        {
                            // Clique no link para a próxima página
                            driver.FindElement(By.CssSelector($"a[href='javascript:carregaListaImoveis({currentPage + 1});']")).Click();

                            // Aguarde um tempo para a próxima página carregar completamente
                            Thread.Sleep(10000);
                        }
                    }
                    // Removendo Id's duplicados
                    numerosImoveisProcessados = numerosImoveisProcessados.Distinct().ToList();
                    #endregion Buscando Id's dos imoveis

                    #region Extraindo dados dos imoveis
                    List<DadosImovel> dadosImoveis = new List<DadosImovel>();

                    // Itera sobre os números de imóveis processados
                    foreach (var numeroImovel in numerosImoveisProcessados)
                    {
                        // Executa o script JavaScript para chamar o método detalhe_imovel com o ID correspondente
                        string script = $"detalhe_imovel({numeroImovel});";
                        ((IJavaScriptExecutor)driver).ExecuteScript(script);

                        // Aguarde um tempo para a próxima página carregar completamente (opcional)
                        Thread.Sleep(1000);

                        // Obtenha os dados esperados
                        // Localiza a div principal que contém as informações
                        IWebElement divPrincipal = driver.FindElement(By.CssSelector("div.content-wrapper.clearfix"));

                        // Definindo o objeto a partir da div principal
                        DadosImovel imovel = DefineObjeto(driver, divPrincipal);
                        dadosImoveis.Add(imovel);

                        // Após lidar com a página de detalhes, você pode voltar à lista de imóveis
                        driver.Navigate().Back();
                    }

                    // Aguarde um tempo para voltar a pagina anterior
                    Thread.Sleep(5000);

                    // Volte para a página de listagem de imóveis
                    driver.Navigate().Back();
                    driver.Quit();
                    #endregion Extraindo dados dos imoveis

                    //dadosImoveis = OrdenaListaImoveis(dadosImoveis);
                    bool succes = SalvarListaComoJson(dadosImoveis, "C:/imoveis/imoveis.json");

                    string caminhoArquivo = @"C:\imoveis\imoveis.json";
                    string tokenTelegram = Config.BotToken;
                    long chatIdTelegram = Config.ChatId;

                    // Passo 1: Ler o conteúdo do arquivo JSON
                    string conteudoJson = LerArquivoJson(caminhoArquivo);

                    bool retTelegram = await EnviarMensagemTelegram(tokenTelegram, chatIdTelegram, "Mensagem com os dados dos imóveis");

                    //if (conteudoJson != null)
                    //{
                    //    // Passo 2: Converter JSON para objeto
                    //    DadosImovel objetoImoveis = ConverterJsonParaObjeto<DadosImovel>(conteudoJson);

                    //    if (objetoImoveis != null)
                    //    {
                    //        // Passo 3: Enviar mensagem para o Telegram
                    //        EnviarMensagemTelegram(tokenTelegram, chatIdTelegram, "Mensagem com os dados dos imóveis");
                    //    }
                    //}
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e.Message);
                    if (!String.IsNullOrWhiteSpace(e.Message))
                    {
                        driver.Quit();
                        Thread.Sleep(10000);
                        await ExecuteAsync(stoppingToken);
                    }
                }
                finally
                {
                    // Certifique-se de fechar o navegador quando terminar
                    driver.Quit();
                }
            }
        }

        #region ExtrairNumeroImovel
        public string ExtrairNumeroImovel(string onclickValue)
        {
            int startIndex = onclickValue.IndexOf("(") + 1;
            int endIndex = onclickValue.IndexOf(")");

            if (startIndex != -1 && endIndex != -1)
            {
                return onclickValue.Substring(startIndex, endIndex - startIndex);
            }

            return null;
        }
        #endregion

        #region DefineObjeto
        public DadosImovel DefineObjeto(EdgeDriver driver, IWebElement divPrincipal)
        {
            DadosImovel imovel = new DadosImovel();
            imovel.valorAvaliacao = ExtraiDadosImovel(divPrincipal, PropriedadesSite.VALOR_AVALIACAO);
            imovel.valorMinimoPrimeiraVenda = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.VALOR_MINIMO_PRIMEIRA_VENDA);
            imovel.valorMinimoSegundaVenda = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.VALOR_MINIMO_SEGUNDA_VENDA);
            imovel.valorMinimoVenda = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.VALOR_MINIMO_VENDA);
            imovel.desconto = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.DESCONTO);
            imovel.valorAvaliacao = Regex.Match(imovel.valorAvaliacao, @"R\$ (\d{1,3}(?:\.\d{3})*(?:,\d{2})?)").Groups[1].Value;
            IWebElement loteamento = divPrincipal.FindElement(By.CssSelector("h5"));
            imovel.nomeLoteamento = loteamento != null ? loteamento.Text : String.Empty;

            imovel.tipoImovel = ExtraiDadosImovel(divPrincipal, PropriedadesSite.TIPO_IMOVEL);
            imovel.quartos = ExtraiDadosImovel(divPrincipal, PropriedadesSite.QUARTOS);
            imovel.garagem = ExtraiDadosImovel(divPrincipal, PropriedadesSite.GARAGEM);
            imovel.numeroImovel = ExtraiDadosImovel(divPrincipal, PropriedadesSite.NUMERO_IMOVEL);
            imovel.matricula = ExtraiDadosImovel(divPrincipal, PropriedadesSite.MATRICULA);
            imovel.comarca = ExtraiDadosImovel(divPrincipal, PropriedadesSite.COMARCA);
            imovel.oficio = ExtraiDadosImovel(divPrincipal, PropriedadesSite.OFICIO);
            imovel.inscricaoImobiliaria = ExtraiDadosImovel(divPrincipal, PropriedadesSite.INSCRICAO_IMOBILIARIA);
            imovel.averbacaoLeilaoNegativos = ExtraiDadosImovel(divPrincipal, PropriedadesSite.AVERBACAO_LEILAO_NEGATIVOS);
            imovel.areaTotal = ExtraiDadosImovel(divPrincipal, PropriedadesSite.AREA_TOTAL);
            imovel.areaPrivativa = ExtraiDadosImovel(divPrincipal, PropriedadesSite.AREA_PRIVATIVA);
            imovel.areaTerreno = ExtraiDadosImovel(divPrincipal, PropriedadesSite.AREA_TERRENO);
            imovel.situacao = ExtraiDadosImovel(divPrincipal, PropriedadesSite.SITUACAO);

            imovel.dadosVendaImovel = new DadosVendaImovel();
            imovel.dadosVendaImovel.edital = ExtraiDadosImovel(divPrincipal, PropriedadesSite.EDITAL);
            imovel.dadosVendaImovel.numeroItem = ExtraiDadosImovel(divPrincipal, PropriedadesSite.NUMERO_ITEM);
            imovel.dadosVendaImovel.leiloeiro = ExtraiDadosImovel(divPrincipal, PropriedadesSite.LEILOEIRO);
            imovel.dadosVendaImovel.dataPrimeiroLeilao = ExtraiDatasLeilao(divPrincipal, PropriedadesSite.DATA_PRIMEIRO_LEILAO);
            imovel.dadosVendaImovel.dataSegundoLeilao = ExtraiDatasLeilao(divPrincipal, PropriedadesSite.DATA_SEGUNDO_LEILAO);
            imovel.dadosVendaImovel.dataLicitacao = ExtraiDatasLeilao(divPrincipal, PropriedadesSite.DATA_LICITACAO_ABERTA);
            imovel.dadosVendaImovel.endereco = ExtraiDadosVendaImovel(divPrincipal, PropriedadesSite.ENDERECO);
            imovel.dadosVendaImovel.descricao = ExtraiDadosVendaImovel(divPrincipal, PropriedadesSite.DESCRICAO);

            // Extrai informações com base na classe "fa-info-circle"
            ReadOnlyCollection<IWebElement> infoCircles = divPrincipal.FindElements(By.CssSelector(".fa-info-circle"));
            imovel.dadosVendaImovel.formasDePagamento = new List<string>();
            foreach (var infoCircle in infoCircles)
            {
                string infoText = ((IJavaScriptExecutor)driver).ExecuteScript("return arguments[0].nextSibling.nodeValue;", infoCircle) as string;
                if (infoText != null)
                {
                    infoText = infoText.Trim();
                    imovel.dadosVendaImovel.formasDePagamento.Add(infoText);
                }
            }
            return imovel;
        }
        #endregion DefineObjeto

        #region ExtraiDadosImovel
        public string ExtraiDadosImovel(IWebElement divPrincipal, string textoProcurado)
        {
            string xpath;
            switch (textoProcurado)
            {
                case PropriedadesSite.VALOR_AVALIACAO:
                    xpath = $".//p[contains(text(),'{textoProcurado}')]";
                    break;
                case PropriedadesSite.SITUACAO:
                    xpath = $".//span[contains(., '{textoProcurado}')]";
                    break;
                default:
                    xpath = $".//span[contains(text(), '{textoProcurado}')]";
                    break;
            }

            IWebElement item = divPrincipal.FindElements(By.XPath(xpath)).FirstOrDefault();
            if (item != null)
            {
                string textoTratado = item.Text
                    .Replace($"{textoProcurado}:", "")
                    .Replace($"{textoProcurado} = ", "")
                    .Replace($"{textoProcurado} - ", "")
                    .Replace($"{textoProcurado}: R$ ", "")
                    .Replace($"{textoProcurado} ", "");
                textoTratado = Regex.Replace(textoTratado, @"\s+", " ").Trim();
                return textoTratado;
            }
            else
                return String.Empty;
        }
        #endregion ExtraiDadosImovel

        #region ExtraiDadosVendaImovel
        public string ExtraiDadosVendaImovel(IWebElement divPrincipal, string textoProcurado)
        {
            // Localiza a div.related-box dentro da divPrincipal
            IWebElement divRelatedBox = divPrincipal.FindElement(By.CssSelector("div.related-box"));
            IWebElement item = divRelatedBox.FindElements(By.XPath($".//p/strong[contains(text(), '{textoProcurado}:')]/following-sibling::text()[1]/parent::*")).FirstOrDefault();

            if (item != null)
            {
                string textoTratado = item.Text.Replace($"{textoProcurado}:", "");
                textoTratado = Regex.Replace(textoTratado, @"\s+", " ").Trim();
                return textoTratado;
            }
            else
                return String.Empty;
        }
        #endregion ExtraiDadosVendaImovel

        #region ExtraiDatasLeilao
        public DateTime? ExtraiDatasLeilao(IWebElement divPrincipal, string textoProcurado)
        {
            // Localiza a div.related-box dentro da divPrincipal
            IWebElement divRelatedBox = divPrincipal.FindElement(By.CssSelector("div.related-box"));
            IWebElement item = divRelatedBox.FindElements(By.XPath($".//span[contains(text(), '{textoProcurado}')]")).FirstOrDefault();

            if (item != null)
            {
                string textoTratado = item.Text.Replace($"{textoProcurado} - ", "");
                textoTratado = Regex.Replace(textoTratado, @"\s+", " ").Trim();
                DateTime? dataHora = ConverterParaData(textoTratado);
                return dataHora;
            }
            else
                return null;
        }
        #endregion

        #region ConverterParaData
        public DateTime? ConverterParaData(string textoTratado)
        {
            // Verifica se a string está vazia
            if (string.IsNullOrWhiteSpace(textoTratado))
                return null;

            // Define os formatos de data que podem ser encontrados
            string[] formatos = { "dd/MM/yyyy - HH'h'mm", "dd/MM/yyyy" };

            // Tenta fazer a conversão usando os formatos definidos
            DateTime dataConvertida;
            if (DateTime.TryParseExact(textoTratado, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out dataConvertida))
            {
                return dataConvertida;
            }
            return null;
        }
        #endregion ConverterParaData

        #region ExtraiValor
        public static string ExtraiValor(string text, string parameter)
        {
            string pattern;

            switch (parameter)
            {
                case PropriedadesSite.VALOR_MINIMO_PRIMEIRA_VENDA:
                    pattern = @"Valor mínimo de venda 1º Leilão: R\$ (\d+.\d+,\d\d)";
                    break;
                case PropriedadesSite.VALOR_MINIMO_SEGUNDA_VENDA:
                    pattern = @"Valor mínimo de venda 2º Leilão: R\$ (\d+.\d+,\d\d)";
                    break;
                case PropriedadesSite.VALOR_MINIMO_VENDA:
                    pattern = @"Valor mínimo de venda: R\$ (\d+.\d+,\d\d)";
                    break;
                case PropriedadesSite.DESCONTO:
                    pattern = @"\(\s*desconto de (\d+(,\d+)?)%\)";
                    break;
                default:
                    return "";
            }

            Match match = Regex.Match(text, pattern);

            if (match.Success)
            {
                return parameter == "desconto" ? match.Groups[1].Value : match.Groups[1].Value;
            }

            return "";
        }
        #endregion ExtraiValor

        #region SalvarListaComoJson
        public bool SalvarListaComoJson(List<DadosImovel> listaOrdenada, string filePath)
        {
            try
            {
                // Converte a lista ordenada para JSON
                string jsonResult = JsonConvert.SerializeObject(listaOrdenada, Formatting.Indented);

                // Garante que o diretório existe
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Salva o JSON no arquivo
                File.WriteAllText(filePath, jsonResult);

                return true; // Indica que a operação foi bem-sucedida
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                return false; // Indica que a operação falhou
            }
        }
        #endregion SalvarListaComoJson

        public string LerArquivoJson(string caminhoArquivo)
        {
            try
            {
                string conteudoJson = File.ReadAllText(caminhoArquivo);
                return conteudoJson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }

        public T ConverterJsonParaObjeto<T>(string conteudoJson)
        {
            try
            {
                // Converte o conteúdo JSON para um objeto C#
                T objeto = JsonConvert.DeserializeObject<T>(conteudoJson);
                return objeto;
            }
            catch (Exception ex)
            {
                // Lida com erros ao converter JSON para objeto
                Console.WriteLine($"Erro ao converter JSON para objeto: {ex.Message}");
                return default(T);
            }
        }

        public async Task<bool> EnviarMensagemTelegram(string token, long chatId, string mensagem)
        {
            try
            {
                // Inicializa o bot com o token
                var botClient = new TelegramBotClient(token);

                // Envia a mensagem para o chat especificado
                await botClient.SendTextMessageAsync(chatId, mensagem);
                return true;
            }
            catch (Exception ex)
            {
                // Lida com erros ao enviar a mensagem
                Console.WriteLine($"Erro ao enviar mensagem para o Telegram: {ex.Message}");
                throw;
            }
        }


        //Metodos para terminar abaixo
        #region OrdenaListaImoveis
        public List<DadosImovel> OrdenaListaImoveis(List<DadosImovel> dadosImoveis)
        {
            // Obtém a data atual
            DateTime dataAtual = DateTime.Now;

            // Filtra e ordena a lista com base na data atual e nas datas dos leilões
            var listaOrdenada = dadosImoveis
                .Where(imovel =>
                    imovel.dadosVendaImovel.dataLicitacao >= dataAtual ||
                    imovel.dadosVendaImovel.dataPrimeiroLeilao >= dataAtual ||
                    imovel.dadosVendaImovel.dataSegundoLeilao >= dataAtual)
                .OrderBy(imovel => new[]
                {
                    imovel.dadosVendaImovel.dataLicitacao,
                    imovel.dadosVendaImovel.dataPrimeiroLeilao,
                    imovel.dadosVendaImovel.dataSegundoLeilao
                }
                .Where(data => data >= dataAtual)
                .Min())
                .ToList();
            return listaOrdenada;
        }
        #endregion OrdenaListaImoveis

        #region ExtraiSituacao
        #endregion ExtraiSituacao
    }
}