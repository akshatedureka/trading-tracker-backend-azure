using System.Threading.Tasks;

namespace TradeUpdateService
{
    public interface IUpdateBlockRange
    {
        public Task<bool> CreateUpdateBlockRangeMessage();
    }
}
