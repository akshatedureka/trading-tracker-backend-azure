using Microsoft.Azure.Cosmos;
using System;
using System.Linq;
using System.Threading.Tasks;
using TradingService.Core.Entities;
using TradingService.Core.Enums;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Infrastructure.CosmosDbData.Interfaces;

namespace TradingService.Infrastructure.CosmosDbData.Repository
{
    public class AccountItemRepository : CosmosDbRepository<Account>, IAccountItemRepository
    {
        public override string ContainerName { get; } = "Accounts";

        public override string GenerateId(Account entity) => Guid.NewGuid().ToString();

        public override PartitionKey ResolvePartitionKey(string userId) => new PartitionKey(userId);

        public AccountItemRepository(ICosmosDbContainerFactory factory) : base(factory)
        { }

        public async Task<AccountTypes> GetAccountTypeByUserId(string userId)
        {
            var result = await GetItemsAsyncByUserId(userId);
            return result.FirstOrDefault().AccountType;
        }
    }
}