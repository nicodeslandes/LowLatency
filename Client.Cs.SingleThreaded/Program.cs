using Common;
using System;
using System.Buffers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Cs.SingleThreaded
{
    class Program
    {
        static byte[] data = new byte[65336];
        static StatisticsLogger _Stats = new StatisticsLogger();

        static int Main(string[] args)
        {
            int port = 8000;
            var host = "localhost";

            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-host":
                        host = args[++i];
                        break;
                    case "-port":
                        port = ushort.Parse(args[++i]);
                        break;
                    case var other:
                        Console.Error.WriteLine("Unknown argument: {0}", other);
                        return 1;
                }
            }

            InitialiseData();
            Thread.Sleep(1000);
            Console.WriteLine("Connecting to server {0}:{1}", host, port);
            _Stats.Start();
            var client = new TcpClient(host, port);
            HandleConnection(client).Wait();
            return 0;
        }

        private static void InitialiseData()
        {
            var rand = new Random();
            for (int i = 0; i < data.Length / 4; i++)
            {
                BitConverter.TryWriteBytes(data.AsSpan()[(i*4)..], rand.Next() % 10_000);
            }
        }

        private static async Task HandleConnection(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = ArrayPool<byte>.Shared.Rent(12);
            var latencySw = new Stopwatch();

            try
            {
                int id = 1;
                int dataOffset = 0;
                while (true)
                {
                    BitConverter.TryWriteBytes(buffer, id++);
                    data.AsSpan()[dataOffset..(dataOffset + 8)].CopyTo(buffer.AsSpan()[4..]);
                    dataOffset = (dataOffset + 8) % data.Length;
                    Log("Send request {0}: {1} + {2}", id,
                        BitConverter.ToInt32(buffer.AsSpan()[4..]), BitConverter.ToInt32(buffer.AsSpan()[8..]));
                    await stream.WriteAsync(buffer.AsMemory(0, 12));
                    _Stats.NewMessageSent(12);
                    latencySw.Restart();

                    int read = 0;
                    do
                    {
                        int r = await stream.ReadAsync(buffer.AsMemory(read, 8 - read));
                        if (r <= 0)
                        {
                            Console.WriteLine("Connection to {0} closed", client.Client.RemoteEndPoint);
                            client.Close();
                            return;
                        }
                        else
                        {
                            read += r;
                        }
                    } while (read < 8);

                    _Stats.NewMessageReceived(8, latencySw.Elapsed);
                    Log("Response: id: {0}, value: {1}",
                        BitConverter.ToInt32(buffer.AsSpan()[0..]), BitConverter.ToInt32(buffer.AsSpan()[4..]));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("IO Exception caught: {0}; closing server connection", ex.Message);
                client.Close();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        [Conditional("DEBUG")]
        static void Log(string message, params object?[] args)
        {
            Console.WriteLine(message, args);
        }
    }
}
