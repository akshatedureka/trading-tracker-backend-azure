using System;
using Newtonsoft.Json;

namespace TradingService.Core.Models
{
    public class OrderIds
    {
        [JsonProperty(PropertyName = "parentOrderId")]
        public Guid ParentOrderId { get; set; }
        [JsonProperty(PropertyName = "takeProfitId")]
        public Guid TakeProfitId { get; set; }
        [JsonProperty(PropertyName = "stopLossOrderId")]
        public Guid StopLossOrderId { get; set; }
    }
}
