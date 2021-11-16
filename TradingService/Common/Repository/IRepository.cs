using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace TradingService.Common.Repository
{
    public interface IRepository
    {
        public Task<Container> GetContainer(string containerId);

        public Task<Container> GetContainer(string containerId, string databaseId, string partitionKey);
    }
}
