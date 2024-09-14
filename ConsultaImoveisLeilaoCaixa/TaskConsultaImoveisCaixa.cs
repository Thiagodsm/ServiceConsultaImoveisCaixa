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
using SeleniumExtras.WaitHelpers;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ConsultaImoveisLeilaoCaixa
{
    public class TaskConsultaImoveisCaixa : BackgroundService
    {
        #region ctor
        private readonly NLog.Logger log;
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
            log = NLog.LogManager.GetLogger("logConsultaImoveisLeilaoCaixa");
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

                EdgeDriver driver = new EdgeDriver();
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
                    List<TituloEditalLeilao> titulosProcessados = await _tituloEditalRepository.GetAllAsync();
                    _logger.LogInformation($"Total licitacoes encontradas: {titulosProcessados.Count}");

                    // Iterar sobre os títulos dos editais e adicioná-los a uma lista
                    foreach (IWebElement h5Element in driver.FindElements(By.TagName("h5")))
                    {
                        string tituloEdital = h5Element.Text;
                        titulosEditais.Add(tituloEdital);
                    }
                    titulosEditais = titulosEditais.Distinct().ToList();

                    if (titulosEditais.Count > 0)
                    {
                        // Comparar as duas listas para encontrar os títulos que estão no banco de dados, mas não estão mais disponíveis no site
                        List<string> titulosExcluidos = titulosProcessados
                            .Where(titulo => !titulosEditais.Contains(titulo.titulo))
                            .Select(titulo => titulo.titulo)
                            .ToList();

                        // Removendo titulos de licitacoes ja processadas e nao que nao serao excluidas
                        titulosEditais = titulosEditais.Except(titulosProcessados.Select(licitacao => licitacao.titulo)).ToList();
                                                
                        foreach (string tituloEdital in titulosExcluidos)
                        {
                            // Excluindo imoveis cujos editais ja passaram para processar novamente se voltar em outro edital
                            await _imoveisLeilaoCaixaRepository.DeleteByTituloAsync(tituloEdital);

                            // Excluindo titulo do edital do banco apos excluir os imoveis
                            await _tituloEditalRepository.DeleteAsync(tituloEdital);
                        }
                    }
                    
                    // Iterar sobre os títulos únicos e processar as páginas correspondentes
                    foreach (string tituloEdital in titulosEditais)
                    {
                        _logger.LogInformation($"Titulo Edital: {tituloEdital}");
                        // Encontre o link correspondente ao título do edital
                        IWebElement linkLeilao = driver.FindElement(By.XPath($"//h5[text()='{tituloEdital}']/following::a[contains(@onclick, 'ListarEdital')]"));

                        // Obtenha a data do arquivo
                        string dataArquivo = driver.FindElements(By.XPath($"//h5[text()='{tituloEdital}']/following::span[contains(., '(Data do arquivo:')]")).Count > 0
                            ? driver.FindElement(By.XPath($"//h5[text()='{tituloEdital}']/following::span[contains(., '(Data do arquivo:')]")).Text
                            : string.Empty;

                        // Obtenha a quantidade de páginas para o edital atual
                        totalPages = ObterQuantidadePaginas(driver, linkLeilao);
                        _logger.LogInformation($"{totalPages} pagina(s) foram encontrada(s)");

                        // Buscar os IDs dos imóveis na página atual
                        numerosImoveisProcessados.AddRange(BuscaIdsImoveis(driver, totalPages, "Imoveis Licitacoes"));

                        // Extrai as informações do site da caixa em forma de objeto
                        dadosImoveis.AddRange(await ExtraiDadosImoveisCaixa(driver, numerosImoveisProcessados, tituloEdital));
                        _logger.LogInformation("Dados dos imoveis processados com sucesso.");

                        _logger.LogInformation("Inserindo dados do imoveis no banco");
                        foreach (DadosImovel imovelNovo in dadosImoveis)
                        {
                            try
                            {
                                _logger.LogInformation($"Inserindo imovel: {imovelNovo.nomeLoteamento} - {imovelNovo.comarca}");
                                DadosImovel imovelAux = await _imoveisLeilaoCaixaRepository.GetByIdAsync(imovelNovo.id);
                                if (imovelAux == null)
                                    await _imoveisLeilaoCaixaRepository.CreateAsync(imovelNovo);
                                else
                                { 
                                    await _imoveisLeilaoCaixaRepository.UpdateAsync(imovelNovo.id, imovelNovo);
                                }
                                _logger.LogInformation($"Inserido com sucesso");
                            }
                            catch (Exception ex)
                            {
                                throw;
                            }
                        }

                        _logger.LogInformation("Definindo objeto da licitacao");
                        TituloEditalLeilao editalLeilao = new TituloEditalLeilao();
                        editalLeilao.dataArquivoSite = ExtrairData(dataArquivo);
                        editalLeilao.titulo = tituloEdital;
                        editalLeilao.data = DateTime.Now;
                        editalLeilao.processado = true;
                        editalLeilao.totalImoveis = numerosImoveisProcessados.Count;

                        TituloEditalLeilao editalAux = await _tituloEditalRepository.GetByIdAsync(editalLeilao.titulo);

                        _logger.LogInformation("Inserindo licitacao");
                        if (editalAux == null)
                            await _tituloEditalRepository.CreateAsync(editalLeilao);
                        else
                            await _tituloEditalRepository.UpdateAsync(editalLeilao.titulo, editalLeilao);
                        _logger.LogInformation("Licitacao inserida com sucesso");

                        try
                        {
                            _logger.LogInformation("Volta para a pagina anterior para processar a proxima licitacao");
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

                            // Aguardar até que o botão "Próximo" esteja clicável, com um timeout de 10 segundos
                            //var btnNext1 = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                            //    .Until(ExpectedConditions.ElementToBeClickable(By.Id("btn_next1")));

                            // Aguarde um tempo adicional (de 10 segundos) para carregar as licitações novamente
                            Thread.Sleep(5000);
                        }
                        catch (Exception ex)
                        {
                            throw;
                        }
                    }
                    _logger.LogInformation("Todos as licitacoes ja foram processadas. Proxima execucao em 10 horas");
                    driver.Quit();
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
                //Thread.Sleep(10000);

                // Clique no botão "Próximo"
                //var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                //btnNext1.Click();

                // Aguardar até que o botão "Próximo" esteja clicável, com um timeout de 10 segundos
                var btnNext1 = new WebDriverWait(driver, TimeSpan.FromSeconds(10))
                    .Until(ExpectedConditions.ElementToBeClickable(By.Id("btn_next1")));

                // Clicar no botão "Próximo"
                btnNext1.Click();

                // Aguarde um tempo adicional (de 20 segundos) para carregar todos os imoveis
                //Thread.Sleep(20000);

                //// Encontre a div com o ID "listalicitacoes"
                //IWebElement divPrincipalLicitacoes = driver.FindElement(By.CssSelector("div#listalicitacoes"));

                // Aguardar até que a div#listalicitacoes (div principal de licitações) esteja visível, com um timeout de 10 segundos
                var divPrincipalLicitacoes = new WebDriverWait(driver, TimeSpan.FromSeconds(20))
                    .Until(ExpectedConditions.ElementIsVisible(By.CssSelector("div#listalicitacoes")));

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
                _logger.LogInformation("Buscando Id dos imoveis");
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
                    Thread.Sleep(5000);

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
                        Thread.Sleep(5000);
                    }
                }
                // Removendo Id's duplicados
                numerosImoveisProcessados = numerosImoveisProcessados.Distinct().ToList();
                if (numerosImoveisProcessados.Count > 0)
                    _logger.LogInformation($"{numerosImoveisProcessados.Count} imoveis foram encontrados");
                else
                    _logger.LogInformation($"Nenhum imovel foi encontrado");

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
                _logger.LogInformation($"Buscando os dados dos {numerosImoveisProcessados.Count} imoveis encontrados");
                List<DadosImovel> dadosImoveis = new List<DadosImovel>();

                // Itera sobre os números de imóveis processados
                int count = 0;
                foreach (var numeroImovel in numerosImoveisProcessados)
                {
                    count++;
                    _logger.LogInformation($"Id do imovel: {numeroImovel} - {count} de {numerosImoveisProcessados.Count}");
                    // Aguarde para o elemento ficar disponivel na tela
                    Thread.Sleep(1000);
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
                    imovel.numeroImovelSiteCaixa = numeroImovel;
                    dadosImoveis.Add(imovel);

                    // Após lidar com a página de detalhes, você pode voltar à lista de imóveis
                    driver.Navigate().Back();
                    log.Info("Buscando proximo imovel\n");
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
            try
            {
                DadosImovel imovel = new DadosImovel();
                _logger.LogInformation("Definindo o objeto com os dados do site");
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
                try
                {
                    _logger.LogInformation("Verificando se o endereco/CEP ja esta cadastrado");
                    if (!String.IsNullOrWhiteSpace(cep))
                    {
                        EnderecoViaCEP enderecoViaCEP = await _enderecoViaCEPRepository.GetByIdAsync(cep);
                        if (enderecoViaCEP != null)
                        {
                            _logger.LogInformation($"Endereco ja cadastrado, {enderecoViaCEP.localidade}, CEP: {enderecoViaCEP.cep}");
                            imovel.informacoesComplementares = enderecoViaCEP;
                        }                            
                        else
                        {
                            _logger.LogInformation($"CEP: {cep} nao cadastrado, consultando na API dos correios");
                            EnderecoViaCEP enderecoAux = await _viaCEPService.ConsultarCep(cep);
                            if (enderecoAux != null && !String.IsNullOrWhiteSpace(enderecoAux.cep))
                            {
                                imovel.informacoesComplementares = enderecoAux;
                                await _enderecoViaCEPRepository.CreateAsync(imovel.informacoesComplementares);
                                _logger.LogInformation("CEP consultado e gravado com sucesso.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    throw;
                }

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
                        imovel.dadosVendaImovel.LinkImagensImovel.Add(imageUrl);
                    }
                }
                imovel.dadosVendaImovel.LinkImagensImovel = imovel.dadosVendaImovel.LinkImagensImovel.Distinct().ToList();

                return imovel;
            }
            catch (Exception ex)
            {
                throw;
            }
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
        public string ExtraiLinkMatriculaImovel(IWebElement divPrincipal, string nomeCampo)
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
        public string ExtrairLinkEditalImovel(IWebElement divPrincipal, string nomeCampo)
        {
            string pattern = @"ExibeDoc\('(.+?)'\)";
            IReadOnlyCollection<IWebElement> strongElements = divPrincipal.FindElements(By.XPath($".//strong[contains(text(), '{nomeCampo}')]"));

            foreach (var strongElement in strongElements)
            {
                //IWebElement link = strongElement.FindElement(By.XPath(".//preceding::a[1]")); //.//ancestor::a"
                IWebElement link = strongElement.FindElement(By.XPath(".//ancestor::a"));
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

        #region ExtrairData
        public DateTime ExtrairData(string input)
        {
            // Expressão regular para extrair a data
            string pattern = @"(\d{2}/\d{2}/\d{4}\s+\d{2}:\d{2}:\d{2})";

            if (string.IsNullOrEmpty(input))
                return DateTime.MinValue;

            try
            {
                Match match = Regex.Match(input, pattern);
                if (match.Success)
                {
                    if (DateTime.TryParse(match.Groups[1].Value, out DateTime data))
                        return data;
                }
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
            return DateTime.MinValue;
        }
        #endregion ExtrairData

        #region ExtraiSituacao
        #endregion ExtraiSituacao
    }
}