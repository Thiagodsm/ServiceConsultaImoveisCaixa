using ConsultaImoveisLeilaoCaixa;
using ConsultaImoveisLeilaoCaixa.Repository;
using ConsultaImoveisLeilaoCaixa.Repository.Interface;
using ConsultaImoveisLeilaoCaixa.Services;
using ConsultaImoveisLeilaoCaixa.Services.Interface;

IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

ConfigurarNLogGeral(configuration);

Config.CaminhoArquivoImoveis = configuration.GetSection("CaminhoArquivoImoveis").Value;
Config.DbName = configuration.GetSection("DbName").Value;
Config.ImoveisCollectionName = configuration.GetSection("ImoveisCollectionName").Value;
Config.EnderecoCollectionName = configuration.GetSection("EnderecoCollectionName").Value;
Config.TituloEditalCollectionName = configuration.GetSection("TituloEditalCollectionName").Value;

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
        services.AddSingleton<ITituloEditalRepository>(provider =>
        {
            return new TituloEditalRepository(
                Config.ConnectionString,
                Config.DbName,
                Config.TituloEditalCollectionName
            );
        });
        services.AddSingleton<IViaCEPService, ViaCEPService>();
        services.AddHostedService<TelegramPollingService>();
        services.AddHostedService<TaskConsultaImoveisCaixa>();
    })
    .Build();



#region ConfigurarNLogGeral
static void ConfigurarNLogGeral(IConfiguration config)
{
    var nLogConfig = new NLog.Config.LoggingConfiguration();
    var logConsultaImoveisLeilaoCaixa = new NLog.Targets.FileTarget("logfile")
    {
        FileName = $"{config.GetSection("NLog:File:Raiz").Value}\\ConsultaImoveisLeilaoCaixa\\ConsultaImoveisLeilaoCaixa.log",
        Layout = "${longdate} ${logger} - ${level:uppercase=true} - ${message} ${exception:format=ToString,StackTrace}",
        ArchiveFileName = $"{config.GetSection("NLog:File:Raiz").Value}\\ConsultaImoveisLeilaoCaixa\\arquivados\\ConsultaImoveisLeilaoCaixa.{{#}}.log",
        ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Sequence,
        ArchiveAboveSize = 10000000,
        MaxArchiveFiles = 20,
        AutoFlush = true
    };

    var logConsole = new NLog.Targets.ColoredConsoleTarget("logConsole")
    {
        Layout = "${longdate} ${logger} - ${level:uppercase=true} - ${message} ${exception:format=ToString,StackTrace}"
    };

    nLogConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logConsultaImoveisLeilaoCaixa, "logConsultaImoveisLeilaoCaixa");

    nLogConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logConsole);
    NLog.LogManager.Configuration = nLogConfig;
}
#endregion

await host.RunAsync();