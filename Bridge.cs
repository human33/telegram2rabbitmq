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
using DotNext.Net.Cluster;
using DotNext.Net.Cluster.Consensus.Raft;
using Telegram.Bot;
using TelegramBridge.Telegram;

namespace TelegramBridge
{
    class Bridge : IDisposable, IHostedService
    {
        CancellationTokenSource workerCancellationTokenSource = new CancellationTokenSource();
        private ILogger<Bridge> _logger;
        private BridgeOptions _options;
        private readonly IRaftCluster _cluster;

        public Task StopAsync(CancellationToken cancellationToken)
        {
            workerCancellationTokenSource.Cancel();
            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                int failureDelay = 1000;
                
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
                        _logger.LogInformation($"Trying to connect to RabbitMQ ({_options.RabbitUri})");

                        this.ConnectTo(
                            cancellationToken: cancellationToken
                        );
                    }
                    catch (System.Exception e)
                    {
                        _logger.LogInformation(e, $"RabbitMQ is unreachable (id:{_options.RabbitUri}), wait {failureDelay} ms");
                        await Task.Delay(millisecondsDelay: failureDelay, cancellationToken);
                        failureDelay *= 2;
                        continue;
                    }

                    break;
                }
            }, cancellationToken);

            _cluster.LeaderChanged += (cluster, leader) =>
            {
                // this process is the leader 
                if (cluster.Leader is {IsRemote: false} &&
                    workerCancellationTokenSource.Token.IsCancellationRequested == false)
                {
                    Task.Run(async () =>
                    {
                        Update updateToProcess = null;
                        
                        // while this process the leader && it's not stopping
                        while (cluster.Leader is {IsRemote: false} && 
                            workerCancellationTokenSource.Token.IsCancellationRequested == false)
                        {
                            if (updateToProcess != null && updateToProcess.Message != null)
                            {
                                try
                                {
                                    HandleTelegramMessage(updateToProcess.Message);
                                }
                                catch (System.Exception e)
                                {
                                    _logger.LogError(e, "Failed to handle telegram message");
                                    
                                    // keep processing messages
                                    continue;
                                }
                            }
                            
                            // get the next message and confirm the last one
                            updateToProcess = await BotClient.GetAndConfirmUpdateAsync(
                                offset: updateToProcess?.UpdateId + 1,
                                timeout: _options.TelegramUpdateFrequencySec
                            );
                            
                            telegrammLastMessageReceived.SetToCurrentTimeUtc();
                        }
                        
                    }, workerCancellationTokenSource.Token);
                }
            };
        }

        private Telegram.BotClient BotClient;

        public Bridge(ILogger<Bridge> logger, BridgeOptions options, IRaftCluster cluster) 
        {
            _logger = logger;
            _options = options;
            _cluster = cluster;

            BotClient = new Telegram.BotClient(
                telegramApiBaseAddress: "https://api.telegram.org", 
                token: options.TelegramToken
            );

            receivedFromBroker = Metrics.CreateCounter(
                "bridge_received_from_broker",
                "Messages received from broker's \"out\" queue"
            );
            
            receivedFromBrokerLast = Metrics.CreateGauge(
                "bridge_received_from_broker_last",
                "When the last message was received from broker's \"out\" queue"
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
            
            sentToBrokerLast = Metrics.CreateGauge(
                "bridge_sent_to_broker_last",
                "When the last message was sent to broker's \"in\" queue"
            );

            connectionInStatus = Metrics.CreateGauge(
                "connection_in_status",
                "In queue connections count"
            );
            
            connectionOutStatus = Metrics.CreateGauge(
                "connection_out_status",
                "Out queue connections count"
            );

            telegrammLastMessageReceived = Metrics.CreateGauge(
                "telegramm_last_message_received",
                "When the last message was received"
            );
        }

        private IConnection ConnectionIn { get; set; }
        private IModel ChannelIn { get; set; }



        private IConnection ConnectionOut { get; set; }
        private IModel ChannelOut { get; set; }

        private Counter receivedFromBroker;
        private Counter sentToTelegram;

        private Counter receivedFromTelegram;
        private Counter sentToBroker;
        private Gauge connectionInStatus;
        private Gauge connectionOutStatus;
        private Gauge telegrammLastMessageReceived;
        private readonly Gauge receivedFromBrokerLast;
        private readonly Gauge sentToBrokerLast;


        // TODO: reconnect in case of network failure
        public void ConnectTo(
            CancellationToken cancellationToken
            )
        {

            _logger.LogDebug($"Connectiong to services...");

            // connect to RabbitMQ out queue

            var factory = new ConnectionFactory() { Uri = new Uri(_options.RabbitUri) };

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
        }

        private async Task OnBrokerMessage(BasicDeliverEventArgs ea)
        {
            receivedFromBroker.Inc();
            
            receivedFromBrokerLast.SetToCurrentTimeUtc();

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
                IMongoDatabase db = dbClient.GetDatabase(_options.MongoDatabase);
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

            await BotClient.SendMessageAsync(
                new MessageToSend
                (
                    ChatId: message.ChatId,
                    Text: message.Text
                )
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
        }

        public async void HandleTelegramMessage(Message message)
        {
            receivedFromTelegram.Inc();

            _logger.LogDebug(
                "Received a text message from telegramm" +
                $" ({message.Text}) in chat {message.Chat.Id}."
            );

            
            // Ensure uniqueness of messages in the outgoing queue
            
            // write message id to mongo with initial state
            // try to send the message to rabbit mq
            // if it's ok
                // update message state to 'sent' or something
                // confirm message in telegram to stop receiving it in further updates
            
            

            // save chat id to database to lookup chat id by user name

            try
            {
                MongoClient dbClient = new MongoClient(_options.MongoConnection);
                IMongoDatabase db = dbClient.GetDatabase(_options.MongoDatabase);
                var collection = db.GetCollection<BsonDocument>("tg_users_chats");
                var filter = new BsonDocument() { { "_id", message.Chat.Username } };
                var data = new BsonDocument() {
                    {"_id", message.Chat.Username },
                    {"chat_id", message.Chat.Id }
                };
                var options = new ReplaceOptions()
                {
                    IsUpsert = true
                };
                await collection.ReplaceOneAsync(filter: filter, replacement: data, options: options);



                var inMessage = new InMessage(
                    text: message.Text,
                    chatId: message.Chat.Id,
                    userLogin: message.Chat.Username
                );
                string jsonMessage = JsonSerializer.Serialize(inMessage);
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
                    sentToBrokerLast.SetToCurrentTimeUtc();
                }

                _logger.LogDebug(
                    $"Sent a message to {_options.RabbitQueueIn}, " +
                    $"json: {jsonMessage}."
                );

                await BotClient.SendMessageAsync(
                    new MessageToSend(
                        ChatId: message.Chat.Id,
                        Text: "Hi! I will answer you asap. " +
                            $"({ChannelIn.ConsumerCount(_options.RabbitQueueIn)} peer(s) are online)"
                    ) 
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