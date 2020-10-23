using System;
using System.Threading;

using Telegram.Bot.Args;

using RabbitMQ.Client;
using System.Text.Json;

namespace TelegramBridge
{
    class Bridge : IDisposable
    {
        private Telegram.Bot.TelegramBotClient BotClient;
        
        public Bridge(string telegramToken) 
        {
            BotClient = new Telegram.Bot.TelegramBotClient(telegramToken);
        }

        private IConnection Connection { get; set; }
        private IModel Channel { get; set; }
        private string QueueName;

        public void ConnectTo(
            CancellationToken cancellationToken, 
            string rabbitMQHost, 
            string queueName
            ) 
        {
            // listen to telegram messages
            BotClient.OnMessage += HandleTelegramMessage;

            BotClient.StartReceiving(
                // Telegram.Bot.Types.Enums.UpdateType.Message,
                null,
                cancellationToken
            );

            // connect to rabbitMQ
            var factory = new ConnectionFactory() { HostName = rabbitMQHost };
            Connection = factory.CreateConnection();
            Channel = Connection.CreateModel();
            QueueName = queueName;

            Channel.QueueDeclare(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );
        }

        public void Dispose()
        {
            if (Connection != null) 
            {
                Connection.Dispose();
                Connection = null;
            }


            if (Channel != null) 
            {
                Channel.Dispose();
                Channel = null;
            }

            
            if (BotClient != null) 
            {
                BotClient.StopReceiving();
                BotClient = null;
            }
        }

        public async void HandleTelegramMessage(object sender, MessageEventArgs e)
        {
            Console.WriteLine(
                $"Received a text message ({e.Message.Text}) in chat {e.Message.Chat.Id}.");

            var message = new InMessage(
                text: e.Message.Text,
                chatId: e.Message.Chat.Id.ToString(),
                userLogin: e.Message.Chat.Username
            );
            string jsonMessage = JsonSerializer.Serialize(message);
            byte[] bytesMessage = System.Text.Encoding.UTF8.GetBytes(jsonMessage);

            Channel.BasicPublish(
                exchange: "",
                routingKey: QueueName,
                basicProperties: null,
                body: bytesMessage
            );

            await BotClient.SendTextMessageAsync(
                chatId: e.Message.Chat,
                text:   "I wish you luck!"
            );
        }
    }
}