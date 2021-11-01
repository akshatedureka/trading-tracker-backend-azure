using Newtonsoft.Json;

namespace TradingService.TradeManagement.Swing.Transfer
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
        [JsonProperty(PropertyName = "currentProfit")]
        public decimal CurrentProfit { get; set; }
        [JsonProperty(PropertyName = "archiveProfit")]
        public decimal ArchiveProfit { get; set; }
        [JsonProperty(PropertyName = "totalProfit")]
        public decimal TotalProfit { get; set; }
    }
}
