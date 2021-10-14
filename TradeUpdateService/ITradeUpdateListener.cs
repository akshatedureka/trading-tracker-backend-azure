using System.Threading.Tasks;

namespace TradeUpdateService
{
    public interface ITradeUpdateListener
    {
        public Task StartListening(string userId, string alpacaKey, string alpacaSecret);
    }
}
