using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using TradingService.Common.Models;
using TradingService.Common.Order;

namespace TradingService.ManageOrders
{
    public static class CloseOpenPosition
    {
        private static readonly string endpointUri = Environment.GetEnvironmentVariable("EndPointUri"); // ToDo: Centralize config values to common project?
        private static readonly string primaryKey = Environment.GetEnvironmentVariable("PrimaryKey");

        private static readonly CosmosClient cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
        private static Database _database;
        private static Container _containerArchive;

        private static readonly string databaseId = "Tracker";
        private static readonly string containerArchiveId = "BlocksArchive";

        [FunctionName("CloseOpenPosition")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to close open positions for symbol.");
            
            _database = await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            _containerArchive = await _database.CreateContainerIfNotExistsAsync(containerArchiveId, "/symbol");

            // Get symbol name
            string symbol = req.Query["symbol"];
            var block = await Order.CloseOpenPositionAndCancelExistingOrders(symbol);

            // ToDo: Move archive block to common module
            await ArchiveBlock(block, block.ExecutedSellPrice);
            log.LogInformation("Created archive record for block id {block.Id} at: {time}", block.Id, DateTimeOffset.Now);
            
            return new OkResult();
        }

        private static async Task ArchiveBlock(Block block, decimal executedSellPrice)
        {
            // ToDo: Create a new object for archive block, only keep the fields relevant to archive, add profit field
            var archiveBlockJson = JsonConvert.SerializeObject(block);
            var archiveBlock = JsonConvert.DeserializeObject<Block>(archiveBlockJson); //deep copy object
            archiveBlock.Id = Guid.NewGuid().ToString();
            archiveBlock.ExecutedSellPrice = executedSellPrice;

            await _containerArchive.CreateItemAsync<Block>(archiveBlock, new PartitionKey(archiveBlock.Symbol));
        }

    }
}
