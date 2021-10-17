using System;
using Newtonsoft.Json;
using TradeUpdateService.Enums;

namespace TradeUpdateService.Models
{
    public class Account
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "userId")]
        public string UserId { get; set; }
        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }
        [JsonProperty(PropertyName = "hasEnteredKeys")]
        public bool HasEnteredKeys { get; set; }
        [JsonProperty(PropertyName = "accountType")]
        public AccountTypes AccountType { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}