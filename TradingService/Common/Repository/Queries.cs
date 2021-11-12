using System;
using System.Threading.Tasks;
using TradingService.SymbolManagement.Models;
using System.Linq;
using TradingService.Common.Models;
using Microsoft.Azure.Cosmos;
using System.Collections.Generic;
using TradingService.AccountManagement.Enums;
using TradingService.AccountManagement.Models;

namespace TradingService.Common.Repository
{
    public static class Queries
    {
        private static readonly string containerIdForAccounts = "Accounts";
        private static readonly string containerIdSymbols = "Symbols";
        private static readonly string containerIdBlocks = "Blocks";
        private static readonly string containerIdLadders = "Ladders";
        private static readonly string containerIdBlocksClosed = "BlocksClosed";
        private static readonly string containerIdBlocksCondensed = "BlocksCondensed"; // long-term archive

        public static async Task<UserSymbol> GetUserSymbolByUserId(string userId)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdSymbols);
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

        public static async Task<Symbol> GetSymbolByUserIdAndSymbolName(string userId, string symbolName)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdSymbols);
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

        public static async Task<Symbol> UpdateTradingStatusForSymbol(string userId, string symbolName)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdSymbols);
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

        public static async Task<UserBlock> GetUserBlockByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocks);
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

        public static async Task<UserBlock> DeleteUserBlockByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocks);
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

        public static async Task<List<ClosedBlock>> GetClosedBlocksByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocksClosed);
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

        public static async Task<List<ClosedBlock>> CreateCondensedBlocksByClosedBlocks(List<ClosedBlock> closedBlocks)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocksCondensed);
                foreach (var closedBlock in closedBlocks)
                {
                    await container.CreateItemAsync(closedBlock, new PartitionKey(closedBlock.UserId));
                }
                return closedBlocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue creating condensed blocks in Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public static async Task<List<ClosedBlock>> DeleteClosedBlocksByClosedBlocks(List<ClosedBlock> closedBlocks)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdForAccounts);
                foreach (var closedBlock in closedBlocks)
                {
                    await container.DeleteItemAsync<ClosedBlock>(closedBlock.Id, new PartitionKey(closedBlock.UserId));
                }
                return closedBlocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue deleting closed blocks from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public static async Task<UserBlock> ResetUserBlockByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocks);
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

                return userBlock;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue resetting user block in Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public static async Task<AccountTypes> GetAccountTypeByUserId(string userId)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdForAccounts);
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
    }
}
