using System;
using Newtonsoft.Json;

namespace TradingService.Core.Entities.Base
{
    public abstract class BaseEntity
    {
        [JsonProperty(PropertyName = "id")]
        public virtual string Id { get; set; }
        [JsonProperty(PropertyName = "userId")]
        public virtual string UserId { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public virtual DateTime DateCreated { get; set; }
    }
}
