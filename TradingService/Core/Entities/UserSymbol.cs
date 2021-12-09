using System.Collections.Generic;
using Newtonsoft.Json;
using TradingService.Core.Entities.Base;

namespace TradingService.Core.Entities
{
    public class UserSymbol : BaseEntity
    {
        [JsonProperty(PropertyName = "symbols")]
        public List<Symbol> Symbols { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }

    public class Symbol : BaseEntity
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "active")]
        public bool Active { get; set; }
        [JsonProperty(PropertyName = "trading")]
        public bool Trading { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
