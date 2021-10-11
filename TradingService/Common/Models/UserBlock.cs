using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TradingService.Common.Models
{
    public class UserBlock
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "numShares")]
        public long NumShares { get; set; }
        [JsonProperty(PropertyName = "blocks")]
        public List<Block> Blocks { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}