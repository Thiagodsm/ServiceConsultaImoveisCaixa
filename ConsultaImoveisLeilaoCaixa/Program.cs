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
        services.AddHostedService<TaskConsultaImoveisCaixa>();
    })
    .Build();

await host.RunAsync();

//using ConsultaImoveisLeilaoCaixa;
//using Telegram.Bot;

//var host = Host.CreateDefaultBuilder(args)
//            .ConfigureServices((hostContext, services) =>
//            {
//                IConfiguration configuration = new ConfigurationBuilder()
//                    .AddJsonFile("appsettings.json")
//                    .Build();

//                Config.CaminhoArquivoImoveis = configuration.GetSection("CaminhoArquivoImoveis").Value;

//                DotNetEnv.Env.Load();

//                Config.BotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
//                Config.ChatId = Environment.GetEnvironmentVariable("TELEGRAM_CHAT_ID");

//                // Registre o serviço ITelegramBotClient
//                services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(Config.BotToken));

//                // Adicione os serviços hospedados
//                services.AddHostedService<TaskTelegramCommandHandler>();
//                services.AddHostedService<TaskConsultaImoveisCaixa>();
//            })
//            .Build();

//var tasks = new List<Task>
//{
//    host.RunAsync(),
//    Task.Delay(5000) // Aguarda 5 segundos antes de iniciar a segunda tarefa (opcional)
//        .ContinueWith(_ => host.Services.GetRequiredService<TaskTelegramCommandHandler>().StartAsync(default))
//};

//await Task.WhenAll(tasks);