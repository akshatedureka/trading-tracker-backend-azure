using System;
using Alpaca.Markets;

namespace TradingService.SwingManagement.TradeManagement.Models
{
    public class OrderUpdateMessage
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public Guid OrderId { get; set; }
        public OrderSide OrderSide { get; set; }
        public decimal ExecutedPrice { get; set; }
    }
}
