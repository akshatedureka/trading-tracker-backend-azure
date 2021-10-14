using System.Threading.Tasks;

namespace TradeUpdateService
{
    public interface IConnectUsers
    {
        public Task<bool> GetUsersToConnect();
    }
}
