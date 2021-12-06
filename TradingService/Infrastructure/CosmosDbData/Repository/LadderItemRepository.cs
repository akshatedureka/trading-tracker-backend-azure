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
    public class LadderItemRepository : CosmosDbRepository<UserLadder>, ILadderItemRepository
    {
        public override string ContainerName { get; } = "Ladders";

        public override string GenerateId(UserLadder entity) => Guid.NewGuid().ToString();

        public override PartitionKey ResolvePartitionKey(string userId) => new PartitionKey(userId);

        public LadderItemRepository(ICosmosDbContainerFactory factory) : base(factory)
        { }

        //public async Task<UserSymbol> GetUserSymbolByUserId(string userId)
        //{
        //    var result = await GetItemsAsyncByUserId(userId);
        //    return result.FirstOrDefault();
        //}

    }
}