using ConsultaImoveisLeilaoCaixa.Model;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

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
                    #endregion Buscando Id's dos imoveis

                    #region Extraindo dados dos imoveis
                    // Removendo Id's duplicados
                    numerosImoveisProcessados = numerosImoveisProcessados.Distinct().ToList();

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

                    foreach (var item in dadosImoveis)
                    {

                    }
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e.Message);
                    if (!String.IsNullOrWhiteSpace(e.Message))
                    {
                        driver.Quit();
                        Thread.Sleep(30000);
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
            imovel.valorAvaliacao = ExtraiDadosImovel(divPrincipal, "Valor de avaliação");
            // Quando a modalidade de venda for leilao
            imovel = ExtraiValoresLeilao(divPrincipal, "Valor mínimo de venda 1º Leilão");
            // Quando a modadelidade de venda for licitação aberta, venda online ou venda direta
            imovel.valorMinimoVenda = ExtraiValorMinimoVenda(divPrincipal, "Valor mínimo de venda");

            IWebElement loteamento = divPrincipal.FindElement(By.CssSelector("h5"));
            imovel.nomeLoteamento = loteamento != null ? loteamento.Text : String.Empty;
            
            imovel.tipoImovel = ExtraiDadosImovel(divPrincipal, "Tipo de imóvel");
            imovel.quartos = ExtraiDadosImovel(divPrincipal, "Quartos");
            imovel.garagem = ExtraiDadosImovel(divPrincipal, "Garagem");
            imovel.numeroImovel = ExtraiDadosImovel(divPrincipal, "Número do imóvel");
            imovel.matricula = ExtraiDadosImovel(divPrincipal, "Matrícula(s)");
            imovel.comarca = ExtraiDadosImovel(divPrincipal, "Comarca");
            imovel.oficio = ExtraiDadosImovel(divPrincipal, "Ofício");
            imovel.inscricaoImobiliaria = ExtraiDadosImovel(divPrincipal, "Inscrição imobiliária");
            imovel.averbacaoLeilaoNegativos = ExtraiDadosImovel(divPrincipal, "Averbação dos leilões negativos");
            imovel.areaTotal = ExtraiDadosImovel(divPrincipal, "Área total");
            imovel.areaPrivativa = ExtraiDadosImovel(divPrincipal, "Área privativa");
            imovel.areaTerreno = ExtraiDadosImovel(divPrincipal, "Área do terreno");

            imovel.dadosVendaImovel = new DadosVendaImovel();
            imovel.dadosVendaImovel.edital = ExtraiDadosImovel(divPrincipal, "Edital");
            imovel.dadosVendaImovel.numeroItem = ExtraiDadosImovel(divPrincipal, "Número do item");
            imovel.dadosVendaImovel.leiloeiro = ExtraiDadosImovel(divPrincipal, "Leiloeiro(a)");
            // Quando a modalidade de venda for leilao
            imovel.dadosVendaImovel.dataPrimeiroLeilao = ExtraiDatasLeilao(divPrincipal, "Data do 1º Leilão");
            imovel.dadosVendaImovel.dataSegundoLeilao = ExtraiDatasLeilao(divPrincipal, "Data do 2º Leilão");
            // Quando a modadelidade de venda for licitação aberta
            imovel.dadosVendaImovel.dataLicitacao = ExtraiDatasLeilao(divPrincipal, "Data da Licitação Aberta");

            imovel.dadosVendaImovel.endereco = ExtraiDadosVendaImovel(divPrincipal, "Endereço");
            imovel.dadosVendaImovel.descricao = ExtraiDadosVendaImovel(divPrincipal, "Descrição");

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
            IWebElement item = divPrincipal.FindElements(By.XPath($".//span[contains(text(), '{textoProcurado}')]")).FirstOrDefault();
            if (item != null)
            {
                string textoTratado = item.Text
                    .Replace($"{textoProcurado}:", "")
                    .Replace($"{textoProcurado} = ", "")
                    .Replace($"{textoProcurado} - ", "")
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
        public string ExtraiDatasLeilao(IWebElement divPrincipal, string textoProcurado)
        {
            // Localiza a div.related-box dentro da divPrincipal
            IWebElement divRelatedBox = divPrincipal.FindElement(By.CssSelector("div.related-box"));
            IWebElement item = divRelatedBox.FindElements(By.XPath($".//span[contains(text(), '{textoProcurado}')]")).FirstOrDefault();

            if (item != null)
            {
                string textoTratado = item.Text.Replace($"{textoProcurado} - ", "");
                textoTratado = Regex.Replace(textoTratado, @"\s+", " ").Trim();
                return textoTratado;
            }
            else
                return String.Empty;
        }
        #endregion

        #region ExtraiValorMinimoVenda
        public string ExtraiValorMinimoVenda(IWebElement divPrincipal, string textoProcurado)
        {
            return "";
            IWebElement item = divPrincipal.FindElements(By.XPath($".//p[contains(text(), '{textoProcurado}')]/b")).FirstOrDefault();
            if (item != null)
            {
                // Extrai o valor mínimo de venda
                string valorMinimoVendaTexto = item.FindElement(By.XPath(".//p[contains(text(), 'Valor mínimo de venda')]/b")).Text;
                string valorMinimoVenda = valorMinimoVendaTexto.Split(' ')[2];
            }
            else
                return String.Empty;
        }
        #endregion ExtraiValorMinimoVenda

        #region ExtraiValoresLeilao
        public DadosImovel ExtraiValoresLeilao(IWebElement divPrincipal, string textoProcurado)
        {
            DadosImovel imovel = new DadosImovel();
            // Localiza a div.related-box dentro da divPrincipal
            IWebElement divRelatedBox = divPrincipal.FindElement(By.CssSelector("div.related-box"));
            IWebElement item = divRelatedBox.FindElements(By.XPath($".//p[contains(text(), '{textoProcurado}')]/b")).FirstOrDefault();

            if (item != null)
            {
                string valoresMinimosTexto = divRelatedBox.FindElement(By.XPath($".//p[contains(text(), '{textoProcurado}')]/b")).Text;

                List<string> valoresMinimosLeilao = valoresMinimosTexto.Split('\n').ToList();
                if (valoresMinimosLeilao.Count > 0)
                {
                    imovel.valorMinimoPrimeiraVenda = valoresMinimosLeilao[0].Replace("Valor mínimo de venda 1º Leilão: R$ ", "");
                    imovel.valorMinimoSegundaVenda = valoresMinimosLeilao[1].Replace("Valor mínimo de venda 2º Leilão: R$ ", "");
                    return imovel;
                }
            }
            return imovel;
        }
        #endregion ExtraiDadosLeilao
    }
}