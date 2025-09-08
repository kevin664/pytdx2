using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Tdx.Core.Utils;

namespace Tdx.Core
{
    public class BaseStockClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private HeartbeatService? _heartbeatService;
        private readonly SemaphoreSlim _asyncLock = new(1, 1);

        private readonly bool _useHeartbeat;
        private readonly bool _autoRetry;
        private readonly bool _raiseException;

        protected string? Ip { get; private set; }
        protected int Port { get; private set; }

        public BaseStockClient(bool heartbeat = false, bool autoRetry = false, bool raiseException = false)
        {
            _useHeartbeat = heartbeat;
            _autoRetry = autoRetry;
            _raiseException = raiseException;
        }

        public async Task<bool> ConnectAsync(string ip, int port, CancellationToken cancellationToken = default)
        {
            Ip = ip;
            Port = port;

            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ip, port, cancellationToken);
                _stream = _tcpClient.GetStream();

                Log.Info($"Connected to {ip}:{port}");

                if (_useHeartbeat)
                {
                    _heartbeatService = new HeartbeatService(DoHeartBeatAsync);
                    _heartbeatService.Start();
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Connection to {ip}:{port} failed.", ex);
                if (_raiseException) throw;
                return false;
            }
        }

        protected virtual Task DoHeartBeatAsync()
        {
            // Can be overridden by derived classes to send a specific heartbeat packet.
            return Task.CompletedTask;
        }

        protected async Task<T?> SendWithRetryAsync<T>(Func<Task<T?>> action) where T : class
        {
            if (_heartbeatService != null)
            {
                _heartbeatService.UpdateLastAckTime();
            }

            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                Log.Error("Request failed.", ex);
                if (!_autoRetry)
                {
                    if (_raiseException) throw;
                    return null;
                }

                // Retry Logic
                var retryDelays = new[] { 100, 500, 1000, 2000 };
                foreach (var delay in retryDelays)
                {
                    try
                    {
                        await Task.Delay(delay);
                        await DisconnectAsync();
                        await ConnectAsync(this.Ip!, this.Port);
                        var result = await action();
                        if (result != null) return result;
                    }
                    catch (Exception retryEx)
                    {
                        Log.Error($"Retry attempt failed.", retryEx);
                    }
                }

                if (_raiseException) throw new Exception("Request failed after multiple retries.", ex);
                return null;
            }
        }

        private async Task<byte[]?> SendAndReceiveAsync(byte[] data)
        {
            if (_tcpClient == null || _stream == null || !_tcpClient.Connected)
            {
                throw new InvalidOperationException("Not connected to the server.");
            }

            await _asyncLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(data, 0, data.Length);

                var headerBuffer = new byte[16];
                await _stream.ReadExactlyAsync(headerBuffer, 0, 16);

                // Correctly parse the <IBIBHHH response header format
                using var reader = new BinaryReader(new MemoryStream(headerBuffer));
                reader.ReadUInt32(); // prefix (I)
                byte zipped = reader.ReadByte(); // zipped (B)
                reader.ReadUInt32(); // customize (I)
                reader.ReadByte();   // unknown (B)
                reader.ReadUInt16(); // msg_id (H)
                ushort zipSize = reader.ReadUInt16(); // zipsize (H)
                ushort _ = reader.ReadUInt16(); // unzip_size (H) - read to advance stream

                var bodyBuffer = new byte[zipSize];
                await _stream.ReadExactlyAsync(bodyBuffer, 0, zipSize);

                if (zipped == 0x1c) // Data is compressed
                {
                    using var compressedStream = new MemoryStream(bodyBuffer);
                    using var zlibStream = new ZLibStream(compressedStream, CompressionMode.Decompress);
                    using var resultStream = new MemoryStream();
                    await zlibStream.CopyToAsync(resultStream);
                    return resultStream.ToArray();
                }

                return bodyBuffer;
            }
            finally
            {
                _asyncLock.Release();
            }
        }

        protected async Task<byte[]?> DoSendAndReceiveAsync(byte[] data)
        {
             return await SendWithRetryAsync(async () => await SendAndReceiveAsync(data));
        }

        public async Task DisconnectAsync()
        {
            Log.Info("Disconnecting...");
            _heartbeatService?.Dispose();
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _heartbeatService = null;
            _stream = null;
            _tcpClient = null;
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();
            _asyncLock.Dispose();
        }
    }
}
