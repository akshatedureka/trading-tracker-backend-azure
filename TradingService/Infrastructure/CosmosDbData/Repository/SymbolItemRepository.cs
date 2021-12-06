using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TradingService.Core.Entities;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.CosmosDbData.Repository
{
    public class SymbolItemRepository : CosmosDbRepository<UserSymbol>, ISymbolItemRepository
    {
        public override string ContainerName { get; } = "Symbols";

        public override string GenerateId(UserSymbol entity) => Guid.NewGuid().ToString();

        public override PartitionKey ResolvePartitionKey(string userId) => new PartitionKey(userId);

        public SymbolItemRepository(ICosmosDbContainerFactory factory) : base(factory)
        { }

        //public async Task<UserSymbol> GetUserSymbolByUserId(string userId)
        //{
        //    var result = await GetItemsAsyncByUserId(userId);
        //    return result.FirstOrDefault();
        //}

    }
}