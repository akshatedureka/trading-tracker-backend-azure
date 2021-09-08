using System;
using Newtonsoft.Json;

namespace TradingService.TradingSymbol.Models
{
    public class SymbolData
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
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
    }
}
