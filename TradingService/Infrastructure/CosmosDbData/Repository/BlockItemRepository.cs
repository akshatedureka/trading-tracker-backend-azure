using Microsoft.Azure.Cosmos;
using System;
using TradingService.Core.Entities;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.CosmosDbData.Repository
{
    public class BlockItemRepository : CosmosDbRepository<Block>, IBlockItemRepository
    {
        public override string ContainerName { get; } = "Blocks";

        public override string GenerateId(Block entity) => Guid.NewGuid().ToString();

        public override PartitionKey ResolvePartitionKey(string userId) => new PartitionKey(userId);

        public BlockItemRepository(ICosmosDbContainerFactory factory) : base(factory)
        { }

        //public async Task<UserSymbol> GetUserSymbolByUserId(string userId)
        //{
        //    var result = await GetItemsAsyncByUserId(userId);
        //    return result.FirstOrDefault();
        //}

    }
}