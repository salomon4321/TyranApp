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
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;

namespace TyranApp.ViewModels;

public class MainViewModel : ViewModelBase
{
    Server? server;
    Client? client;
    Node? meNode;
    IntervalAction? pinger;
    private bool _autoScroll = true;
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

    private bool _connectionInProgress = false;
    private int _leaderBadPingResponse = 0;
    private bool _isElectionInProgress = false;
    private bool _anyResponseFromElection = false;

    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

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
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _isConnected, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _isConnected, value);
            }
            ValidateForm();
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
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _nodeId, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _nodeId, value);
            }
            ValidateForm();
        }
    }

    private ObservableCollection<string> _logs;
    public ObservableCollection<string> Logs
    {
        get => _logs;
        set => this.RaiseAndSetIfChanged(ref _logs, value);
    }

    public bool AutoScroll {
        get => _autoScroll;
        set => this.RaiseAndSetIfChanged(ref _autoScroll, value);
    }

    public int Timeout
    {
        get => _timeout;
        set
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _timeout, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _timeout, value);
            }
            ValidateForm();
        }
    }

    public int LeaderCheckInterval
    {
        get => _leaderCheckInterval;
        set
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _leaderCheckInterval, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _leaderCheckInterval, value);
            }
            ValidateForm();
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
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _networkAddress, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _networkAddress, value);
            }
            ValidateForm();
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
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _networkPort, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _networkPort, value);
            }
            ValidateForm();
        }
    }

    public string LeaderAddress
    {
        get => _leaderAddress;
        set
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _leaderAddress, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _leaderAddress, value);
            }
            ValidateForm();
        }
    }

    public int LeaderPort
    {
        get => _leaderPort;
        set
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => this.RaiseAndSetIfChanged(ref _leaderPort, value));
            }
            else
            {
                this.RaiseAndSetIfChanged(ref _leaderPort, value);
            }
            ValidateForm();
        }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
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
            ValidateForm();
        }
    }

    public MainViewModel()
    {
        ConnectCommand = ReactiveCommand.Create(Connect, outputScheduler: AvaloniaScheduler.Instance);
        Logs = new ObservableCollection<string>();
        InitializeNetworkAddress();
        pinger = new IntervalAction(LeaderPing ,LeaderCheckInterval);
        pinger.Log += (s, e) =>
        {
            AddLog($"{e.Message}");
        };
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

    private async Task LeaderPing()
    {
        if (meNode.IsLeader)
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        TimeSpan timeout = TimeSpan.FromMilliseconds(Timeout);

        try
        {
            OnlineMessage message = new OnlineMessage( new object[0], OnlineMessage.Command.PING, NodeId);
            var leader = NetworkNodes.First(node => node.IsLeader == true);
            var rpcTask = SendMessageAsync(leader.IpAddress, leader.Port, message);

            if (await Task.WhenAny(rpcTask, Task.Delay(timeout)) == rpcTask)
            {
                stopwatch.Stop();
                var result = await rpcTask;
                if (result == "!") { 
                    AddLog($"Otrzymano odpowiedz na PING do lidera. Blad: !");
                    if(++_leaderBadPingResponse == 3)
                    {
                        _leaderBadPingResponse = 0;
                        _ = Task.Run(() => StartElection(leader.NodeId));
                    }
                }
            }
            else
            {
                stopwatch.Stop();
                AddLog($"PING lidera timed out after {stopwatch.ElapsedMilliseconds} ms.");
                if (++_leaderBadPingResponse == 3)
                {
                    _leaderBadPingResponse = 0;
                    _ = Task.Run(() => StartElection(leader.NodeId));
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            AddLog($"Error w wysylaniu PING: {ex.Message} (Elapsed time: {stopwatch.ElapsedMilliseconds} ms)");
        }
    }

    private async Task StartElection(int leaderId) {
        if (_isElectionInProgress)
        {
            AddLog("Elekcja juz trwa!");
            return;
        }
        
        if (meNode.IsLeader) {
            AddLog("Nie rozpoczne elekcji. Jestem liderem");
            return;
        }

        if(!NetworkNodes.First(node => node.NodeId == leaderId).IsLeader)
        {
            AddLog($"Nie rozpoczne elekcji. nodeID: {leaderId} nie jest moim liderem!");
            return; 
        }

        _isElectionInProgress = true;
        pinger.Stop();
        AddLog("Rozpoczynam elekcje.");

        NetworkNodes.First(node => node.IsLeader == true).IsActive = false;
        NetworkNodes.First(node => node.IsLeader == true).IsLeader = false;

        List<Task<int>> tasks = new List<Task<int>>();

        foreach (var node in NetworkNodes) {
            if(node.NodeId <= NodeId) { continue; }
            var task = Task.Run(async Task<int> () => {
                var stopwatch = Stopwatch.StartNew();
                TimeSpan timeout = TimeSpan.FromMilliseconds(Timeout);
                try
                {          
                    OnlineMessage message = new OnlineMessage(new object[] { leaderId }, OnlineMessage.Command.STARTELECT, NodeId);
                    var rpcTask = SendMessageAsync(node.IpAddress, node.Port, message);
                    if (await Task.WhenAny(rpcTask, Task.Delay(timeout)) == rpcTask)
                    {
                        stopwatch.Stop();
                        var result = await rpcTask;
                        if (result == "!")
                        {
                            AddLog($"Blad wsylania STARTELECT do nodeID: {node.NodeId}.");
                            return node.NodeId;
                        }

                        var resultCode = int.Parse(result);
                        if (resultCode < 0)
                        {
                            AddLog($"Otrzymano odpowiedz na STARTELECT od nodeID: {node.NodeId}. Blad: {resultCode}");
                        }
                        else { 
                            AddLog($"Otrzymano odpowiedz na STARTELECT od nodeID: {node.NodeId}");
                        }

                        _anyResponseFromElection = true;
                        return -node.NodeId;
                    }
                    else
                    {
                        stopwatch.Stop();
                        AddLog($"STARTELECT do nodeID: {node.NodeId} timed out after {stopwatch.ElapsedMilliseconds} ms.");
                        return node.NodeId;
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    AddLog($"Error w wysylaniu STARTELECT: {ex.Message} (Elapsed time: {stopwatch.ElapsedMilliseconds} ms)");
                    return node.NodeId;
                }
            });
            tasks.Add(task);
        }

        var results = await Task.WhenAll(tasks);

        if (!_anyResponseFromElection) {
            AddLog("Brak odpowiedzi od wyzszych nodeID. Zostalem liderem.");
            meNode.IsLeader = true;
            LeaderAddress = meNode.IpAddress;
            LeaderPort = meNode.Port;
            await SendNewElect();
            //Deaktywacja wezlow ktore nie odpowiedzialy na startelekcji
            /*foreach (var id in results) {
                if (id < 0) { continue; }
                NetworkNodes.First(node => node.NodeId == id).IsActive = false;
            }
            foreach (var node in NetworkNodes) { 
                if(node == meNode) { continue; }
                SendListUpdateToNode(node);
            }
            AddLog("Zaktualizowalem wszystkim liste wezlow.");*/
        }
        else
        {
            _anyResponseFromElection = false;
        }


        AddLog("Koniec elekcji.");
        _leaderBadPingResponse = 0;
        pinger.Start();
        _isElectionInProgress = false;
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

        server.RequestReceived += (s, e) =>
        {
            var response = HandleMessage(e.Message).GetAwaiter().GetResult();
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

    private async Task SendNewElect() {
        List<Task> tasks = new List<Task>();
        OnlineMessage message = new OnlineMessage(new object[0], OnlineMessage.Command.NEWELECT, NodeId);

        foreach (var node in NetworkNodes) {
            if (node.NodeId >= NodeId) { continue; }
            var task = Task.Run(async () => {
                var response = await SendMessageAsync(node.IpAddress, node.Port, message);
                if (response == "!") {
                    AddLog($"Error w trakcie wyslania NEWELECT do nodeID:{node.NodeId}! Uznaje go za nieaktywny!");
                    node.IsActive = false;
                    return;
                }

                var resultCode = int.Parse(response);
                if (resultCode < 0) {
                    AddLog($"Otrzymano odpowiedz na NEWELECT od nodeID: {node.NodeId}. Blad: {resultCode}");
                    return;
                }

                AddLog($"Otrzymano odpowiedz na NEWELECT od nodeID: {node.NodeId}");
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);
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

    private void SendListUpdateToNode(Node receiver)
    {
        foreach (var node in NetworkNodes)
        {
            if (node == meNode) { continue; }
            SendUpdateToNode(node, receiver).GetAwaiter().GetResult();
        }
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

        _connectionInProgress = true;
        string response = await SendMessageAsync(LeaderAddress, LeaderPort, message);
        _connectionInProgress = false;

        if (response == "!") {
            AddLog("Error w trakcie wyslania CONNECT!");
            return;
        }

        int responseCode = int.Parse(response);
        if (responseCode > 0) {
            AddLog($"Otrzymano odpowiedz na CONNECT. Kolizja ID. Zmiana nodeID: {responseCode}");
            NodeId = responseCode;
            meNode.NodeId = responseCode;
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
        pinger.Start();
    }

    private async Task<string> HandleMessage(OnlineMessage message)
    {
        string response = "";

        if (!IsActive)
        {
            AddLog($"Odebrano komunikat od ID:{message.SenderId}. Odpowiadam bledem. IsActive=false!");
            return "!";
        }

        if (!IsConnected && !_connectionInProgress)
        {
            AddLog($"Odebrano komunikat od ID:{message.SenderId}. Odpowiadam bledem. IsConnected=false!");
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

        if (message.Kod == OnlineMessage.Command.PING)
        {
            return HandlePing(message.SenderId).ToString();
        }

        if (message.Kod == OnlineMessage.Command.STARTELECT)
        {
            return HandleStartElect(message.Param, message.SenderId).ToString();
        }

        if (message.Kod == OnlineMessage.Command.NEWELECT)
        {
            return HandleNewElect(message.SenderId).ToString();
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

        AddLog($"Odebrano komunikat CONNECT od ID:{senderId}. Dodano do sieci.");
        await SendUpdateToNode(meNode, sender);
        await SendUpdateToNodesOnList(sender);
        SendListUpdateToNode(sender);
        NetworkNodes.Add(sender);
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

    private int HandlePing(int senderId) {
        AddLog($"Otrzymano wiadomosc PING od {senderId}.");
        return 0;
    }

    private int HandleStartElect(object[] param, int senderId) {
        if (meNode.IsLeader) {
            AddLog($"Otrzymano STARTELECT od nodeID: {senderId}. Blad: Jestem liderem!");
            return -1;
        }

        int leaderId;

        try
        {
            leaderId = int.Parse(param[0].ToString());
        }
        catch (Exception ex) {
            AddLog($"ERROR!!!!! Mam cie kurwo: {ex.Message}");
            return  -8888;
        }

        if (leaderId == 0) {
            AddLog($"Otrzymano STARTELECT od nodeID: {senderId}. Blad: Zly parametr!");
            return -2;
        }

        var leaderDead = NetworkNodes.First(node => node.NodeId == leaderId);

        if (leaderDead == null) {
            AddLog($"Otrzymano STARTELECT od nodeID: {senderId}. Blad: Nie znaleziono nodeID: {leaderId}");
            return -3;
        }

        if (!leaderDead.IsActive) {
            AddLog($"Otrzymano STARTELECT od nodeID: {senderId}. Blad: Lider nodeID: {leaderId} juz nie zyje!");
            return -4;
        }

        AddLog($"Otrzymano STARTELECT od nodeID: {senderId}");
        _ = Task.Run(() => StartElection(leaderDead.NodeId));
        return 0;
    }

    private int HandleNewElect(int senderId) {
        var newLeader = NetworkNodes.First(node => node.NodeId == senderId);
        if (newLeader == null) {
            AddLog($"Otrzymano NEWELECT od nodeID: {senderId}. Blad: Brak takiego node'a na liscie!");
            return -1;
        }

        pinger?.Stop();

        if (meNode.IsLeader) { 
            meNode.IsLeader = false;
        }

        if (NetworkNodes.Any(node => node.IsLeader)) {
            var currentLeader = NetworkNodes.First(node => node.IsLeader);
            currentLeader.IsLeader = false;
            currentLeader.IsActive = false;
            if (currentLeader.NodeId > newLeader.NodeId) {
                //AddLog($"Otrzymano NEWELECT od nodeID: {senderId}. Blad: Nowy lider ma mniejszy ID! curr: {currentLeader.NodeId}; nowy: {newLeader.NodeId}");
                //return -2;
            }
        }

        AddLog($"Otrzymano NEWELECT od nodeID: {senderId}.");
        newLeader.IsLeader = true;
        LeaderAddress = newLeader.IpAddress;
        LeaderPort = newLeader.Port;
        _leaderBadPingResponse = 0;
        pinger?.Start();
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
        ConnectionAvailable = false;
        if (IsConnected == true) return;
        if (NodeId < 0) return;
        if (NetworkAddress.Equals(string.Empty)) return;
        if (NetworkPort < 1 || NetworkPort > 65535) return;
        if (LeaderAddress.Equals(string.Empty)) return;
        if (LeaderPort < 1 || LeaderPort > 65535) return;
        if (LeaderCheckInterval < 1) return;
        if (Timeout < 1) return;
        //if (IsActive == false) return;
        ConnectionAvailable = true;
    }

    private void AddLog(string message)
    {
        _semaphore.Wait();
        try
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            Logs.Add($"NR:{Logs.Count} [{timestamp}] {message}\n");
        }
        finally {
            _semaphore.Release();
        }
    }

    private void Reset() { 
        
    }
}
