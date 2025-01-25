using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TyranApp.Network
{
    internal class LogEventArgs : EventArgs
    {
        public string Message;
        public LogEventArgs(string message) {
            Message = message;
        }
    }
}
