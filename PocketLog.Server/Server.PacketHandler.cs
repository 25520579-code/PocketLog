using System.Net.Sockets;
using PocketLog.Shared;

namespace PocketLog.Server
{
    public partial class Server
    {
        // ====================================================================
        // XỬ LÝ GÓI TIN TCP
        // ====================================================================
        private async Task ProcessPacketAsync(TcpClient client, NetworkStream stream, TcpPacket packet)
        {
            switch (packet.Type)
            {
                case PacketType.LOGIN_REQ:
                    {
                        var req = packet.GetPayload<LoginRequestDto>();
                        var res = HandleLoginSql(req);
                        if (res.Success)
                        {
                            connectedClients[client] = res.UserId;
                            RefreshOnlineListUI();
                            Log($"🔑 [{res.FullName}] (ID={res.UserId}) đã đăng nhập.");
                        }
                        await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.LOGIN_RES, res));
                        await BroadcastRefreshToAllClientsAsync();
                        break;
                    }

                case PacketType.SYNC_REQ:
                    {
                        int userId = packet.GetPayload<int>();
                        var syncData = BuildSyncDataForUser(userId);
                        await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.SYNC_RES, syncData));
                        break;
                    }

                case PacketType.CREATE_POST_REQ:
                    {
                        var postDto = packet.GetPayload<PostDto>();
                        SaveNewPostToSql(postDto);
                        Log($"📸 [{postDto.AuthorName}] vừa đăng ảnh mới: {postDto.NoteTop}");
                        await BroadcastRefreshToAllClientsAsync();
                        break;
                    }

                case PacketType.ADD_FRIEND_REQ:
                    {
                        var req = packet.GetPayload<AddFriendRequestDto>();
                        AddFriendInSql(req);
                        Log($"👥 User#{req.RequesterId} vừa kết bạn với '{req.TargetKeyword}'");
                        await BroadcastRefreshToAllClientsAsync();
                        break;
                    }

                case PacketType.UPLOAD_PROOF_REQ:
                    {
                        var req = packet.GetPayload<UploadProofRequestDto>();
                        SaveProofImageToSql(req);
                        Log($"🧾 Đã nhận ảnh bill chuyển khoản cho khoản chia #{req.SplitId}");
                        await BroadcastRefreshToAllClientsAsync();
                        break;
                    }

                case PacketType.VERIFY_BILL_REQ:
                    {
                        var req = packet.GetPayload<VerifyBillRequestDto>();
                        VerifySplitBillInSql(req);
                        Log($"✓ [{req.VerifierName}] đã xác nhận hoàn tất khoản chia #{req.SplitId}");
                        await BroadcastRefreshToAllClientsAsync();
                        break;
                    }

                case PacketType.UPDATE_BUDGET_REQ:
                    {
                        var req = packet.GetPayload<UpdateBudgetRequestDto>();
                        UpdateUserBudgetInSql(req);
                        Log($"💰 User#{req.UserId} cập nhật tổng hạn mức: {req.NewMonthlyBudget:N0}đ");
                        break;
                    }
            }
        }
    }
}
