using System.Threading.Tasks;

namespace TradeUpdates
{
    public interface ITradeUpdateListener
    {
        public Task StartListening(string userId, string alpacaKey, string alpacaSecret);
    }
}
