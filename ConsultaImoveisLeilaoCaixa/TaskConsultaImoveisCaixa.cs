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
                    // Captura o t�tulo da nova p�gina
                    var newPageTitle = driver.Title;
                    Console.WriteLine($"Novo T�tulo da P�gina: {newPageTitle}");

                    // Navegue para a p�gina da Caixa
                    driver.Navigate().GoToUrl("https://venda-imoveis.caixa.gov.br/sistema/busca-imovel.asp?sltTipoBusca=imoveis");

                    // Selecione o estado (SP) no dropdown
                    var estadoDropdown = new SelectElement(driver.FindElement(By.Id("cmb_estado")));
                    estadoDropdown.SelectByText("SP");

                    // Aguarde a p�gina carregar as cidades
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                    wait.Until(d => d.FindElement(By.Id("cmb_cidade")));

                    // Aguarde um tempo adicional (de 5 segundos) antes de selecionar a cidade
                    Thread.Sleep(5000);

                    // Selecione a cidade (Guaruj�) no dropdown
                    var cidadeDropdown = new SelectElement(driver.FindElement(By.Id("cmb_cidade")));
                    cidadeDropdown.SelectByText("GUARUJA");

                    // Aguarde um tempo adicional (de 5 segundos) antes de clicar em proximo
                    Thread.Sleep(5000);

                    // Clique no bot�o "Pr�ximo"
                    var btnNext = driver.FindElement(By.Id("btn_next0"));
                    btnNext.Click();

                    // Aguarde um tempo adicional (de 5 segundos) antes de clicar em proximo
                    Thread.Sleep(5000);

                    // Clique no bot�o "Pr�ximo"
                    var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                    btnNext1.Click();

                    // Aguarde um tempo adicional (de 20 segundos) antes de verificar a quantidade de paginas
                    Thread.Sleep(20000);

                    // Conjunto para rastrear n�meros de im�veis j� processados
                    List<string> numerosImoveisProcessados = new List<string>();

                    // Obt�m o n�mero total de p�ginas
                    var totalPages = int.Parse(driver.FindElement(By.Id("hdnQtdPag")).GetAttribute("value"));

                    for (int currentPage = 1; currentPage <= totalPages; currentPage++)
                    {
                        // L�gica para extrair detalhes de cada im�vel na p�gina atual
                        ReadOnlyCollection<IWebElement> detalhesLinks = driver.FindElements(By.CssSelector("a[onclick*='detalhe_imovel']"));

                        foreach (IWebElement detalhesLink in detalhesLinks)
                        {
                            string onclickValue = detalhesLink.GetAttribute("onclick");
                            string numeroImovel = ExtrairNumeroImovel(onclickValue);
                            //detalhesLink.Click();

                            // Adiciona o n�mero do im�vel a lista
                            numerosImoveisProcessados.Add(numeroImovel);
                        }

                        // Aguarde um tempo para a pr�xima p�gina carregar completamente
                        Thread.Sleep(10000);

                        // Navegue para a pr�xima p�gina, se houver
                        if (currentPage < totalPages)
                        {
                            // Clique no link para a pr�xima p�gina
                            driver.FindElement(By.CssSelector($"a[href='javascript:carregaListaImoveis({currentPage + 1});']")).Click();

                            // Aguarde um tempo para a pr�xima p�gina carregar completamente
                            Thread.Sleep(10000);
                        }
                    }

                    // Removendo Id's duplicados
                    numerosImoveisProcessados = numerosImoveisProcessados.Distinct().ToList();

                    List<DadosImovel> dadosImoveis = new List<DadosImovel>();

                    // Itera sobre os n�meros de im�veis processados
                    foreach (var numeroImovel in numerosImoveisProcessados)
                    {
                        // Executa o script JavaScript para chamar o m�todo detalhe_imovel com o ID correspondente
                        string script = $"detalhe_imovel({numeroImovel});";
                        ((IJavaScriptExecutor)driver).ExecuteScript(script);

                        // Aguarde um tempo para a pr�xima p�gina carregar completamente (opcional)
                        Thread.Sleep(1000);

                        // Obtenha os dados esperados
                        // Localiza a div principal que cont�m as informa��es
                        IWebElement divPrincipal = driver.FindElement(By.CssSelector("div.content-wrapper.clearfix"));

                        // Definindo o objeto a partir da div principal
                        DadosImovel imovel = DefineObjeto(driver, divPrincipal);
                        dadosImoveis.Add(imovel);

                        // Ap�s lidar com a p�gina de detalhes, voc� pode voltar � lista de im�veis
                        driver.Navigate().Back();
                    }

                    // Aguarde um tempo para voltar a pagina anterior
                    Thread.Sleep(5000);

                    // Volte para a p�gina de listagem de im�veis
                    driver.Navigate().Back();

                    driver.Quit();
                    // Adicione aqui a l�gica para lidar com a pr�xima p�gina, se necess�rio
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

            //string valorAvaliacao = divPrincipal.FindElement(By.XPath(".//p[contains(text(), 'Valor de avalia��o')]")).Text;
            IWebElement loteamento = divPrincipal.FindElement(By.CssSelector("h5"));
            imovel.nomeLoteamento = loteamento != null ? loteamento.Text : String.Empty;
            imovel.valorAvaliacao = BuscaInfoImovel(divPrincipal, "Valor de avalia��o");
            imovel.valorMinimoVenda = "";
            imovel.valorMinimoPrimeiraVenda = "";
            imovel.valorMinimoSegundaVenda = "";
            imovel.tipoImovel = BuscaInfoImovel(divPrincipal, "Tipo de im�vel");
            imovel.quartos = BuscaInfoImovel(divPrincipal, "Quartos");
            imovel.garagem = BuscaInfoImovel(divPrincipal, "Garagem");
            imovel.numeroItem = BuscaInfoImovel(divPrincipal, "N�mero do item");
            imovel.numeroImovel = BuscaInfoImovel(divPrincipal, "N�mero do im�vel");
            imovel.matricula = BuscaInfoImovel(divPrincipal, "Matr�cula(s)");
            imovel.comarca = BuscaInfoImovel(divPrincipal, "Comarca");
            imovel.oficio = BuscaInfoImovel(divPrincipal, "Of�cio");
            imovel.inscricaoImobiliaria = BuscaInfoImovel(divPrincipal, "Inscri��o imobili�ria");
            imovel.averbacaoLeilaoNegativos = BuscaInfoImovel(divPrincipal, "Averba��o dos leil�es negativos");
            imovel.areaTotal = BuscaInfoImovel(divPrincipal, "�rea total");
            imovel.areaPrivativa = BuscaInfoImovel(divPrincipal, "�rea privativa");
            imovel.areaTerreno = BuscaInfoImovel(divPrincipal, "�rea do terreno");

            imovel.dadosVendaImovel = new DadosVendaImovel();
            imovel.dadosVendaImovel.endereco = BuscaInfoVendaImovel(divPrincipal, "Endere�o");
            imovel.dadosVendaImovel.descricao = BuscaInfoVendaImovel(divPrincipal, "Descri��o");

            // Extrai informa��es com base na classe "fa-info-circle"
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