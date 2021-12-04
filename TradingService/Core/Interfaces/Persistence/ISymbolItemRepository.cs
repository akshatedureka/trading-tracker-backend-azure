using System.Threading.Tasks;
using TradingService.Core.Entities;

namespace TradingService.Core.Interfaces.Persistence
{
    public interface ISymbolItemRepository : IRepository<UserSymbol>
    {
        Task<UserSymbol> GetUserSymbolByUserId(string userId);
    }
}
