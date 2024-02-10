using ConsultaImoveisLeilaoCaixa;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Config.CaminhoArquivoImoveis = configuration.GetSection("CaminhoArquivoImoveis").Value;
Config.DbName = configuration.GetSection("DbName").Value;
Config.CollectionName = configuration.GetSection("CollectionName").Value;

DotNetEnv.Env.Load();
Config.DatabasePassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD");
Config.BotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
Config.ChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

Config.ConnectionString = configuration.GetConnectionString("MongoDB").Replace("<password>", Config.DatabasePassword);


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<TelegramPollingService>();
        services.AddHostedService<TaskConsultaImoveisCaixa>();
    })
    .Build();

await host.RunAsync();