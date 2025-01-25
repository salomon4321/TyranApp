using Avalonia.ReactiveUI;
using Avalonia.Threading;
using DynamicData;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TyranApp.Network;
using TyranApp.ViewModels;
using System.Collections.Generic;
using System.Xml.Linq;

namespace TyranApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    Server? server;
    Client? client;
    Node? meNode;
    private int _nodeId = 1;
    private int _timeout = 5000;
    private int _leaderCheckInterval = 5000;
    private string _networkAddress = "127.0.0.2";
    private int _networkPort = 1234;
    private string _leaderAddress = "127.0.0.2";
    private int _leaderPort = 1234;
    private bool _connectionAvailable = false;
    private bool _isConnected = false;
    private bool _isActive = false;
    private string _log = "";

    public ObservableCollection<Node> NetworkNodes { get; } = new ObservableCollection<Node>();


    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

    public bool ConnectionAvailable
    {
        get => _connectionAvailable;
        set
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _connectionAvailable, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _connectionAvailable, value);
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _isConnected, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _isConnected, value);
            }
        }
    }

    public int NodeId
    {
        get => _nodeId;
        set
        {
            if (meNode != null)
            {
                meNode.NodeId = value;
            }
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _nodeId, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _nodeId, value);
            }
        }
    }

    public string Log
    {
        get => _log;
        set
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _log, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _log, value);
            }
        }
    }

    public int Timeout
    {
        get => _timeout;
        set
        {
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _timeout, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _timeout, value);
            }
        }
    }

    public int LeaderCheckInterval
    {
        get => _leaderCheckInterval;
        set
        {
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _leaderCheckInterval, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _leaderCheckInterval, value);
            }
        }
    }

    public string NetworkAddress
    {
        get => _networkAddress;
        set
        {
            if (meNode != null)
            {
                meNode.IpAddress = value;
            }
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _networkAddress, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _networkAddress, value);
            }
        }
    }

    public int NetworkPort
    {
        get => _networkPort;
        set
        {
            if (meNode != null)
            {
                meNode.Port = value;
            }
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _networkPort, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _networkPort, value);
            }
        }
    }

    public string LeaderAddress
    {
        get => _leaderAddress;
        set
        {
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _leaderAddress, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _leaderAddress, value);
            }
        }
    }

    public int LeaderPort
    {
        get => _leaderPort;
        set
        {
            ValidateForm();
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _leaderPort, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _leaderPort, value);
            }
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            ValidateForm();
            if (meNode != null)
            {
                meNode.IsActive = value;
            }

            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _isActive, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _isActive, value);
            }
        }
    }

    public MainViewModel()
    {
        ConnectCommand = ReactiveCommand.Create(Connect, outputScheduler: AvaloniaScheduler.Instance);
        InitializeNetworkAddress();
        meNode = new Node
        {
            NodeId = this.NodeId,
            IpAddress = this.NetworkAddress,
            Port = this.NetworkPort,
            IsLeader = false,
            IsActive = this.IsActive
        };
        NetworkNodes.Add(meNode);
    }

    private void Connect()
    {

        if (!InitializeNetworkServer())
        {
            return;
        }
        InitializeNetworkClient();
        ConnectToLeader();
    }

    private bool InitializeNetworkServer()
    {
        if(server != null) {
            AddLog("Server juz jest zainicjowany!");
            return true; 
        }

        server = new Server(_networkPort);

        server.Log += (s, e) =>
        {
            AddLog($"{e.Message}");
        };

        server.RequestReceived += async (s, e) =>
        {
            var response = await HandleMessage(e.Message);
            e.ResponseData = response;
        };

        var result = server.StartAsync();

        if (!result)
        {
            server = null;
            AddLog($"Blad inicjacji serwera.");
            return false;
        }

        IsActive = true;
        AddLog("Zainicjowano server.");
        return true;

    }

    private void InitializeNetworkClient()
    {
        if (client != null)
        {
            AddLog("Klient juz zainicjalizowany.");
            return;
        }

        client = new Client();

        client.Log += (s, e) =>
        {
            AddLog(e.Message);
        };

        AddLog($"Zainicjowano klienta.");
    }

    private async Task<string> SendMessageAsync(string address, int port, OnlineMessage message)
    {
        if (!IsActive) {
            AddLog("Proba wyslania wiadomosci zakonczyla sie niepowodzeniem. IsActive=false!");
            return "!";
        }
        string response = await client.SendAsync(address, port, message);
        return response;
    }

    private async Task SendUpdateToNode(Node updatedNode, Node receiver)
    {
        OnlineMessage message = new OnlineMessage(new object[] { System.Text.Json.JsonSerializer.Serialize(updatedNode) }, OnlineMessage.Command.UPDATE, NodeId);
        var response = await SendMessageAsync(receiver.IpAddress, receiver.Port, message);
        if (response == "!")
        {
            AddLog($"Error w trakcie wyslania UPDATE do ID:{receiver.NodeId}!");
            return;
        }
        int responseCode = int.Parse(response);
        if (responseCode < 0)
        {
            AddLog($"Otrzymano odpowiedz na UPDATE do ID:{receiver.NodeId}. Blad: {responseCode}");
            return;
        }
        if (responseCode == 0)
        {
            AddLog($"Otrzymano odpowiedz na UPDATE do ID:{receiver.NodeId}. Sukces.");
            return;
        }
    }

    private async Task SendUpdateToNodesOnList(Node updatedNode) {
        

        var tasks = new List<Task>();

        foreach (var node in NetworkNodes) {
            if(node == meNode) { continue; }
            tasks.Add(Task.Run( () => SendUpdateToNode(updatedNode, node)));
        }

        await Task.WhenAll(tasks);
    }

    private async Task SendListUpdateToNode(Node receiver)
    {


        var tasks = new List<Task>();

        foreach (var node in NetworkNodes)
        {
            if (node == meNode) { continue; }
            tasks.Add(Task.Run(() => SendUpdateToNode(node, receiver)));
        }

        await Task.WhenAll(tasks);
    }

    private async void ConnectToLeader()
    {
        if (IsConnected)
        {
            AddLog($"Wezel jest juz polaczony z liderem!");
            return;
        }

        if((NetworkAddress == LeaderAddress || LeaderAddress.Contains("127.0.0.")) && NetworkPort == LeaderPort)
        {
            meNode.IsLeader = true;
            IsConnected = true;
            AddLog("Utworzono nowa siec wezlow. Zostalem liderem.");
            return;
        }

        OnlineMessage message = new OnlineMessage(new object[] { System.Text.Json.JsonSerializer.Serialize(meNode) } , OnlineMessage.Command.CONNECT, NodeId);

        string response = await SendMessageAsync(LeaderAddress, LeaderPort, message);

        if (response == "!") {
            AddLog("Error w trakcie wyslania CONNECT!");
            return;
        }

        int responseCode = int.Parse(response);
        if (responseCode > 0) {
            AddLog($"Otrzymano odpowiedz na CONNECT. Kolizja ID. Zmiana nodeID: {responseCode}");
            NodeId = responseCode;
            AddLog("Reconnecting");
            ConnectToLeader();
            return;
        }

        if (responseCode < 0) {
            AddLog($"Otrzymano odpowiedz na CONNECT. Blad: {responseCode}");
            return;
        }

        IsConnected = true;
        AddLog("Otrzymano odpowiedz na CONNECT. Polaczono z liderem.");
    }

    private async Task<string> HandleMessage(OnlineMessage message)
    {
        string response = "";

        if (!IsActive)
        {
            AddLog($"Odebrano komunikat od ID:{message.SenderId}. Odpowiadam bledem. IsActive=false!");
            return "!";
        }

        if (message.Kod == OnlineMessage.Command.CONNECT)
        {
            return (await HandleConnect(message.Param, message.SenderId)).ToString();
        }

        if (message.Kod == OnlineMessage.Command.UPDATE)
        {
            return HandleUpdate(message.Param, message.SenderId).ToString();
        }

        return response;
    }

    private async Task<int> HandleConnect(object[] param, int senderId) {
        var sender = System.Text.Json.JsonSerializer.Deserialize<Node>(param[0].ToString());

        if (sender == null)
        {
            AddLog($"Odebrano komunikat CONNECT od ID:{senderId}. Blad parametru");
            return -1;
        }

        if (!meNode.IsLeader) {
            AddLog($"Odebrano komunikat CONNECT od ID:{senderId}. Blad, nie jestem liderem!");
            return -2;
        }

        if (NetworkNodes.Any(node => node.NodeId == senderId)) {
            AddLog($"Odebrano komunikat CONNECT od ID:{senderId}. Blad, kolizja ID: {senderId}");
            senderId = 1;
            while (NetworkNodes.Any(node => node.NodeId == senderId)){
                senderId++;
            }
            return senderId;
        }

        NetworkNodes.Add(sender);
        AddLog($"Odebrano komunikat CONNECT od ID:{senderId}. Dodano do sieci.");
        await SendUpdateToNode(meNode, sender);
        await SendUpdateToNodesOnList(sender);
        await SendListUpdateToNode(sender);
        return 0;
    }

    private int HandleUpdate(object[] param, int senderId)
    {
        var updatedNode = System.Text.Json.JsonSerializer.Deserialize<Node>(param[0].ToString());

        if (updatedNode == null || senderId <= 0)
        {
            AddLog($"Odebrano komunikat UPDATE od ID:{senderId}. Blad parametru");
            return -1;
        }

        Node? sender = NetworkNodes.FirstOrDefault(node => node.NodeId == senderId);

        if (sender == null) {
            if(senderId != updatedNode.NodeId || !updatedNode.IsLeader)
            {
                AddLog($"Odebrano komunikat UPDATE od ID:{senderId}. Blad nie ma w liscie wezla o ID: {senderId}");
                return -2;
            }
            sender = updatedNode;
        }

        if (!sender.IsLeader) {
            AddLog($"Odebrano komunikat UPDATE od ID:{senderId}. Blad, sender nie jest liderem" );
            return -3;
        }

        if (updatedNode.NodeId == meNode.NodeId)
        {
            if (updatedNode != meNode)
            {
                AddLog($"Odebrano komunikat UPDATE od ID:{senderId}. Blad, autoaktualizacja z roznica!");
                return -4;
            }

            AddLog($"Odebrano komunikat UPDATE od ID:{senderId}. Pomijam, autoaktualizacja.");
            return 0;
        }

        if(NetworkNodes.Any(node => node.NodeId == updatedNode.NodeId))
        {
            NetworkNodes.Replace(NetworkNodes.First(node => node.NodeId == updatedNode.NodeId), updatedNode);
            AddLog($"Odebrano komunikat UPDATE od ID:{senderId}. Zamieniono node o ID: {updatedNode.NodeId}");
            return 0;
        }

        NetworkNodes.Add(updatedNode);
        AddLog($"Odebrano komunikat UPDATE od ID:{senderId}. Dodano node o ID: {updatedNode.NodeId}");
        return 0;
    }

    private void InitializeNetworkAddress()
    {
        try
        {
            // Get all network interfaces
            var interfaces = NetworkInterface.GetAllNetworkInterfaces();

            // Iterate over each network interface
            foreach (var @in in interfaces)
            {
                if (@in.OperationalStatus == OperationalStatus.Up)
                {
                    // Get the IP properties for the interface
                    var ipProperties = @in.GetIPProperties();

                    // Check for a default gateway
                    var gatewayAddress = ipProperties.GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

                    if (gatewayAddress != null)
                    {
                        string? address = ipProperties.UnicastAddresses
                            .FirstOrDefault(ua => ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            ?.Address.ToString();

                        if (address != null)
                        {
                            NetworkAddress = address;
                            AddLog($"Setting IP Address: {NetworkAddress}. Interface name: {@in.Name}.");
                            return;
                        }
                    }
                }
            }
            // Jeśli interfejs Ethernet nie został znaleziony lub brak adresów
            AddLog("No valid interface found. Using address 127.0.0.1");
            NetworkAddress = "127.0.0.1";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            AddLog($"Error retrieving IP address: {ex.Message}. Using default IP 127.0.0.1.");
            NetworkAddress = "127.0.0.1";
        }
    }

    private void ValidateForm()
    {
        if (IsConnected == true) ConnectionAvailable = false;
        if (NodeId < 0) ConnectionAvailable = false;
        if (NetworkAddress.Equals(string.Empty)) ConnectionAvailable = false;
        if (NetworkPort < 1 || NetworkPort > 65535) ConnectionAvailable = false;
        if (LeaderAddress.Equals(string.Empty)) ConnectionAvailable = false;
        if (LeaderPort < 1 || LeaderPort > 65535) ConnectionAvailable = false;
        if (LeaderCheckInterval < 1) ConnectionAvailable = false;
        if (Timeout < 1) ConnectionAvailable = false;
        if (IsActive == false) ConnectionAvailable = false;
        ConnectionAvailable = true;
    }

    private void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        Log += $"[{timestamp}] {message}\n";
    }
}
