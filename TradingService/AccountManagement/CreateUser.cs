using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using TradingService.AccountManagement.Enums;
using TradingService.Common.Repository;

namespace TradingService.AccountManagement
{
    public static class CreateUser
    {
        [FunctionName("CreateUser")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var userId = req.Headers["From"].FirstOrDefault();

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User id has not been provided.");
            }

            const string containerId = "Users";
            var container = await Repository.GetContainer(containerId);
            return new OkResult();
            //var userToAdd = new Models.User
            //{
            //    Id = Guid.NewGuid().ToString(),
            //    DateCreated = DateTime.Now,
            //    UserId = userId,
            //    HasEnteredKeys = false,
            //    AccountType = AccountTypes.NotSet
            //};

            //try
            //{
            //    var user = container.GetItemLinqQueryable<Models.User>(allowSynchronousQueryExecution: true)
            //        .Where(u => u.UserId == userId).ToList().FirstOrDefault();

            //    if (user != null) return new OkObjectResult(user);

            //    var newUserResponse = await container.CreateItemAsync(userToAdd,
            //            new PartitionKey(userToAdd.UserId));
            //    return new OkObjectResult(newUserResponse.Resource.ToString());
            //}
            //catch (CosmosException ex)
            //{
            //    log.LogError("Issue creating new symbol in Cosmos DB {ex}", ex);
            //    return new BadRequestObjectResult("Error while creating new user in Cosmos DB: " + ex);
            //}
            //catch (Exception ex)
            //{
            //    log.LogError("Issue creating new symbol {ex}", ex);
            //    return new BadRequestObjectResult("Error while creating user: " + ex);
            //}
        }
    }
}
