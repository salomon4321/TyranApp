using Avalonia.Media;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TyranApp
{
    public class Node : ReactiveObject, INotifyPropertyChanged
    {
        private bool _isActive;
        private bool _isLeader;
        private int _nodeId;
        private string _ipAddress;
        private int _port;
        public int NodeId {
            get => _nodeId;
            set
            {
                this.RaiseAndSetIfChanged(ref _nodeId, value);
                OnPropertyChanged(nameof(NodeId));
            }
        }
        public string IpAddress {
            get => _ipAddress;
            set
            {
                this.RaiseAndSetIfChanged(ref _ipAddress, value);
                OnPropertyChanged(nameof(IpAddress));
            }
        }
        public int Port { 
            get => _port;
            set {
                this.RaiseAndSetIfChanged(ref _port, value);
                OnPropertyChanged(nameof(Port));
            } }

        private IBrush _backgroundColor;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                this.RaiseAndSetIfChanged(ref _isActive, value);
                UpdateBackgroundColor();
            }
        }

        public bool IsLeader
        {
            get => _isLeader;
            set
            {
                this.RaiseAndSetIfChanged(ref _isLeader, value);
                UpdateBackgroundColor();
            }
        }

        public IBrush BackgroundColor
        {
            get => _backgroundColor;
            private set
            {
                this.RaiseAndSetIfChanged(ref _backgroundColor, value);
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }

        public Node()
        {
            UpdateBackgroundColor(); // Inicjalizacja koloru
        }

        private void UpdateBackgroundColor()
        {
            if (!IsActive)
            {
                BackgroundColor = Brushes.Red;
            }
            else if (IsLeader)
            {
                BackgroundColor = Brushes.DarkViolet;
            }
            else
            {
                BackgroundColor = Brushes.Green;
            }
        }

        public override string ToString()
        {
            return $"{NodeId}:{IpAddress}:{Port}:{IsActive}";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
