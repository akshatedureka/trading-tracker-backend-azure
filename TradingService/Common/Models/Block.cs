using System;
using Newtonsoft.Json;

namespace TradingService.Common.Models
{
    public class Block
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "numShares")]
        public long NumShares { get; set; }
        [JsonProperty(PropertyName = "confidenceLevel")]
        public int ConfidenceLevel { get; set; }
        [JsonProperty(PropertyName = "externalBuyOrderId")]
        public Guid ExternalBuyOrderId { get; set; }
        [JsonProperty(PropertyName = "externalSellOrderId")]
        public Guid ExternalSellOrderId { get; set; }
        [JsonProperty(PropertyName = "buyOrderCreated")]
        public bool BuyOrderCreated { get; set; }
        [JsonProperty(PropertyName = "buyOrderPrice")]
        public decimal BuyOrderPrice { get; set; }
        [JsonProperty(PropertyName = "buyOrderExecuted")]
        public bool BuyOrderExecuted { get; set; }
        [JsonProperty(PropertyName = "executedBuyPrice")]
        public decimal ExecutedBuyPrice { get; set; }
        [JsonProperty(PropertyName = "dateBuyOrderExecuted")]
        public DateTime DateBuyOrderExecuted { get; set; }
        [JsonProperty(PropertyName = "sellOrderCreated")]
        public bool SellOrderCreated { get; set; }
        [JsonProperty(PropertyName = "sellOrderPrice")]
        public decimal SellOrderPrice { get; set; }
        [JsonProperty(PropertyName = "executedSellPrice")]
        public decimal ExecutedSellPrice { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

}
