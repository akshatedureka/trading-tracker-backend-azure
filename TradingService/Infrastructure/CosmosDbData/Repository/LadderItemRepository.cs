using Microsoft.Azure.Cosmos;
using System;
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

    }
}