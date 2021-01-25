using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
    public class StatisticsLogger
    {
        private long _messageReceived;
        private long _messageSent;
        private long _bytesReceived;
        private long _bytesSent;
        private long _loggingIntervalTicks = TimeSpan.FromSeconds(1).Ticks;
        private List<long> _latenciesTicks = new List<long>(200_000);
        private List<long> _latenciesTicks2 = new List<long>(200_000);

        private static readonly double TicksPerMicroseconds = TimeSpan.TicksPerMillisecond / 1000.0;

        public TimeSpan LoggingInterval
        {
            get => TimeSpan.FromTicks(Volatile.Read(ref _loggingIntervalTicks));
            set => Volatile.Write(ref _loggingIntervalTicks, value.Ticks);
        }

        public void Start()
        {
            _ = Task.Run(async () =>
            {
                var sw = Stopwatch.StartNew();
                while (true)
                {
                    await Task.Delay(LoggingInterval);
                    var messageReceived = Interlocked.Exchange(ref _messageReceived, 0);
                    var bytesReceived = Interlocked.Exchange(ref _bytesReceived, 0);
                    var messageSent = Interlocked.Exchange(ref _messageSent, 0);
                    var byteSent = Interlocked.Exchange(ref _bytesSent, 0);
                    var elapsed = sw.Elapsed;
                    sw.Restart();
                    var latencyData = "";
                    if (_latenciesTicks.Count > 0)
                    {
                        var latencies = Interlocked.Exchange(ref _latenciesTicks, Volatile.Read(ref _latenciesTicks2));
                        var min = latencies.Min() / TicksPerMicroseconds;
                        var max = latencies.Max() / TicksPerMicroseconds;
                        var avg = latencies.Average() / TicksPerMicroseconds;
                        latencies.Sort();
                        var percentile90 = latencies[(int)(latencies.Count * 0.9)] / TicksPerMicroseconds;
                        var percentile99 = latencies[(int)(latencies.Count * 0.99)] / TicksPerMicroseconds;
                        var median = latencies[(int)(latencies.Count * 0.5)] / TicksPerMicroseconds;
                        latencyData = $", latencies: min/avg/max (μs) {min:N2}/{avg:N2}/{max:N2}, med/90%/99% (μs): {median:N2}/{percentile90:N2}/{percentile99:N2}";

                        latencies.Clear();
                        Volatile.Write(ref _latenciesTicks2, latencies);
                    }

                    Console.WriteLine("Received: {0:N0} msg/s, {1:N2} MB/s; Sent: {2:N0} msg/s, {3:N2} MB/s{4}",
                        messageReceived / elapsed.TotalSeconds, bytesReceived / elapsed.TotalSeconds / 1_048_576,
                        messageSent / elapsed.TotalSeconds, byteSent / elapsed.TotalSeconds / 1_048_576, latencyData);
                }
            });
        }

        public void NewMessageSent(long messageBytes)
        {
            Interlocked.Increment(ref _messageSent);
            Interlocked.Add(ref _bytesSent, messageBytes);
        }

        public void NewMessageReceived(long messageBytes, TimeSpan? latency = null)
        {
            Interlocked.Increment(ref _messageReceived);
            Interlocked.Add(ref _bytesReceived, messageBytes);
            if (latency is TimeSpan l) _latenciesTicks.Add(l.Ticks);
        }
    }
}
