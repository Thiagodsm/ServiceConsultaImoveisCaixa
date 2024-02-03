using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsultaImoveisLeilaoCaixa
{
    public class TelegramPollingService : BackgroundService
    {
        private readonly string botToken = Config.BotToken;
        private readonly TelegramBotClient botClient;
        private readonly BlockingCollection<CommandRequest> commandQueue = new BlockingCollection<CommandRequest>();

        public TelegramPollingService()
        {
            botClient = new TelegramBotClient(botToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Inicie a tarefa que processa os comandos da fila
            await Task.Run(() => ProcessarComandos());

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

        private void ProcessarAtualizacoes(Update[] updates)
        {
            // Lógica para processar as atualizações recebidas
            foreach (var update in updates)
            {
                if (update.Message != null && !string.IsNullOrEmpty(update.Message.Text))
                {
                    string comandoRecebido = update.Message.Text;
                    long chatId = update.Message.Chat.Id;

                    // Adiciona o comando à fila
                    EnqueueCommand(comandoRecebido, chatId);
                }
            }
        }

        private void ProcessarComandos()
        {
            foreach (var commandRequest in commandQueue.GetConsumingEnumerable())
            {
                // Lógica para processar o comando recebido
                if (DeveResponder(commandRequest.Command))
                {
                    EnviarResposta(commandRequest.ChatId, "Resposta ao comando: " + commandRequest.Command);
                }
            }
        }

        private void EnqueueCommand(string command, long chatId)
        {
            commandQueue.Add(new CommandRequest { Command = command, ChatId = chatId });
        }

        private bool DeveResponder(string command)
        {
            // Lógica para verificar se o bot deve ou não responder ao comando
            // Implemente a lógica conforme necessário
            return true;
        }

        private void EnviarResposta(long chatId, string resposta)
        {
            // Lógica para enviar uma resposta ao chat específico
            botClient.SendTextMessageAsync(chatId, resposta);
        }

        private class CommandRequest
        {
            public string Command { get; set; }
            public long ChatId { get; set; }
        }
    }
}
