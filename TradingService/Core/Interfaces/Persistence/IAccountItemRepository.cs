using System.Threading.Tasks;
using TradingService.Core.Entities;
using TradingService.Core.Enums;

namespace TradingService.Core.Interfaces.Persistence
{
    public interface IAccountItemRepository : IRepository<Account>
    {
        public Task<AccountTypes> GetAccountTypeByUserId(string userId);
    }
}
