using Microsoft.Azure.Cosmos;
using System;
using TradingService.Core.Entities;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.CosmosDbData.Repository
{
    public class BlockCondensedItemRepository : CosmosDbRepository<UserCondensedBlock>, IBlockCondensedItemRepository
    {
        public override string ContainerName { get; } = "BlocksCondensed";

        public override string GenerateId(UserCondensedBlock entity) => Guid.NewGuid().ToString();

        public override PartitionKey ResolvePartitionKey(string userId) => new PartitionKey(userId);

        public BlockCondensedItemRepository(ICosmosDbContainerFactory factory) : base(factory)
        { }

    }
}