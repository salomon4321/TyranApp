using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TyranApp.Network
{
    internal class OnlineMessage
    {
        public enum Command
        {
            CONNECT = 0,
            UPDATE,
            PING,
            STARTELECT,
            NEWELECT
        }
        public Command Kod {  get; set; }
        public object[] Param { get; set; }
        public int SenderId { get; set; }
        public int MessId { get; set; }

        public OnlineMessage(object[] param, Command kod, int senderId) {
            Param = param;
            Kod = kod;
            SenderId = senderId;
            MessId = -1;
        }
    }
}
