using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Queues;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using TradeUpdateService.Models;
using TradeUpdateService.Enums;
using Newtonsoft.Json;

namespace TradeUpdateService
{
    public class CreateOrders : ICreateOrders
    {
        private readonly IConfiguration _configuration;

        public CreateOrders(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> CreateBuySellOrders()
        {
            // The Azure Cosmos DB endpoint for running this sample.
            var endpointUri = _configuration.GetValue<string>("EndPointUri");

            // The primary key for the Azure Cosmos account.
            var primaryKey = _configuration.GetValue<string>("PrimaryKey");

            // Connect to Cosmos DB using endpoint
            var cosmosClient = new CosmosClient(endpointUri, primaryKey, new CosmosClientOptions() { ApplicationName = "TradingService" });
            const string databaseId = "TMS";
            const string containerId = "Accounts";
            const string containerSymbolsId = "Symbols";

            var database = (Database)await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
            var container = (Container)await database.CreateContainerIfNotExistsAsync(containerId, "/userId");
            var containerSymbols = (Container)await database.CreateContainerIfNotExistsAsync(containerSymbolsId, "/userId");

            var accounts = container
                .GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true).ToList();

            // Get the connection string from app settings
            var connectionString = _configuration.GetValue<string>("AzureWebJobsStorage");

            // Instantiate a QueueClient which will be used to create and manipulate the queue
            var queueClientLong = new QueueClient(connectionString, "swingbuyorderqueue");
            var queueClientShort = new QueueClient(connectionString, "swingsellorderqueue");
            await queueClientLong.CreateIfNotExistsAsync();
            await queueClientShort.CreateIfNotExistsAsync();

            foreach (var account in accounts.Where(account => account.AccountType == AccountTypes.SwingLong || account.AccountType == AccountTypes.SwingShort))
            {
                // Read symbols for user from Cosmos DB
                var userSymbolResponse = containerSymbols
                    .GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == account.UserId).ToList().FirstOrDefault();

                if (userSymbolResponse != null)
                {
                    var symbols = userSymbolResponse.Symbols;

                    if (symbols == null) continue;

                    foreach (var symbol in symbols.Where(s => s.Trading))
                    {
                        var msg = new OrderCreationMessage
                        {
                            UserId = account.UserId,
                            Symbol = symbol.Name
                        };

                        if (account.AccountType == AccountTypes.SwingLong)
                        {
                            await queueClientLong.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(msg)));
                        }
                        else
                        {
                            await queueClientShort.SendMessageAsync(Base64Encode(JsonConvert.SerializeObject(msg)));
                        }
                    }
                }
            }

            return true;
        }

        private static string Base64Encode(string plainText) // ToDo: Make this a common function
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
}