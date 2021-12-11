using System;
using Alpaca.Markets;
using TradingService.Core.Enums;

namespace TradingService.Core.Models
{
    public class OrderMessage
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
        public Guid OrderId { get; set; }
        public OrderSide OrderSide { get; set; }
        public decimal ExecutedPrice { get; set; }
        public OrderMessageTypes OrderMessageType { get; set; }
    }
}
