using System;
using Newtonsoft.Json;

namespace TradingService.Common.Models
{
    public class CondensedBlock
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "dateUpdated")]
        public DateTime DateUpdated { get; set; }
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "profit")]
        public decimal Profit { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

}
