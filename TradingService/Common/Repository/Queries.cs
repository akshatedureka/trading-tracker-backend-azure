using System;
using System.Threading.Tasks;
using TradingService.SymbolManagement.Models;
using System.Linq;
using TradingService.Common.Models;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using TradingService.AccountManagement.Enums;
using TradingService.AccountManagement.Models;
using Microsoft.Extensions.Configuration;
using TradingService.BlockManagement.Models;

namespace TradingService.Common.Repository
{
    public class Queries : IQueries
    {
        private readonly string containerIdForAccounts = "Accounts";
        private readonly string containerIdSymbols = "Symbols";
        private readonly string containerIdBlocks = "Blocks";
        private readonly string containerIdLadders = "Ladders";
        private readonly string containerIdBlocksClosed = "BlocksClosed";
        private readonly string containerIdBlocksCondensed = "BlocksCondensed"; // long-term archive

        private readonly IConfiguration _configuration;
        private readonly IRepository _repository;

        public Queries(IConfiguration configuration, IRepository repository)
        {
            _configuration = configuration;
            _repository = repository;
        }

        public async Task<UserSymbol> GetUserSymbolByUserId(string userId)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdSymbols);
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();
                return userSymbol;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting user symbol from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<Symbol> GetSymbolByUserIdAndSymbolName(string userId, string symbolName)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdSymbols);
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                var symbol = userSymbol.Symbols.FirstOrDefault(s => s.Name == symbolName);
                return symbol;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting symbol from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<List<Symbol>> GetActiveTradingSymbolsByUserId(string userId)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdSymbols);
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                var symbols = new List<Symbol>();

                foreach (var symbol in userSymbol.Symbols)
                {
                    if (symbol.Trading)
                    {
                        symbols.Add(symbol);
                    }
                }

                return symbols;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting user symbol from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<Symbol> UpdateTradingStatusForSymbol(string userId, string symbolName)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdSymbols);
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                var symbol = userSymbol.Symbols.FirstOrDefault(s => s.Name == symbolName);
                symbol.Trading = !symbol.Trading;

                var updateSymbolResponse = await container.ReplaceItemAsync(userSymbol, userSymbol.Id, new PartitionKey(userSymbol.UserId));
                return symbol;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue updating trading symbol in Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<List<Block>> GetBlocksByUserIdAndSymbols(string userId, List<Symbol> symbols)
        {
            var symbolNames = new List<string>();
            try
            {
                foreach (var symbol in symbols)
                {
                    symbolNames.Add(symbol.Name);
                }

                var container = await _repository.GetContainer(containerIdBlocks);
                var blocks = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId && symbolNames.Contains(s.Symbol)).ToList().FirstOrDefault().Blocks;

                return blocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting blocks from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<List<Block>> GetBlocksByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var userBlock = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId && s.Symbol == symbol).ToList().FirstOrDefault();
                return userBlock.Blocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting user block from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<UserBlock> GetUserBlockByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var userBlock = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId && s.Symbol == symbol).ToList().FirstOrDefault();
                return userBlock;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting user block from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<UserBlock> DeleteUserBlockByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var userBlock = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(u => u.UserId == userId && u.Symbol == symbol).ToList().FirstOrDefault();

                var deleteUserBlockResponse = await container.DeleteItemAsync<UserBlock>(userBlock.Id, new PartitionKey(userBlock.UserId));
                return deleteUserBlockResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue deleting blocks from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<bool> DeleteBlocksByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var blocks = await GetBlocksByUserIdAndSymbol(userId, symbol);

                foreach (var block in blocks)
                {
                    var deleteBlockResponse = await container.DeleteItemAsync<Block>(block.Id, new PartitionKey(userId));
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue deleting blocks from Cosmos DB: {ex.Message}.");
                return false;
            }
        }

        public async Task<bool> DeleteBlockByBlockUserIdAndBlockId(string userId, string blockId)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var deleteBlockResponse = await container.DeleteItemAsync<Block>(blockId, new PartitionKey(userId));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue deleting block from Cosmos DB: {ex.Message}.");
                return false;
            }
        }

        public async Task<bool> UpdateUserBlock(UserBlock userBlock)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var userBlockResponse = await container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue updating user block from Cosmos DB: {ex.Message}.");
                return false;
            }
        }

        public async Task<List<ClosedBlock>> GetClosedBlocksByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocksClosed);
                var closedBlocks = container.GetItemLinqQueryable<ClosedBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId && b.Symbol == symbol).ToList();
                return closedBlocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting closed blocks from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public async Task<bool> CreateCondensedBlockByUserIdAndSymbol(string userId, string symbol, decimal profit)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocksCondensed);
                var userCondensedBlock = container.GetItemLinqQueryable<UserCondensedBlock>(allowSynchronousQueryExecution: true)
                .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (userCondensedBlock == null) // Initial UserCondensedBlock item creation
                {
                    var userCondensedBlockToCreate = new UserCondensedBlock()
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateCreated = DateTime.Now,
                        UserId = userId,
                        CondensedBlocks = new List<CondensedBlock>()
                    };
                    var userCondensedBlockResponse = await container.CreateItemAsync(userCondensedBlockToCreate, new PartitionKey(userCondensedBlockToCreate.UserId));
                    return true;
                }

                var condensedBlockToUpdate = userCondensedBlock.CondensedBlocks.FirstOrDefault(l => l.Symbol == symbol);

                if (condensedBlockToUpdate == null)
                {
                    var condensedBlockToAdd = new CondensedBlock
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateUpdated = DateTime.Now,
                        Symbol = symbol,
                        Profit = profit
                    };
                    userCondensedBlock.CondensedBlocks.Add(condensedBlockToAdd);
                }
                else
                {
                    condensedBlockToUpdate.DateUpdated = DateTime.Now;
                    condensedBlockToUpdate.Profit += profit;
                }

                var updateUserCondensedBlock = await container.ReplaceItemAsync(userCondensedBlock, userCondensedBlock.Id, new PartitionKey(userCondensedBlock.UserId));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue creating condensed block in Cosmos DB: {ex.Message}.");
                return false;
            }
        }

        public async Task<bool> DeleteClosedBlocksByClosedBlocks(List<ClosedBlock> closedBlocks)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocksClosed);
                foreach (var closedBlock in closedBlocks)
                {
                    await container.DeleteItemAsync<ClosedBlock>(closedBlock.Id, new PartitionKey(closedBlock.UserId));
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue deleting closed blocks from Cosmos DB: {ex.Message}.");
                return false;
            }
        }

        public async Task<bool> ResetUserBlocksByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var userBlock = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId && s.Symbol == symbol).ToList().FirstOrDefault();

                foreach (var block in userBlock.Blocks)
                {
                    block.ExternalBuyOrderId = new Guid();
                    block.ExternalSellOrderId = new Guid();
                    block.ExternalStopLossOrderId = new Guid();
                    block.BuyOrderCreated = false;
                    block.BuyOrderFilled = false;
                    block.BuyOrderFilledPrice = 0;
                    block.DateBuyOrderFilled = DateTime.MinValue;
                    block.SellOrderCreated = false;
                    block.SellOrderFilled = false;
                    block.SellOrderFilledPrice = 0;
                    block.DateSellOrderFilled = DateTime.MinValue;
                }

                var updateUserBlock = await container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue resetting user block in Cosmos DB: {ex.Message}.");
                return false;
            }
        }

        public async Task<bool> ResetUserBlockByUserIdAndSymbol(string userId, string symbol, string blockId)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdBlocks);
                var userBlock = await GetUserBlockByUserIdAndSymbol(userId, symbol);

                if (userBlock == null)
                {
                    return false;
                }

                var blockToUpdate = userBlock.Blocks.Where(b => b.Id == blockId).FirstOrDefault();

                if (userBlock == null)
                {
                    return false;
                }

                // Reset block
                blockToUpdate.ExternalBuyOrderId = new Guid();
                blockToUpdate.ExternalSellOrderId = new Guid();
                blockToUpdate.ExternalStopLossOrderId = new Guid();
                blockToUpdate.BuyOrderCreated = false;
                blockToUpdate.BuyOrderFilled = false;
                blockToUpdate.BuyOrderFilledPrice = 0;
                blockToUpdate.DateBuyOrderFilled = DateTime.MinValue;
                blockToUpdate.SellOrderCreated = false;
                blockToUpdate.SellOrderFilled = false;
                blockToUpdate.SellOrderFilledPrice = 0;
                blockToUpdate.DateSellOrderFilled = DateTime.MinValue;

                // Replace the item with the updated content
                var userBlockReplaceResponse = await container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue resetting user block in Cosmos DB: {ex.Message}.");
                return false;
            }
        }

        public async Task<AccountTypes> GetAccountTypeByUserId(string userId)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdForAccounts);
                var accountType = container.GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true)
                .Where(u => u.UserId == userId).ToList().FirstOrDefault().AccountType;

                return accountType;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue resetting user block in Cosmos DB: {ex.Message}.");
                return AccountTypes.NotSet;
            }
        }

        public async Task<UserLadder> GetLaddersByUserId(string userId)
        {
            try
            {
                var container = await _repository.GetContainer(containerIdLadders);
                var userLadder = container
                    .GetItemLinqQueryable<UserLadder>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                return userLadder;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue resetting user block in Cosmos DB: {ex.Message}.");
                return null;
            }
        }
    }
}
