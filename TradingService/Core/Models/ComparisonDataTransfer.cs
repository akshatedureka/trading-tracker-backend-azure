using System;
using Newtonsoft.Json;


namespace TradingService.Core.Models
{
    public class ComparisonDataTransfer
    {
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "blockId")]
        public string BlockId { get; set; }
        [JsonProperty(PropertyName = "externalBuyOrderId")]
        public Guid ExternalBuyOrderId { get; set; }
        [JsonProperty(PropertyName = "externalSellOrderId")]
        public Guid ExternalSellOrderId { get; set; }
        [JsonProperty(PropertyName = "externalStopLossOrderId")]
        public Guid ExternalStopLossOrderId { get; set; }
        [JsonProperty(PropertyName = "buyOrderCreated")]
        public bool BuyOrderCreated { get; set; }
        [JsonProperty(PropertyName = "buyOrderFilled")]
        public bool BuyOrderFilled { get; set; }
        [JsonProperty(PropertyName = "buyOrderPrice")]
        public decimal BuyOrderPrice { get; set; }
        [JsonProperty(PropertyName = "buyOrderFilledPrice")]
        public decimal BuyOrderFilledPrice { get; set; }
        [JsonProperty(PropertyName = "dateBuyOrderFilled")]
        public DateTime DateBuyOrderFilled { get; set; }
        [JsonProperty(PropertyName = "sellOrderCreated")]
        public bool SellOrderCreated { get; set; }
        [JsonProperty(PropertyName = "sellOrderFilled")]
        public bool SellOrderFilled { get; set; }
        [JsonProperty(PropertyName = "sellOrderPrice")]
        public decimal SellOrderPrice { get; set; }
        [JsonProperty(PropertyName = "sellOrderFilledPrice")]
        public decimal SellOrderFilledPrice { get; set; }
        [JsonProperty(PropertyName = "dateSellOrderFilled")]
        public DateTime DateSellOrderFilled { get; set; }
        [JsonProperty(PropertyName = "stopLossOrderCreated")]
        public bool StopLossOrderCreated { get; set; }
        [JsonProperty(PropertyName = "stopLossOrderPrice")]
        public decimal StopLossOrderPrice { get; set; }
        [JsonProperty(PropertyName = "hasDiscrepancy")]
        public bool hasDiscrepancy { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
