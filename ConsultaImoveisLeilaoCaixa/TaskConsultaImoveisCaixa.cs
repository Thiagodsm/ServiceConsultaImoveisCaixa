using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;

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
                    // Navegue para a p�gina da Caixa
                    driver.Navigate().GoToUrl("https://venda-imoveis.caixa.gov.br/sistema/busca-imovel.asp?sltTipoBusca=imoveis");

                    // Selecione o estado (SP) no dropdown
                    var estadoDropdown = new SelectElement(driver.FindElement(By.Id("cmb_estado")));
                    estadoDropdown.SelectByText("SP");

                    // Aguarde a p�gina carregar as cidades
                    var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                    wait.Until(d => d.FindElement(By.Id("cmb_cidade")));

                    // Aguarde um tempo adicional (por exemplo, 5 segundos) antes de selecionar a cidade
                    Thread.Sleep(5000);

                    // Selecione a cidade (Guaruj�) no dropdown
                    var cidadeDropdown = new SelectElement(driver.FindElement(By.Id("cmb_cidade")));
                    cidadeDropdown.SelectByText("GUARUJA");

                    // Aguarde um tempo adicional (por exemplo, 15 segundos) antes de selecionar proximo
                    Thread.Sleep(15000);

                    // Clique no bot�o "Pr�ximo"
                    var btnNext = driver.FindElement(By.Id("btn_next0"));
                    btnNext.Click();

                    // Aguarde um tempo adicional (por exemplo, 5 segundos) antes de selecionar proximo
                    Thread.Sleep(5000);

                    // Clique no bot�o "Pr�ximo"
                    var btnNext1 = driver.FindElement(By.Id("btn_next1"));
                    btnNext1.Click();

                    
                    // Adicione aqui a l�gica para lidar com a pr�xima p�gina, se necess�rio

                    // Exemplo: Capturar o t�tulo da nova p�gina
                    var newPageTitle = driver.Title;
                    Console.WriteLine($"Novo T�tulo da P�gina: {newPageTitle}");

                    // Mantenha o console aberto para manter a aplica��o em execu��o
                    Console.ReadLine();
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
    }
}