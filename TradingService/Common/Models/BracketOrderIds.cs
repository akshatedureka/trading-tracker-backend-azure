using System;
using Newtonsoft.Json;

namespace TradingService.Common.Models
{
    public class BracketOrderIds
    {
        [JsonProperty(PropertyName = "parentOrderId")]
        public Guid ParentOrderId { get; set; }
        [JsonProperty(PropertyName = "takeProfitId")]
        public Guid TakeProfitId { get; set; }
        [JsonProperty(PropertyName = "stopLossOrderId")]
        public Guid StopLossOrderId { get; set; }
    }
}
