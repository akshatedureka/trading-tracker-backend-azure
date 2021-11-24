﻿using Alpaca.Markets;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingService.Common.Models;
using TradingService.Common.Order;
using TradingService.Common.Repository;
using TradingService.TradeManagement.Swing.Common;

namespace TradingService.TradeManagement.Swing.BusinessLogic
{
    public class TradeManagementHelper : ITradeManagementHelper
    {
        private readonly IConfiguration _configuration;
        private readonly IQueries _queries;
        private readonly IRepository _repository;
        private readonly ITradeOrder _order;

        public TradeManagementHelper(IConfiguration configuration, IRepository repository, IQueries queries, ITradeOrder order)
        {
            _configuration = configuration;
            _repository = repository;
            _queries = queries;
            _order = order;
        }

        public List<Block> GetBlocksWithoutOpenOrders()
        {
            throw new NotImplementedException();
        }

        public List<IOrder> GetOrdersWithoutOpenBlocks()
        {
            throw new NotImplementedException();
        }

        public async Task CreateLongBracketOrdersBasedOnCurrentPrice(List<Block> blocks, string userId, string symbol, ILogger log)
        {
            var currentPrice = await _order.GetCurrentPrice(_configuration, userId, symbol);

            // Get blocks above and below the current price to create buy orders
            var blocksAbove = GetLongBlocksAboveCurrentPriceByPercentage(blocks, currentPrice, 5);
            var blocksBelow = GetLongBlocksBelowCurrentPriceByPercentage(blocks, currentPrice, 5);

            // Create limit / stop limit orders for each block above and below current price
            var countAboveAndBelow = 2;

            // Two blocks above
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksAbove[x];
                var stopPrice = block.BuyOrderPrice - (decimal)0.05;

                if (block.BuyOrderCreated) continue; // Order already exists

                var orderIds = await _order.CreateStopLimitBracketOrder(_configuration, OrderSide.Buy, userId, symbol, block.NumShares, stopPrice, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
                log.LogInformation($"Created initial buy bracket orders for for user {userId} symbol {symbol} for stop price {stopPrice} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {block.StopLossOrderPrice} at {DateTime.Now}.");

                //ToDo: Refactor to combine with blocks below
                // Update Cosmos DB item
                var blockToUpdate = blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalSellOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.BuyOrderCreated = true;

                await _queries.UpdateBlock(blockToUpdate);
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket buy orders at {DateTime.Now}.");
            }

            // Two blocks below
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksBelow[x];

                if (block.BuyOrderCreated) continue; // Order already exists

                var orderIds = await _order.CreateLimitBracketOrder(_configuration, OrderSide.Buy, userId,symbol, block.NumShares, block.BuyOrderPrice, block.SellOrderPrice, block.StopLossOrderPrice);
                log.LogInformation($"Created initial buy bracket orders for for user {userId} symbol {symbol} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {block.StopLossOrderPrice} at {DateTime.Now}.");

                var blockToUpdate = blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalSellOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.BuyOrderCreated = true;

                await _queries.UpdateBlock(blockToUpdate);
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket buy orders at {DateTime.Now}.");
            }
        }

        public async Task CreateShortBracketOrdersBasedOnCurrentPrice(List<Block> blocks, string userId, string symbol, ILogger log)
        {
            var currentPrice = await _order.GetCurrentPrice(_configuration, userId, symbol);

            // Get blocks above and below the current price to create sell orders
            var blocksAbove = GetShortBlocksAboveCurrentPriceByPercentage(blocks, currentPrice, 5);
            var blocksBelow = GetShortBlocksBelowCurrentPriceByPercentage(blocks, currentPrice, 5);

            // Create limit / stop limit orders for each block above and below current price
            var countAboveAndBelow = 2;

            // Two blocks below
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksBelow[x];
                var stopPrice = block.SellOrderPrice + (decimal)0.05;

                if (block.SellOrderCreated) continue; // Order already exists

                var orderIds = await _order.CreateStopLimitBracketOrder(_configuration, OrderSide.Sell, userId, symbol, block.NumShares, stopPrice, block.SellOrderPrice, block.BuyOrderPrice, block.StopLossOrderPrice);
                log.LogInformation($"Created initial sell bracket orders for user {userId} symbol {symbol} for stop price {stopPrice} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {block.StopLossOrderPrice}  at {DateTime.Now}.");

                //ToDo: Refactor to combine with blocks below
                // Update Cosmos DB item
                var blockToUpdate = blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external order ids generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalSellOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.SellOrderCreated = true;

                await _queries.UpdateBlock(blockToUpdate);
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders at {DateTime.Now}.");
            }

