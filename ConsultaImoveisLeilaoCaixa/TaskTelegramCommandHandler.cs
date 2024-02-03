using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsultaImoveisLeilaoCaixa
{
    public class TaskTelegramCommandHandler : BackgroundService, IDisposable
    {
        private readonly ITelegramBotClient _botClient;
        private readonly BlockingCollection<string> _commandQueue = new BlockingCollection<string>();
        private readonly CancellationTokenSource _cts;

        public TaskTelegramCommandHandler(ITelegramBotClient botClient)
        {
            _botClient = botClient;
            _cts = new CancellationTokenSource();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Task.Run(() => ProcessarComandos(_cts.Token));

            int offset = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Update[] updates = await _botClient.GetUpdatesAsync(offset, cancellationToken: stoppingToken);
                    await ProcessarAtualizacoes(updates);

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

        private async Task ProcessarAtualizacoes(Update[] updates)
        {
            foreach (var update in updates)
            {
                if (update.Message != null && !string.IsNullOrEmpty(update.Message.Text))
                {
                    string comandoRecebido = update.Message.Text;
                    _commandQueue.Add(comandoRecebido);
                }
            }
        }

        private void ProcessarComandos(CancellationToken cancellationToken)
        {
            try
            {
                foreach (var comando in _commandQueue.GetConsumingEnumerable(cancellationToken))
                {
                    Console.WriteLine($"Comando recebido: {comando}");
                    // Lógica para processar o comando recebido
                    // ...
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operação cancelada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar comandos: {ex.Message}");
                // Log ou manipulação adicional de exceções, se necessário
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _cts.Cancel();
            await base.StopAsync(cancellationToken);
        }

        public void Dispose()
        {
            _cts?.Dispose();
        }
    }
}
