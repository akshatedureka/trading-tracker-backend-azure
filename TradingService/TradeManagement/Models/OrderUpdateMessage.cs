using System;
using Alpaca.Markets;

namespace TradingService.TradeManagement.Models
{
    public class OrderUpdateMessage
    {
        public Guid OrderId { get; set; }
        public OrderSide OrderSide { get; set; }
        public decimal ExecutedPrice { get; set; }
    }
}
