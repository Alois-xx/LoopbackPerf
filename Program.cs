using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace LoopbackPerf
{
    internal class Program
    {
        /// <summary>
        /// We use ipv4
        /// </summary>
        const string LoopBackIP = "127.0.0.1";

        static int TcpPort = 4934;
        static int HttpPort = 8049;

        static int SendDataSizeMB = 4000;

        static readonly string HelpStr = "LoopbackPerf [-server] [-connect http/tcp [-SizeMB dd] [-Runs dd]]" + Environment.NewLine +
                                         "  Test loopback device performance for TCP and WebSocket connections." + Environment.NewLine +
                                        $"  LoopbackPerf creates an ipv4 server on ports {TcpPort} and {HttpPort}." + Environment.NewLine +
                                         "  You can tune settings with netsh and test the settings without the need to restart the server part." + Environment.NewLine +
                                        $"  The netsh settings are persistent and will survive reboots." + Environment.NewLine +
                                         "       netsh int ipv6|ipv4 set gl loopbackexecutionmode = adaptive | inline | worker " + Environment.NewLine +
                                         "       netsh int ipv6|ipv4 set gl loopbackworkercount = <value> " + Environment.NewLine +
                                         "       netsh int ipv6|ipv4 set gl loopbacklargemtu = enable | disable" + Environment.NewLine +
                                         "       To view curent settings use netsh int ipv6|ipv4 show global" + Environment.NewLine +
                                         "    -server               Start server which listens to connections from clients which test Http/TCP throughput." + Environment.NewLine +
                                         "    -connect http/tcp     Connect to localhost and measure throughput." + Environment.NewLine +
                                         "Examples" + Environment.NewLine +
                                         "Start Server" + Environment.NewLine +
                                         "  LoopbackPerf -server" + Environment.NewLine + 
                                         "Test TCP Loopback Performance" + Environment.NewLine  + 
                                         "  LoopbackPerf -connect tcp" + Environment.NewLine + 
                                         "Test HTTP WebSocket Performance" + Environment.NewLine +
                                         "  LoopbackPerf -connect http" + Environment.NewLine 
                                     ;


        const int ReceiveBufferSizeMB = 1;
        const int MB = 1024 * 1024;

        private Queue<string> myArgs;

        enum ProgramAction
        {
            Help,
            Server,
            HttpClient,
            TcpClient
        }

        ProgramAction myCurrentAction = ProgramAction.Help;

        /// <summary>
        /// How often the test should be performed;
        /// </summary>
        public int Runs { get; private set; } = 1;

        public Program(string[] args)
        {
            myArgs = new Queue<string>(args);
        }

        static void Main(string[] args)
        {
            Program p = new(args);

            try
            {
                p.Run();
            }
            catch(Exception e) 
            {
                p.Help();
                Console.WriteLine(e.ToString());
            }
        }

        private void Run()
        {
            if (ParseCmdLine())
            {
                switch (myCurrentAction)
                {
                    case ProgramAction.Server:
                        {
                            using Server server = new(HttpPort, TcpPort);
                            Console.WriteLine("Press Enter to exit");
                            Console.ReadLine();
                        }
                        break;
                    case ProgramAction.HttpClient:
                        for (int i = 0; i < Runs; i++)
                        {
                            ReceiveHttp().Wait();
                        }
                        break;
                    case ProgramAction.TcpClient:
                        for (int i = 0; i < Runs; i++)
                        {
                            ReceiveTcp();
                        }
                        break;
                    default:
                        Help();
                        break;
                }
            }
        }

        private void ReceiveTcp()
        {
            using var tcpClient = new TcpClient(LoopBackIP, TcpPort);
            ConfigureSocket(tcpClient.Client);
            using NetworkStream stream = tcpClient.GetStream();

            byte[] receiveBuffer = new byte[ReceiveBufferSizeMB * MB]; // 1MB

            SendHowMuchMBWeWantToReceive(SendDataSizeMB, stream);

            Stopwatch sw = Stopwatch.StartNew();

            long receivedBytes = 0;
            long totalBytes = 0;
            while ((receivedBytes = stream.Read(receiveBuffer, 0, receiveBuffer.Length)) != 0)
            {
                totalBytes += receivedBytes;
            }

            sw.Stop();
            var timeInSeconds = sw.Elapsed.TotalMilliseconds / 1000.0;

            Console.WriteLine($"TCP: Received {totalBytes / MB:F0} MB in {timeInSeconds:F2} s {(totalBytes / MB) / timeInSeconds:N0} MB/s");
        }

        private static void SendHowMuchMBWeWantToReceive(int sendDataSizeMB, NetworkStream stream)
        {
            // send an integer to the server which defines how many MB he will send us
            MemoryStream writeStream = new();
            BinaryWriter writer = new (writeStream);
            writer.Write(sendDataSizeMB);
            writer.Flush();
            writeStream.Position = 0;
            stream.Write(writeStream.ToArray(), (int) writeStream.Position, (int) writeStream.Length);
        }

        private static async Task SendHowMuchMBWeWantToReceive(int sendDataSizeMB, ClientWebSocket socket)
        {
            // send an integer to the server which defines how many MB he will send us
            MemoryStream writeStream = new();
            BinaryWriter writer = new (writeStream);
            writer.Write(sendDataSizeMB);
            writer.Flush();
            writeStream.Position = 0;
            byte[] bytes = writeStream.ToArray();
            ArraySegment<byte> segment = new (bytes, 0, (int) writeStream.Length);
            await socket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None);
        }

        private async Task ReceiveHttp()
        {
            var ws = new ClientWebSocket();

            ws.Options.SetBuffer(65536, 65536);
            await ws.ConnectAsync(new Uri($"ws://localhost:{HttpPort}/websocketserver"), CancellationToken.None);

            await SendHowMuchMBWeWantToReceive(SendDataSizeMB, ws);

         //   Console.WriteLine("Connected. Start Receiving Data ...");

            var buffer = new byte[ReceiveBufferSizeMB * MB];
            var segment = new ArraySegment<byte>(buffer);

            var sw = Stopwatch.StartNew();

            long receivedBytes = 0;
            long expectedBytes = SendDataSizeMB * 1L * MB;



            WebSocketReceiveResult result = null;

            try
            {
                while (receivedBytes < expectedBytes)
                {
                    do
                    {
                        result = ws.ReceiveAsync(segment, CancellationToken.None).Result;
                        receivedBytes += result.Count;
                    }
                    while (!result.EndOfMessage);
                }
            }
            finally
            {
              //  Console.WriteLine($"Received Bytes: {receivedBytes} vs expected: {expectedBytes}");
            }


            sw.Stop();
            var timeInSeconds = sw.Elapsed.TotalMilliseconds / 1000.0;

            double totalSizeMB = receivedBytes / MB;
            double MBPerSeconds = totalSizeMB / timeInSeconds;

            Console.WriteLine($"HTTP: Received {totalSizeMB:F0} MB in {timeInSeconds:F2} s {MBPerSeconds:N0} MB/s");
        }

        private bool ParseCmdLine()
        {
            bool lret = false;

            if (myArgs.Count == 0)
            {
                Help();
                return lret;
            }

            string currentArg;

            while (myArgs.Count > 0 && (currentArg = myArgs.Dequeue()) != null)
            {
                string lowerArg = currentArg.ToLower();
                switch (lowerArg)
                {
                    case "-server":
                        myCurrentAction = ProgramAction.Server;
                        break;
                    case "-sizemb":
                        string sizeMB = myArgs.Dequeue();
                        SendDataSizeMB = int.Parse(sizeMB);
                        break;
                    case "-runs":
                        string runsStr = myArgs.Dequeue();
                        Runs = int.Parse(runsStr);
                        break;
                    case "-connect":
                        string mode = myArgs.Dequeue();

                        myCurrentAction = mode.ToLower() switch
                        {
                            "http" => ProgramAction.HttpClient,
                            "tcp" => ProgramAction.TcpClient,
                            _ => ProgramAction.Help,
                        };
                        break;
                    default:
                        throw new NotSupportedException($"The argument {lowerArg} is not valid.");
                }
            }

            lret = true;

            return lret;
        }

        private void Help()
        {
            Console.Write(HelpStr);
        }

        void ConfigureSocket(Socket socket)
        {
            // The socket will linger for 10 seconds after 
            // Socket.Close is called.
            socket.LingerState = new LingerOption(true, 10);

            // Disable the Nagle Algorithm for this tcp socket.
            socket.NoDelay = true;

            // Set the receive buffer size to 8k
            socket.ReceiveBufferSize = 8192;

            // Set the timeout for synchronous receive methods to 
            // 1 second (1000 milliseconds.)
            socket.ReceiveTimeout = 2000;

            // Set the send buffer size to 8k.
            socket.SendBufferSize = 8192;

            // Set the timeout for synchronous send methods
            // to 1 second (1000 milliseconds.)            
            socket.SendTimeout = 2000;
        }
    }
}
