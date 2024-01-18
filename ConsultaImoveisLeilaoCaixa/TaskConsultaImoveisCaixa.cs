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

                    // Aguarde um tempo adicional (de 20 segundos) antes de selecionar proximo
                    Thread.Sleep(20000);

                    // Clique no botão "Próximo"
                    var btnNext = driver.FindElement(By.Id("btn_next0"));
                    btnNext.Click();

                    // Aguarde um tempo adicional (de 10 segundos) antes de selecionar proximo
                    Thread.Sleep(10000);

                    // Clique no botão "Próximo"
                    var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                    btnNext1.Click();

                    // Aguarde um tempo adicional (de 5 segundos) antes de verificar a quantidade de paginas
                    Thread.Sleep(5000);

                    // Conjunto para rastrear números de imóveis já processados
                    List<string> numerosImoveisProcessados = new List<string>();

                    // Obtém o número total de páginas
                    var totalPages = int.Parse(driver.FindElement(By.Id("hdnQtdPag")).GetAttribute("value"));

                    for (int currentPage = 1; currentPage <= totalPages; currentPage++)
                    {
                        // Lógica para extrair detalhes de cada imóvel na página atual
                        var detalhesLinks = driver.FindElements(By.CssSelector("a[onclick*='detalhe_imovel']"));

                        foreach (var detalhesLink in detalhesLinks)
                        {
                            string onclickValue = detalhesLink.GetAttribute("onclick");
                            string numeroImovel = ExtrairNumeroImovel(onclickValue);

                            //detalhesLink.Click();

                            // Adicione aqui a lógica para lidar com a página de detalhes do imóvel
                            // ...

                            
                            // Adiciona o número do imóvel ao conjunto de números processados
                            numerosImoveisProcessados.Add(numeroImovel);
                        }

                        // Removendo Id's duplicados
                        numerosImoveisProcessados = numerosImoveisProcessados.Distinct().ToList();

                        // Navegue para a próxima página, se houver
                        if (currentPage < totalPages)
                        {
                            // Aguarde um tempo para a próxima página carregar completamente
                            Thread.Sleep(5000); 

                            // Clique no link para a próxima página
                            driver.FindElement(By.CssSelector($"a[href='javascript:carregaListaImoveis({currentPage + 1});']")).Click();
                        }
                    }

                    // Volte para a página de listagem de imóveis
                    driver.Navigate().Back();

                    driver.Quit();
                    // Adicione aqui a lógica para lidar com a próxima página, se necessário
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
            // Este é apenas um exemplo básico. Você pode precisar ajustar conforme a estrutura real do atributo onclick.
            // A ideia é extrair o número do imóvel da string.

            // Aqui, estamos assumindo que o número do imóvel está entre parênteses. 
            // Se a estrutura real for diferente, ajuste conforme necessário.
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