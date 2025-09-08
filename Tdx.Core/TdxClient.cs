using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tdx.Core.Parsers;
using Tdx.Core.Utils;

namespace Tdx.Core
{
    public class TdxClient : BaseStockClient
    {
        public TdxClient(bool heartbeat = false, bool autoRetry = false, bool raiseException = false)
            : base(heartbeat, autoRetry, raiseException) { }

        public async Task<TResponse?> CallAsync<TResponse>(BaseParser<TResponse> parser) where TResponse : class
        {
            var requestBytes = parser.Serialize();
            var responseBytes = await DoSendAndReceiveAsync(requestBytes);
            if (responseBytes == null)
            {
                return null;
            }
            return parser.Deserialize(responseBytes);
        }

        public async Task<bool> LoginAsync()
        {
            try
            {
                var response = await CallAsync(new LoginParser());
                return response != null;
            }
            catch (Exception ex)
            {
                Log.Error("Login failed.", ex);
                return false;
            }
        }

        public async Task<bool> ConnectAsync(string? ip = null, int port = 7709, CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(ip))
            {
                return await base.ConnectAsync(ip, port, cancellationToken);
            }

            var latencyTasks = TdxConstants.TdxHosts.Select(async host =>
            {
                try
                {
                    await using var client = new TdxClient(raiseException: true);
                    var stopwatch = Stopwatch.StartNew();
                    var connected = await client.ConnectAsync(host.Ip, host.Port, new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);
                    stopwatch.Stop();
                    return connected ? new { Host = host, Latency = stopwatch.ElapsedMilliseconds } : null;
                }
                catch
                {
                    return null;
                }
            }).ToList();

            var results = await Task.WhenAll(latencyTasks);
            var bestHost = results.Where(r => r != null).OrderBy(r => r!.Latency).FirstOrDefault();

            if (bestHost == null)
            {
                throw new Exception("No available servers found.");
            }

            return await base.ConnectAsync(bestHost.Host.Ip, bestHost.Host.Port, cancellationToken);
        }

        protected override async Task DoHeartBeatAsync()
        {
            await CallAsync(new HeartBeatParser());
        }

        public async Task<List<StockBar>?> GetSecurityBarsAsync(Market market, string code, KlineType klineType, ushort start, ushort count)
        {
            const int maxCountPerRequest = 800;
            var allBars = new List<StockBar>();
            ushort currentStart = start;
            ushort remainingCount = count;

            while (remainingCount > 0)
            {
                ushort requestCount = (ushort)Math.Min((int)remainingCount, maxCountPerRequest);
                var parser = new BarsParser(market, code, klineType, currentStart, requestCount);
                var part = await CallAsync(parser);

                if (part == null || part.Count == 0)
                {
                    break;
                }

                allBars.InsertRange(0, part);

                if (part.Count < maxCountPerRequest)
                {
                    break;
                }

                currentStart += (ushort)part.Count;
                remainingCount -= (ushort)part.Count;
            }

            return allBars;
        }

        public async Task<List<Quote>?> GetSecurityQuotesAsync(params (Market market, string code)[] stocks)
        {
            if (stocks == null || stocks.Length == 0)
            {
                return new List<Quote>();
            }
            return await CallAsync(new QuotesParser(stocks));
        }

        public async Task<StockCountResponse?> GetSecurityCountAsync(Market market)
        {
            return await CallAsync(new StockCountParser(market));
        }

        public async Task<List<StockListEntry>?> GetSecurityListAsync(Market market, ushort start, ushort count = 1600)
        {
            return await CallAsync(new StockListParser(market, start, count));
        }

        public async Task<List<Order>?> GetOrdersAsync(Market market, string code)
        {
            return await CallAsync(new OrdersParser(market, code));
        }

        public async Task<List<Order>?> GetHistoryOrdersAsync(Market market, string code, DateTime date)
        {
            return await CallAsync(new HistoryOrdersParser(market, code, date));
        }

        public async Task<List<Transaction>?> GetTransactionAsync(Market market, string code)
        {
            const ushort maxCountPerRequest = 1800;
            var allTransactions = new List<Transaction>();
            ushort start = 0;

            while (true)
            {
                var part = await CallAsync(new TransactionParser(market, code, start, maxCountPerRequest));
                if (part == null || part.Count == 0)
                {
                    break;
                }
                allTransactions.InsertRange(0, part);
                if (part.Count < maxCountPerRequest)
                {
                    break;
                }
                start += (ushort)part.Count;
            }
            return allTransactions;
        }

        public async Task<List<HistoryTransaction>?> GetHistoryTransactionAsync(Market market, string code, DateTime date)
        {
            const ushort maxCountPerRequest = 2000;
            var allTransactions = new List<HistoryTransaction>();
            ushort start = 0;

            while (true)
            {
                var part = await CallAsync(new HistoryTransactionParser(market, code, date, start, maxCountPerRequest));
                if (part == null || part.Count == 0)
                {
                    break;
                }
                allTransactions.InsertRange(0, part);
                if (part.Count < maxCountPerRequest)
                {
                    break;
                }
                start += (ushort)part.Count;
            }
            return allTransactions;
        }

        public async Task<List<object>> GetCompanyInfoAsync(Market market, string code)
        {
            var results = new List<object>();
            var categories = await CallAsync(new CompanyInfoCategoryParser(market, code));
            if (categories == null) return results;

            foreach (var category in categories)
            {
                var content = await CallAsync(new CompanyInfoContentParser(market, code, category.Filename, category.Start, category.Length));
                if (content != null)
                {
                    results.Add(new { Name = category.Name, Content = content.Content });
                }
            }

            var xdxr = await CallAsync(new XdxrInfoParser(market, code));
            if (xdxr != null)
            {
                results.Add(new { Name = "除权分红", Content = xdxr });
            }

            var finance = await CallAsync(new FinanceInfoParser(market, code));
            if (finance != null)
            {
                results.Add(new { Name = "财报", Content = finance });
            }

            return results;
        }

        public async Task<List<BlockEntry>?> GetBlockInfoAsync(BlockFileType blockFileType)
        {
            var meta = await CallAsync(new MetaParser(blockFileType));
            if (meta == null)
            {
                return null;
            }

            const uint chunkSize = 0x7530;
            var fileContent = new MemoryStream();

            for (uint start = 0; start < meta.Size; start += chunkSize)
            {
                var parser = new BlockInfoParser(blockFileType, start, chunkSize);
                var response = await CallAsync(parser);
                if (response?.Data != null)
                {
                    await fileContent.WriteAsync(response.Data, 0, response.Data.Length);
                }
            }

            var blockReader = new BlockReader();
            return blockReader.GetFlatData(fileContent.ToArray());
        }
    }
}
