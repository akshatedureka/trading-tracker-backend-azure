using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.Core.Entities.Base;

namespace TradingService.Core.Interfaces.Persistence
{
    public interface IRepository<T> where T : BaseEntity
    {
        Task<List<T>> GetItemsAsyncByUserId(string userId);
        Task<T> GetItemAsync(string id);
        Task AddItemAsync(T item);
        Task UpdateItemAsync(string id, T item);
        Task DeleteItemAsync(string id);
    }
}
