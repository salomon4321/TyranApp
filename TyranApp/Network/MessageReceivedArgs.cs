using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TyranApp.Network
{
    internal class MessageReceivedArgs : EventArgs
    {
        public OnlineMessage Message { get; }
        public string ResponseData { get; set; }

        public MessageReceivedArgs(OnlineMessage message)
        {
            this.Message = message;
            this.ResponseData = string.Empty;
        }
    }
}
