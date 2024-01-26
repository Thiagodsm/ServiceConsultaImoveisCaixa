namespace ConsultaLicitacaoImoveisCaixa
{
    public class TaskConsultaLicitacaImoveisCaixa : BackgroundService
    {
        private readonly ILogger<TaskConsultaLicitacaImoveisCaixa> _logger;

        public TaskConsultaLicitacaImoveisCaixa(ILogger<TaskConsultaLicitacaImoveisCaixa> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}