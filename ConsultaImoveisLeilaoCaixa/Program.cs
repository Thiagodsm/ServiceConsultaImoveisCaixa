using ConsultaImoveisLeilaoCaixa;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Config.BotToken = configuration.GetSection("Telegram:BotToken").Value;
Config.ChatId = int.Parse(configuration.GetSection("Telegram:ChatId").Value);

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<TaskConsultaImoveisCaixa>();
    })
    .Build();

await host.RunAsync();
