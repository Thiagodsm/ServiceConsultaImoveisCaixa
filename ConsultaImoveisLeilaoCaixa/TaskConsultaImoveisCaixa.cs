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
using Telegram.Bot.Types;

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
                _logger.LogInformation("Iniciando o servico: {time}", DateTimeOffset.Now);
                //await telegramPollingService.IniciarPolling();

                string edgeDriverPath = @"C:\Users\thiag\Documents\WebDriver\msedgedriver.exe";
                EdgeDriver driver = new EdgeDriver(edgeDriverPath);

                try
                {
                    // Navega nas paginas do site da Caixa
                    int totalPages = Navegacao(driver);

                    // Conjunto para rastrear números de imóveis já processados
                    List<string> numerosImoveisProcessados = BuscaIdsImoveis(driver, totalPages);

                    // Extrai as informações do site da caixa em forma de objeto
                    List<DadosImovel> dadosImoveis = ExtraiDadosImoveisCaixa(driver, numerosImoveisProcessados);

                    // Salvando informacoes dos imoveis
                    ImoveisLeilaoCaixa imoveisLeilaoCaixa = new ImoveisLeilaoCaixa();
                    imoveisLeilaoCaixa.dataProcessamento = DateTime.Now;
                    imoveisLeilaoCaixa.totalImoveis = dadosImoveis.Count;
                    imoveisLeilaoCaixa.imoveis = dadosImoveis;

                    // Ler o conteúdo do arquivo JSON
                    string imoveisCarregadosString = LerArquivoJson(Config.CaminhoArquivoImoveis);
                    ImoveisLeilaoCaixa imoveisCarregados = ConverterJsonParaObjeto(imoveisCarregadosString);

                    //temporario
                    //imoveisLeilaoCaixa = imoveisCarregados;

                    if (imoveisLeilaoCaixa.imoveis.Count > 0)
                    {
                        bool succes = SalvarListaComoJson(imoveisLeilaoCaixa, Config.CaminhoArquivoImoveis);

                        string caminhoArquivo = Config.CaminhoArquivoImoveis;
                        string tokenTelegram = Config.BotToken;
                        string chatIdTelegram = Config.ChatId;

                        string mensagem = "";
                        foreach (DadosImovel item in imoveisLeilaoCaixa.imoveis)
                        {
                            mensagem = MontaMensagamTelegram(item);
                            bool retTelegram = await EnviarMensagemComFoto(tokenTelegram, chatIdTelegram, mensagem, item.dadosVendaImovel.LinkImagensImovel.FirstOrDefault());
                        }
                    }
                    Thread.Sleep(36000000);
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e.Message);
                    if (!String.IsNullOrWhiteSpace(e.Message))
                    {
                        driver.Quit();
                        Thread.Sleep(60000);
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

        #region Navegacao
        public int Navegacao(EdgeDriver driver)
        {
            try
            {
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

                // Obtém o número total de páginas
                int totalPages = int.Parse(driver.FindElement(By.Id("hdnQtdPag")).GetAttribute("value"));
                return totalPages;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion Navegacao

        #region BuscaIdsImoveis
        public List<string> BuscaIdsImoveis(EdgeDriver driver,  int totalPages)
        {
            try
            {
                // Conjunto para rastrear números de imóveis já processados
                List<string> numerosImoveisProcessados = new List<string>();


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
                return numerosImoveisProcessados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
            
        }
        #endregion BuscaIdsImoveis

        #region ExtraiDadosImoveisCaixa
        public List<DadosImovel> ExtraiDadosImoveisCaixa(EdgeDriver driver, List<string> numerosImoveisProcessados)
        {
            try
            {
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
                    // Localiza a div pelo ID "galeria-imagens"
                    IWebElement divGaleriaImagens = driver.FindElement(By.Id("galeria-imagens"));

                    // Definindo o objeto a partir da div principal
                    DadosImovel imovel = DefineObjeto(driver, divPrincipal, divGaleriaImagens);
                    dadosImoveis.Add(imovel);

                    // Após lidar com a página de detalhes, você pode voltar à lista de imóveis
                    driver.Navigate().Back();
                }

                // Aguarde um tempo para voltar a pagina anterior
                Thread.Sleep(5000);

                // Volte para a página de listagem de imóveis
                driver.Navigate().Back();
                driver.Quit();
                return dadosImoveis;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion ExtraiDadosImoveisCaixa

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
        public DadosImovel DefineObjeto(EdgeDriver driver, IWebElement divPrincipal, IWebElement divGaleriaImagens)
        {
            DadosImovel imovel = new DadosImovel();
            imovel.visivelCaixaImoveis = true;
            IWebElement loteamento = divPrincipal.FindElement(By.CssSelector("h5"));
            imovel.nomeLoteamento = loteamento != null ? loteamento.Text : String.Empty;
            imovel.valorAvaliacao = ExtraiDadosImovel(divPrincipal, PropriedadesSite.VALOR_AVALIACAO);
            imovel.valorMinimoPrimeiraVenda = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.VALOR_MINIMO_PRIMEIRA_VENDA);
            imovel.valorMinimoSegundaVenda = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.VALOR_MINIMO_SEGUNDA_VENDA);
            imovel.valorMinimoVenda = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.VALOR_MINIMO_VENDA);
            imovel.desconto = ExtraiValor(imovel.valorAvaliacao, PropriedadesSite.DESCONTO);
            imovel.valorAvaliacao = Regex.Match(imovel.valorAvaliacao, @"R\$ (\d{1,3}(?:\.\d{3})*(?:,\d{2})?)").Groups[1].Value;

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
            imovel.dadosVendaImovel.linkMatriculaImovel = ExtraiLinkMatriculaImovel(divPrincipal, PropriedadesSite.LINK_MATRICULA_IMOVEL);
            imovel.dadosVendaImovel.linkEditalImovel = ExtrairLinkEditalImovel(divPrincipal, PropriedadesSite.LINK_EDITAL_IMOVEL);

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

            IList<IWebElement> imagens = divGaleriaImagens.FindElements(By.CssSelector("img"));
            imovel.dadosVendaImovel.LinkImagensImovel = new List<string>();
            foreach (var imagem in imagens)
            {
                string imageUrl = imagem.GetAttribute("src");
                if (!string.IsNullOrEmpty(imageUrl))
                {
                    //byte[] imagemBytes = BaixarImagem(imageUrl);
                    imovel.dadosVendaImovel.LinkImagensImovel.Add(imageUrl);
                }
            }
            imovel.dadosVendaImovel.LinkImagensImovel = imovel.dadosVendaImovel.LinkImagensImovel.Distinct().ToList();
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
                return "-";
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
                return "-";
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
                    return "-";
            }

            Match match = Regex.Match(text, pattern);

            if (match.Success)
            {
                return parameter == "desconto" ? match.Groups[1].Value : match.Groups[1].Value;
            }

            return "-";
        }
        #endregion ExtraiValor

        #region EnviarMensagemTelegram
        public async Task<bool> EnviarMensagemTelegram(string token, string chatId, string mensagem)
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
        #endregion EnviarMensagemTelegram

        #region SalvarListaComoJson
        public bool SalvarListaComoJson(ImoveisLeilaoCaixa imoveisLeilaoCaixa, string filePath)
        {
            try
            {
                string jsonResult = JsonConvert.SerializeObject(imoveisLeilaoCaixa, Formatting.Indented);

                string directoryPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                System.IO.File.WriteAllText(filePath, jsonResult);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion SalvarListaComoJson

        #region LerArquivoJson
        public string LerArquivoJson(string caminhoArquivo)
        {
            try
            {
                string conteudoJson = System.IO.File.ReadAllText(caminhoArquivo);
                return conteudoJson;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion LerArquivoJson

        #region ExtraiLinkMatriculaImovel
        public static string ExtraiLinkMatriculaImovel(IWebElement divPrincipal, string nomeCampo)
        {
            string pattern = @"ExibeDoc\('(.+?)'\)";
            IReadOnlyCollection<IWebElement> links = divPrincipal.FindElements(By.XPath($".//a[contains(text(), '{nomeCampo}')]"));

            foreach (var link in links)
            {
                Match match = Regex.Match(link.GetAttribute("onclick"), pattern);
                if (match.Success)
                {
                    string caminhoDocumento = match.Groups[1].Value;
                    return "https://venda-imoveis.caixa.gov.br" + caminhoDocumento;
                }
            }

            return null;
        }
        #endregion ExtraiLinkMatriculaImovel

        #region ExtrairLinkEditalImovel
        public static string ExtrairLinkEditalImovel(IWebElement divPrincipal, string nomeCampo)
        {
            string pattern = @"ExibeDoc\('(.+?)'\)";
            IReadOnlyCollection<IWebElement> strongElements = divPrincipal.FindElements(By.XPath($".//strong[contains(text(), '{nomeCampo}')]"));

            foreach (var strongElement in strongElements)
            {
                IWebElement link = strongElement.FindElement(By.XPath(".//preceding::a[1]"));
                if (link != null)
                {
                    Match match = Regex.Match(link.GetAttribute("onclick"), pattern);
                    if (match.Success)
                    {
                        string caminhoDocumento = match.Groups[1].Value;
                        return "https://venda-imoveis.caixa.gov.br" + caminhoDocumento;
                    }
                }
            }

            return null;
        }
        #endregion ExtrairLinkEditalImovel

        #region BaixarImagem
        public byte[] BaixarImagem(string url)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                HttpResponseMessage response = httpClient.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    return response.Content.ReadAsByteArrayAsync().Result;
                }
                else
                {
                    Console.WriteLine($"Falha ao baixar a imagem. Status: {response.StatusCode}");
                    return null;
                }
            }
        }
        #endregion BaixarImagem

        #region ConverterJsonParaObjeto
        public ImoveisLeilaoCaixa ConverterJsonParaObjeto(string conteudoJson)
        {
            try
            {
                ImoveisLeilaoCaixa imoveisLeilaoCaixa = JsonConvert.DeserializeObject<ImoveisLeilaoCaixa>(conteudoJson);
                return imoveisLeilaoCaixa;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion ConverterJsonParaObjeto

        #region MontaMensagamTelegram
        public string MontaMensagamTelegram(DadosImovel imovel)
        {
            string mensagem = 
                    $"Nome do Loteamento: {VerificarValorNuloOuVazio(imovel.nomeLoteamento)}\n" +
                    $"Valor de avaliação: R$ {VerificarValorNuloOuVazio(imovel.valorAvaliacao)}\n" +
                    $"Valor minimo de venda: R$ {VerificarValorNuloOuVazio(imovel.valorMinimoVenda)}\n" +
                    $"Desconto: {VerificarValorNuloOuVazio(imovel.desconto)}%\n" +
                    $"Valor minimo de venda 1° Leilão: R$ {VerificarValorNuloOuVazio(imovel.valorMinimoPrimeiraVenda)}\n" +
                    $"Valor minimo de venda 2° Leilão: R$ {VerificarValorNuloOuVazio(imovel.valorMinimoSegundaVenda)}\n" +
                    $"Tipo de imóvel: {VerificarValorNuloOuVazio(imovel.tipoImovel)}\n" +
                    $"Quartos: {VerificarValorNuloOuVazio(imovel.quartos)}\n" +
                    $"Garagem: {VerificarValorNuloOuVazio(imovel.garagem)}\n" +
                    $"Numero do imóvel: {VerificarValorNuloOuVazio(imovel.numeroImovel)}\n" +
                    $"Matricula(s): {VerificarValorNuloOuVazio(imovel.matricula)}\n" +
                    $"Comarca: {VerificarValorNuloOuVazio(imovel.comarca)}\n" +
                    $"Oficio: {VerificarValorNuloOuVazio(imovel.oficio)}\n" +
                    $"Inscricao imobiliaria: {VerificarValorNuloOuVazio(imovel.inscricaoImobiliaria)}\n" +
                    $"Averbação dos leilões negativos: {VerificarValorNuloOuVazio(imovel.averbacaoLeilaoNegativos)}\n" +
                    $"Area total: {VerificarValorNuloOuVazio(imovel.areaTotal)}\n" +
                    $"Area privativa: {VerificarValorNuloOuVazio(imovel.areaPrivativa)}\n" +
                    $"Area do terreno: {VerificarValorNuloOuVazio(imovel.areaTerreno)}\n" +
                    $"Situação: {VerificarValorNuloOuVazio(imovel.situacao)}\n" +

                    $"Edital: {VerificarValorNuloOuVazio(imovel.dadosVendaImovel.edital)}\n" +
                    $"Número do Item: {VerificarValorNuloOuVazio(imovel.dadosVendaImovel.numeroItem)}\n" +
                    $"Leiloeiro: {VerificarValorNuloOuVazio(imovel.dadosVendaImovel.leiloeiro)}\n" +
                    $"Data da Licitação: {imovel.dadosVendaImovel.dataLicitacao}\n" +
                    $"Data do 1° Leilão: {imovel.dadosVendaImovel.dataPrimeiroLeilao}\n" +
                    $"Data do 2° Leilão: {imovel.dadosVendaImovel.dataSegundoLeilao}\n" +
                    $"Endereço: {VerificarValorNuloOuVazio(imovel.dadosVendaImovel.endereco)}\n" +
                    $"Descrição: {VerificarValorNuloOuVazio(imovel.dadosVendaImovel.descricao)}\n" +
                    $"Link Matrícula Imóvel: {VerificarValorNuloOuVazio(imovel.dadosVendaImovel.linkMatriculaImovel)}\n" +
                    $"Link Edital Imóvel: {VerificarValorNuloOuVazio(imovel.dadosVendaImovel.linkEditalImovel)}\n";
           
            return mensagem;
        }

        public string VerificarValorNuloOuVazio(string valor, string valorPadrao = "-")
        {
            return String.IsNullOrWhiteSpace(valor) ? valorPadrao : valor;
        }
        #endregion MontaMensagamTelegram

        public async Task<bool> EnviarMensagemComFoto(string botToken, string chatId, string mensagem, string linkImagem)
        {
            try
            {
                // Crie um cliente HTTP
                using (var httpClient = new HttpClient())
                {
                    // Construa a URL para enviar a foto
                    var apiUrl = $"https://api.telegram.org/bot{botToken}/sendPhoto";

                    // Crie um formulário de conteúdo para enviar a mensagem e a foto
                    var formContent = new MultipartFormDataContent
                    {
                        { new StringContent(chatId), "chat_id" },
                        { new StringContent(mensagem), "caption" },
                        { new StringContent(linkImagem), "photo" } // Adicione a imagem como uma string URL
                    };

                    // Envie a mensagem com a foto usando o método POST
                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, formContent);
                    if (response.IsSuccessStatusCode)
                        return true;
                    else
                        return false;

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }

        // para terminar
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