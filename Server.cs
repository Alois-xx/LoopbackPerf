using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace LoopbackPerf
{
    internal class Server : IDisposable
    {
        CancellationToken CancelToken;
        CancellationTokenSource CancelSource;
        Exception LastStartException = null;

        /// <summary>
        /// Interrupted function call e.g. closing a socket when someone is listening is raising this error.
        /// This happens during unit testing quite frequently which is the reason why we do not flag
        /// this as an error.
        /// </summary>
        const int WSAEINTR = 10004;


        public Server(int httpPort, int tcpPort)
        {
            StartTcpServer(tcpPort);
            _ = StartHttpServerAsync(httpPort);
        }

        private void StartTcpServer(int tcpPort)
        {
            if (tcpPort <= 0)
            {
                throw new ArgumentException("port must be > 0");
            }

            using Barrier untilStartedListening = new(2);
            var listener = new TcpListener(IPAddress.Any, tcpPort);
            CancelSource = new CancellationTokenSource();
            CancelToken = CancelSource.Token;
            Task.Factory.StartNew(() => StartAcceptingConnections(untilStartedListening, listener), CancelToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            if (untilStartedListening.SignalAndWait(5000) == false)
            {
                throw new InvalidOperationException($"Could not start network receiver on port {tcpPort}. LastStartException: {LastStartException}");
            }
        }


        void StartAcceptingConnections(Barrier untilStartedListening, TcpListener listener)
        {
            bool bHasWaited = false;

            try
            {
                while (!CancelToken.IsCancellationRequested)
                {
                    listener.Start();
                    if (!bHasWaited)
                    {
                        untilStartedListening.SignalAndWait();
                        bHasWaited = true;
                    }

                    var client = listener.AcceptTcpClient();

                    if (CancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    Task.Factory.StartNew(() => SendTcpData(client));
                }
            }
            catch (SocketException ex)
            {
                Debug.Print("Got SocketException in server: {0}", ex);
                LastStartException = ex;
                if (ex.ErrorCode != WSAEINTR)
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                LastStartException = ex;
                throw;
            }
        }

        void InitializeBuffer(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)'A';
            }
        }

        private void SendTcpData(TcpClient client)
        {
            using NetworkStream networkStream = client.GetStream();
            byte[] payload = new byte[1024 * 1024]; // 1 MB send buffer. Do not change or size will be wrong!

            int sendDataSizeMB = ReadSendSizeFromClient(networkStream, payload);

            InitializeBuffer(payload);

            var sw = Stopwatch.StartNew();
            // Send 4 GB of data 
            for (int i = 0; i < sendDataSizeMB; i++)   // send 1 MB of data per iteration
            {
                networkStream.Write(payload, 0, payload.Length);
            }

            sw.Stop();
            Console.WriteLine($"TCP: Sent {sendDataSizeMB} MB in {sw.Elapsed.TotalSeconds:F3} s");
        }

        private static int ReadSendSizeFromClient(NetworkStream networkStream, byte[] payload)
        {
            // read size to send in MB
            networkStream.Read(payload, 0, 4);
            BinaryReader reader = new BinaryReader(new MemoryStream(payload));
            int MBToSend = reader.ReadInt32();
            return MBToSend;
        }

        public void SendHttpData(WebSocket websocket)
        {
       //     Console.WriteLine("Start sending data ...");
            Task.Run(async () =>
            {
                var payload = new byte[1024 * 1024];  // 1 MB send buffer. Do not change or size will be wrong!
                InitializeBuffer(payload);

                int sendDataSizeMB = await ReadSendSizeFromClient(websocket);

                var segment = new ArraySegment<byte>(payload);
                double totalTime = 0;

                var stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < sendDataSizeMB; i++)  // send 1 MB of data per iteration
                {
                    await websocket.SendAsync(segment, WebSocketMessageType.Binary, true, CancellationToken.None);
                }

                stopwatch.Stop();
                totalTime = stopwatch.ElapsedMilliseconds;

                Console.WriteLine($"HTTP: Sent {sendDataSizeMB} MB in {stopwatch.Elapsed.TotalSeconds:F3} s");
            }).Wait();
       //     Console.WriteLine("Closed");
        }

        private async Task<int> ReadSendSizeFromClient(WebSocket websocket)
        {
            byte[] buffer = new byte[1024 * 1024];
            ArraySegment<byte> segment = new ArraySegment<byte>(buffer);
            WebSocketReceiveResult res =  await websocket.ReceiveAsync(segment, CancellationToken.None);
            BinaryReader reader = new BinaryReader(new MemoryStream(buffer));
            int sendSizeMB = reader.ReadInt32();
            return sendSizeMB;
        }

        public async Task StartHttpServerAsync(int httpPort)
        {
            try
            {
                HttpListener listener = new();
                listener.Prefixes.Add($"http://*:{httpPort}/websocketserver/");
                listener.Start();
                Console.WriteLine($"Start listening http on port {httpPort}");

                while (true)
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        ProcessWebSocketRequest(context);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                    else
                    {
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while starting the server {0}", ex);
            }

        }

        private async Task ProcessWebSocketRequest(HttpListenerContext context)
        {
            WebSocketContext websocketContext = null;
            try
            {
                websocketContext = await context.AcceptWebSocketAsync(null, 65536, TimeSpan.FromMinutes(3));
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
                Console.WriteLine("Exception: {0}", ex);
            }

            SendHttpData(websocketContext.WebSocket);
            await websocketContext.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Normal closing", CancellationToken.None);
        }

        public void Dispose()
        {
            CancelSource.Dispose();
        }
    }
}
