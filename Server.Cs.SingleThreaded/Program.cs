using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Server.Cs.SingleThreaded
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            int port = 8000;
            var host = IPAddress.Any;

            for (var i = 0; i<args.Length; i++)
            {
                switch (args[i])
                {
                    case "-host":
                        host = (await Dns.GetHostAddressesAsync(args[++i])).FirstOrDefault()
                            ?? throw new Exception($"Invalid hostname: {args[i]}");
                        break;
                    case "-port":
                        port = ushort.Parse(args[++i]);
                        break;
                    case var other:
                        Console.Error.WriteLine("Unknown argument: {0}", other);
                        return 1;
                }
            }

            Console.WriteLine($"Starting server on port {port}");
            var listener = new TcpListener(host, port);
            listener.Start();
            _ = AcceptClientConnections(listener);
            Console.WriteLine("Server started");
            Console.ReadLine();
            return 0;
        }

        static async Task AcceptClientConnections(TcpListener listener)
        {
            while (true)
            {
                var tcpClient = await listener.AcceptTcpClientAsync();
                Log("New Tcp Client: {0}", tcpClient.Client.RemoteEndPoint);
                _ = Task.Run(() => HandleTcpClient(tcpClient));
            }
        }

        static async Task HandleTcpClient(TcpClient client)
        {
            var stream = client.GetStream();
            var buffer = ArrayPool<byte>.Shared.Rent(12);

            try
            {
                while (true)
                {
                    int read = 0;
                    do
                    {
                        int r = await stream.ReadAsync(buffer.AsMemory(read, 12 - read));
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
                    }
                    while (read < 12);

                    WriteResponseToBuffer(buffer);
                    await stream.WriteAsync(buffer.AsMemory(0, 8));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("IO Exception caught: {0}; closing client connection", ex.Message);
                client.Close();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            static void WriteResponseToBuffer(byte[] buffer)
            {
                Span<byte> span = buffer.AsSpan();

                var id = BitConverter.ToInt32(span[0..4]);
                var a = BitConverter.ToInt32(span[4..8]);
                var b = BitConverter.ToInt32(span[8..12]);

                var response = a + b;
                Log("New request (id:{0}): a:{1}, b:{2}; reponse: {3}", id, a, b, response);

                BitConverter.TryWriteBytes(span[4..], response);
            }
        }

        [Conditional("DEBUG")]
        static void Log(string message, params object?[] args)
        {
            Console.WriteLine(message, args);
        }
    }
}
