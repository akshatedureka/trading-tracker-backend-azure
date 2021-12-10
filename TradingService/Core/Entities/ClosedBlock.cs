using System;
using Newtonsoft.Json;
using TradingService.Core.Entities.Base;

namespace TradingService.Core.Entities
{
    public class ClosedBlock : BaseEntity
    {
        [JsonProperty(PropertyName = "blockId")]
        public string BlockId { get; set; }
        [JsonProperty(PropertyName = "symbol")]
        public string Symbol { get; set; }
        [JsonProperty(PropertyName = "numShares")]
        public long NumShares { get; set; }
        [JsonProperty(PropertyName = "profit")]
        public decimal Profit { get; set; }
        [JsonProperty(PropertyName = "externalBuyOrderId")]
        public Guid ExternalBuyOrderId { get; set; }
        [JsonProperty(PropertyName = "externalSellOrderId")]
        public Guid ExternalSellOrderId { get; set; }
        [JsonProperty(PropertyName = "externalStopLossOrderId")]
        public Guid ExternalStopLossOrderId { get; set; }
        [JsonProperty(PropertyName = "buyOrderFilledPrice")]
        public decimal BuyOrderFilledPrice { get; set; }
        [JsonProperty(PropertyName = "dateBuyOrderFilled")]
        public DateTime DateBuyOrderFilled { get; set; }
        [JsonProperty(PropertyName = "dateSellOrderFilled")]
        public DateTime DateSellOrderFilled { get; set; }
        [JsonProperty(PropertyName = "sellOrderFilledPrice")]
        public decimal SellOrderFilledPrice { get; set; }
        [JsonProperty(PropertyName = "isShort")]
        public bool IsShort { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

}
