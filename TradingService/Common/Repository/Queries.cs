﻿using System;
using System.Threading.Tasks;
using TradingService.SymbolManagement.Models;
using System.Linq;
using TradingService.Common.Models;

namespace TradingService.Common.Repository
{
    public static class Queries
    {
        public static async Task<UserSymbol> GetUserSymbolByUserId(string userId)
        {
            const string databaseId = "Tracker";
            const string containerId = "Symbols";
            var container = await Repository.GetContainer(databaseId, containerId);

            try
            {
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();
                return userSymbol;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting user symbol from Cosmos DB: {ex.Message}.");
                throw;
            }
        }

        public static async Task<Symbol> GetSymbolByUserIdAndSymbolName(string userId, string symbolName)
        {
            const string databaseId = "Tracker";
            const string containerId = "Symbols";
            var container = await Repository.GetContainer(databaseId, containerId);

            try
            {
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                var symbol = userSymbol.Symbols.FirstOrDefault(s => s.Name == symbolName);
                return symbol;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting symbol from Cosmos DB: {ex.Message}.");
                throw;
            }
        }

        public static async Task<UserBlock> GetUserBlockByUserIdAndSymbol(string userId, string symbol)
        {
            const string databaseId = "Tracker";
            const string containerId = "Blocks";
            var container = await Repository.GetContainer(databaseId, containerId);

            try
            {
                var userBlock = container.GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId && s.Symbol == symbol).ToList().FirstOrDefault();
                return userBlock;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Issue getting user block from Cosmos DB: {ex.Message}.");
                throw;
            }
        }
    }
}