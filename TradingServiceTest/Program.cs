using System;
using Microsoft.Azure.Cosmos;
using TradingService.AccountManagement.Enums;
using TradingService.AccountManagement.Models;
using TradingService.Common.Repository;
using Microsoft.Extensions.Configuration;
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using TradingService.SymbolManagement.Models;
using System.Collections.Generic;
using TradingService.Common.Models;
using TradingService.TradeManagement.Models;
using Alpaca.Markets;
using Azure.Storage.Queues;
using Newtonsoft.Json;

namespace TradingServiceTest
{
    class Program
    {
        public static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            BuildConfig(builder);

            var host = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddTransient<TestRun>();
                }).Build();

            var svc = ActivatorUtilities.CreateInstance<TestRun>(host.Services);
            svc.Run();
        }

        public static void BuildConfig(IConfigurationBuilder builder)
        {
            builder.SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: false, reloadOnChange: true).AddEnvironmentVariables();
        }

        public class TestRun // ToDo: Pull out to separate file
        {
            private readonly IConfiguration _config;

            public TestRun(IConfiguration config)
            {
                _config = config;
            }

            public void Run()
            {
                // ToDo: Figure out how to set the environment variables on run
                var endpointUri = _config.GetValue<string>("EndpointUri");
                Environment.SetEnvironmentVariable("EndpointUri", endpointUri);

                var primaryKey = _config.GetValue<string>("PrimaryKey");
                Environment.SetEnvironmentVariable("PrimaryKey", primaryKey);



                // Create user
                var userAccount = CreateUserAccountIfNotCreated();

                Console.WriteLine(userAccount.UserId);

                // Create user symbol
                var userSymbol = CreateUserSymbolIfNotCreated();

                // Create blocks
                var userBlocks = CreateBlocks();

                // Populate blocks for user with buy order created
                for (var x = 98; x < 103; x++)
                {
                    var buyOrderId = CreateBuyOrderForBlock(userBlocks, x.ToString());
                }

                // Message on queue for buy order filled
                var userBlockBuy = CreateQueueMsgForOrderFilledForBlock("100", OrderSide.Buy, 9.01M);

                // Message on queue for sell order filled
                var userBlockSell = CreateQueueMsgForOrderFilledForBlock("100", OrderSide.Sell, 10M);

            }

            private Account CreateUserAccountIfNotCreated()
            {
                const string containerId = "Accounts";
                const string userId = "1234";

                var container = Repository.GetContainer(containerId).Result;
                var account = container.GetItemLinqQueryable<Account>(allowSynchronousQueryExecution: true).Where(u => u.UserId == userId).ToList().FirstOrDefault();

                if (account != null) return account; // Return account if already created

                var accountToCreate = new Account
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    AccountType = AccountTypes.SwingLong
                };

                var newAccountResponse = container.CreateItemAsync(accountToCreate, new PartitionKey(accountToCreate.UserId)).Result;
                return newAccountResponse.Resource;
            }

            private UserSymbol CreateUserSymbolIfNotCreated()
            {
                const string containerId = "Symbols";
                const string userId = "1234";

                var container = Repository.GetContainer(containerId).Result;
                var userSymbol = container.GetItemLinqQueryable<UserSymbol>(allowSynchronousQueryExecution: true)
                    .Where(s => s.UserId == userId).ToList().FirstOrDefault();

                var symbolToAdd = new Symbol
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    NumShares = 100,
                    Name = "TNA",
                    Active = true
                };

                if (userSymbol == null) // Initial UserSymbol item creation
                {
                    // Create UserSymbol item for user with symbol added
                    var userSymbolToCreate = new UserSymbol()
                    {
                        Id = Guid.NewGuid().ToString(),
                        DateCreated = DateTime.Now,
                        UserId = userId,
                        Symbols = new List<Symbol>
                        {
                            symbolToAdd
                        }
                    };
                    var newUserSymbolResponse = container.CreateItemAsync(userSymbolToCreate, new PartitionKey(userSymbolToCreate.UserId)).Result;
                    return newUserSymbolResponse.Resource;
                }

                // Check if symbol is added already, if so, return a conflict result
                var existingSymbols = userSymbol.Symbols.ToList();
                if (existingSymbols.Any(s => s.Name == symbolToAdd.Name))
                {
                    return userSymbol;
                }

                // Add new symbol to existing UserSymbol item
                userSymbol.Symbols.Add(symbolToAdd);
                var addSymbolResponse = container.ReplaceItemAsync(userSymbol, userSymbol.Id, new PartitionKey(userSymbol.UserId)).Result;
                return addSymbolResponse.Resource;
            }

            private UserBlock CreateBlocks()
            {
                const string userId = "1234";
                const string containerId = "Blocks";
                const string symbol = "TNA";
                var container = Repository.GetContainer(containerId).Result;


                var block1 = new Block
                {
                    Id = "98",
                    DateCreated = DateTime.Now,
                    BuyOrderPrice = 5,
                    SellOrderPrice = 6
                };

                var block2 = new Block
                {
                    Id = "99",
                    DateCreated = DateTime.Now,
                    BuyOrderPrice = 7,
                    SellOrderPrice = 8
                };

                var block3 = new Block
                {
                    Id = "100",
                    DateCreated = DateTime.Now,
                    BuyOrderPrice = 9,
                    SellOrderPrice = 10
                };

                var block4 = new Block
                {
                    Id = "101",
                    DateCreated = DateTime.Now,
                    BuyOrderPrice = 11,
                    SellOrderPrice = 12
                };

                var block5 = new Block
                {
                    Id = "102",
                    DateCreated = DateTime.Now,
                    BuyOrderPrice = 13,
                    SellOrderPrice = 14
                };

                var blocks = new List<Block>
                {
                    block1,
                    block2,
                    block3,
                    block4,
                    block5
                };

                var userBlockToCreate = new UserBlock()
                {
                    Id = Guid.NewGuid().ToString(),
                    DateCreated = DateTime.Now,
                    UserId = userId,
                    Symbol = symbol,
                    NumShares = 100,
                    Blocks = blocks
                };

                var userBlockResponse = container
                    .GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId && b.Symbol == symbol).ToList().FirstOrDefault();

                // Delete blocks if already created to reset
                if (userBlockResponse != null)
                {
                    var deleteUserBlockResponse = container.DeleteItemAsync<UserBlock>(userBlockResponse.Id, new PartitionKey(userId));
                }

                var newUserBlockResponse = container.CreateItemAsync(userBlockToCreate, new PartitionKey(userBlockToCreate.UserId)).Result;
                return newUserBlockResponse.Resource;
            }

            private Guid CreateBuyOrderForBlock(UserBlock userBlock, string blockId)
            {
                const string containerId = "Blocks";
                var container = Repository.GetContainer(containerId).Result;

                var blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.Id == blockId);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = Guid.NewGuid();
                blockToUpdate.ExternalSellOrderId = Guid.NewGuid();
                blockToUpdate.ExternalStopLossOrderId = Guid.NewGuid();
                blockToUpdate.BuyOrderCreated = true;

                // Replace the item with the updated content
                var blockReplaceResponse = container.ReplaceItemAsync(userBlock, userBlock.Id, new PartitionKey(userBlock.UserId)).Result;
                return blockToUpdate.ExternalBuyOrderId;
            }

            private UserBlock CreateQueueMsgForOrderFilledForBlock(string blockId, OrderSide orderSide, decimal executedPrice)
            {
                const string userId = "1234";
                const string containerId = "Blocks";
                const string symbol = "TNA";

                // Get the queue connection string from app settings
                var connectionString = _config.GetValue<string>("AzureWebJobsStorageRemote");
                var queueName = "tradeupdatequeueswinglong";
                var queueClient = new QueueClient(connectionString, queueName);

                var container = Repository.GetContainer(containerId).Result;

                var userBlockResponse = container
                    .GetItemLinqQueryable<UserBlock>(allowSynchronousQueryExecution: true)
                    .Where(b => b.UserId == userId && b.Symbol == symbol).ToList().FirstOrDefault();

                var block = userBlockResponse.Blocks.Where(b => b.Id == blockId).FirstOrDefault();

                var orderId = orderSide == OrderSide.Buy ? block.ExternalBuyOrderId : block.ExternalSellOrderId;

                var msg = new OrderMessage
                { UserId = "1234", Symbol = "TNA", OrderId = orderId, OrderSide = orderSide, ExecutedPrice = executedPrice };

                // Send a message to the queue to simulate buy order being filled
                queueClient.SendMessage(Base64Encode(JsonConvert.SerializeObject(msg)));

                Console.WriteLine($"Message sent to queue for {orderSide} order filled for block id {blockId}");

                return userBlockResponse;
            }

            private static string Base64Encode(string plainText)
            {
                var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                return System.Convert.ToBase64String(plainTextBytes);
            }
        }
    }
}
