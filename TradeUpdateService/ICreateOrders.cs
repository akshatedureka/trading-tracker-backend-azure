using System.Threading.Tasks;

namespace TradeUpdateService
{
    public interface ICreateOrders
    {
        public Task<bool> CreateBuySellOrders();
    }
}
