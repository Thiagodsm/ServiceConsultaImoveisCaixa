using ConsultaImoveisLeilaoCaixa;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Config.CaminhoArquivoImoveis = configuration.GetSection("CaminhoArquivoImoveis").Value;

DotNetEnv.Env.Load();

Config.BotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
Config.ChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<TelegramPollingService>();
        services.AddHostedService<TaskConsultaImoveisCaixa>();
    })
    .Build();

await host.RunAsync();