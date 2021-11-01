using System;
using System.Threading.Tasks;
using TradingService.SymbolManagement.Models;
using System.Linq;

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
                Console.WriteLine(ex.Message);
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
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
