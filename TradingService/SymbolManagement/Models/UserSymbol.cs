﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TradingService.SymbolManagement.Models
{
    public class UserSymbol
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "user")]
        public string UserId { get; set; }
        [JsonProperty(PropertyName = "symbols")]
        public List<Symbol> Symbols { get; set; }
        [JsonProperty(PropertyName = "dateCreated")]
        public DateTime DateCreated { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}