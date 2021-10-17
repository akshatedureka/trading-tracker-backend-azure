using System.Threading.Tasks;
using TradeUpdateService.Enums;

namespace TradeUpdateService
{
    public interface ITradeUpdateListener
    {
        public Task StartListening(string userId, AccountTypes accountType, string alpacaKey, string alpacaSecret);
    }
}
