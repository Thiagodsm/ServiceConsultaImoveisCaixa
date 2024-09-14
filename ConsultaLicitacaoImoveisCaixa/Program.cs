using ConsultaLicitacaoImoveisCaixa;

IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

ConfigurarNLogGeral(config);

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<TaskConsultaLicitacaImoveisCaixa>();
    })
    .Build();


static void ConfigurarNLogGeral(IConfiguration config)
{
    var nLogConfig = new NLog.Config.LoggingConfiguration();
    var logDesconsolidacao_LogSantos = new NLog.Targets.FileTarget("logfile")
    {
        FileName = $"{config.GetSection("NLog:File:Raiz").Value}\\TaskDesconsolidacao_LogSantos\\TaskDesconsolidacao_LogSantos.log",
        Layout = "${longdate} ${logger} - ${level:uppercase=true} - ${message} ${exception:format=ToString,StackTrace}",
        ArchiveFileName = $"{config.GetSection("NLog:File:Raiz").Value}\\TaskDesconsolidacao_LogSantos\\arquivados\\TaskDesconsolidacao_LogSantos.{{#}}.log",
        ArchiveNumbering = NLog.Targets.ArchiveNumberingMode.Sequence,
        ArchiveAboveSize = 10000000,
        MaxArchiveFiles = 20,
        AutoFlush = true
    };

    var logConsole = new NLog.Targets.ColoredConsoleTarget("logConsole")
    {
        Layout = "${longdate} ${logger} - ${level:uppercase=true} - ${message} ${exception:format=ToString,StackTrace}"
    };

    // TaskDesconsolidacao
    nLogConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logDesconsolidacao_LogSantos, "logDesconsolidacao_LogSantos");

    nLogConfig.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, logConsole);
    NLog.LogManager.Configuration = nLogConfig;
}

await host.RunAsync();
