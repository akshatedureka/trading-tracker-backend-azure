using System.Collections.Generic;
using Newtonsoft.Json;
using TradingService.Core.Entities.Base;

namespace TradingService.Core.Entities
{
    public class UserLadder : BaseEntity
    {
        [JsonProperty(PropertyName = "ladders")]
        public List<Ladder> Ladders { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class Ladder : BaseEntity
    {
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "numSharesPerBlock")]
        public long NumSharesPerBlock { get; set; }
        [JsonProperty(PropertyName = "numSharesMax")]
        public long NumSharesMax { get; set; }
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