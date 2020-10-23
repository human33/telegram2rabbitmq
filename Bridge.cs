using System;
using System.Threading;

using Telegram.Bot.Args;

using RabbitMQ.Client;
using System.Text.Json;
using RabbitMQ.Client.Events;
using NLog;

namespace TelegramBridge
{
    class Bridge : IDisposable
    {
        private Telegram.Bot.TelegramBotClient BotClient;
        Logger log = LogManager.GetCurrentClassLogger();
        
        public Bridge(string telegramToken) 
        {
            BotClient = new Telegram.Bot.TelegramBotClient(telegramToken);
        }

        private IConnection ConnectionIn { get; set; }
        private IModel ChannelIn { get; set; }
        private string QueueNameIn;


        
        private IConnection ConnectionOut { get; set; }
        private IModel ChannelOut { get; set; }
        private string QueueNameOut;

        public void ConnectTo(
            CancellationToken cancellationToken, 
            string rabbitMQHost, 
            string queueNameIn,
            string queueNameOut
            ) 
        {
            log.Debug("Try to connect to telegramm...");

            // listen to telegram messages
            BotClient.OnMessage += HandleTelegramMessage;

            BotClient.StartReceiving(
                // Telegram.Bot.Types.Enums.UpdateType.Message,
                null,
                cancellationToken
            );

            log.Debug("Connected to Telegram.");


            // connect to RabbitMQ out queue
            log.Debug("Try to connect to RabbitMQ in queue...");

            var factory = new ConnectionFactory() { HostName = rabbitMQHost };
            ConnectionIn = factory.CreateConnection();
            ChannelIn = ConnectionIn.CreateModel();
            QueueNameIn = queueNameIn;

            ChannelIn.QueueDeclare(
                queue: QueueNameIn,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            log.Debug("Connected to RabbitMQ in queue.");

            // connect to RabbitMQ out queue
            
            log.Debug("Try to connect to RabbitMQ out queue...");

            ConnectionOut = factory.CreateConnection();
            ChannelOut = ConnectionOut.CreateModel();
            QueueNameOut = queueNameOut;

            ChannelOut.QueueDeclare(
                queue: QueueNameOut,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            var consumer = new EventingBasicConsumer(ChannelOut);

            consumer.Received += async (model, ea) =>
            {    

                log.Debug(
                    $"Received a packet from {QueueNameOut}"
                );

                var body = ea.Body.ToArray();
                var messageText = System.Text.Encoding.UTF8.GetString(body);
                OutMessage message = JsonSerializer.Deserialize<OutMessage>(messageText);
                
                log.Debug(
                    "Received a text message from RabbitMQ"+
                    $" ({message.Text}) in chat {message.ChatId}."
                );

                await BotClient.SendTextMessageAsync(
                    chatId: new Telegram.Bot.Types.ChatId(message.ChatId),
                    text: message.Text
                );
            };

			ChannelOut.BasicConsume(
				queue: QueueNameOut,
                autoAck: true,
                consumer: consumer
			);


            log.Debug("Connected to RabbitMQ out queue.");
        }

        public void Dispose()
        {
            if (ChannelIn != null) 
            {
                ChannelIn.Dispose();
                ChannelIn = null;
            }

            if (ConnectionIn != null) 
            {
                ConnectionIn.Dispose();
                ConnectionIn = null;
            }
            
            if (BotClient != null) 
            {
                BotClient.StopReceiving();
                BotClient = null;
            }
        }

        public async void HandleTelegramMessage(object sender, MessageEventArgs e)
        {
            log.Debug(
                "Received a text message from telegramm"+
                $" ({e.Message.Text}) in chat {e.Message.Chat.Id}."
            );

            var message = new InMessage(
                text: e.Message.Text,
                chatId: e.Message.Chat.Id.ToString(),
                userLogin: e.Message.Chat.Username
            );
            string jsonMessage = JsonSerializer.Serialize(message);
            byte[] bytesMessage = System.Text.Encoding.UTF8.GetBytes(jsonMessage);

            ChannelIn.BasicPublish(
                exchange: "",
                routingKey: QueueNameIn,
                basicProperties: null,
                body: bytesMessage
            );

            log.Debug(
                $"Sent message to {QueueNameIn}, "+
                $"json: {jsonMessage}."
            );

            await BotClient.SendTextMessageAsync(
                chatId: e.Message.Chat,
                text: "Hi! I will answer you asap. "+ 
                    $"({ChannelIn.ConsumerCount(QueueNameIn)} peer(s) online)"
            );
        }
    }
}