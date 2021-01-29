using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Prometheus;

namespace TelegramBridge 
{
    public class MetricsHostedService: IHostedService
    {
        KestrelMetricServer _metricServer;
        
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _metricServer = new KestrelMetricServer(port: 9090);
            _metricServer.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _metricServer.StopAsync();
        }
    }
}