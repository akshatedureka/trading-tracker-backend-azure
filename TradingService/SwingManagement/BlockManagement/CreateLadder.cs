using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.SwingManagement.BlockManagement.Models;
using TradingService.Common.Repository;

namespace TradingService.SwingManagement.BlockManagement
{
    public static class CreateLadder
    {
        [FunctionName("CreateLadder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var userId = req.Headers["From"].FirstOrDefault();
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var ladderData = JsonConvert.DeserializeObject<Ladder>(requestBody);

            if (ladderData is null || string.IsNullOrEmpty(ladderData.Symbol) || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Required data is missing from request.");
            }

            const string databaseId = "Tracker";
            const string containerId = "Ladders";
            var container = await Repository.GetContainer(databaseId, containerId);

            // Create new ladder to save
            var ladderToAdd = new Ladder()
            {
                Id = Guid.NewGuid().ToString(),
                DateCreated = DateTime.Now,
                Symbol = ladderData.Symbol,
                InitialNumShares = ladderData.InitialNumShares,
                BuyPercentage = ladderData.BuyPercentage,
                SellPercentage = ladderData.SellPercentage,
                StopLossPercentage = ladderData.StopLossPercentage,
                BlocksCreated = false
            };

            try
            {
                var userLadder = container.GetItemLinqQueryable<UserLadder>(allowSynchronousQueryExecution: true)
                    .Where(l => l.UserId == userId).ToList().FirstOrDefault();

                if (userLadder == null) // Initial UserLadder item creation
                {
                    // Create UserLadder item for user with ladder added
                    var userLadderToCreate = new UserLadder()
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateCreated = DateTime.Now,
                        UserId = userId,
                        Ladders = new List<Ladder>
                        {
                            ladderToAdd
                        }
                    };
                    var newUserLadderResponse = await container.CreateItemAsync(userLadderToCreate,
                        new PartitionKey(userLadderToCreate.UserId));
                    return new OkObjectResult(newUserLadderResponse.Resource.ToString());
                }

                // Check if ladder is added already, if so, return a conflict result
                var existingLadders = userLadder.Ladders.ToList();
                if (existingLadders.Any(l => l.Symbol == ladderToAdd.Symbol))
                {
                    return new ConflictResult();
                }

                // Add new ladder to existing UserLadder item
                userLadder.Ladders.Add(ladderToAdd);
                var addLadderResponse =
                    await container.ReplaceItemAsync(userLadder, userLadder.Id, new PartitionKey(userLadder.UserId));
                return new OkObjectResult(addLadderResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("Issue creating new ladder in Cosmos DB {ex}", ex);
                return new BadRequestObjectResult("Error while creating new ladder in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError("Issue creating new ladder {ex}", ex);
                return new BadRequestObjectResult("Error while creating new ladder: " + ex);
            }
        }
    }
}