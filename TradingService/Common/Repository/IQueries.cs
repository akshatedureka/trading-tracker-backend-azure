using System.Collections.Generic;
using System.Threading.Tasks;
using TradingService.AccountManagement.Enums;
using TradingService.BlockManagement.Models;
using TradingService.Common.Models;
using TradingService.SymbolManagement.Models;

namespace TradingService.Common.Repository
{
    public interface IQueries
    {
        public Task<UserSymbol> GetUserSymbolByUserId(string userId);

        public Task<Symbol> GetSymbolByUserIdAndSymbolName(string userId, string symbolName);

        public Task<List<Symbol>> GetActiveTradingSymbolsByUserId(string userId);

        public Task<Symbol> UpdateTradingStatusForSymbol(string userId, string symbolName);

        public Task<List<Block>> GetBlocksByUserIdAndSymbols(string userId, List<Symbol> symbols);

        public Task<List<Block>> GetBlocksByUserIdAndSymbol(string userId, string symbol);

        public Task<UserBlock> GetUserBlockByUserIdAndSymbol(string userId, string symbol);
        public Task<bool> UpdateUserBlock(UserBlock userBlock);

        public Task<bool> DeleteBlocksByUserIdAndSymbol(string userId, string symbol);

        public Task<bool> DeleteBlockByBlockUserIdAndBlockId(string userId, string blockId);

        public Task<List<ClosedBlock>> GetClosedBlocksByUserIdAndSymbol(string userId, string symbol);

        public Task<bool> CreateCondensedBlockByUserIdAndSymbol(string userId, string symbol, decimal profit);

        public Task<bool> DeleteClosedBlocksByClosedBlocks(List<ClosedBlock> closedBlocks);

        public Task<bool> ResetUserBlocksByUserIdAndSymbol(string userId, string symbol);

        public Task<bool> ResetUserBlockByUserIdAndSymbol(string userId, string symbol, string blockId);

        public Task<AccountTypes> GetAccountTypeByUserId(string userId);

        public Task<UserLadder> GetLaddersByUserId(string userId);
    }
}
