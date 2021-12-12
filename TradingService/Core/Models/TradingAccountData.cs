using Newtonsoft.Json;

namespace TradingService.Core.Models
{
    public class TradingAccountData
    {
        [JsonProperty(PropertyName = "buyingPower")]
        public decimal BuyingPower { get; set; }

        [JsonProperty(PropertyName = "accountValue")]
        public decimal AccountValue { get; set; }

        [JsonProperty(PropertyName = "accountValuePreviousDay")]
        public decimal AccountValuePreviousDay { get; set; }

    }
}
