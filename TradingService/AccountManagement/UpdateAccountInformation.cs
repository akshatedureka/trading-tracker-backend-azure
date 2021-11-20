using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Azure.Data.AppConfiguration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.AccountManagement.Models;
using TradingService.Common.Repository;

namespace TradingService.AccountManagement
{
    public class UpdateAccountInformation
    {
        private readonly IRepository _repository;

        public UpdateAccountInformation(IRepository repository)
        {
            _repository = repository;
        }

        [FunctionName("UpdateAccountInformation")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var account = JsonConvert.DeserializeObject<Account>(requestBody);
            var userId = req.Headers["From"].FirstOrDefault();
            var alpacaKeyFromHeader = req.Headers["alpacaKey"];
            var alpacaSecretFromHeader = req.Headers["alpacaSecret"];
            var email = req.Headers["email"];

            // ToDo: Validate keys are valid by getting account information from Alpaca

            if (account is null || string.IsNullOrEmpty(userId))
            {
                return new BadRequestObjectResult("Account information or user was not provided.");
            }

            // Add or update Alpaca config values in Azure
            var client = new ConfigurationClient(Environment.GetEnvironmentVariable("appConfiguration"));
            var settingAlpacaKey = new ConfigurationSetting("AlpacaPaperAPIKey" + ":" + userId, alpacaKeyFromHeader);
            var settingAlpacaSec = new ConfigurationSetting("AlpacaPaperAPISec" + ":" + userId, alpacaSecretFromHeader);

            var settingAlpacaKeyResponse = await client.SetConfigurationSettingAsync(settingAlpacaKey);
            var settingAlpacaSecretResponse = await client.SetConfigurationSettingAsync(settingAlpacaSec);

            const string containerId = "Accounts";

            try
            {
                var container = await _repository.GetContainer(containerId);
                var accountInformation = container.GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                if (accountInformation == null) return new NotFoundObjectResult("Account information not found.");

                accountInformation.HasEnteredKeys = true;
                accountInformation.AccountType = account.AccountType;
                accountInformation.Email = email;

                var updateAccount = await container.ReplaceItemAsync(accountInformation, accountInformation.Id,
                        new PartitionKey(accountInformation.UserId));
                return new OkObjectResult(updateAccount.Resource.ToString());
            }
            catch (CosmosException ex)
            {
                log.LogError("$Issue updating account in Cosmos DB {ex}");
                return new BadRequestObjectResult("Error while updating account in Cosmos DB: " + ex);
            }
            catch (Exception ex)
            {
                log.LogError($"Issue updating account {ex}");
                return new BadRequestObjectResult("Error while updating account: " + ex);
            }
        }
    }
}