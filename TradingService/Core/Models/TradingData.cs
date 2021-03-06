using Newtonsoft.Json;

namespace TradingService.Core.Models
{
    public class TradingData
    {
        [JsonProperty(PropertyName = "symbolId")]
        public string SymbolId { get; set; }
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "trading")]
        public bool Trading { get; set; }
        [JsonProperty(PropertyName = "active")]
        public bool Active { get; set; }
        [JsonProperty(PropertyName = "currentQuantity")]
        public decimal CurrentQuantity { get; set; }
        [JsonProperty(PropertyName = "openProfit")]
        public decimal OpenProfit { get; set; }
        [JsonProperty(PropertyName = "closedProfit")]
        public decimal ClosedProfit { get; set; }
        [JsonProperty(PropertyName = "condensedProfit")]
        public decimal CondensedProfit { get; set; }
        [JsonProperty(PropertyName = "totalProfit")]
        public decimal TotalProfit { get; set; }
    }
}
