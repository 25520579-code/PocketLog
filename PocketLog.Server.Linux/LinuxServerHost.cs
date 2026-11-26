using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PocketLog.Shared;

namespace PocketLog.Server.Linux
{
    public sealed class LinuxServerHost
    {
        private readonly IPocketLogStore store;
        private readonly int tcpPort;
        private readonly int udpPort;

        private TcpListener? tcpListener;
        private UdpClient? udpDiscovery;
        private volatile bool isRunning;

        private readonly ConcurrentDictionary<TcpClient, int> connectedClients = new();
        private readonly ConcurrentDictionary<TcpClient, SemaphoreSlim> clientSendLocks = new();

        public LinuxServerHost(IPocketLogStore store, int tcpPort = 8888, int udpPort = 8889)
        {
            this.store = store;
            this.tcpPort = tcpPort;
            this.udpPort = udpPort;
        }

        private static void Log(string msg)
        {
            Console.WriteLine($"[{ServerCommon.NowVn():yyyy-MM-dd HH:mm:ss}] {msg}");
        }

        public async Task RunAsync(CancellationToken ct)
        {
            store.EnsureDatabaseSetup();
            Log($"✓ Đã khởi tạo CSDL thành công: {store.BackendDescription}");

            tcpListener = new TcpListener(IPAddress.Any, tcpPort);
            tcpListener.Start();
            isRunning = true;
            Log($"✓ Đã mở cổng TCP {tcpPort}. Sẵn sàng nhận kết nối từ PocketLog.Client...");

            StartUdpDiscoveryLoop(ct);

            using var reg = ct.Register(Stop);

            try
            {
                while (isRunning && !ct.IsCancellationRequested)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync(ct);
                    connectedClients[client] = 0;
                    Log($"+ Máy mới kết nối: {client.Client.RemoteEndPoint} (Đang online: {connectedClients.Count})");
                    _ = Task.Run(() => HandleClientAsync(client, ct), CancellationToken.None);
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (!isRunning || ct.IsCancellationRequested) { }
            finally
            {
                Stop();
            }
        }

        public void Stop()
        {
            if (!isRunning) return;
            isRunning = false;

            try { udpDiscovery?.Close(); } catch { }
            udpDiscovery = null;

            try { tcpListener?.Stop(); } catch { }
            tcpListener = null;

            foreach (var c in connectedClients.Keys.ToList())
            {
                try { c.Close(); } catch { }
            }
            connectedClients.Clear();
            foreach (var sem in clientSendLocks.Values.ToList())
            {
                try { sem.Dispose(); } catch { }
            }
            clientSendLocks.Clear();
            Log("⏹ Đã dừng PocketLog Linux Server an toàn.");
        }

        private void StartUdpDiscoveryLoop(CancellationToken ct)
        {
            if (udpPort <= 0) return;
            try
            {
                udpDiscovery = new UdpClient(udpPort) { EnableBroadcast = true };
                Log($"📡 Đã mở cổng UDP {udpPort} (Broadcast Discovery).");

                _ = Task.Run(async () =>
                {
                    while (isRunning && udpDiscovery != null && !ct.IsCancellationRequested)
                    {
                        try
                        {
                            var result = await udpDiscovery.ReceiveAsync(ct);
                            string msg = Encoding.UTF8.GetString(result.Buffer).Trim();
                            if (msg == "POCKETLOG_DISCOVER_REQ")
                            {
                                string reply = $"POCKETLOG_DISCOVER_RES|{tcpPort}|{Environment.MachineName}";
                                byte[] replyBytes = Encoding.UTF8.GetBytes(reply);
                                await udpDiscovery.SendAsync(replyBytes, replyBytes.Length, result.RemoteEndPoint);
                            }
                        }
                        catch
                        {
                            if (!isRunning || ct.IsCancellationRequested) break;
                        }
                    }
                }, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log($"⚠ Không mở cổng UDP {udpPort} (không ảnh hưởng kết nối TCP {tcpPort}): {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
        {
            string remoteEp = "Unknown";
            try { remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "Unknown"; } catch { }

            try
            {
                NetworkStream stream = client.GetStream();
                while (client.Connected && isRunning && !ct.IsCancellationRequested)
                {
                    TcpPacket? packet = await TcpPacketHelper.ReadPacketAsync(stream);
                    if (packet == null) break;
                    await ProcessPacketAsync(client, packet);
                }
            }
            catch (Exception ex)
            {
                if (isRunning && !ct.IsCancellationRequested)
                    Log($"⚠ Ngắt kết nối từ {remoteEp}: {ex.Message}");
            }
            finally
            {
                connectedClients.TryRemove(client, out _);
                if (clientSendLocks.TryRemove(client, out var sem))
                {
                    try { sem.Dispose(); } catch { }
                }
                try { client.Close(); } catch { }

                if (isRunning && !ct.IsCancellationRequested)
                {
                    await BroadcastRefreshToAllClientsAsync();
                }
            }
        }

        private async Task ProcessPacketAsync(TcpClient client, TcpPacket packet)
        {
            try
            {
                if (packet.Type != PacketType.LOGIN_REQ &&
                    (!connectedClients.TryGetValue(client, out int authUid) || authUid <= 0))
                {
                    Log($"⚠ Bỏ qua gói {packet.Type} từ client chưa đăng nhập.");
                    return;
                }

                switch (packet.Type)
                {
                    case PacketType.LOGIN_REQ:
                        {
                            var req = packet.GetPayload<LoginRequestDto>();
                            var res = store.HandleLogin(req);
                            if (res.Success) connectedClients[client] = res.UserId;
                            await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.LOGIN_RES, res));

                            if (res.Success)
                            {
                                string actionTxt = (req?.IsRegister == true) ? "đăng ký mới & đăng nhập" : "đăng nhập";
                                Log($"🔑 [{res.FullName}] (ID={res.UserId}) đã {actionTxt}.");
                                await BroadcastRefreshToAllClientsAsync();
                            }
                            else
                            {
                                Log($"⚠ Từ chối xác thực '{req?.Username}': {res.Message}");
                            }
                            break;
                        }

                    case PacketType.SYNC_REQ:
                        {
                            int userId = connectedClients[client];
                            var onlineSet = connectedClients.Values.Where(id => id > 0).ToHashSet();
                            var syncData = store.BuildSyncDataForUser(userId, onlineSet);
                            await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.SYNC_RES, syncData));
                            break;
                        }

                    case PacketType.CREATE_POST_REQ:
                        {
                            var postDto = packet.GetPayload<PostDto>();
                            if (postDto == null) break;
                            postDto.AuthorId = connectedClients[client];
                            store.SaveNewPost(postDto);
                            Log($"📸 [{postDto.AuthorName}] vừa đăng ảnh mới: {postDto.NoteTop}");
                            await BroadcastRefreshToAllClientsAsync();
                            break;
                        }

                    case PacketType.ADD_FRIEND_REQ:
                        {
                            var req = packet.GetPayload<AddFriendRequestDto>();
                            if (req == null) break;
                            req.RequesterId = connectedClients[client];
                            var res = store.AddFriend(req);
                            await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.ADD_FRIEND_RES, res));
                            if (res.Success)
                            {
                                Log($"👥 User#{req.RequesterId} kết bạn thành công với '{req.TargetKeyword}'");
                                await BroadcastRefreshToAllClientsAsync();
                            }
                            break;
                        }

                    case PacketType.REMOVE_FRIEND_REQ:
                        {
                            var req = packet.GetPayload<AddFriendRequestDto>();
                            if (req == null) break;
                            req.RequesterId = connectedClients[client];
                            var res = store.RemoveFriend(req);
                            await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.ADD_FRIEND_RES, res));
                            if (res.Success)
                            {
                                Log($"💔 User#{req.RequesterId} đã hủy kết bạn với '{req.TargetKeyword}'");
                                await BroadcastRefreshToAllClientsAsync();
                            }
                            break;
                        }

