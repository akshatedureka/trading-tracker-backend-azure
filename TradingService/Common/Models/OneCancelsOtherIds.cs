using System;
using Newtonsoft.Json;

namespace TradingService.Common.Models
{
    public class OneCancelsOtherIds
    {
        [JsonProperty(PropertyName = "takeProfitId")]
        public Guid TakeProfitId { get; set; }
        [JsonProperty(PropertyName = "stopLossOrderId")]
        public Guid StopLossOrderId { get; set; }
    }
}
