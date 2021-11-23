using System;
using Alpaca.Markets;

namespace TradingService.TradeManagement.Swing.Models
{
    public class OrderMessage
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public Guid OrderId { get; set; }
        public OrderSide OrderSide { get; set; }
        public decimal ExecutedPrice { get; set; }
        public bool IsOrderCreation { get; set; }
    }
}