                    case PacketType.UPLOAD_PROOF_REQ:
                        {
                            var req = packet.GetPayload<UploadProofRequestDto>();
                            if (req == null) break;
                            req.UserId = connectedClients[client];
                            store.SaveProofImage(req);
                            Log($"🧾 Đã nhận ảnh bill chuyển khoản cho khoản chia #{req.SplitId}");
                            await BroadcastRefreshToAllClientsAsync();
                            break;
                        }

                    case PacketType.VERIFY_BILL_REQ:
                        {
                            var req = packet.GetPayload<VerifyBillRequestDto>();
                            if (req == null) break;
                            store.VerifySplitBill(req);
                            Log($"✓ [{req.VerifierName}] đã xác nhận hoàn tất khoản chia #{req.SplitId}");
                            await BroadcastRefreshToAllClientsAsync();
                            break;
                        }

                    case PacketType.UPDATE_BUDGET_REQ:
                        {
                            var req = packet.GetPayload<UpdateBudgetRequestDto>();
                            if (req == null) break;
                            req.UserId = connectedClients[client];
                            store.UpdateUserBudget(req);
                            Log($"💰 User#{req.UserId} cập nhật tổng hạn mức: {req.NewMonthlyBudget:N0}đ");
                            var onlineSet = connectedClients.Values.Where(id => id > 0).ToHashSet();
                            var syncData = store.BuildSyncDataForUser(req.UserId, onlineSet);
                            await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.SYNC_RES, syncData));
                            break;
                        }

                    case PacketType.UPDATE_CATEGORY_BUDGET_REQ:
                        {
                            var req = packet.GetPayload<UpdateCategoryBudgetRequestDto>();
                            if (req == null) break;
                            req.UserId = connectedClients[client];
                            store.UpdateCategoryBudget(req);
                            Log($"📊 User#{req.UserId} đặt hạn mức '{req.Category}': {req.NewLimit:N0}đ");
                            var onlineSet = connectedClients.Values.Where(id => id > 0).ToHashSet();
                            var syncData = store.BuildSyncDataForUser(req.UserId, onlineSet);
                            await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.SYNC_RES, syncData));
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Lỗi xử lý gói {packet.Type}: {ex.Message}");
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
            catch { }
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
