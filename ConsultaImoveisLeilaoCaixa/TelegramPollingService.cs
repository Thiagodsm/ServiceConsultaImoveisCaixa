using ConsultaImoveisLeilaoCaixa;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

public class TelegramPollingService
{
    private readonly string botToken = Config.BotToken;
    private readonly TelegramBotClient botClient;

    public TelegramPollingService()
    {
        botClient = new TelegramBotClient(botToken);
    }

    public async Task IniciarPolling()
    {
        int offset = 0;

        while (true)
        {
            try
            {
                // Obtém as atualizações usando o método getUpdates
                Update[] updates = await botClient.GetUpdatesAsync(offset);

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

            // Aguarda por algum tempo antes de verificar novamente
            await Task.Delay(TimeSpan.FromSeconds(5));
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
                ProcessarComando(comandoRecebido);
            }
        }
    }

    private void ProcessarComando(string comando)
    {
        // Lógica para processar o comando recebido
        Console.WriteLine($"Comando recebido: {comando}");
    }
}