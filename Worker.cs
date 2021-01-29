// using System.Threading;
// using System.Threading.Tasks;
// using Microsoft.Extensions.Hosting;
// using Microsoft.Extensions.Logging;

// namespace TelegramBridge 
// {
//     public class Worker : IHostedService
//     {
//         CancellationTokenSource workerCancellationTokenSource = new CancellationTokenSource();
//         private ILogger<Worker> _logger;

//         public Worker(ILogger<Worker> logger, IBridge bridge) 
//         {
//             _logger = logger;
//             _bridge = bridge;
//         }

//         public Task StartAsync(CancellationToken cancellationToken)
//         {
//             Task.Run(async () => 
//             {
//                 var bridge = new Bridge(telegramToken, mongoConnection);
//                 var cancellationTokenSource = new CancellationTokenSource();
                
//                 while(true)
//                 {
//                     if (workerCancellationTokenSource.Token.IsCancellationRequested) 
//                     {
//                         _logger.LogInformation("Cancellation reqiested, stopping...");
//                         break;
//                     }

//                     try 
//                     {
//                         _logger.LogInformation($"Trying to connect to RabbitMQ ({rabbitHost})");

//                         bridge.ConnectTo(
//                             cancellationToken: cancellationTokenSource.Token, 
//                             rabbitMQHost: rabbitHost, 
//                             queueNameIn: rabbitQueueIn, 
//                             queueNameOut: rabbitQueueOut
//                         );
//                     } 
//                     // TODO: catch appropriate exception
//                     catch (System.Exception e)
//                     {
//                         _logger.LogInformation(e, $"RabbitMQ is unreachable (id:{rabbitHost})");
//                         await Task.Delay(1000);
//                         continue;
//                     }

//                     break;
//                 }
//             });

//             return Task.CompletedTask;
//         }

//         public Task StopAsync(CancellationToken cancellationToken)
//         {
//             throw new System.NotImplementedException();
//         }
//     }
// }