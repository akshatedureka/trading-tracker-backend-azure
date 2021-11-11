using System;
using Alpaca.Markets;

namespace TradeUpdateService.Models
{
    public class OrderCreationMessage
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
    }
}
