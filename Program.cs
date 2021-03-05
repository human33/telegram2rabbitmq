using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace TelegramBridge
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton<BridgeOptions>(
                        (serviceProvider) => 
                        {
                            var config = hostContext.Configuration;

                            return new BridgeOptions
                            (
                                TelegramToken: config.GetValue<string>("Telegram.Token"),
                                RabbitUri: config.GetValue<string>("Rabbit.Uri"),
                                RabbitQueueIn: config.GetValue<string>("Rabbit.QueueIn"),
                                RabbitQueueOut: config.GetValue<string>("Rabbit.QueueOut"),
                                MongoConnection: config.GetValue<string>("Mongo.Connection")
                            );
                        }
                    );
                    
                    services.AddHostedService<Bridge>();
                    services.AddHostedService<MetricsHostedService>();
                });
        }

    }
}
