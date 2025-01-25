using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;

namespace TyranApp.Network
{
    internal class Client
    {
        public event EventHandler<LogEventArgs> Log;
        private int messagesCounter = 0;
        public async Task<string> SendAsync(string ipAddress, int port, OnlineMessage message)
        {
            var peerKey = $"{ipAddress}:{port}";
            Socket clientSocket = await ConnectAsync(ipAddress, port);
            if (clientSocket == null) {
                AddLog($"Nie wyslano wiadomosci {message.Kod.ToString()} do {peerKey}");
                return "!";
            }

            int messId = messagesCounter++ % 1000;
            var clientEndpoint = clientSocket.LocalEndPoint as IPEndPoint;
            AddLog($"Utworzono socket dla messId: {messId}. Socket: {clientEndpoint.Address}:{clientEndpoint.Port}");

            message.MessId = messId;
            var messageJson = System.Text.Json.JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var buffer = new byte[1024];
            


            try
            {
                await clientSocket.SendAsync(messageBytes, SocketFlags.None);
                AddLog($"Wysłano wiadomość do {peerKey}. messID: {messId}");

                int bytesRead = await clientSocket.ReceiveAsync(buffer, SocketFlags.None);
                if (bytesRead == 0) return "!";
                var receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                AddLog($"Otrzymano odpowiedz: {receivedMessage}. messID: {messId}");
                return receivedMessage;
            }
            catch (Exception ex)
            {
                AddLog($"Błąd podczas wysyłania messID: {messId}: {ex.Message}");
                return "!";
            }
            finally
            {
                clientSocket.Close();
                AddLog($"Zamknieto polaczenie z {peerKey} dla messID: {messId}");
            }
        }

        private async Task<Socket> ConnectAsync(string ipAddress, int port)
        {
            var peerKey = $"{ipAddress}:{port}";
            try
            {
                var clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                await clientSocket.ConnectAsync(new IPEndPoint(IPAddress.Parse(ipAddress), port));

                AddLog($"Połączono z węzłem: {peerKey}");

                return clientSocket;
            }
            catch (Exception ex)
            {
                AddLog($"Nie udało się połączyć z węzłem {peerKey}: {ex.Message}");
                return null;
            }
        }

        private void AddLog(string message)
        {
            #if RELEASE
                return;
            #endif
            Log.Invoke(this, new LogEventArgs("Client message: " + message));
        }
    }
}
