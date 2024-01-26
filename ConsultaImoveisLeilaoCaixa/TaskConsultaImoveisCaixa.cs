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
                    // Adicione aqui a lógica para lidar com a próxima página, se necessário
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

            //string valorAvaliacao = divPrincipal.FindElement(By.XPath(".//p[contains(text(), 'Valor de avaliação')]")).Text;
            IWebElement loteamento = divPrincipal.FindElement(By.CssSelector("h5"));
            imovel.nomeLoteamento = loteamento != null ? loteamento.Text : String.Empty;
            imovel.valorAvaliacao = BuscaInfoImovel(divPrincipal, "Valor de avaliação");
            imovel.valorMinimoVenda = "";
            imovel.valorMinimoPrimeiraVenda = "";
            imovel.valorMinimoSegundaVenda = "";
            imovel.tipoImovel = BuscaInfoImovel(divPrincipal, "Tipo de imóvel");
            imovel.quartos = BuscaInfoImovel(divPrincipal, "Quartos");
            imovel.garagem = BuscaInfoImovel(divPrincipal, "Garagem");
            imovel.numeroItem = BuscaInfoImovel(divPrincipal, "Número do item");
            imovel.numeroImovel = BuscaInfoImovel(divPrincipal, "Número do imóvel");
            imovel.matricula = BuscaInfoImovel(divPrincipal, "Matrícula(s)");
            imovel.comarca = BuscaInfoImovel(divPrincipal, "Comarca");
            imovel.oficio = BuscaInfoImovel(divPrincipal, "Ofício");
            imovel.inscricaoImobiliaria = BuscaInfoImovel(divPrincipal, "Inscrição imobiliária");
            imovel.averbacaoLeilaoNegativos = BuscaInfoImovel(divPrincipal, "Averbação dos leilões negativos");
            imovel.areaTotal = BuscaInfoImovel(divPrincipal, "Área total");
            imovel.areaPrivativa = BuscaInfoImovel(divPrincipal, "Área privativa");
            imovel.areaTerreno = BuscaInfoImovel(divPrincipal, "Área do terreno");

            imovel.dadosVendaImovel = new DadosVendaImovel();
            imovel.dadosVendaImovel.endereco = BuscaInfoVendaImovel(divPrincipal, "Endereço");
            imovel.dadosVendaImovel.descricao = BuscaInfoVendaImovel(divPrincipal, "Descrição");

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
        #endregion

        #region BuscaInfoImovel
        public string BuscaInfoImovel(IWebElement divPrincipal, string textoProcurado)
        {
            IWebElement item = divPrincipal.FindElements(By.XPath($".//span[contains(text(), '{textoProcurado}')]/strong")).FirstOrDefault();
            if (item != null)
                return item.Text;
            else
                return String.Empty;
        }
        #endregion

        #region BuscaInfoVendaImovel
        public string BuscaInfoVendaImovel(IWebElement divPrincipal, string textoProcurado)
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
        #endregion
    }
}