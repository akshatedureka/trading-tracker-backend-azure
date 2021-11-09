using System;
using Alpaca.Markets;

namespace TradingService.TradeManagement.Swing.Models
{
    public class OrderCreationMessage
    {
        public string UserId { get; set; }
        public string Symbol { get; set; }
    }
}
