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
        private static readonly string containerIdBlocksArchive = "BlocksArchive";
        private static readonly string containerIdBlocksLTArchive = "BlocksLTArchive"; // long-term archive

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

        public static async Task<List<ArchiveBlock>> GetArchiveBlocksByUserIdAndSymbol(string userId, string symbol)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocksArchive);
                var archiveBlocks = container.GetItemLinqQueryable<ArchiveBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId && b.Symbol == symbol).ToList();
                return archiveBlocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting archives blocks from Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public static async Task<List<ArchiveBlock>> CreateLongTermArchiveBlocksByArchiveBlocks(List<ArchiveBlock> archiveBlocks)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocksLTArchive);
                foreach (var archiveBlock in archiveBlocks)
                {
                    await container.CreateItemAsync(archiveBlock, new PartitionKey(archiveBlock.UserId));
                }
                return archiveBlocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue creating long-term archive blocks in Cosmos DB: {ex.Message}.");
                return null;
            }
        }

        public static async Task<List<ArchiveBlock>> DeleteBlockArchivesByArchiveBlocks(List<ArchiveBlock> archiveBlocks)
        {
            try
            {
                var container = await Repository.GetContainer(containerIdBlocksArchive);
                foreach (var archiveBlock in archiveBlocks)
                {
                    await container.DeleteItemAsync<ArchiveBlock>(archiveBlock.Id, new PartitionKey(archiveBlock.UserId));
                }
                return archiveBlocks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue deleting block archives from Cosmos DB: {ex.Message}.");
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
