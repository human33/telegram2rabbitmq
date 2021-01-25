using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TelegramBridge
{
    class Program
    {
        static int Main2(string[] args)
        {// Create a root command with some options
            var rootCommand = new RootCommand
            {
                new Option<string>(
                    "--telegram-token",
                    description: "Telegram bot API token"),
                new Option<string>(
                    "--rabbit-host", 
                    description: "RabbitMQ service host to connect to"),
                new Option<string>(
                    "--rabbit-queue-in", 
                    description: "RabbitMQ queue name where to put messages from telegramm"),
                new Option<string>(
                    "--rabbit-queue-out", 
                    description: "RabbitMQ queue name to read from to send to telegramm"),
                new Option<string>(
                    "--mongo-connection", 
                    description: "")
            };

            rootCommand.Description = "Telegram bridge service to connect telegram to RabbitMQ";
            
            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<string, string, string, string, string>(
                async (telegramToken, rabbitHost, rabbitQueueIn, rabbitQueueOut, mongoConnection) =>
            {
                Console.WriteLine($"The value for --telegram-token is: {telegramToken}");

                
            });

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }


        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                });
        }

    }
}
