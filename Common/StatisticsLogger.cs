using System;
using System.Diagnostics;
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
                    Console.WriteLine("Received: {0:N0} msg/s, {1:N2} MB/s; Sent: {2:N0} msg/s, {3:N2} MB/s",
                        messageReceived / elapsed.TotalSeconds, bytesReceived / elapsed.TotalSeconds / 1_048_576,
                        messageSent / elapsed.TotalSeconds, byteSent / elapsed.TotalSeconds / 1_048_576);
                }
            });
        }

        public void NewMessageSent(long messageBytes)
        {
            Interlocked.Increment(ref _messageSent);
            Interlocked.Add(ref _bytesSent, messageBytes);
        }

        public void NewMessageReceived(long messageBytes)
        {
            Interlocked.Increment(ref _messageReceived);
            Interlocked.Add(ref _bytesReceived, messageBytes);
        }
    }
}
