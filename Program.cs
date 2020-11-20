using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

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

                var bridge = new Bridge(telegramToken, mongoConnection);
                var cancellationTokenSource = new CancellationTokenSource();
                
                while(true)
                {
                    try 
                    {
                        Console.WriteLine($"Trying to connect to RabbitMQ ({rabbitHost})");

                        bridge.ConnectTo(
                            cancellationToken: cancellationTokenSource.Token, 
                            rabbitMQHost: rabbitHost, 
                            queueNameIn: rabbitQueueIn, 
                            queueNameOut: rabbitQueueOut
                        );
                    } 
                    // TODO: catch appropriate exception
                    catch (System.Exception e)
                    {
                        Console.WriteLine($"RabbitMQ is unreachable (id:{rabbitHost})");
                        await Task.Delay(1000);
                        continue;
                    }

                    break;
                }

                while (Console.ReadLine() != "exit") 
                cancellationTokenSource.Cancel();
            });

            // Parse the incoming args and invoke the handler
            return rootCommand.InvokeAsync(args).Result;
        }
    }
}
