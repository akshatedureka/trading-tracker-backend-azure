using System;
using Newtonsoft.Json;

namespace TradingService.BlockManagement.Models
{
    public class Ladder
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "initialNumShares")]
        public long InitialNumShares { get; set; }
        [JsonProperty(PropertyName = "buyPercentage")]
        public decimal BuyPercentage { get; set; }
        [JsonProperty(PropertyName = "sellPercentage")]
        public decimal SellPercentage { get; set; }
        [JsonProperty(PropertyName = "stopLossPercentage")]
        public decimal StopLossPercentage { get; set; }
        [JsonProperty(PropertyName = "blocksCreated")]
        public bool BlocksCreated { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
