using ConsultaImoveisLeilaoCaixa.Model;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsultaImoveisLeilaoCaixa
{
    public class TelegramPollingService : BackgroundService
    {
        private readonly string botToken = Config.BotToken;
        private readonly TelegramBotClient botClient;
        private readonly BlockingCollection<CommandRequest> commandQueue = new BlockingCollection<CommandRequest>();
        private readonly SemaphoreSlim commandSemaphore = new SemaphoreSlim(10); // Limita a 10 comandos simultâneos

        #region ctor
        public TelegramPollingService()
        {
            botClient = new TelegramBotClient(botToken);

            // Inicia a tarefa que processa os comandos da fila
            Task.Run(() => ProcessarComandos());
        }
        #endregion ctor

        #region ExecuteAsync
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int offset = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Obtém as atualizações usando o método getUpdates
                    Update[] updates = await botClient.GetUpdatesAsync(offset, cancellationToken: stoppingToken);

                    // Processa as atualizações
                    ProcessarAtualizacoes(updates);

                    // Atualiza o offset para evitar receber as mesmas atualizações novamente
                    if (updates.Length > 0)
                    {
                        offset = updates[^1].Id + 1;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao obter atualizações do Telegram: {ex.Message}");
                }
            }
        }
        #endregion ExecuteAsync

        #region ProcessarAtualizacoes
        private void ProcessarAtualizacoes(Update[] updates)
        {
            foreach (var update in updates)
            {
                if (update.Message != null && !string.IsNullOrEmpty(update.Message.Text))
                {
                    string comandoRecebido = update.Message.Text;
                    long chatId = update.Message.Chat.Id;

                    // Verifica se é a primeira mensagem e envia os comandos disponíveis
                    if (IsFirstMessage(comandoRecebido) || comandoRecebido == "/start")
                    {
                        HandleFirstMessage(chatId);
                    }
                    else
                    {
                        // Adiciona o comando à fila
                        EnqueueCommand(comandoRecebido, chatId);
                    }

                    // Adiciona o comando à fila
                    //EnqueueCommand(comandoRecebido, chatId);
                }
            }
        }
        #endregion ProcessarAtualizacoes

        #region ProcessarComandos
        private async Task ProcessarComandos()
        {
            while (!commandQueue.IsCompleted)
            {
                if (commandQueue.TryTake(out var commandRequest, TimeSpan.FromSeconds(1)))
                {
                    await commandSemaphore.WaitAsync();

                    try
                    {
                        // Lógica para processar o comando recebido
                        //if (DeveResponder(commandRequest.Command))
                        //{
                        //    await EnviarResposta(commandRequest.ChatId, "Resposta ao comando: " + commandRequest.Command);
                        //}
                        // Lógica para processar o comando recebido
                        await HandleCommand(commandRequest.Command, commandRequest.ChatId);
                    }
                    finally
                    {
                        commandSemaphore.Release();
                    }
                }
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
        }
        #endregion ProcessarComandos

        #region EnqueueCommand
        private void EnqueueCommand(string command, long chatId)
        {
            Console.WriteLine($"Comando: {command} - chatId: {chatId}");
            commandQueue.Add(new CommandRequest { Command = command, ChatId = chatId });
        }
        #endregion EnqueueCommand

        #region DeveResponder
        private bool DeveResponder(string command)
        {
            // Lógica para verificar se o bot deve ou não responder ao comando
            return true;
        }
        #endregion DeveResponder

        #region EnviarResposta
        private async Task EnviarResposta(long chatId, string resposta)
        {
            // Lógica para enviar uma resposta ao chat específico
            await botClient.SendTextMessageAsync(chatId, resposta);
        }
        #endregion EnviarResposta

        #region HandleCommand
        public async Task HandleCommand(string command, long chatId)
        {
            string response;
            switch (command.ToLower())
            {
                case "/imoveis/estado":
                    response = "Lista de imóveis filtrada por estado";
                    // Lógica para obter e enviar a lista de imóveis filtrada por estado
                    break;
                case "/imoveis/estado/cidade":
                    response = "Lista de imóveis filtrada por estado e cidade";
                    // Lógica para obter e enviar a lista de imóveis filtrada por estado e cidade
                    break;
                case "/imoveis/valor/menor/1000":
                    response = "Lista de imóveis com valor de venda menor que 1000";
                    // Lógica para obter e enviar a lista de imóveis com valor de venda menor que 1000
                    break;
                default:
                    response = "Comando não reconhecido. Por favor, verifique e tente novamente.";
                    break;
            }

            await botClient.SendTextMessageAsync(chatId, response);
        }
        #endregion HandleCommand

        #region HandleFirstMessage
        public void HandleFirstMessage(long chatId)
        {
            string message = "Olá! Bem-vindo ao Consulta Imóveis Leilão Caixa Bot.\n\n" +
                             "Aqui estão os comandos disponíveis:\n" +
                             "/imoveis/estado - filtra imóveis por estado\n" +
                             "/imoveis/estado/cidade - filtra imóveis por estado e cidade\n" +
                             "/imoveis/valor/menor - filtra imóveis por valor\n" +
                             "/imoveis/valor/maior - filtra imóveis por valor de venda\n\n" +
                             "Envie um dos comandos acima para começar.";

            botClient.SendTextMessageAsync(chatId, message);
        }
        #endregion HandleFirstMessage

        #region IsFirstMessage
        public bool IsFirstMessage(string message)
        {
            // Verifica se a mensagem não contém nenhum comando conhecido
            return !Regex.IsMatch(message, @"\/imoveis\/(estado|valor|cidade)");
        }
        #endregion IsFirstMessage
    }
}