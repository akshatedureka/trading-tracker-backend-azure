using System;

namespace TradingService.Common.Models
{
    public class ArchiveBlockMessage
    {
        public string BlockId { get; set; }
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public long NumShares { get; set; }
        public Guid ExternalBuyOrderId { get; set; }
        public Guid ExternalSellOrderId { get; set; }
        public Guid ExternalStopLossOrderId { get; set; }
        public decimal BuyOrderFilledPrice { get; set; }
        public DateTime DateBuyOrderFilled { get; set; }
        public DateTime DateSellOrderFilled { get; set; }
        public decimal SellOrderFilledPrice { get; set; }
        public bool IsShort { get; set; }
    }
}
