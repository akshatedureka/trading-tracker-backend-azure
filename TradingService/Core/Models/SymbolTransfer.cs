using Newtonsoft.Json;

namespace TradingService.Core.Models
{
    public class SymbolTransfer
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "active")]
        public bool Active { get; set; }
        [JsonProperty(PropertyName = "numShares")]
        public long NumShares { get; set; }
        [JsonProperty(PropertyName = "takeProfitOffset")]
        public decimal TakeProfitOffset { get; set; }
        [JsonProperty(PropertyName = "stopLossOffset")]
        public decimal StopLossOffset { get; set; }
        [JsonProperty(PropertyName = "trading")]
        public bool Trading { get; set; }
    }
}
