using System;
using Newtonsoft.Json;

namespace TradingService.Common.Models
{
    public class BracketOrderIds
    {
        [JsonProperty(PropertyName = "buyOrderId")]
        public Guid BuyOrderId { get; set; }
        [JsonProperty(PropertyName = "sellOrderId")]
        public Guid SellOrderId { get; set; }
        [JsonProperty(PropertyName = "stopLossOrderId")]
        public Guid StopLossOrderId { get; set; }
    }
}
