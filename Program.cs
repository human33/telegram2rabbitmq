using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;

namespace TelegramBridge
{
    class Program
    {
        static int Main(string[] args)
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
                    "--rabbit-queue", 
                    description: "RabbitMQ queue name")
            };

            rootCommand.Description = "Telegram bridge service to connect telegram to RabbitMQ";

            // Note that the parameters of the handler method are matched according to the names of the options
            rootCommand.Handler = CommandHandler.Create<string, string, string>((telegramToken, rabbitHost, rabbitQueue) =>
            {
                Console.WriteLine($"The value for --telegram-token is: {telegramToken}");

                var bridge = new Bridge(telegramToken);
                var cancellationTokenSource = new CancellationTokenSource();
                bridge.ConnectTo(cancellationTokenSource.Token, rabbitHost, rabbitQueue);

                Console.ReadLine();
                cancellationTokenSource.Cancel();
            });

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
