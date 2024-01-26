using ConsultaLicitacaoImoveisCaixa;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<TaskConsultaLicitacaImoveisCaixa>();
    })
    .Build();

await host.RunAsync();
