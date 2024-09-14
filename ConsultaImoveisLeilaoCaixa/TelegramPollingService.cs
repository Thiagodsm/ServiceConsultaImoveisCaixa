using ConsultaImoveisLeilaoCaixa.Model;
using ConsultaImoveisLeilaoCaixa.Repository.Interface;
using ConsultaImoveisLeilaoCaixa.Util;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsultaImoveisLeilaoCaixa
{
    public class TelegramPollingService : BackgroundService
    {
        private readonly ILogger<TaskConsultaImoveisCaixa> _logger;
        private readonly string botToken = Config.BotToken;
        private readonly TelegramBotClient botClient;
        private readonly BlockingCollection<CommandRequest> commandQueue = new BlockingCollection<CommandRequest>();
        private readonly SemaphoreSlim commandSemaphore = new SemaphoreSlim(10); // Limita a 10 comandos simultâneos
        private readonly IImoveisLeilaoCaixaRepository _imoveisLeilaoCaixaRepository;

        #region ctor
        public TelegramPollingService(ILogger<TaskConsultaImoveisCaixa> logger, IImoveisLeilaoCaixaRepository imoveisLeilaoCaixaRepository)
        {
            botClient = new TelegramBotClient(botToken);

            // Inicia a tarefa que processa os comandos da fila
            Task.Run(() => ProcessarComandos());
            _logger = logger;
            _imoveisLeilaoCaixaRepository = imoveisLeilaoCaixaRepository;
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

                    // Verifica se e a primeira mensagem e envia os comandos disponíveis
                    if (comandoRecebido == "/start" || comandoRecebido == "/começar" || comandoRecebido == "/comecar")
                    {
                        HandleFirstMessage(chatId);
                    }
                    else
                    {
                        // Adiciona o comando a fila
                        EnqueueCommand(comandoRecebido, chatId);
                    }
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

        #region EnviarResposta
        private async Task EnviarResposta(long chatId, string resposta)
        {
            // Lógica para enviar uma resposta ao chat específico
            await botClient.SendTextMessageAsync(chatId, resposta);
        }
        #endregion EnviarResposta

        public enum ComandoIdentificador
        {
            Localidade,
            Valor,
            DataVenda,
            Desconto,
            VendaDireta,
            LicitacaoAberta
        }

        #region HandleCommand
        public async Task HandleCommand(string command, long chatId)
        {
            try
            {
                // Divide o comando em partes usando '/' como delimitador
                string[] parts = command.Split('/');

                // Verifica se o comando tem o formato correto
                if (parts.Length < 3 || parts.Length > 4)
                {
                    await botClient.SendTextMessageAsync(chatId, "Comando inválido. Use um dos seguintes formatos: " +
                        "\n/Localidade/[sigla estado]/[cidade]\n" +
                        "\n/VendaDireta/[sigla estado]/[cidade]\n" +
                        "\n/LicitacaoAberta/[sigla estado]/[cidade]\n" +
                        "\n/Valor/[sigla estado]/[cidade]\n" +
                        "\n/DataVenda/[sigla estado]/[cidade]\n" +
                        "\n/Desconto/[sigla estado]/[cidade]\n\n" +
                        "Exemplos de como começar: \n" +
                        "\n/Localidade/sp/guarujá;santos - retorna imoveis da(s) cidade(s) escolhida(s)\n" +
                        "\n/VendaDireta/sp/guarujá;cubatão - retorna as vendas diretas da(s) cidade(s) escolhida(s)\n" +
                        "\n/LicitacaoAberta/sp/guarujá - retorna as licitações abertas da cidade escolhida\n" +
                        "\n\n Os comandos abaixo listam (se encontrados) os top 5 imoveis\n" +
                        "\n/Valor/sp/praia grande;gurujá;bertioga - retorna os imoveis com menores preços\n" +
                        "\n/DataVenda/sp/praia grande;gurujá;bertioga -  retorna os imoveis com datas de vendas mais proximas\n" +
                        "\n/Desconto/sp/praia grande;gurujá;santos\n - retorna os imoveis com maiores descontos");
                    return;
                }

                // Extrai o identificador do comando
                ComandoIdentificador identificador;
                if (!Enum.TryParse(parts[1], true, out identificador))
                {
                    await botClient.SendTextMessageAsync(chatId, "Identificador de comando inválido.");
                    return;
                }

                string uf = parts[2];
                string cidades = parts.Length > 3 ? parts[3] : null;

                List<DadosImovel> imoveis = new List<DadosImovel>();
                switch (identificador)
                {
                    case ComandoIdentificador.Localidade:
                        imoveis = await _imoveisLeilaoCaixaRepository.GetByCidades(uf, cidades);
                        break;

                    case ComandoIdentificador.VendaDireta:
                        imoveis = await _imoveisLeilaoCaixaRepository.GetByVendaDireta(uf, cidades);
                        break;

                    case ComandoIdentificador.LicitacaoAberta:
                        imoveis = await _imoveisLeilaoCaixaRepository.GetByLicitacaoAberta(uf, cidades);
                        break;

                    case ComandoIdentificador.Valor:
                        imoveis = await _imoveisLeilaoCaixaRepository.GetTop5ByValor(uf, cidades);
                        break;

                    case ComandoIdentificador.DataVenda:
                        imoveis = await _imoveisLeilaoCaixaRepository.GetTop5ByDataVenda(uf, cidades);
                        break;

                    case ComandoIdentificador.Desconto:
                        imoveis = await _imoveisLeilaoCaixaRepository.GetTop5ByDesconto(uf, cidades);
                        break;

                    default:
                        await botClient.SendTextMessageAsync(chatId, "Identificador de comando não suportado.");
                        return;
                }

                if (imoveis != null && imoveis.Any())
                {
                    // Envia a resposta para o chat
                    await botClient.SendTextMessageAsync(chatId, $"{imoveis.Count} imóveis foram encontrados. Aguarde até que todos sejam processados.");

                    foreach (var imovel in imoveis)
                    {
                        string mensagem = MontaMensagamTelegram(imovel);
                        Thread.Sleep(5000);
                        bool success = await EnviarMensagemTelegram(
                            botToken,
                            chatId.ToString(),
                            mensagem,
                            imovel.dadosVendaImovel.LinkImagensImovel.FirstOrDefault());
                    }
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId, "Nenhum imóvel encontrado com base nos critérios fornecidos.");
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        #endregion HandleCommand

        #region HandleFirstMessage
        public void HandleFirstMessage(long chatId)
        {
            string message ="Olá! Bem-vindo ao bot Consulta Imóveis Leilão Caixa.\n\n" +
                            "Aqui estão os comandos disponíveis.\n\n" +
                            "\n/Localidade/[sigla estado]/[cidade]\n" +
                            "\n/VendaDireta/[sigla estado]/[cidade]\n" +
                            "\n/LicitacaoAberta/[sigla estado]/[cidade]\n" +
                            "\n/Valor/[sigla estado]/[cidade]\n" +
                            "\n/DataVenda/[sigla estado]/[cidade]\n" +
                            "\n/Desconto/[sigla estado]/[cidade]\n\n" +
                            "Exemplos de como começar: \n" +
                            "\n/Localidade/sp/guarujá;santos - retorna imoveis da(s) cidade(s) escolhida(s)\n" +
                            "\n/VendaDireta/sp/guarujá;cubatão - retorna as vendas diretas da(s) cidade(s) escolhida(s)\n" +
                            "\n/LicitacaoAberta/sp/guarujá - retorna as licitações abertas da cidade escolhida\n" +
                            "\n\n Os comandos abaixo listam (se encontrados) os top 5 imoveis\n" +
                            "\n/Valor/sp/praia grande;gurujá;bertioga - retorna os imoveis com menores preços\n" +
                            "\n/DataVenda/sp/praia grande;gurujá;bertioga -  retorna os imoveis com datas de vendas mais proximas\n" +
                            "\n/Desconto/sp/praia grande;gurujá;santos\n - retorna os imoveis com maiores descontos" +
                            "\n\nEnvie um dos comandos acima para começar.";

            botClient.SendTextMessageAsync(chatId, message);
        }
        #endregion HandleFirstMessage

        #region MontaMensagamTelegram
        public string MontaMensagamTelegram(DadosImovel imovel)
        {
            string mensagem =
                    VerificarValorNuloOuVazio(PropriedadesSite.NOME_LOTEAMENTE, imovel.nomeLoteamento) +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_AVALIACAO, imovel.valorAvaliacao, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_MINIMO_VENDA, imovel.valorMinimoVenda, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.DESCONTO, imovel.desconto, sufixo: "%") +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_MINIMO_PRIMEIRA_VENDA, imovel.valorMinimoPrimeiraVenda, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.VALOR_MINIMO_SEGUNDA_VENDA, imovel.valorMinimoSegundaVenda, "R$") +
                    VerificarValorNuloOuVazio(PropriedadesSite.TIPO_IMOVEL, imovel.tipoImovel) +
                    VerificarValorNuloOuVazio(PropriedadesSite.QUARTOS, imovel.quartos) +
                    VerificarValorNuloOuVazio(PropriedadesSite.GARAGEM, imovel.garagem) +
                    VerificarValorNuloOuVazio(PropriedadesSite.NUMERO_IMOVEL, imovel.numeroImovel) +
                    VerificarValorNuloOuVazio(PropriedadesSite.MATRICULA, imovel.matricula) +
                    VerificarValorNuloOuVazio(PropriedadesSite.COMARCA, imovel.comarca) +
                    VerificarValorNuloOuVazio(PropriedadesSite.OFICIO, imovel.oficio) +
                    VerificarValorNuloOuVazio(PropriedadesSite.INSCRICAO_IMOBILIARIA, imovel.inscricaoImobiliaria) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AVERBACAO_LEILAO_NEGATIVOS, imovel.averbacaoLeilaoNegativos) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AREA_TOTAL, imovel.areaTotal) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AREA_PRIVATIVA, imovel.areaPrivativa) +
                    VerificarValorNuloOuVazio(PropriedadesSite.AREA_TERRENO, imovel.areaTerreno) +
                    VerificarValorNuloOuVazio(PropriedadesSite.SITUACAO, imovel.situacao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.EDITAL, imovel.dadosVendaImovel.edital) +
                    VerificarValorNuloOuVazio(PropriedadesSite.NUMERO_ITEM, imovel.dadosVendaImovel.numeroItem) +
                    VerificarValorNuloOuVazio(PropriedadesSite.LEILOEIRO, imovel.dadosVendaImovel.leiloeiro) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DATA_LICITACAO_ABERTA, imovel.dadosVendaImovel.dataLicitacao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DATA_PRIMEIRO_LEILAO, imovel.dadosVendaImovel.dataPrimeiroLeilao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DATA_SEGUNDO_LEILAO, imovel.dadosVendaImovel.dataSegundoLeilao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.ENDERECO, imovel.dadosVendaImovel.endereco) +
                    VerificarValorNuloOuVazio(PropriedadesSite.DESCRICAO, imovel.dadosVendaImovel.descricao) +
                    VerificarValorNuloOuVazio(PropriedadesSite.LINK_MATRICULA_IMOVEL, imovel.dadosVendaImovel.linkMatriculaImovel) +
                    VerificarValorNuloOuVazio(PropriedadesSite.LINK_EDITAL_IMOVEL, imovel.dadosVendaImovel.linkEditalImovel) +
                    VerificarValorNuloOuVazio(PropriedadesSite.FORMAS_DE_PAGAMENTO, ConcatenarComQuebraDeLinha(imovel.dadosVendaImovel.formasDePagamento));
            return mensagem;
        }

        public string VerificarValorNuloOuVazio(string texto, string valor, string prefixo = "", string sufixo = "")
        {
            return !String.IsNullOrWhiteSpace(valor) && valor != "-" ? $"{texto}: {prefixo} {valor}{sufixo}\n" : "";
        }

        public string VerificarValorNuloOuVazio(string texto, DateTime? valor, string unidade = "")
        {
            return valor != null ? $"{texto} {unidade}: {valor}\n" : "";
        }

        public string ConcatenarComQuebraDeLinha(List<string> listaDeStrings)
        {
            StringBuilder resultado = new StringBuilder();
            foreach (string str in listaDeStrings)
            {
                resultado.Append(str);
                resultado.Append(Environment.NewLine);
            }
            return resultado.ToString();
        }
        #endregion MontaMensagamTelegram

        #region EnviarMensagemTelegram
        public async Task<bool> EnviarMensagemTelegram(string botToken, string chatId, string mensagem, string linkImagem)
        {
            try
            {
                const int maxCharacters = 1024;
                bool success = false;

                if (mensagem.Length > maxCharacters)
                {
                    // Tenta quebrar pelo "Descrição:"
                    List<string> partes = SplitMensagemPorPalavra(mensagem, PropriedadesSite.DESCRICAO + ":", maxCharacters);

                    // Se não houver "Descrição:", tenta quebrar pelo "Endereço:"
                    if (partes.Count == 1)
                    {
                        partes = SplitMensagemPorPalavra(mensagem, PropriedadesSite.ENDERECO + ":", maxCharacters);
                    }

                    // Se ainda não houver, quebra na posição 1024
                    if (partes.Count == 1)
                    {
                        partes = SplitMensagem(mensagem, maxCharacters);
                    }

                    // Envia cada parte sequencialmente
                    for (int i = 0; i < partes.Count; i++)
                    {
                        // Adiciona um identificador à parte da mensagem
                        string mensagemParte = partes.Count > 1 ? $"{i + 1}/{partes.Count} - {partes[i]}" : partes[i];

                        // Envia a parte da mensagem
                        success = await EnviarMensagemComFoto(botToken, chatId, mensagemParte, linkImagem);
                    }
                }
                else
                    success = await EnviarMensagemComFoto(botToken, chatId, mensagem, linkImagem);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion EnviarMensagemTelegram

        #region EnviarMensagemComFoto
        public async Task<bool> EnviarMensagemComFoto(string botToken, string chatId, string mensagem, string linkImagem)
        {
            try
            {
                // Crie um cliente HTTP
                using (var httpClient = new HttpClient())
                {
                    // Construa a URL para enviar a foto
                    var apiUrl = $"https://api.telegram.org/bot{botToken}/sendPhoto";

                    // Crie um formulário de conteúdo para enviar a mensagem e a foto
                    var formContent = new MultipartFormDataContent
                    {
                        { new StringContent(chatId), "chat_id" },
                        { new StringContent(mensagem), "caption" },
                        { new StringContent(linkImagem), "photo" } // Adicione a imagem como uma string URL
                    };

                    // Envie a mensagem com a foto usando o método POST
                    HttpResponseMessage response = await httpClient.PostAsync(apiUrl, formContent);
                    if (response.IsSuccessStatusCode)
                        return true;
                    else
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                throw;
            }
        }
        #endregion EnviarMensagemComFoto

        #region DivideMensagem
        private List<string> SplitMensagemPorPalavra(string mensagem, string palavra, int maxCharacters)
        {
            // Tenta quebrar o texto pela palavra específica
            int indicePalavra = mensagem.IndexOf(palavra);

            if (indicePalavra >= 0 && indicePalavra < maxCharacters)
            {
                return new List<string> { mensagem.Substring(0, indicePalavra + palavra.Length), mensagem.Substring(indicePalavra + palavra.Length) };
            }

            // Se não encontrar a palavra ou estiver além do limite, retorna a mensagem original
            return SplitMensagem(mensagem, maxCharacters);
        }

        private List<string> SplitMensagem(string mensagem, int maxCharacters)
        {
            // Divide a mensagem na posição máxima de caracteres
            return Enumerable.Range(0, mensagem.Length / maxCharacters)
                .Select(i => mensagem.Substring(i * maxCharacters, maxCharacters))
                .ToList();
        }
        #endregion DivideMensagem
    }
}