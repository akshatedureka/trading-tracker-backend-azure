
namespace TradingService.CreateBlocksFromSymbol.Models
{
    public class BlockPrices
    {
        public decimal BuyPrice { get; set; }
        public decimal SellPrice { get; set; }

        public decimal StopLossPrice { get; set; }
    }
}
