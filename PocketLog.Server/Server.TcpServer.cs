using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using PocketLog.Shared;

namespace PocketLog.Server
{
    public partial class Server
    {
        private void StartTcpServer()
        {
            if (isRunning) return;
            try
            {
                tcpListener = new TcpListener(IPAddress.Any, 8888);
                tcpListener.Start();
                isRunning = true;

                lblStatus.Text = "● SERVER ĐANG HOẠT ĐỘNG (PORT 8888)";
                lblStatus.ForeColor = Color.FromArgb(46, 204, 113);
                btnToggleServer.Text = "⏹ Dừng Server";
                btnToggleServer.BackColor = Color.FromArgb(192, 57, 43);

                Log("✓ Đã mở cổng TCP 8888. Sẵn sàng nhận kết nối...");

                Task.Run(async () =>
                {
                    while (isRunning && tcpListener != null)
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

        private void StopTcpServer()
        {
            if (!isRunning) return;
            isRunning = false;

            try { tcpListener?.Stop(); } catch { }
            tcpListener = null;

            foreach (var client in connectedClients.Keys.ToList())
            {
                try { client.Close(); } catch { }
            }
            connectedClients.Clear();
            clientSendLocks.Clear();

            if (!IsDisposed)
            {
                RefreshOnlineListUI();
                lblStatus.Text = "● SERVER ĐÃ TẠM DỪNG";
                lblStatus.ForeColor = Color.FromArgb(231, 76, 60);
                btnToggleServer.Text = "▶ Bật lại Server";
                btnToggleServer.BackColor = Color.FromArgb(29, 99, 76);
                Log("⏹ Đã dừng TCP Server và đóng các kết nối hiện tại.");
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            string remoteEp = "Unknown";
            try { remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "Unknown"; } catch { }

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
                if (isRunning)
                {
                    Log($"⚠ Ngắt kết nối từ {remoteEp}: {ex.Message}");
                }
            }
            finally
            {
                connectedClients.TryRemove(client, out _);
                if (clientSendLocks.TryRemove(client, out var sem))
                {
                    try { sem.Dispose(); } catch { }
                }
                try { client.Close(); } catch { }

                if (isRunning && !IsDisposed)
                {
                    RefreshOnlineListUI();
                    await BroadcastRefreshToAllClientsAsync();
                }
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