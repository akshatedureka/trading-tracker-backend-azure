using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.Data.AppConfiguration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TradingService.Core.Interfaces.Persistence;
using TradingService.Core.Entities;

namespace TradingService.AccountManagement
{
    public class UpdateAccountInformation
    {
        private readonly IAccountItemRepository _accountRepo;

        public UpdateAccountInformation(IAccountItemRepository accountRepo)
        {
            _accountRepo = accountRepo;
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

            try
            {
                var accounts = await _accountRepo.GetItemsAsyncByUserId(userId);
                var accountInformation = accounts.FirstOrDefault();

                if (accountInformation == null) return new NotFoundObjectResult("Account information not found.");

                accountInformation.HasEnteredKeys = true;
                accountInformation.AccountType = account.AccountType;
                accountInformation.Email = email;

                var updatedAccount = await _accountRepo.UpdateItemAsync(accountInformation);
 
                return new OkObjectResult(updatedAccount.ToString());
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