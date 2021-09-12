using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace TradingService.TradingSymbol.Transfer
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
        [JsonProperty(PropertyName = "trading")]
        public bool Trading { get; set; }
    }
}
