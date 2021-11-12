using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TradingService.Common.Models
{
    public class UserCondensedBlock
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }
        [JsonProperty(PropertyName = "condensedBlocks")]
        public List<CondensedBlock> CondensedBlocks { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}