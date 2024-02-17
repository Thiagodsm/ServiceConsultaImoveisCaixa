using ConsultaImoveisLeilaoCaixa.Model;
using ConsultaImoveisLeilaoCaixa.Repository.Interface;
using ConsultaImoveisLeilaoCaixa.Services.Interface;
using ConsultaImoveisLeilaoCaixa.Util;
using MongoDB.Bson;
using MongoDB.Driver;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ConsultaImoveisLeilaoCaixa
{
    public class TaskConsultaImoveisCaixa : BackgroundService
    {
        #region ctor
        private readonly ILogger<TaskConsultaImoveisCaixa> _logger;
        private readonly IImoveisLeilaoCaixaRepository _imoveisLeilaoCaixaRepository;
        private readonly IEnderecoViaCEPRepository _enderecoViaCEPRepository;
        private readonly ITituloEditalRepository _tituloEditalRepository;
        private readonly IViaCEPService _viaCEPService;

        public TaskConsultaImoveisCaixa(ILogger<TaskConsultaImoveisCaixa> logger, 
            IImoveisLeilaoCaixaRepository imoveisLeilaoCaixaRepository, 
            IEnderecoViaCEPRepository enderecoViaCEPRepository,
            ITituloEditalRepository tituloEditalRepository,
            IViaCEPService viaCEPService)
        {
            _logger = logger;
            _imoveisLeilaoCaixaRepository = imoveisLeilaoCaixaRepository;
            _enderecoViaCEPRepository = enderecoViaCEPRepository;
            _tituloEditalRepository = tituloEditalRepository;
            _viaCEPService = viaCEPService;
        }
        #endregion ctor

        #region ExecuteAsync
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Iniciando o servico: {time}", DateTimeOffset.Now);

                string edgeDriverPath = @"C:\Users\thiag\Documents\WebDriver\msedgedriver.exe";
                EdgeDriver driver = new EdgeDriver(edgeDriverPath);
                List<string> numerosImoveisProcessados = new List<string>();
                List<DadosImovel> dadosImoveis = new List<DadosImovel>();
                await _imoveisLeilaoCaixaRepository.TestConnection(Config.ConnectionString, Config.DbName);
                await _enderecoViaCEPRepository.TestConnection(Config.ConnectionString, Config.DbName);
                await _tituloEditalRepository.TestConnection(Config.ConnectionString, Config.DbName);

                try
                {
                    List<string> titulosEditais = new List<string>();
                    // Navega nas paginas do site da Caixa
                    int totalPages = 0;// NavegacaoImoveis(driver);
                    List<IWebElement> linksLicitacoes = NavegacaoLicitacoes(driver);
                    Licitacao licitacao = new Licitacao();

                    // Iterar sobre os títulos dos editais e adicioná-los a uma lista
                    foreach (IWebElement h5Element in driver.FindElements(By.TagName("h5")))
                    {
                        string tituloEdital = h5Element.Text;
                        if (!titulosEditais.Contains(tituloEdital))
                        {
                            titulosEditais.Add(tituloEdital);
                        }
                    }
                    titulosEditais = titulosEditais.Distinct().ToList();

                    // Iterar sobre os títulos únicos e processar as páginas correspondentes
                    foreach (string tituloEdital in titulosEditais)
                    {
                        _logger.LogInformation($"Titulo Edital: {tituloEdital}");
                        // Encontre o link correspondente ao título do edital
                        IWebElement linkLeilao = driver.FindElement(By.XPath($"//h5[text()='{tituloEdital}']/following::a[contains(@onclick, 'ListarEdital')]"));

                        // Obtenha a quantidade de páginas para o edital atual
                        totalPages = ObterQuantidadePaginas(driver, linkLeilao);

                        // Buscar os IDs dos imóveis na página atual
                        numerosImoveisProcessados.AddRange(BuscaIdsImoveis(driver, totalPages, "Imoveis Licitacoes"));

                        TituloEditalLeilao editalLeilao = new TituloEditalLeilao();
                        editalLeilao.titulo = tituloEdital;
                        editalLeilao.data = DateTime.Now;
                        editalLeilao.processado = true;
                        editalLeilao.totalImoveis = numerosImoveisProcessados.Count;

                        TituloEditalLeilao editalAux = await _tituloEditalRepository.GetByIdAsync(editalLeilao.titulo);

                        if (editalAux == null)
                            await _tituloEditalRepository.CreateAsync(editalLeilao);
                        else
                            await _tituloEditalRepository.UpdateAsync(editalLeilao.titulo, editalLeilao);

                        // Extrai as informações do site da caixa em forma de objeto
                        dadosImoveis.AddRange(await ExtraiDadosImoveisCaixa(driver, numerosImoveisProcessados, tituloEdital));

                        foreach (DadosImovel imovelNovo in dadosImoveis)
                        {
                            DadosImovel imovelAux = await _imoveisLeilaoCaixaRepository.GetByIdAsync(imovelNovo.id);
                            if (imovelAux == null)
                                await _imoveisLeilaoCaixaRepository.CreateAsync(imovelNovo);
                            else
                            {
                                await _imoveisLeilaoCaixaRepository.UpdateAsync(imovelNovo.id, imovelNovo);
                            }
                        }

                        // Voltar à página anterior
                        IWebElement botaoVoltar = driver.FindElement(By.CssSelector("button.voltaLicitacoes"));
                        botaoVoltar.Click();

                        // Limpa lista de imoveis processados
                        numerosImoveisProcessados.Clear();

                        // Aguarde um tempo para que a página volte completamente
                        Thread.Sleep(5000);

                        // Selecione o estado (SP) no dropdown
                        var estadoDropdown = new SelectElement(driver.FindElement(By.Id("cmb_estado")));
                        estadoDropdown.SelectByText("SP");

                        // Aguarde um tempo adicional (de 5 segundos) após selecionar o estado
                        Thread.Sleep(5000);

                        // Clique no botão "Próximo"
                        var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                        btnNext1.Click();

                        // Aguarde um tempo adicional (de 10 segundos) para carregar as licitações novamente
                        Thread.Sleep(10000);
                    }

                    // Salvando informacoes dos imoveis
                    //ImoveisLeilaoCaixa imoveisLeilaoCaixa = new ImoveisLeilaoCaixa();
                    //imoveisLeilaoCaixa.imoveis = dadosImoveis;

                    //// Ler o conteúdo do arquivo JSON
                    //string imoveisCarregadosString = LerArquivoJson(Config.CaminhoArquivoImoveis);
                    //ImoveisLeilaoCaixa imoveisCarregados = ConverterJsonParaObjeto(imoveisCarregadosString);

                    //if (imoveisLeilaoCaixa.imoveis.Count > 0)
                    //{
                    //    bool succes = SalvarListaComoJson(imoveisLeilaoCaixa, Config.CaminhoArquivoImoveis);

                    //    string caminhoArquivo = Config.CaminhoArquivoImoveis;
                    //    string tokenTelegram = Config.BotToken;
                    //    string chatIdTelegram = Config.ChatId;

                    //    string mensagem = "";
                    //    foreach (DadosImovel item in imoveisLeilaoCaixa.imoveis)
                    //    {
                    //        mensagem = MontaMensagamTelegram(item);
                    //        bool retTelegram = false;
                    //        if (item.dadosVendaImovel.LinkImagensImovel.Any())
                    //             retTelegram = await EnviarMensagemTelegram(tokenTelegram, chatIdTelegram, mensagem, item.dadosVendaImovel.LinkImagensImovel.FirstOrDefault());
                    //        else
                    //            retTelegram = await EnviarMensagemTelegram(tokenTelegram, chatIdTelegram, mensagem, "");
                    //        // Evite enviar mais de uma mensagem por segundo
                    //        Thread.Sleep(5000);
                    //    }
                    //}

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
        #endregion ExecuteAsync

        #region NavegacaoImoveis
        public int NavegacaoImoveis(EdgeDriver driver)
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
        #endregion NavegacaoImoveis

        #region NavegacaoLicitacoes
        public List<IWebElement> NavegacaoLicitacoes(EdgeDriver driver)
        {
            try
            {
                // Navegue para a página da Caixa
                driver.Navigate().GoToUrl("https://venda-imoveis.caixa.gov.br/sistema/busca-licitacoes.asp?sltTipoBusca=licitacoes");

                // Selecione o estado (SP) no dropdown
                var estadoDropdown = new SelectElement(driver.FindElement(By.Id("cmb_estado")));
                estadoDropdown.SelectByText("SP");

                // Aguarde um tempo adicional (de 10 segundos) antes de selecionar a cidade
                Thread.Sleep(10000);

                // Clique no botão "Próximo"
                var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                btnNext1.Click();

                // Aguarde um tempo adicional (de 20 segundos) para carregar todos os imoveis
                Thread.Sleep(20000);

                // Encontre a div com o ID "listalicitacoes"
                IWebElement divPrincipalLicitacoes = driver.FindElement(By.CssSelector("div#listalicitacoes"));

                // Encontre todos os elementos 'a' dentro da div
                IList<IWebElement> links = divPrincipalLicitacoes.FindElements(By.TagName("a"));

                // Lista para armazenar os links que correspondem às características desejadas
                List<IWebElement> linksDesejados = new List<IWebElement>();

                // Filtrar os links com base nas características
                foreach (IWebElement link in links)
                {
                    string onclickValue = link.GetAttribute("onclick");
                    string titleValue = link.GetAttribute("title");

                    if (onclickValue != null && titleValue != null &&
                        (onclickValue.Contains("ListarEdital") ||
                        titleValue.Contains(PropriedadesSite.LINK_LISTA_LICITACOES) ||
                        titleValue.Contains(PropriedadesSite.LINK_LISTA_VENDA_ONLINE)))
                    {
                        linksDesejados.Add(link);
                    }
                }

                return linksDesejados;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion NavegacaoLicitacoes

        #region ObterQuantidadePaginas
        public int ObterQuantidadePaginas(EdgeDriver driver, IWebElement link)
        {
            try
            {
                // Clique no link para carregar a página com a quantidade de páginas
                link.Click();

                // Aguarde um tempo para que a página seja totalmente carregada
                Thread.Sleep(10000);

                // Encontre o elemento que contém o valor de hdnQtdPag
                IWebElement elemento = driver.FindElement(By.Id("hdnQtdPag"));

                // Obtenha o valor de hdnQtdPag
                string valorHdnQtdPag = elemento.GetAttribute("value");

                // Se o valor não estiver vazio, converta para um inteiro e retorne
                if (!string.IsNullOrEmpty(valorHdnQtdPag))
                {
                    return Convert.ToInt32(valorHdnQtdPag);
                }
                else
                {
                    // Se o valor estiver vazio, retorne zero
                    return 0;
                }
            }
            catch (NoSuchElementException)
            {
                // Se o elemento não for encontrado, retorne zero
                return 0;
            }
            catch (Exception ex)
            {
                // Em caso de qualquer outra exceção, registre o erro e retorne zero
                _logger.LogError(ex.Message);
                return 0;
            }
        }
        #endregion ObterQuantidadePaginas

        #region BuscaIdsImoveis
        public List<string> BuscaIdsImoveis(EdgeDriver driver, int totalPages, string tipoPagina = "")
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

                        // Adiciona o número do imóvel a lista
                        numerosImoveisProcessados.Add(numeroImovel);
                    }

                    // Aguarde um tempo para a próxima página carregar completamente
                    Thread.Sleep(10000);

                    // Navegue para a próxima página, se houver
                    if (currentPage < totalPages)
                    {
                        if (tipoPagina == "Imoveis Licitacoes")
                        {
                            // Clique no link para a próxima página
                            driver.FindElement(By.CssSelector($"a[href='javascript:carregaListaImoveisLicitacoes({currentPage + 1});']")).Click();
                        }
                        else
                        {
                            // Clique no link para a próxima página
                            driver.FindElement(By.CssSelector($"a[href='javascript:carregaListaImoveis({currentPage + 1});']")).Click();
                        }

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
        public async Task<List<DadosImovel>> ExtraiDadosImoveisCaixa(EdgeDriver driver, List<string> numerosImoveisProcessados, string tituloEditalImovel)
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
                    DadosImovel imovel = await DefineObjeto(driver, divPrincipal, divGaleriaImagens);
                    imovel.tituloEditalImovel = tituloEditalImovel;
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
        public async Task<DadosImovel> DefineObjeto(EdgeDriver driver, IWebElement divPrincipal, IWebElement divGaleriaImagens)
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

            // Busca informacoesComplementares pelo CEP
            string cep = ExtrairCEP(imovel.dadosVendaImovel.endereco);
            if (!String.IsNullOrWhiteSpace(cep))
                imovel.informacoesComplementares = await _viaCEPService.ConsultarCep(cep);

            // Id = matricula + numeroImovel tirando caracteres nao numericos
            imovel.id = $"{imovel.matricula}{Regex.Replace(imovel.numeroImovel, "[^0-9]", "")}";
            imovel.dataProcessamento = DateTime.Now;

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
                return null;
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
                return null;
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
                    return null;
            }

            Match match = Regex.Match(text, pattern);

            if (match.Success)
            {
                return parameter == "desconto" ? match.Groups[1].Value : match.Groups[1].Value;
            }

            return "-";
        }
        #endregion ExtraiValor

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
                    VerificarValorNuloOuVazio(PropriedadesSite.NOME_LOTEAMENTE, imovel.nomeLoteamento) +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_AVALIACAO, imovel.valorAvaliacao, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_MINIMO_VENDA, imovel.valorMinimoVenda, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.DESCONTO, imovel.desconto, sufixo: "%") +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_MINIMO_PRIMEIRA_VENDA, imovel.valorMinimoPrimeiraVenda, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_MINIMO_SEGUNDA_VENDA, imovel.valorMinimoSegundaVenda, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.TIPO_IMOVEL, imovel.tipoImovel) +
                    VerificarValorNuloOuVazio(PropriedadesSite.QUARTOS, imovel.quartos) +
                    VerificarValorNuloOuVazio(PropriedadesSite.GARAGEM, imovel.garagem) +
                    VerificarValorNuloOuVazio(PropriedadesSite.NUMERO_IMOVEL, imovel.numeroImovel) +
                    VerificarValorNuloOuVazio(PropriedadesSite.MATRICULA, imovel.matricula) +
                    VerificarValorNuloOuVazio(PropriedadesSite.COMARCA, imovel.comarca) +
                    VerificarValorNuloOuVazio(PropriedadesSite.OFICIO, imovel.oficio) +
                    VerificarValorNuloOuVazio(PropriedadesSite.INSCRICAO_IMOBILIARIA, imovel.inscricaoImobiliaria) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AVERBACAO_LEILAO_NEGATIVOS, imovel.averbacaoLeilaoNegativos) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AREA_TOTAL, imovel.areaTotal) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AREA_PRIVATIVA, imovel.areaPrivativa) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AREA_TERRENO, imovel.areaTerreno) +
                    VerificarValorNuloOuVazio(PropriedadesSite.SITUACAO, imovel.situacao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.EDITAL, imovel.dadosVendaImovel.edital) +
                    VerificarValorNuloOuVazio(PropriedadesSite.NUMERO_ITEM, imovel.dadosVendaImovel.numeroItem) +
                    VerificarValorNuloOuVazio(PropriedadesSite.LEILOEIRO, imovel.dadosVendaImovel.leiloeiro) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DATA_LICITACAO_ABERTA, imovel.dadosVendaImovel.dataLicitacao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DATA_PRIMEIRO_LEILAO, imovel.dadosVendaImovel.dataPrimeiroLeilao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DATA_SEGUNDO_LEILAO, imovel.dadosVendaImovel.dataSegundoLeilao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.ENDERECO, imovel.dadosVendaImovel.endereco) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DESCRICAO, imovel.dadosVendaImovel.descricao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.LINK_MATRICULA_IMOVEL, imovel.dadosVendaImovel.linkMatriculaImovel) +
                    VerificarValorNuloOuVazio(PropriedadesSite.LINK_EDITAL_IMOVEL, imovel.dadosVendaImovel.linkEditalImovel);
            return mensagem;
        }

        public string VerificarValorNuloOuVazio(string texto, string valor, string prefixo = "", string sufixo = "")
        {
            return !String.IsNullOrWhiteSpace(valor) && valor != "-" ? $"{texto}: {prefixo} {valor}{sufixo}\n" : "";
        }

        public string VerificarValorNuloOuVazio(string texto, DateTime? valor, string unidade = "")
        {
            return valor != null ? $"{texto} {unidade}: {valor}\n" : "";
        }
        #endregion MontaMensagamTelegram

        #region EnviarMensagemTelegram
        public async Task<bool> EnviarMensagemTelegram(string botToken, string chatId, string mensagem, string linkImagem)
        {
            try
            {
                const int maxCharacters = 1024;
                bool success = false;

                if (mensagem.Length > maxCharacters)
                {
                    // Tenta quebrar pelo "Descrição:"
                    List<string> partes = SplitMensagemPorPalavra(mensagem, PropriedadesSite.DESCRICAO + ":", maxCharacters);

                    // Se não houver "Descrição:", tenta quebrar pelo "Endereço:"
                    if (partes.Count == 1)
                    {
                        partes = SplitMensagemPorPalavra(mensagem, PropriedadesSite.ENDERECO + ":", maxCharacters);
                    }

                    // Se ainda não houver, quebra na posição 1024
                    if (partes.Count == 1)
                    {
                        partes = SplitMensagem(mensagem, maxCharacters);
                    }

                    // Envia cada parte sequencialmente
                    for (int i = 0; i < partes.Count; i++)
                    {
                        // Adiciona um identificador à parte da mensagem
                        string mensagemParte = partes.Count > 1 ? $"{i + 1}/{partes.Count} - {partes[i]}" : partes[i];

                        // Envia a parte da mensagem
                        success = await EnviarMensagemComFoto(botToken, chatId, mensagemParte, linkImagem);
                    }
                }
                else
                    success = await EnviarMensagemComFoto(botToken, chatId, mensagem, linkImagem);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion EnviarMensagemTelegram

        #region EnviarMensagemComFoto
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
        #endregion EnviarMensagemComFoto

        #region DivideMensagem
        private List<string> SplitMensagemPorPalavra(string mensagem, string palavra, int maxCharacters)
        {
            // Tenta quebrar o texto pela palavra específica
            int indicePalavra = mensagem.IndexOf(palavra);

            if (indicePalavra >= 0 && indicePalavra < maxCharacters)
            {
                return new List<string> { mensagem.Substring(0, indicePalavra + palavra.Length), mensagem.Substring(indicePalavra + palavra.Length) };
            }

            // Se não encontrar a palavra ou estiver além do limite, retorna a mensagem original
            return SplitMensagem(mensagem, maxCharacters);
        }

        private List<string> SplitMensagem(string mensagem, int maxCharacters)
        {
            // Divide a mensagem na posição máxima de caracteres
            return Enumerable.Range(0, mensagem.Length / maxCharacters)
                .Select(i => mensagem.Substring(i * maxCharacters, maxCharacters))
                .ToList();
        }
        #endregion DivideMensagem

        #region ExtrairCEP
        public string ExtrairCEP(string endereco)
        {
            // Usando uma expressão regular para encontrar o padrão de CEP na string
            Match match = Regex.Match(endereco, @"\bCEP:\s*(\d{5}-\d{3})\b");

            if (match.Success)
            {
                // Se o padrão for encontrado, retorna apenas os dígitos numéricos
                string cep = Regex.Replace(match.Groups[1].Value, @"[^\d]", "");
                return cep;
            }
            else
            {
                return "";
            }
        }
        #endregion ExtrairCEP

        #region ExtraiSituacao
        #endregion ExtraiSituacao
    }
}