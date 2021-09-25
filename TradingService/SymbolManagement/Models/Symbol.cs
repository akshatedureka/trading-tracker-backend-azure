using System;
using Newtonsoft.Json;

namespace TradingService.SymbolManagement.Models
{
    public class Symbol
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "active")]
        public bool Active { get; set; }
        [JsonProperty(PropertyName = "trading")]
        public bool Trading { get; set; }
    }
}
