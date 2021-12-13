using System.Threading.Tasks;

namespace TradingService.Infrastructure.Helpers.Interfaces
{
    public interface ITradingServiceHelper
    {
        public Task<bool> IsSymbolTrading(string userId, string symbol);
    }
}
