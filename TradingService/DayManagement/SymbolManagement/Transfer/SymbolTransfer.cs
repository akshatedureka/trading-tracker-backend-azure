using Newtonsoft.Json;

namespace TradingService.DayManagement.SymbolManagement.Transfer
{
    public class SymbolTransfer
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "oldName")]
        public string OldName { get; set; }
        [JsonProperty(PropertyName = "active")]
        public bool Active { get; set; }
        [JsonProperty(PropertyName = "swingTrading")]
        public bool SwingTrading { get; set; }
        [JsonProperty(PropertyName = "dayTrading")]
        public bool DayTrading { get; set; }
    }
}
