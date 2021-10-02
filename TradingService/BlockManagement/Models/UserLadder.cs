using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TradingService.BlockManagement.Models
{
    public class UserLadder
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "user")]
        public string UserId { get; set; }
        [JsonProperty(PropertyName = "ladders")]
        public List<Ladder> Ladders { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}