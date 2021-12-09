using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using TradingService.Common.Models;
using TradingService.Common.Repository;

namespace TradingService.TradeManagement
{
    public class GetClosedBlocks
    {
        private readonly IQueries _queries;
        private readonly IRepository _repository;

        public GetClosedBlocks(IRepository repository, IQueries queries)
        {
            _repository = repository;
            _queries = queries;
        }

        [FunctionName("GetClosedBlocks")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request to get closed blocks.");

            // The name of the database and container we will create
            const string containerId = "BlocksClosed";
            var blocks = new List<ClosedBlock>();

            // Read closed blocks from Cosmos DB
            try
            {
                var container = await _repository.GetContainer(containerId);
                using var setIterator = container.GetItemLinqQueryable<ClosedBlock>().ToFeedIterator();
                while (setIterator.HasMoreResults)
                {
                    blocks.AddRange(await setIterator.ReadNextAsync());
                }
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue getting closed blocks from Cosmos DB item {ex.Message}.");
            }

            return new OkObjectResult(JsonConvert.SerializeObject(blocks));
        }
    }
}
