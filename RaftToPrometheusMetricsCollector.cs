using System;
using Prometheus;

namespace TelegramBridge
{
    public class RaftToPrometheusMetricsCollector: DotNext.Net.Cluster.Consensus.Raft.MetricsCollector
    {
        private readonly Gauge stateInCluster;
        private readonly Gauge lastHeartBeat;
        private readonly Gauge broadcastTime;

        public RaftToPrometheusMetricsCollector()
        {
            stateInCluster = Prometheus.Metrics.CreateGauge(
                "raft_state_in_cluster",
                "Node state in the Raft cluster (0 - undefined, 1 - follower, 2 - candidate, 3 - leader)"
            );
            
            stateInCluster.Set(1);
            
            lastHeartBeat = Prometheus.Metrics.CreateGauge(
                "raft_last_heart_beat",
                "When the last heartbeat was seen (It's a milliseconds timestamp)"
            );
            
            broadcastTime = Prometheus.Metrics.CreateGauge(
                "raft_broadcast_time",
                "Broadcast time in the cluster (in milliseconds)"
            );
        }
        
        public override void ReportBroadcastTime(TimeSpan value)
        {
            base.ReportBroadcastTime(value);
            broadcastTime.Set(value.TotalMilliseconds);
        }

        public override void MovedToCandidateState()
        {
            base.MovedToCandidateState();
            stateInCluster.Set(2);
        }

        public override void MovedToFollowerState()
        {
            base.MovedToFollowerState();
            stateInCluster.Set(1);
        }

        public override void MovedToLeaderState()
        {
            base.MovedToLeaderState();
            stateInCluster.Set(3);
        }

        public override void ReportHeartbeat()
        {
            base.ReportHeartbeat();
            lastHeartBeat.SetToCurrentTimeUtc();
        }
    }
}