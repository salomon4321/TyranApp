using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TyranApp.Network
{
    internal class Server
    {
        private int _port;
        private bool _isRunning;
        public event EventHandler<LogEventArgs> Log;
        public event EventHandler<MessageReceivedArgs> RequestReceived;

        public Server(int port)
        {
            _port = port;
        }

        public bool StartAsync()
        {
            var endpoint = new IPEndPoint(IPAddress.Any, _port);
            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _isRunning = true;

            try
            {
                serverSocket.Bind(endpoint);
                serverSocket.Listen();
                AddLog($"Węzeł nasłuchuje na porcie {_port}...");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (_isRunning)
                        {
                            var clientSocket = await serverSocket.AcceptAsync();
                            var remoteEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
                            var peerKey = $"{remoteEndPoint.Address}:{remoteEndPoint.Port}";
                            AddLog($"Otrzymano nowe polaczenie. {peerKey}");

                            _ = Task.Run(() => HandlePeerAsync(clientSocket));
                        }
                    }
                    catch (Exception ex)
                    {
                        AddLog($"Błąd serwera: {ex.Message}");
                    }
                    finally { 
                        serverSocket.Close();
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                AddLog($"Błąd serwera: {ex.Message}");
                return false;
            }
        }

        private async Task HandlePeerAsync(Socket clientSocket)
        {
            var buffer = new byte[1024];
            try
            {
                while (true)
                {
                    int bytesRead = await clientSocket.ReceiveAsync(buffer, SocketFlags.None);
                    if (bytesRead == 0) break;

                    var receivedJson = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var receivedCorrection = bytesRead > 25 ? receivedJson.Substring(0, 25) + "..." : receivedJson;
                    AddLog($"Otrzymano wiadomość: {receivedCorrection}");

                    var receivedMessage = System.Text.Json.JsonSerializer.Deserialize<OnlineMessage>(receivedJson);
                    MessageReceivedArgs args = new MessageReceivedArgs(receivedMessage);
                    RequestReceived.Invoke(this, args);

                    if (!string.IsNullOrEmpty(args.ResponseData))
                    {
                        var responseBytes = Encoding.UTF8.GetBytes(args.ResponseData);
                        await clientSocket.SendAsync(responseBytes, SocketFlags.None);
                        AddLog($"Wysłano odpowiedź: {args.ResponseData}");
                    }
                }
            }
            catch (Exception ex)
            {
                var remoteEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
                var peerKey = $"{remoteEndPoint.Address}:{remoteEndPoint.Port}";
                AddLog($"Błąd podczas obsługi węzła {peerKey}: {ex.Message}");
            }
            finally
            {
                var remoteEndPoint = clientSocket.RemoteEndPoint as IPEndPoint;
                var peerKey = $"{remoteEndPoint.Address}:{remoteEndPoint.Port}";
                clientSocket.Close();
                AddLog($"Węzeł rozłączony. { peerKey}");
            }
        }

        public void Stop() {
            _isRunning = false;
            AddLog("Listening stopped");
        }

        private void AddLog(string message)
        {
        #if RELEASE
            return;
        #endif
            Log.Invoke(this, new LogEventArgs("Server message: " + message));
        }
    }
}
