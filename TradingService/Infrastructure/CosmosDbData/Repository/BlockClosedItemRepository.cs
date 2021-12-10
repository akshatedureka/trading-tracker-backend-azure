using Microsoft.Azure.Cosmos;
using System;
using TradingService.Core.Entities;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.CosmosDbData.Repository
{
    public class BlockClosedItemRepository : CosmosDbRepository<ClosedBlock>, IBlockClosedItemRepository
    {
        public override string ContainerName { get; } = "BlocksClosed";

        public override string GenerateId(ClosedBlock entity) => Guid.NewGuid().ToString();

        public override PartitionKey ResolvePartitionKey(string userId) => new PartitionKey(userId);

        public BlockClosedItemRepository(ICosmosDbContainerFactory factory) : base(factory)
        { }
    }
}