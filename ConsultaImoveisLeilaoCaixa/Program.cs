using ConsultaImoveisLeilaoCaixa;
using ConsultaImoveisLeilaoCaixa.Repository;
using ConsultaImoveisLeilaoCaixa.Repository.Interface;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Config.CaminhoArquivoImoveis = configuration.GetSection("CaminhoArquivoImoveis").Value;
Config.DbName = configuration.GetSection("DbName").Value;
Config.ImoveisCollectionName = configuration.GetSection("ImoveisCollectionName").Value;
Config.EnderecoCollectionName = configuration.GetSection("EnderecoCollectionName").Value;

DotNetEnv.Env.Load();
Config.DatabasePassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD");
Config.BotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
Config.ChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

Config.ConnectionString = configuration.GetConnectionString("MongoDB").Replace("<password>", Config.DatabasePassword);


IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<IImoveisLeilaoCaixaRepository>(provider =>
        {
            return new ImoveisLeilaoCaixaRepository(
                Config.ConnectionString,
                Config.DbName,
                Config.ImoveisCollectionName
            );
        });
        services.AddSingleton<IEnderecoViaCEPRepository>(provider =>
        {
            return new EnderecoViaCEPRepository(
                Config.ConnectionString,
                Config.DbName,
                Config.EnderecoCollectionName
            );
        });
        services.AddHostedService<TelegramPollingService>();
        services.AddHostedService<TaskConsultaImoveisCaixa>();
    })
    .Build();

await host.RunAsync();