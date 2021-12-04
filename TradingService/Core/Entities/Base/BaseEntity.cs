using System;
using Newtonsoft.Json;

namespace TradingService.Core.Entities.Base
{
    public abstract class BaseEntity
    {
        [JsonProperty(PropertyName = "id")]
        public virtual string Id { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }
    }
}
