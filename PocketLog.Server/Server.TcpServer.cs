using System.Net;
using System.Net.Sockets;
using PocketLog.Shared;

namespace PocketLog.Server
{
    public partial class Server
    {
        // ====================================================================
        // TCP LISTENER PORT 8888
        // ====================================================================
        private void StartTcpServer()
        {
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, 8888);
                tcpListener.Start();
                isRunning = true;
                lblStatus.Text = "● SERVER ĐANG HOẠT ĐỘNG (PORT 8888)";
                Log("✓ Đã mở cổng TCP 8888. Sẵn sàng nhận kết nối...");

                Task.Run(async () =>
                {
                    while (isRunning)
                    {
                        try
                        {
                            TcpClient client = await tcpListener.AcceptTcpClientAsync();
                            connectedClients[client] = 0;
                            RefreshOnlineListUI();
                            Log($"+ Máy mới kết nối: {client.Client.RemoteEndPoint}");
                            _ = Task.Run(() => HandleClientAsync(client));
                        }
                        catch
                        {
                            if (!isRunning) break;
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Log("❌ Không thể mở cổng 8888: " + ex.Message);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            try
            {
                while (client.Connected && isRunning)
                {
                    TcpPacket? packet = await TcpPacketHelper.ReadPacketAsync(stream);
                    if (packet == null) break;

                    await ProcessPacketAsync(client, stream, packet);
                }
            }
            catch (Exception ex)
            {
                Log($"⚠ Ngắt kết nối từ {client.Client.RemoteEndPoint}: {ex.Message}");
            }
            finally
            {
                connectedClients.TryRemove(client, out _);
                clientSendLocks.TryRemove(client, out _);
                client.Close();
                RefreshOnlineListUI();
                await BroadcastRefreshToAllClientsAsync();
            }
        }

        private async Task SendToClientSafeAsync(TcpClient client, TcpPacket packet)
        {
            if (client == null || !client.Connected) return;

            var sendLock = clientSendLocks.GetOrAdd(client, _ => new SemaphoreSlim(1, 1));
            await sendLock.WaitAsync();
            try
            {
                if (client.Connected)
                {
                    await TcpPacketHelper.SendPacketAsync(client.GetStream(), packet);
                }
            }
            catch
            {
                // Tránh crash nếu Client vừa ngắt kết nối đột ngột
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task BroadcastRefreshToAllClientsAsync()
        {
            var refreshPacket = TcpPacket.Create(PacketType.REFRESH_BROADCAST, "SYNC");
            foreach (var kv in connectedClients.Keys.ToList())
            {
                await SendToClientSafeAsync(kv, refreshPacket);
            }
        }
    }
}
