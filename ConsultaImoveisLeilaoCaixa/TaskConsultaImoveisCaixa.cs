using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

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

                var edgeDriverPath = @"C:\Users\thiag\Documents\WebDriver\msedgedriver.exe";
                var driver = new EdgeDriver(edgeDriverPath);

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

                    // Aguarde um tempo adicional (de 20 segundos) antes de selecionar proximo
                    Thread.Sleep(20000);

                    // Clique no bot�o "Pr�ximo"
                    var btnNext = driver.FindElement(By.Id("btn_next0"));
                    btnNext.Click();

                    // Aguarde um tempo adicional (de 10 segundos) antes de selecionar proximo
                    Thread.Sleep(10000);

                    // Clique no bot�o "Pr�ximo"
                    var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                    btnNext1.Click();

                    // Aguarde um tempo adicional (de 5 segundos) antes de verificar a quantidade de paginas
                    Thread.Sleep(5000);

                    // Conjunto para rastrear n�meros de im�veis j� processados
                    List<string> numerosImoveisProcessados = new List<string>();

                    // Obt�m o n�mero total de p�ginas
                    var totalPages = int.Parse(driver.FindElement(By.Id("hdnQtdPag")).GetAttribute("value"));

                    for (int currentPage = 1; currentPage <= totalPages; currentPage++)
                    {
                        // L�gica para extrair detalhes de cada im�vel na p�gina atual
                        var detalhesLinks = driver.FindElements(By.CssSelector("a[onclick*='detalhe_imovel']"));

                        foreach (var detalhesLink in detalhesLinks)
                        {
                            string onclickValue = detalhesLink.GetAttribute("onclick");
                            string numeroImovel = ExtrairNumeroImovel(onclickValue);

                            //detalhesLink.Click();

                            // Adicione aqui a l�gica para lidar com a p�gina de detalhes do im�vel
                            // ...

                            
                            // Adiciona o n�mero do im�vel ao conjunto de n�meros processados
                            numerosImoveisProcessados.Add(numeroImovel);
                        }

                        // Removendo Id's duplicados
                        numerosImoveisProcessados = numerosImoveisProcessados.Distinct().ToList();

                        // Navegue para a pr�xima p�gina, se houver
                        if (currentPage < totalPages)
                        {
                            // Aguarde um tempo para a pr�xima p�gina carregar completamente
                            Thread.Sleep(5000); 

                            // Clique no link para a pr�xima p�gina
                            driver.FindElement(By.CssSelector($"a[href='javascript:carregaListaImoveis({currentPage + 1});']")).Click();
                        }
                    }

                    // Volte para a p�gina de listagem de im�veis
                    driver.Navigate().Back();

                    driver.Quit();
                    // Adicione aqui a l�gica para lidar com a pr�xima p�gina, se necess�rio
                }
                catch (Exception e)
                {
                    _logger.LogCritical(e.Message);
                    throw;
                }
                finally
                {
                    // Certifique-se de fechar o navegador quando terminar
                    driver.Quit();
                }
            }
        }

        public string ExtrairNumeroImovel(string onclickValue)
        {
            // Este � apenas um exemplo b�sico. Voc� pode precisar ajustar conforme a estrutura real do atributo onclick.
            // A ideia � extrair o n�mero do im�vel da string.

            // Aqui, estamos assumindo que o n�mero do im�vel est� entre par�nteses. 
            // Se a estrutura real for diferente, ajuste conforme necess�rio.
            int startIndex = onclickValue.IndexOf("(") + 1;
            int endIndex = onclickValue.IndexOf(")");

            if (startIndex != -1 && endIndex != -1)
            {
                return onclickValue.Substring(startIndex, endIndex - startIndex);
            }

            return null;
        }
    }
}