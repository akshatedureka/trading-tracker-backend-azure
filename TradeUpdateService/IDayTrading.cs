using System.Threading.Tasks;

namespace TradeUpdateService
{
    public interface IDayTrading
    {
        public Task<bool> TriggerDayTrades();
    }
}
