using System.Linq;
using System.Threading.Tasks;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.Helpers.Interfaces;

namespace TradingService.Infrastructure.Helpers
{
    public class TradingServiceHelper : ITradingServiceHelper
    {
        private readonly ISymbolItemRepository _symbolRepo;

        public TradingServiceHelper(ISymbolItemRepository symbolRepo)
        {
            _symbolRepo = symbolRepo;
        }
        
        public async Task<bool> IsSymbolTrading(string userId, string symbol)
        {
            var userSymbols = await _symbolRepo.GetItemsAsyncByUserId(userId);
            return userSymbols.FirstOrDefault().Symbols.Where(s => s.Name == symbol).FirstOrDefault().Trading;
        }
    }
}
