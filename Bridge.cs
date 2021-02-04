using System;
using System.Threading;

using Telegram.Bot.Args;

using RabbitMQ.Client;
using System.Text.Json;
using RabbitMQ.Client.Events;
using NLog;
using MongoDB.Driver;
using MongoDB.Bson;
using Prometheus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramBridge
{
    class Bridge : IDisposable, IHostedService
    {
        CancellationTokenSource workerCancellationTokenSource = new CancellationTokenSource();
        private ILogger<Bridge> _logger;
        private BridgeOptions _options;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            workerCancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.Run(async () => 
            {
                // try to connect in the loop
                while(true)
                {
                    if (
                        workerCancellationTokenSource.Token.IsCancellationRequested ||
                        cancellationToken.IsCancellationRequested
                    ) 
                    {
                        _logger.LogInformation("Cancellation reqiested, stopping...");
                        break;
                    }

                    try 
                    {
                        _logger.LogInformation($"Trying to connect to RabbitMQ ({_options.RabbitHost})");

                        this.ConnectTo(
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (System.Exception e)
                    {
                        _logger.LogInformation(e, $"RabbitMQ is unreachable (id:{_options.RabbitHost})");
                        await Task.Delay(1000);
                        continue;
                    }

                    break;
                }
            });
        }

        private Telegram.Bot.TelegramBotClient BotClient;

        public Bridge(ILogger<Bridge> logger, BridgeOptions options) 
        {
            _logger = logger;
            _options = options;
            
            BotClient = new Telegram.Bot.TelegramBotClient(_options.TelegramToken);

            receivedFromBroker = Metrics.CreateCounter(
                "bridge_received_from_broker",
                "Messages received from broker's \"out\" queue"
            );

            sentToTelegram = Metrics.CreateCounter(
                "bridge_sent_to_telegram",
                "Messages sent to Telegram from broker's \"out\" queue"
            );

            receivedFromTelegram = Metrics.CreateCounter(
                "bridge_received_from_telegram",
                "Messages received from Telegram"
            );

            sentToBroker = Metrics.CreateCounter(
                "bridge_sent_to_broker",
                "Messages sent to broker's \"in\" queue"
            );

            connectionInStatus = Metrics.CreateGauge(
                "connection_in_status",
                "In queue connections count"
            );
            
            connectionOutStatus = Metrics.CreateGauge(
                "connection_out_status",
                "Out queue connections count"
            );

            telegrammConnectionStatus = Metrics.CreateGauge(
                "telegramm_connection_status",
                "Show connections to telegram"
            );
        }

        private IConnection ConnectionIn { get; set; }
        private IModel ChannelIn { get; set; }



        private IConnection ConnectionOut { get; set; }
        private IModel ChannelOut { get; set; }

        private Counter receivedFromBroker;
        private Counter sentToTelegram;

        private bool SubscribedToTelegramNewMessage = false;
        private Counter receivedFromTelegram;
        private Counter sentToBroker;
        private Gauge connectionInStatus;
        private Gauge connectionOutStatus;
        private Gauge telegrammConnectionStatus;

        protected void ConnectToTelegram(CancellationToken cancellationToken)
        {
            if (!SubscribedToTelegramNewMessage)
            {
                _logger.LogDebug("Trying to connect to telegramm...");

                // listen to telegram messages
                BotClient.OnMessage += HandleTelegramMessage;

                BotClient.StartReceiving(
                    // Telegram.Bot.Types.Enums.UpdateType.Message,
                    null,
                    cancellationToken
                );

                telegrammConnectionStatus.Inc();
                SubscribedToTelegramNewMessage = true;
                _logger.LogDebug("Connected to Telegram.");
            }
            else
            {
                _logger.LogDebug("Already connected to Telegram.");
            }
        }


        // TODO: reconnect in case of network failure
        public void ConnectTo(
            CancellationToken cancellationToken
            )
        {

            _logger.LogDebug($"Connectiong to services...");

            // connect to RabbitMQ out queue

            var factory = new ConnectionFactory() { HostName = _options.RabbitHost };

            if (ConnectionIn == null || !ConnectionIn.IsOpen || 
                ChannelIn == null || !ChannelIn.IsOpen)
            {
                _logger.LogDebug($"Try to connect to RabbitMQ in queue ({_options.RabbitQueueIn})...");
                
                ConnectionIn = factory.CreateConnection();

                ConnectionIn.ConnectionShutdown += (model, ea) =>
                {
                    connectionInStatus.Dec();
                    ConnectTo(workerCancellationTokenSource.Token);
                };

                ChannelIn = ConnectionIn.CreateModel();

                ChannelIn.QueueDeclare(
                    queue: _options.RabbitQueueIn,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                ); 
                
                connectionInStatus.Inc();
                
                _logger.LogDebug("Connected to RabbitMQ in queue.");
            }
            else 
            {
                _logger.LogDebug("Connection to RabbitMQ in queue is already opened.");
            }


            // connect to RabbitMQ out queue

            if (ConnectionOut == null || !ConnectionOut.IsOpen || 
                ChannelOut == null || !ChannelOut.IsOpen) 
            {
                _logger.LogDebug($"Try to connect to RabbitMQ out queue({_options.RabbitQueueOut})...");

                ConnectionOut = factory.CreateConnection();

                ConnectionOut.ConnectionShutdown += (model, ea) =>
                {
                    connectionOutStatus.Dec();
                    ConnectTo(workerCancellationTokenSource.Token);
                };

                ChannelOut = ConnectionOut.CreateModel();

                ChannelOut.QueueDeclare(
                    queue: _options.RabbitQueueOut,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null
                );

                connectionOutStatus.Inc();   
                var consumer = new EventingBasicConsumer(ChannelOut);

                consumer.Received += async(model, ea) => await OnBrokerMessage(ea);

                ChannelOut.BasicConsume(
                    queue: _options.RabbitQueueOut,
                    autoAck: false,
                    consumer: consumer
                );

                
                _logger.LogDebug("Connected to RabbitMQ out queue.");
            } 
            else
            {
                _logger.LogDebug("Connection to RabbitMQ out queue is already opened.");
            } 

            ConnectToTelegram(cancellationToken);
        }

        private async Task OnBrokerMessage(BasicDeliverEventArgs ea)
        {   
            receivedFromBroker.Inc();

            _logger.LogDebug(
                $"Received a packet from {_options.RabbitQueueOut}"
            );

            var body = ea.Body.ToArray();
            var messageText = System.Text.Encoding.UTF8.GetString(body);
            OutMessage message = JsonSerializer.Deserialize<OutMessage>(messageText);

            _logger.LogDebug(
                "Received a text message from RabbitMQ" +
                $" ({message.Text}), chat {message.ChatId}, login {message.UserLogin}."
            );

            if (message.ChatId == default && message.UserLogin != default)
            {
                // try to get chat id by user login

                MongoClient dbClient = new MongoClient(_options.MongoConnection);
                IMongoDatabase db = dbClient.GetDatabase("bridge");
                var collection = db.GetCollection<BsonDocument>("tg_users_chats");
                var collectionFilter = new BsonDocument() { { "_id", message.UserLogin } };
                BsonDocument loginInfo = collection.Find(collectionFilter).FirstOrDefault();

                if (loginInfo != null)
                {
                    message.ChatId = loginInfo["chat_id"].AsString;
                }
                else
                {
                    _logger.LogWarning($"Chat isn't found by login {message.UserLogin}");

                    // can't handle anyway
                    ChannelOut.BasicAck(ea.DeliveryTag, multiple: false);
                }
            }

            if (message.ChatId == default)
            {
                _logger.LogWarning("Cannot send a message without chat id (it's required)");
                return;
            }

            await BotClient.SendTextMessageAsync(
                chatId: new Telegram.Bot.Types.ChatId(message.ChatId),
                text: message.Text
            );

            lock(ChannelOut) 
            {
                ChannelOut.BasicAck(ea.DeliveryTag, multiple: false);
            }
            
            sentToTelegram.Inc();
        }

        public void Dispose()
        {
            if (ChannelIn != null)
            {
                ChannelIn.Dispose();
                ChannelIn = null;
            }

            if (ConnectionOut != null)
            {
                ConnectionOut.Dispose();
                ConnectionOut = null;
            }

            if (BotClient != null)
            {
                BotClient.StopReceiving();
                BotClient = null;
            }
        }

        public async void HandleTelegramMessage(object sender, MessageEventArgs e)
        {
            receivedFromTelegram.Inc();

            _logger.LogDebug(
                "Received a text message from telegramm" +
                $" ({e.Message.Text}) in chat {e.Message.Chat.Id}."
            );


            // save chat id to database to lookup chat id by user name

            try
            {
                MongoClient dbClient = new MongoClient(_options.MongoConnection);
                IMongoDatabase db = dbClient.GetDatabase("bridge");
                var collection = db.GetCollection<BsonDocument>("tg_users_chats");
                var filter = new BsonDocument() { { "_id", e.Message.Chat.Username } };
                var data = new BsonDocument() {
                    {"_id", e.Message.Chat.Username },
                    {"chat_id", e.Message.Chat.Id.ToString() }
                };
                var options = new ReplaceOptions()
                {
                    IsUpsert = true
                };
                await collection.ReplaceOneAsync(filter: filter, replacement: data, options: options);



                var message = new InMessage(
                    text: e.Message.Text,
                    chatId: e.Message.Chat.Id.ToString(),
                    userLogin: e.Message.Chat.Username
                );
                string jsonMessage = JsonSerializer.Serialize(message);
                byte[] bytesMessage = System.Text.Encoding.UTF8.GetBytes(jsonMessage);

                lock (ChannelIn)
                {
                    ChannelIn.BasicPublish(
                        exchange: "",
                        routingKey: _options.RabbitQueueIn,
                        basicProperties: null,
                        body: bytesMessage
                    );

                    sentToBroker.Inc();
                }

                _logger.LogDebug(
                    $"Sent a message to {_options.RabbitQueueIn}, " +
                    $"json: {jsonMessage}."
                );

                await BotClient.SendTextMessageAsync(
                    chatId: e.Message.Chat,
                    text: "Hi! I will answer you asap. " +
                        $"({ChannelIn.ConsumerCount(_options.RabbitQueueIn)} peer(s) are online)"
                );
            }
            catch (System.Exception exception)
            {
                _logger.LogError(
                    exception, 
                    "Exception while handling telegram message"
                );
            }
        }

    }
}