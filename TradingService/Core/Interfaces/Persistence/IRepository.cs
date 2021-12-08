using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.Core.Entities.Base;

namespace TradingService.Core.Interfaces.Persistence
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<T> GetItemAsync(string id);
        Task<T> AddItemAsync(T item);
        Task<T> UpdateItemAsync(T item);
        Task DeleteItemAsync(T item);
        Task<List<T>> GetItemsAsyncByUserId(string userId);
        Task<List<T>> GetItemsAsyncByUserIdAndSymbol(string userId, string symbol);
    }
}
