using System;
using Alpaca.Markets;

namespace TradeUpdateService
{
    public class OrderUpdateMessage
    {
        public Guid OrderId { get; set; }
        public OrderSide OrderSide { get; set; }
        public decimal ExecutedPrice { get; set; }
    }
}
