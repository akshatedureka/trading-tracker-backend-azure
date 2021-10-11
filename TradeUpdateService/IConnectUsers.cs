using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire;

namespace TradeUpdates
{
    public interface IConnectUsers
    {
        public Task<bool> GetUsersToConnect();
    }
}
