using Newtonsoft.Json;

namespace TradingService.DayManagement.TradeManagement.Models
{
    public class Position
    {
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "quantity")]
        public decimal Quantity { get; set; }
        [JsonProperty(PropertyName = "profit")]
        public decimal Profit { get; set; }
    }
}
