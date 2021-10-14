using System;
using Newtonsoft.Json;

namespace TradeUpdateService.Models
{
    public class User
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }
        [JsonProperty(PropertyName = "hasEnteredKeys")]
        public bool HasEnteredKeys { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}