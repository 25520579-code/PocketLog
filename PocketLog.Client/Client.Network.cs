using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Threading.Tasks;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        private async Task SendPacketSafeAsync(TcpPacket packet)
        {
            if (!isConnected || netStream == null) return;
            await sendLock.WaitAsync();
            try
            {
                await TcpPacketHelper.SendPacketAsync(netStream, packet);
            }
            catch { }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task ListenFromServerLoopAsync()
        {
            bool unexpectedDisconnect = false;
            try
            {
                while (isConnected && netStream != null && tcpClient != null && tcpClient.Connected)
                {
                    TcpPacket? packet = await TcpPacketHelper.ReadPacketAsync(netStream);
                    if (packet == null)
                    {
                        unexpectedDisconnect = isConnected && currentUserId > 0;
                        break;
                    }

                    switch (packet.Type)
                    {
                        case PacketType.LOGIN_RES:
                            {
                                var res = packet.GetPayload<LoginResponseDto>();
                                if (res != null && res.Success)
                                {
                                    onAuthFailedUI = null;
                                    currentUserId = res.UserId;
                                    currentUsername = res.Username;
                                    currentFullName = res.FullName;
                                    totalBudgetLimit = res.MonthlyBudget;
                                    InitializeDefaultBudgets();

                                    BeginInvoke(new Action(() =>
                                    {
                                        UpdateHeaderUserUI(currentFullName);
                                        pnlBottomNav.Visible = true;
                                        OpenInitialAuthenticatedTab("FEED");
                                    }));

                                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
                                }
                                else
                                {
                                    string errMsg = res?.Message ?? "Đăng nhập thất bại!";
                                    CloseCurrentSocket();
                                    BeginInvoke(new Action(() => onAuthFailedUI?.Invoke(errMsg)));
                                    return;
                                }
                                break;
                            }

                        case PacketType.ADD_FRIEND_RES:
                            {
                                var res = packet.GetPayload<AddFriendResponseDto>();
                                if (res != null)
                                {
                                    BeginInvoke(new Action(() =>
                                    {
                                        friendDrawerFeedback = res.Message;
                                        friendDrawerFeedbackSuccess = res.Success;
                                        if (pnlDrawerBackdrop.Visible)
                                        {
                                            RenderFriendsDrawerContent("");
                                        }
                                    }));
                                }
                                break;
                            }

                        case PacketType.SYNC_RES:
                            {
                                var syncData = packet.GetPayload<SyncDataResponseDto>();
                                if (syncData != null)
                                {
                                    BeginInvoke(new Action(() => ApplySyncedDataToUI(syncData)));
                                }
                                break;
                            }

                        case PacketType.REFRESH_BROADCAST:
                            {
                                if (currentUserId > 0)
                                {
                                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
                                }
                                break;
                            }
                    }
                }
            }
            catch
            {
                unexpectedDisconnect = isConnected && currentUserId > 0;
            }

            bool authInterrupted = isConnected && currentUserId == 0;
            if (unexpectedDisconnect && !IsDisposed)
            {
                CloseCurrentSocket();
                BeginInvoke(new Action(() =>
                {
                    DisposeCachedImages();
                    currentUserId = 0;
                    currentFullName = "";
                    ShowAuthScreen("Mất kết nối tới Server! Vui lòng kết nối lại.");
                }));
            }
            else if (authInterrupted && !IsDisposed)
            {
                CloseCurrentSocket();
                BeginInvoke(new Action(() => onAuthFailedUI?.Invoke("Server đã ngắt kết nối trong lúc đăng nhập!")));
            }
        }

        private void ApplySyncedDataToUI(SyncDataResponseDto data)
        {
            totalBudgetLimit = data.MonthlyBudget > 0 ? data.MonthlyBudget : totalBudgetLimit;

            if (data.CategoryBudgets != null)
            {
                foreach (var kv in data.CategoryBudgets)
                {
                    if (kv.Value > 0)
                        categoryBudgets[kv.Key] = kv.Value;
                }
            }

            myFriends.Clear();
            foreach (var f in data.MyFriends)
            {
                myFriends.Add(new FriendUser
                {
                    UserId = f.UserId,
                    Name = f.FullName,
                    Handle = f.Username,
                    IsOnline = f.IsOnline
                });
            }

            friendSuggestions.Clear();
            foreach (var s in data.SuggestedUsers)
            {
                friendSuggestions.Add(new FriendUser
                {
                    UserId = s.UserId,
                    Name = s.FullName,
                    Handle = s.Username,
                    IsOnline = s.IsOnline
                });
            }

            DisposeCachedImages();

            foreach (var p in data.Posts)
            {
                bool mine = (p.AuthorId == currentUserId);
                posts.Add(new LocketPost
                {
                    PostId = p.PostId,
                    IsMine = mine,
                    Author = mine ? $"{p.AuthorName} (Bạn)" : p.AuthorName,
                    HasTag = p.HasTag,
                    TaggedFriends = p.TaggedFriends ?? new List<string>(),
                    Category = p.Category,
                    NoteTop = p.NoteTop,
                    CaptionInside = p.CaptionInside,
                    Amount = p.Amount,
                    Time = p.Time,
                    Emoji = GetCategoryEmoji(p.Category),
                    PhotoBg = mine ? Color.FromArgb(82, 122, 106) : Color.FromArgb(148, 128, 114),
                    CustomImage = Base64ToImage(p.ImageBase64)
                });
            }

            foreach (var b in data.SplitBills)
            {
                taggedDebts.Add(new TaggedDebtItem
                {
                    SplitId = b.SplitId,
                    PostId = b.PostId,
                    CreditorId = b.CreditorId,
                    CreditorName = b.CreditorName,
                    DebtorId = b.DebtorId,
                    DebtorName = b.DebtorName,
                    Title = b.Title,
                    Category = b.Category,
                    TotalAmount = b.TotalAmount,
                    MyShare = b.AmountOwed,
                    SplitDetail = b.SplitDetail,
                    Time = b.Time,
                    IsSentProof = b.IsSentProof,
                    IsConfirmedPaid = b.IsPaid,
                    ProofImage = Base64ToImage(b.ProofImageBase64),
                    StatusNote = b.StatusNote
                });
            }

            RefreshDataDrivenPanesOnSync();

            if (pnlDrawerBackdrop.Visible)
                RenderFriendsDrawerContent(currentFriendsSearchKeyword);
        }

        private void CloseCurrentSocket()
        {
            isConnected = false;
            try { netStream?.Close(); } catch { }
            try { tcpClient?.Close(); } catch { }
            netStream = null;
            tcpClient = null;
        }
    }
}