using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TradingService.Core.Entities.Base;

namespace TradingService.Core.Entities
{
    public class UserCondensedBlock : BaseEntity
    {
        [JsonProperty(PropertyName = "condensedBlocks")]
        public List<CondensedBlock> CondensedBlocks { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

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