            // Two blocks above
            for (var x = 0; x < countAboveAndBelow; x++)
            {
                var block = blocksAbove[x];

                if (block.SellOrderCreated) continue; // Order already exists

                var orderIds = await _order.CreateLimitBracketOrder(_configuration, OrderSide.Sell, userId, symbol, block.NumShares, block.SellOrderPrice, block.BuyOrderPrice, block.StopLossOrderPrice);
                log.LogInformation($"Created initial sell bracket orders for user {userId} symbol {symbol} limit price {block.SellOrderPrice} take profit price {block.BuyOrderPrice} stop loss price {block.StopLossOrderPrice} at {DateTime.Now}.");

                var blockToUpdate = blocks.FirstOrDefault(b => b.Id == block.Id);

                // Update with external buy id generated from Alpaca
                blockToUpdate.ExternalBuyOrderId = orderIds.TakeProfitId;
                blockToUpdate.ExternalSellOrderId = orderIds.ParentOrderId;
                blockToUpdate.ExternalStopLossOrderId = orderIds.StopLossOrderId;
                blockToUpdate.SellOrderCreated = true;

                await _queries.UpdateBlock(blockToUpdate);
                log.LogInformation($"Updated block id {blockToUpdate.Id} with initial bracket sell orders at {DateTime.Now}.");
            }
        }

        public async Task UpdateLongBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice, ILogger log)
        {
            // Buy order has been executed, update block to record buy order has been filled
            log.LogInformation($"Buy order executed for trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed buy price {executedBuyPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade block
            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

            // Update block designating buy order has been executed
            var blockToUpdate = blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId);

            if (blockToUpdate != null)
            {
                blockToUpdate.BuyOrderFilled = true;
                blockToUpdate.DateBuyOrderFilled = DateTime.Now;
                blockToUpdate.BuyOrderFilledPrice = executedBuyPrice;
                blockToUpdate.SellOrderCreated = true;

                await _queries.UpdateBlock(blockToUpdate);
            }
            else
            {
                log.LogError($"Could not find block for buy user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
                return;
            }

            log.LogInformation($"Saved block id {blockToUpdate.Id} to DB with sell order created flag to true at: {DateTimeOffset.Now}.");
        }

        public async Task UpdateLongSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice, ILogger log)
        {
            // Sell order has been executed, create new buy order in Alpaca, close and reset block
            log.LogInformation($"Sell order executed for trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId} executed sell price {executedSellPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade block
            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

            // Update block designating buy order has been executed
            var blockToUpdate = blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId);

            if (blockToUpdate != null)
            {
                log.LogInformation($"Sell order has been executed for block id {blockToUpdate.Id}, external id {externalOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Retry 3 times to get block to allow for records to be updated
                //var retryAttemptCount = 1;
                //const int maxAttempts = 3;
                //while (!blockToUpdate.BuyOrderFilled && retryAttemptCount <= maxAttempts)
                //{
                //    await Task.Delay(1000); // Wait one second in between attempts
                //    log.LogError($"Error while updating sell order executed. Buy order has not had BuyOrderFilled flag set to true yet. Retry attempt {retryAttemptCount}");
                //    userBlock = await _queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
                //    blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId);
                //    retryAttemptCount += 1;
                //}

                blockToUpdate.SellOrderFilledPrice = executedSellPrice;

                // Put message on a queue to be processed by a different function from a message queue
                await TradeManagementCommon.CreateClosedBlockMsg(log, _configuration, blockToUpdate);

                // Reset block
                await _queries.ResetUserBlock(blockToUpdate);

                log.LogInformation($"Reset long block id {blockToUpdate.Id} symbol {symbol} at: {DateTimeOffset.Now}.");
            }
            else
            {
                log.LogError($"Could not find block for sell for user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
                throw new Exception("Could not find block.");
            }
        }

        public async Task UpdateShortSellOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedSellPrice, ILogger log)
        {
            // Sell order has been executed, create new buy order in Alpaca, close and reset block
            log.LogInformation($"Sell order executed for swing short trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed sell price {executedSellPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade blocks
            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

            // Update block designating sell order has been executed
            //ToDo: Block could be found using either sell order or stop loss order id's
            var blockToUpdate = blocks.FirstOrDefault(b => b.ExternalSellOrderId == externalOrderId);

            if (blockToUpdate != null)
            {
                blockToUpdate.SellOrderFilled = true;
                blockToUpdate.DateSellOrderFilled = DateTime.Now;
                blockToUpdate.SellOrderFilledPrice = executedSellPrice;
                blockToUpdate.BuyOrderCreated = true;
            }
            else
            {
                log.LogError($"Could not find block for sell user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
                return;
            }

            await _queries.UpdateBlock(blockToUpdate);
            log.LogInformation($"Saved block id {blockToUpdate.Id} to DB with buy order created flag to true at: {DateTimeOffset.Now}.");
        }

        public async Task UpdateShortBuyOrderExecuted(string userId, string symbol, Guid externalOrderId, decimal executedBuyPrice, ILogger log)
        {
            // Buy order has been executed, update block to record buy order has been filled
            log.LogInformation($"Buy order executed for swing short trading block for user id {userId}, symbol {symbol}, external order id {externalOrderId}, executed buy price {executedBuyPrice} at: {DateTimeOffset.Now}.");

            // Get swing trade block
            var blocks = await _queries.GetBlocksByUserIdAndSymbol(userId, symbol);

            var blockToUpdate = blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId);

            if (blockToUpdate != null)
            {
                log.LogInformation($"Buy order has been executed for block id {blockToUpdate.Id}, external id {externalOrderId} and saved to DB at: {DateTimeOffset.Now}.");

                // Retry 3 times to get block to allow for records to be updated
                //var retryAttemptCount = 1;
                //const int maxAttempts = 3;
                //while (!blockToUpdate.SellOrderFilled && retryAttemptCount <= maxAttempts)
                //{
                //    await Task.Delay(1000); // Wait one second in between attempts
                //    log.LogError($"Error while updating buy order executed. Sell order has not had SellOrderFilled flag set to true yet. Retry attempt {retryAttemptCount}");
                //    userBlock = await _queries.GetUserBlockByUserIdAndSymbol(userId, symbol);
                //    blockToUpdate = userBlock.Blocks.FirstOrDefault(b => b.ExternalBuyOrderId == externalOrderId || b.ExternalStopLossOrderId == externalOrderId);
                //    retryAttemptCount += 1;
                //}

                blockToUpdate.BuyOrderFilledPrice = executedBuyPrice;

                // Put message on a queue to be processed by a different function from a message queue
                await TradeManagementCommon.CreateClosedBlockMsg(log, _configuration, blockToUpdate);

                // Reset block
                await _queries.ResetUserBlock(blockToUpdate);

                log.LogInformation($"Reset short block id {blockToUpdate.Id} symbol {symbol} at: {DateTimeOffset.Now}.");
            }
            else
            {
                log.LogError($"Could not find block for buy for user id {userId}, symbol {symbol}, external order id {externalOrderId} at: {DateTimeOffset.Now}.");
                throw new Exception("Could not find block.");
            }
        }

        private List<Block> GetLongBlocksAboveCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks above current price based on percentage
            var buyOrderPriceMaxAmount = currentPrice + (currentPrice * (percentage / 100));
            var blocksAbove = blocks.Where(b => b.BuyOrderPrice >= currentPrice && b.BuyOrderPrice <= buyOrderPriceMaxAmount).OrderBy(b => b.BuyOrderPrice).ToList();
            return blocksAbove;
        }

        private List<Block> GetLongBlocksBelowCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks below current price based on percentage
            var buyOrderPriceMaxAmount = currentPrice - (currentPrice * (percentage / 100));
            var blocksBelow = blocks.Where(b => b.BuyOrderPrice < currentPrice && b.BuyOrderPrice >= buyOrderPriceMaxAmount).OrderByDescending(b => b.BuyOrderPrice).ToList();
            return blocksBelow;
        }

        private List<Block> GetShortBlocksAboveCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks above current price based on percentage
            var sellOrderPriceMaxAmount = currentPrice + (currentPrice * (percentage / 100));
            var blocksAbove = blocks.Where(b => b.SellOrderPrice >= currentPrice && b.SellOrderPrice <= sellOrderPriceMaxAmount).OrderBy(b => b.SellOrderPrice).ToList();
            return blocksAbove;
        }

        private List<Block> GetShortBlocksBelowCurrentPriceByPercentage(List<Block> blocks, decimal currentPrice, decimal percentage)
        {
            // Get blocks below current price based on percentage
            var sellOrderPriceMaxAmount = currentPrice - (currentPrice * (percentage / 100));
            var blocksBelow = blocks.Where(b => b.SellOrderPrice < currentPrice && b.SellOrderPrice >= sellOrderPriceMaxAmount).OrderByDescending(b => b.SellOrderPrice).ToList();
            return blocksBelow;
        }

    }
}
