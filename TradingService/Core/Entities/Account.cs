using Newtonsoft.Json;
using TradingService.Core.Entities.Base;
using TradingService.Core.Enums;

namespace TradingService.Core.Entities
{
    public class Account : BaseEntity
    {
        [JsonProperty(PropertyName = "email")]
        public string Email { get; set; }
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