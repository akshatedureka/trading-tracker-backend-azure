using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using TradeUpdateService.Enums;
using TradeUpdateService.Models;

namespace TradeUpdateService
{
    public class DayTrading : IDayTrading
    {
        private readonly IConfiguration _configuration;
        private QueueClient _queueClient;

        public DayTrading(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> TriggerDayTrades()
        {
            // ToDo: check for job already running for user before enqueue
            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = _configuration.GetValue<string>("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = _configuration.GetValue<string>("PrimaryKey");

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            const string databaseId = "TMS";
            const string containerId = "Accounts";

            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/userId");

            var accounts = container
                .GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true).ToList();

            // Get the connection string from app settings
            var connectionString = _configuration.GetValue<string>("AzureWebJobsStorage");

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            //_queueClient = new QueueClient(connectionString, "daytradequeue");
            _queueClient = new QueueClient(connectionString, "daytrademarketqueue");

            // Create the queue if it doesn't already exist
            await _queueClient.CreateIfNotExistsAsync();

            //foreach (var account in accounts.Where(account => account.AccountType == AccountTypes.Day))
            //{
            //    await _queueClient.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(account.UserId)));
            //}

            foreach (var account in accounts.Where(account => account.AccountType == AccountTypes.DayLong || account.AccountType == AccountTypes.DayShort))
            {
                await _queueClient.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(account)));
            }

            return true;
        }

        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}