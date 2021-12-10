
namespace TradingService.Core.Models
{
    public class ResetBlockMessage
    {
        public string BlockId { get; set; }
        public string UserId { get; set; }
        public string Symbol { get; set; }
    }
}
