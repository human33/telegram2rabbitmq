using System;
using System.Threading;

using Telegram.Bot.Args;

namespace TelegramBridge
{
    class Bridge 
    {
        private Telegram.Bot.TelegramBotClient BotClient;
        
        public Bridge(string telegramToken) 
        {
            BotClient = new Telegram.Bot.TelegramBotClient(telegramToken);
        }

        public void ConnectTo(CancellationToken cancellationToken, string queueName) 
        {
            // listen to telegram messages
            BotClient.OnMessage += HandleTelegramMessage;

            BotClient.StartReceiving(
                // Telegram.Bot.Types.Enums.UpdateType.Message,
                null,
                cancellationToken
            );

            // connect to rabbitMQ

            // wire them up
        }

        public async void HandleTelegramMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine(
                $"Received a text message ({e.Message.Text}) in chat {e.Message.Chat.Id}.");

            await BotClient.SendTextMessageAsync(
                chatId: e.Message.Chat,
                text:   "I wish you luck!"
            );
        }
    }
}