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
using TradingService.AccountManagement.Models;
using TradingService.Common.Repository;

namespace TradingService.AccountManagement
{
    public static class GetAccountInformation
    {
        [FunctionName("GetAccountInformation")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            var userId = req.Headers["From"].FirstOrDefault();
            var email = req.Headers["email"];

            if (string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("User id has not been provided.");
            }

            const string databaseId = "Tracker";
            const string containerId = "Accounts";
            var container = await Repository.GetContainer(databaseId, containerId);

            try
            {
                var account = container.GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true)
                    .Where(u => u.UserId == userId).ToList().FirstOrDefault();

                // ToDo: Check if email address changed, if so, update it
                
                if (account != null) return new OkObjectResult(account);
                
                // Create new account if it does not exist
                var accountToCreate = new Account
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    HasEnteredKeys = false,
                    Email = email,
                    AccountType = AccountTypes.NotSet
                };

                var newAccountResponse = await container.CreateItemAsync(accountToCreate,
                        new PartitionKey(accountToCreate.UserId));
                return new OkObjectResult(newAccountResponse.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError($"Issue getting / creating new account in Cosmos DB {ex}");
                return new BadRequestObjectResult("Error while creating new account in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError($"Issue getting / creating new account {ex}");
                return new BadRequestObjectResult("Error while creating account: " + ex);
            }
        }
    }
}