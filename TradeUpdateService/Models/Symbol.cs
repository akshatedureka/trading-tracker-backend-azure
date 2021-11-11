using System;
using Newtonsoft.Json;

namespace TradeUpdateService.Models
{
    public class Symbol
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "active")]
        public bool Active { get; set; }
        [JsonProperty(PropertyName = "numShares")]
        public long NumShares { get; set; }
        [JsonProperty(PropertyName = "trading")]
        public bool Trading { get; set; }
        [JsonProperty(PropertyName = "takeProfitOffset")]
        public decimal TakeProfitOffset { get; set; }
        [JsonProperty(PropertyName = "stopLossOffset")]
        public decimal StopLossOffset { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
