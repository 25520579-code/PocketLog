using System.Net.Sockets;
using System.Threading.Tasks;
using PocketLog.Shared;

namespace PocketLog.Server
{
    public partial class Server
    {
        private async Task ProcessPacketAsync(TcpClient client, NetworkStream stream, TcpPacket packet)
        {
            switch (packet.Type)
            {
                case PacketType.LOGIN_REQ:
                    {
                        var req = packet.GetPayload<LoginRequestDto>();
                        var res = HandleLoginSql(req);
                        await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.LOGIN_RES, res));

                        if (res.Success)
                        {
                            connectedClients[client] = res.UserId;
                            RefreshOnlineListUI();
                            string actionTxt = req.IsRegister ? "đăng ký mới & đăng nhập" : "đăng nhập";
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
                        var res = AddFriendInSql(req);
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
                        var res = RemoveFriendInSql(req);
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
                        var syncData = BuildSyncDataForUser(req.UserId);
                        await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.SYNC_RES, syncData));
                        break;
                    }

                case PacketType.UPDATE_CATEGORY_BUDGET_REQ:
                    {
                        var req = packet.GetPayload<UpdateCategoryBudgetRequestDto>();
                        UpdateCategoryBudgetInSql(req);
                        Log($"📊 User#{req.UserId} đặt hạn mức '{req.Category}': {req.NewLimit:N0}đ");
                        var syncData = BuildSyncDataForUser(req.UserId);
                        await SendToClientSafeAsync(client, TcpPacket.Create(PacketType.SYNC_RES, syncData));
                        break;
                    }
            }
        }
    }
}