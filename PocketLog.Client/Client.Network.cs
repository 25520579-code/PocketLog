using System.Net.Sockets;
using PocketLog.Shared;

namespace PocketLog.Client
{
    public partial class Client
    {
        // ====================================================================
        // TRUYỀN/NHẬN GÓI TIN TCP & ĐỒNG BỘ DỮ LIỆU
        // ====================================================================
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
            try
            {
                while (isConnected && netStream != null && tcpClient != null && tcpClient.Connected)
                {
                    TcpPacket? packet = await TcpPacketHelper.ReadPacketAsync(netStream);
                    if (packet == null) break;

                    switch (packet.Type)
                    {
                        case PacketType.LOGIN_RES:
                            {
                                var res = packet.GetPayload<LoginResponseDto>();
                                if (res != null && res.Success)
                                {
                                    currentUserId = res.UserId;
                                    currentUsername = res.Username;
                                    currentFullName = res.FullName;
                                    totalBudgetLimit = res.MonthlyBudget;

                                    BeginInvoke(new Action(() =>
                                    {
                                        lblHeaderUser.Text = $"👤 {currentFullName}";
                                        btnLogoutTop.Visible = true;
                                        pnlBottomNav.Visible = true;
                                        activeTabName = "FEED";
                                        ActivateMenu(btnTabFeed, BuildFeedScreen());
                                    }));

                                    await SendPacketSafeAsync(TcpPacket.Create(PacketType.SYNC_REQ, currentUserId));
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
            catch { }
        }

        private void ApplySyncedDataToUI(SyncDataResponseDto data)
        {
            totalBudgetLimit = data.MonthlyBudget > 0 ? data.MonthlyBudget : totalBudgetLimit;

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

            posts.Clear();
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

            taggedDebts.Clear();
            foreach (var b in data.SplitBills)
            {
                taggedDebts.Add(new TaggedDebtItem
                {
                    SplitId = b.SplitId,
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

            if (activeTabName == "FEED")
                ActivateMenu(btnTabFeed, BuildFeedScreen());
            else if (activeTabName == "EXPENSE")
                ActivateMenu(btnTabExpense, BuildExpenseScreen());

            if (pnlDrawerBackdrop.Visible)
                RenderFriendsDrawerContent("");
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
