using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace MessagingApp
{
    public partial class MessagingForm : Form
    {
        private sealed class ParsedChatEntry
        {
            public string RawEntry { get; init; } = string.Empty;
            public string Id { get; init; } = string.Empty;
            public string Sender { get; init; } = string.Empty;
            public string Timestamp { get; init; } = string.Empty;
            public string Body { get; init; } = string.Empty;
            public string ReplyToId { get; init; } = string.Empty;
            public bool IsControl { get; init; }
            public string ControlAction { get; init; } = string.Empty;
            public string TargetMessageId { get; init; } = string.Empty;
            public string ReactionEmoji { get; init; } = string.Empty;
        }

        private sealed class ConversationMessage
        {
            public string Id { get; init; } = string.Empty;
            public string Sender { get; init; } = string.Empty;
            public string Timestamp { get; init; } = string.Empty;
            public string Body { get; set; } = string.Empty;
            public string ReplyToId { get; init; } = string.Empty;
            public bool IsDeleted { get; set; }
            public Dictionary<string, string> ReactionsByUser { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> ReadBy { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ConversationState
        {
            public List<ConversationMessage> Messages { get; } = new List<ConversationMessage>();
            public Dictionary<string, ConversationMessage> ById { get; } = new Dictionary<string, ConversationMessage>(StringComparer.OrdinalIgnoreCase);
            public string LastPreview { get; set; } = string.Empty;
        }

        private sealed class RenderedMessageRange
        {
            public string MessageId { get; init; } = string.Empty;
            public int Start { get; init; }
            public int End { get; init; }
        }

        private const string MessageMetadataPrefix = "[#META#]";
        private readonly List<RenderedMessageRange> renderedMessages = new List<RenderedMessageRange>();
        private readonly Dictionary<string, string> outgoingMessageStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> pinnedChats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DateTime> typingIndicators = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private Panel replyPreviewPanel = null!;
        private Label replyPreviewLabel = null!;
        private Button clearReplyButton = null!;
        private Panel pinnedMessagePanel = null!;
        private Label pinnedMessageLabel = null!;
        private Button unpinMessageButton = null!;
        private ContextMenuStrip messageContextMenu = null!;
        private ContextMenuStrip chatListContextMenu = null!;
        private System.Windows.Forms.Timer typingIndicatorTimer = null!;
        private System.Windows.Forms.Timer typingStopTimer = null!;

        private ConversationState? activeConversationState;
        private string? replyTargetMessageId;
        private string replyTargetSender = string.Empty;
        private string replyTargetPreview = string.Empty;
        private string? pinnedMessageId;
        private string? contextMenuMessageId;
        private string? localTypingRecipient;
        private bool localTypingStateSent;

        private string PinnedChatsFilePath => Path.Combine(AppPaths.CurrentWorkspaceRoot, "PinnedChats.txt");

        private string GetPinnedMessageFilePath(string contactName)
        {
            string directory = Path.Combine(AppPaths.CurrentWorkspaceRoot, "PinnedMessages");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, GetConversationFileName(currentUserName, contactName) + ".txt");
        }

        private void InitializePinnedMessagePanel(Control chatHeaderInfoPanel)
        {
            pinnedMessagePanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Visible = false,
                BackColor = BlendColors(currentSurfaceColor, currentAccentColor, 0.06f),
                Padding = new Padding(10, 4, 8, 4)
            };

            pinnedMessageLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(fontFamily, 9f, FontStyle.Bold),
                ForeColor = currentTextColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            unpinMessageButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 68,
                Text = "Unpin",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            unpinMessageButton.Click += (_, _) => UnpinCurrentMessage();
            ConfigureActionButton(unpinMessageButton, BlendColors(currentSurfaceColor, currentAccentColor, 0.14f), currentTextColor);

            pinnedMessagePanel.Controls.Add(pinnedMessageLabel);
            pinnedMessagePanel.Controls.Add(unpinMessageButton);
            chatHeaderInfoPanel.Controls.Add(pinnedMessagePanel);
            pinnedMessagePanel.BringToFront();
        }

        private void InitializeReplyPreviewPanel(Control messageEditorPanel)
        {
            replyPreviewPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Visible = false,
                BackColor = BlendColors(currentComposerColor, currentAccentColor, 0.12f),
                Padding = new Padding(10, 4, 6, 4)
            };

            replyPreviewLabel = new Label
            {
                Dock = DockStyle.Fill,
                Font = new Font(fontFamily, 9f, FontStyle.Bold),
                ForeColor = currentTextColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            clearReplyButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 30,
                Text = "X",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            clearReplyButton.Click += (_, _) => ClearReplyTarget();
            ConfigureActionButton(clearReplyButton, BlendColors(currentComposerColor, currentDangerColor, 0.2f), currentTextColor);

            replyPreviewPanel.Controls.Add(replyPreviewLabel);
            replyPreviewPanel.Controls.Add(clearReplyButton);
            messageEditorPanel.Controls.Add(replyPreviewPanel);
            replyPreviewPanel.BringToFront();
        }

        private void InitializeAdvancedChatFeatures()
        {
            LoadPinnedChats();
            InitializeMessageContextMenu();
            InitializeChatListContextMenu();
            InitializeTypingTimers();
        }

        private void InitializeMessageContextMenu()
        {
            messageContextMenu = new ContextMenuStrip();
            messageContextMenu.Opening += (_, e) =>
            {
                if (string.IsNullOrWhiteSpace(contextMenuMessageId) || activeConversationState == null || !activeConversationState.ById.TryGetValue(contextMenuMessageId, out ConversationMessage? message))
                {
                    e.Cancel = true;
                    return;
                }

                messageContextMenu.Items["replyItem"]!.Visible = !message.IsDeleted;
                messageContextMenu.Items["reactionItem"]!.Visible = !message.IsDeleted;
                messageContextMenu.Items["retractItem"]!.Visible = string.Equals(message.Sender, currentUserName, StringComparison.OrdinalIgnoreCase) && !message.IsDeleted;
                messageContextMenu.Items["pinItem"]!.Text = string.Equals(pinnedMessageId, message.Id, StringComparison.OrdinalIgnoreCase) ? "Unpin Message" : "Pin Message";
            };

            ToolStripMenuItem replyItem = new ToolStripMenuItem("Reply")
            {
                Name = "replyItem"
            };
            replyItem.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(contextMenuMessageId))
                {
                    SetReplyTarget(contextMenuMessageId);
                }
            };

            ToolStripMenuItem reactionItem = new ToolStripMenuItem("Add Reaction")
            {
                Name = "reactionItem"
            };
            foreach (string reaction in new[] { "👍", "❤️", "😂", "😮", "🎉" })
            {
                ToolStripMenuItem reactionChoice = new ToolStripMenuItem(reaction);
                reactionChoice.Click += (_, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(contextMenuMessageId))
                    {
                        SendReactionEvent(contextMenuMessageId, reaction);
                    }
                };
                reactionItem.DropDownItems.Add(reactionChoice);
            }

            ToolStripMenuItem pinItem = new ToolStripMenuItem("Pin Message")
            {
                Name = "pinItem"
            };
            pinItem.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(contextMenuMessageId))
                {
                    TogglePinnedMessage(contextMenuMessageId);
                }
            };

            ToolStripMenuItem retractItem = new ToolStripMenuItem("Retract Message")
            {
                Name = "retractItem"
            };
            retractItem.Click += (_, _) =>
            {
                if (!string.IsNullOrWhiteSpace(contextMenuMessageId))
                {
                    RetractMessage(contextMenuMessageId);
                }
            };

            messageContextMenu.Items.Add(replyItem);
            messageContextMenu.Items.Add(reactionItem);
            messageContextMenu.Items.Add(pinItem);
            messageContextMenu.Items.Add(retractItem);
        }

        private void InitializeChatListContextMenu()
        {
            chatListContextMenu = new ContextMenuStrip();

            ToolStripMenuItem togglePinItem = new ToolStripMenuItem("Pin Chat");
            togglePinItem.Click += (_, _) =>
            {
                if (peopleList.SelectedItem is string contact)
                {
                    TogglePinnedChat(contact);
                }
            };

            ToolStripMenuItem clearChatItem = new ToolStripMenuItem("Clear Local Chat");
            clearChatItem.Click += (_, _) => ClearSelectedChat(false);

            ToolStripMenuItem clearChatBotMemoryItem = new ToolStripMenuItem("Clear Chat + Reset ChatBot Memory");
            clearChatBotMemoryItem.Click += (_, _) => ClearSelectedChat(true);

            chatListContextMenu.Opening += (_, e) =>
            {
                if (peopleList.SelectedItem is not string selectedContact || string.IsNullOrWhiteSpace(selectedContact))
                {
                    e.Cancel = true;
                    return;
                }

                togglePinItem.Text = pinnedChats.Contains(selectedContact) ? "Unpin Chat" : "Pin Chat";
                clearChatBotMemoryItem.Visible = IsChatBotContact(selectedContact);
            };

            chatListContextMenu.Items.Add(togglePinItem);
            chatListContextMenu.Items.Add(clearChatItem);
            chatListContextMenu.Items.Add(clearChatBotMemoryItem);
        }

        private void InitializeTypingTimers()
        {
            typingIndicatorTimer = new System.Windows.Forms.Timer
            {
                Interval = 1000
            };
            typingIndicatorTimer.Tick += (_, _) => CleanupExpiredTypingIndicators();
            typingIndicatorTimer.Start();

            typingStopTimer = new System.Windows.Forms.Timer
            {
                Interval = 1500
            };
            typingStopTimer.Tick += (_, _) => StopLocalTypingIndicator();
        }

        private void LoadPinnedChats()
        {
            pinnedChats.Clear();
            if (!File.Exists(PinnedChatsFilePath))
            {
                return;
            }

            foreach (string line in File.ReadAllLines(PinnedChatsFilePath))
            {
                string contact = line.Trim();
                if (!string.IsNullOrWhiteSpace(contact))
                {
                    pinnedChats.Add(contact);
                }
            }
        }

        private void SavePinnedChats()
        {
            File.WriteAllLines(PinnedChatsFilePath, pinnedChats.OrderBy(static contact => contact, StringComparer.OrdinalIgnoreCase));
        }

        private void TogglePinnedChat(string contactName)
        {
            if (!pinnedChats.Add(contactName))
            {
                pinnedChats.Remove(contactName);
            }

            SavePinnedChats();
            UpdatePeopleList();
        }

        private void LoadPinnedMessage(string? contactName)
        {
            pinnedMessageId = null;
            if (string.IsNullOrWhiteSpace(contactName))
            {
                UpdatePinnedMessagePanel();
                return;
            }

            string filePath = GetPinnedMessageFilePath(contactName);
            if (File.Exists(filePath))
            {
                string storedId = File.ReadAllText(filePath).Trim();
                if (!string.IsNullOrWhiteSpace(storedId))
                {
                    pinnedMessageId = storedId;
                }
            }

            UpdatePinnedMessagePanel();
        }

        private void SavePinnedMessage(string? contactName)
        {
            if (string.IsNullOrWhiteSpace(contactName))
            {
                return;
            }

            string filePath = GetPinnedMessageFilePath(contactName);
            if (string.IsNullOrWhiteSpace(pinnedMessageId))
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                UpdatePinnedMessagePanel();
                return;
            }

            File.WriteAllText(filePath, pinnedMessageId);
            UpdatePinnedMessagePanel();
        }

        private void TogglePinnedMessage(string messageId)
        {
            pinnedMessageId = string.Equals(pinnedMessageId, messageId, StringComparison.OrdinalIgnoreCase)
                ? null
                : messageId;
            SavePinnedMessage(currentPerson);
            RenderActiveConversation();
        }

        private void UnpinCurrentMessage()
        {
            pinnedMessageId = null;
            SavePinnedMessage(currentPerson);
            RenderActiveConversation();
        }

        private void UpdatePinnedMessagePanel()
        {
            if (pinnedMessagePanel == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(pinnedMessageId) || activeConversationState == null || !activeConversationState.ById.TryGetValue(pinnedMessageId, out ConversationMessage? pinnedMessage))
            {
                pinnedMessagePanel.Visible = false;
                return;
            }

            string snippet = pinnedMessage.IsDeleted
                ? "Pinned message was retracted."
                : NormalizePreview(pinnedMessage.Body);
            pinnedMessageLabel.Text = "Pinned: " + snippet;
            pinnedMessagePanel.Visible = true;
        }

        private void SetReplyTarget(string messageId)
        {
            if (activeConversationState == null || !activeConversationState.ById.TryGetValue(messageId, out ConversationMessage? message))
            {
                return;
            }

            replyTargetMessageId = message.Id;
            replyTargetSender = message.Sender;
            replyTargetPreview = message.IsDeleted ? "Deleted message" : NormalizePreview(message.Body);
            replyPreviewLabel.Text = "Replying to " + replyTargetSender + ": " + replyTargetPreview;
            replyPreviewPanel.Visible = true;
            messageInput.Focus();
        }

        private void ClearReplyTarget()
        {
            replyTargetMessageId = null;
            replyTargetSender = string.Empty;
            replyTargetPreview = string.Empty;
            if (replyPreviewPanel != null)
            {
                replyPreviewPanel.Visible = false;
            }
        }

        private void ChatboxMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int charIndex = chatbox.GetCharIndexFromPosition(e.Location);
            RenderedMessageRange? target = renderedMessages.FirstOrDefault(range => charIndex >= range.Start && charIndex <= range.End);
            if (target == null)
            {
                return;
            }

            contextMenuMessageId = target.MessageId;
            messageContextMenu.Show(chatbox, e.Location);
        }

        private void PeopleListMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int index = peopleList.IndexFromPoint(e.Location);
            if (index < 0)
            {
                return;
            }

            peopleList.SelectedIndex = index;
            chatListContextMenu.Show(peopleList, e.Location);
        }

        private string CreateTextMessagePayload(string body, string? messageId = null)
        {
            string timestamp = DateTime.Now.ToString("HH:mm-dd/MM/yyyy");
            string id = string.IsNullOrWhiteSpace(messageId) ? Guid.NewGuid().ToString("N") : messageId;

            StringBuilder builder = new StringBuilder();
            builder.Append(currentUserName).Append(" [").Append(timestamp).Append("]: ").Append(body).AppendLine();
            builder.Append(MessageMetadataPrefix).Append("id=").Append(id).AppendLine();
            builder.Append(MessageMetadataPrefix).Append("kind=text").AppendLine();
            if (!string.IsNullOrWhiteSpace(replyTargetMessageId))
            {
                builder.Append(MessageMetadataPrefix).Append("replyTo=").Append(replyTargetMessageId).AppendLine();
            }
            builder.Append(delimiter);
            return builder.ToString();
        }

        private string CreateControlPayload(string action, string targetMessageId, string? reactionEmoji = null)
        {
            string timestamp = DateTime.Now.ToString("HH:mm-dd/MM/yyyy");
            string body = action switch
            {
                "read" => "[Read receipt]",
                "reaction" => "[Reaction] " + reactionEmoji,
                "delete" => "[Message retracted]",
                _ => "[Chat control]"
            };

            StringBuilder builder = new StringBuilder();
            builder.Append(currentUserName).Append(" [").Append(timestamp).Append("]: ").Append(body).AppendLine();
            builder.Append(MessageMetadataPrefix).Append("id=").Append(Guid.NewGuid().ToString("N")).AppendLine();
            builder.Append(MessageMetadataPrefix).Append("kind=control").AppendLine();
            builder.Append(MessageMetadataPrefix).Append("control=").Append(action).AppendLine();
            builder.Append(MessageMetadataPrefix).Append("target=").Append(targetMessageId).AppendLine();
            if (!string.IsNullOrWhiteSpace(reactionEmoji))
            {
                builder.Append(MessageMetadataPrefix).Append("reaction=").Append(reactionEmoji).AppendLine();
            }
            builder.Append(delimiter);
            return builder.ToString();
        }

        private bool SendConversationPayload(string recipient, string payload, string? localStatusMessageId = null)
        {
            string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUserName, recipient));
            Directory.CreateDirectory(conversationFolder);
            File.WriteAllText(AppPaths.MessageNowFile, recipient + Environment.NewLine + currentUserName + Environment.NewLine + payload);
            File.AppendAllText(conversationFile, payload + Environment.NewLine);

            if (!string.IsNullOrWhiteSpace(localStatusMessageId))
            {
                outgoingMessageStatuses[localStatusMessageId] = "sending";
            }

            skipNextConversationWatcherChange = true;
            lastFileSize = new FileInfo(conversationFile).Length;
            pendingConversationChunk = string.Empty;

            RefreshConversationMetadata();
            if (string.Equals(currentPerson, recipient, StringComparison.OrdinalIgnoreCase))
            {
                RenderActiveConversation();
            }
            UpdatePeopleList();

            StreamWriter? pipeWriter = PipeConnectionManager.PipeWriter;
            if (pipeWriter == null)
            {
                if (!string.IsNullOrWhiteSpace(localStatusMessageId))
                {
                    outgoingMessageStatuses[localStatusMessageId] = "failed";
                }

                MessageBox.Show("PipeWriter is not connected.");
                return false;
            }

            try
            {
                pipeWriter.WriteLine("TEXT_MESSAGE");
                pipeWriter.Flush();
                return true;
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(localStatusMessageId))
                {
                    outgoingMessageStatuses[localStatusMessageId] = "failed";
                }

                MessageBox.Show("Error communicating with C++ program: " + ex.Message);
                return false;
            }
        }

        private void SendReactionEvent(string targetMessageId, string reactionEmoji)
        {
            if (string.IsNullOrWhiteSpace(currentPerson))
            {
                return;
            }

            string payload = CreateControlPayload("reaction", targetMessageId, reactionEmoji);
            SendConversationPayload(currentPerson, payload);
        }

        private void RetractMessage(string targetMessageId)
        {
            if (string.IsNullOrWhiteSpace(currentPerson))
            {
                return;
            }

            DialogResult dialogResult = MessageBox.Show(
                "Retract this message for both sides?",
                "Retract Message",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (dialogResult != DialogResult.Yes)
            {
                return;
            }

            string payload = CreateControlPayload("delete", targetMessageId);
            SendConversationPayload(currentPerson, payload);
        }

        private void SendReadReceipt(string recipient, string targetMessageId)
        {
            string payload = CreateControlPayload("read", targetMessageId);
            SendConversationPayload(recipient, payload);
        }

        private void SendPendingReadReceipts(string recipient, ConversationState state)
        {
            if (IsChatBotContact(recipient))
            {
                return;
            }

            List<string> unreadIncomingMessageIds = state.Messages
                .Where(message =>
                    !message.IsDeleted &&
                    !string.Equals(message.Sender, currentUserName, StringComparison.OrdinalIgnoreCase) &&
                    !message.ReadBy.Contains(currentUserName))
                .Select(message => message.Id)
                .ToList();

            foreach (string messageId in unreadIncomingMessageIds)
            {
                SendReadReceipt(recipient, messageId);
            }
        }

        private void ClearSelectedChat(bool resetChatBotMemory)
        {
            if (string.IsNullOrWhiteSpace(currentPerson))
            {
                MessageBox.Show("Please select a chat first.");
                return;
            }

            if (resetChatBotMemory && !IsChatBotContact(currentPerson))
            {
                resetChatBotMemory = false;
            }

            string prompt = resetChatBotMemory
                ? "Clear this ChatBot conversation locally and reset the ChatBot memory on the server?"
                : "Clear this conversation locally?";
            DialogResult dialogResult = MessageBox.Show(prompt, "Clear Chat", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dialogResult != DialogResult.Yes)
            {
                return;
            }

            string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUserName, currentPerson));
            if (File.Exists(conversationFile))
            {
                File.Delete(conversationFile);
            }

            pinnedMessageId = null;
            SavePinnedMessage(currentPerson);
            outgoingMessageStatuses.Clear();
            ClearReplyTarget();

            if (resetChatBotMemory)
            {
                StreamWriter? pipeWriter = PipeConnectionManager.PipeWriter;
                if (pipeWriter != null)
                {
                    pipeWriter.WriteLine("CLEAR_CHATBOT_MEMORY");
                    pipeWriter.Flush();
                }
            }

            RefreshConversationMetadata();
            RenderActiveConversation();
            UpdatePeopleList();
        }

        private void TogglePinnedCurrentChat()
        {
            if (!string.IsNullOrWhiteSpace(currentPerson))
            {
                TogglePinnedChat(currentPerson);
            }
        }

        private void ClearCurrentChatFromMenu()
        {
            ClearSelectedChat(false);
        }

        private void ResetCurrentChatBotMemoryFromMenu()
        {
            ClearSelectedChat(true);
        }

        private void InitializeConversationUiForContact(string? recipient)
        {
            LoadPinnedMessage(recipient);
            if (!string.Equals(recipient, currentPerson, StringComparison.OrdinalIgnoreCase))
            {
                ClearReplyTarget();
            }
        }

        private static string ComputeStableMessageId(string rawEntry)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawEntry));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private bool TryExtractMetadata(string message, out Dictionary<string, string> metadata, out string messageWithoutMetadata)
        {
            metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> contentLines = new List<string>();
            string normalized = message.Replace("\r\n", "\n");
            foreach (string rawLine in normalized.Split('\n'))
            {
                string trimmedLine = rawLine.Trim();
                if (trimmedLine.StartsWith(MessageMetadataPrefix, StringComparison.Ordinal))
                {
                    string keyValue = trimmedLine.Substring(MessageMetadataPrefix.Length);
                    int separatorIndex = keyValue.IndexOf('=');
                    if (separatorIndex > 0)
                    {
                        string key = keyValue.Substring(0, separatorIndex).Trim();
                        string value = keyValue.Substring(separatorIndex + 1).Trim();
                        metadata[key] = value;
                    }
                }
                else
                {
                    contentLines.Add(rawLine);
                }
            }

            messageWithoutMetadata = string.Join(Environment.NewLine, contentLines).Trim();
            return metadata.Count > 0;
        }

        private ParsedChatEntry ParseChatEntry(string rawEntry)
        {
            TryExtractMetadata(rawEntry, out Dictionary<string, string> metadata, out string messageWithoutMetadata);
            TryParseChatMessage(messageWithoutMetadata, out string sender, out string timestamp, out string body);

            string id = metadata.TryGetValue("id", out string? metadataId) && !string.IsNullOrWhiteSpace(metadataId)
                ? metadataId
                : ComputeStableMessageId(rawEntry);

            bool isControl = metadata.TryGetValue("kind", out string? kind) &&
                string.Equals(kind, "control", StringComparison.OrdinalIgnoreCase);

            return new ParsedChatEntry
            {
                RawEntry = rawEntry,
                Id = id,
                Sender = sender,
                Timestamp = timestamp,
                Body = body,
                ReplyToId = metadata.TryGetValue("replyTo", out string? replyTo) ? replyTo : string.Empty,
                IsControl = isControl,
                ControlAction = metadata.TryGetValue("control", out string? controlAction) ? controlAction : string.Empty,
                TargetMessageId = metadata.TryGetValue("target", out string? targetMessageId) ? targetMessageId : string.Empty,
                ReactionEmoji = metadata.TryGetValue("reaction", out string? reactionEmoji) ? reactionEmoji : string.Empty
            };
        }

        private ConversationState BuildConversationState(string conversationFile)
        {
            ConversationState state = new ConversationState();
            if (!File.Exists(conversationFile))
            {
                return state;
            }

            string fileContent = File.ReadAllText(conversationFile);
            foreach (string entry in fileContent
                .Split(new[] { delimiter }, StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Trim())
                .Where(entry => !string.IsNullOrWhiteSpace(entry)))
            {
                ParsedChatEntry parsedEntry = ParseChatEntry(entry);
                if (!parsedEntry.IsControl)
                {
                    ConversationMessage message = new ConversationMessage
                    {
                        Id = parsedEntry.Id,
                        Sender = parsedEntry.Sender,
                        Timestamp = parsedEntry.Timestamp,
                        Body = parsedEntry.Body,
                        ReplyToId = parsedEntry.ReplyToId
                    };
                    state.Messages.Add(message);
                    state.ById[message.Id] = message;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(parsedEntry.TargetMessageId) || !state.ById.TryGetValue(parsedEntry.TargetMessageId, out ConversationMessage? targetMessage))
                {
                    continue;
                }

                switch (parsedEntry.ControlAction)
                {
                    case "reaction":
                        if (string.IsNullOrWhiteSpace(parsedEntry.ReactionEmoji))
                        {
                            targetMessage.ReactionsByUser.Remove(parsedEntry.Sender);
                        }
                        else
                        {
                            targetMessage.ReactionsByUser[parsedEntry.Sender] = parsedEntry.ReactionEmoji;
                        }
                        break;
                    case "read":
                        targetMessage.ReadBy.Add(parsedEntry.Sender);
                        break;
                    case "delete":
                        targetMessage.IsDeleted = true;
                        break;
                }
            }

            ConversationMessage? lastVisibleMessage = state.Messages.LastOrDefault(message => !message.IsDeleted && !string.IsNullOrWhiteSpace(message.Body));
            if (lastVisibleMessage != null)
            {
                state.LastPreview = string.Equals(lastVisibleMessage.Sender, currentUserName, StringComparison.OrdinalIgnoreCase)
                    ? "You: " + NormalizePreview(lastVisibleMessage.Body)
                    : NormalizePreview(lastVisibleMessage.Body);
            }

            return state;
        }

        private string BuildReactionSummary(ConversationMessage message)
        {
            if (message.ReactionsByUser.Count == 0)
            {
                return string.Empty;
            }

            string summary = string.Join("  ", message.ReactionsByUser
                .GroupBy(pair => pair.Value)
                .Select(group => group.Key + " " + group.Count()));
            return "Reactions: " + summary;
        }

        private string GetMessageStatusText(ConversationMessage message)
        {
            if (!string.Equals(message.Sender, currentUserName, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(currentPerson) && message.ReadBy.Contains(currentPerson))
            {
                return "Read";
            }

            if (outgoingMessageStatuses.TryGetValue(message.Id, out string? status))
            {
                return status switch
                {
                    "sending" => "Sending...",
                    "failed" => "Failed",
                    "delivered" => "Delivered",
                    _ => "Delivered"
                };
            }

            return "Delivered";
        }

        private void RenderConversationState(ConversationState state)
        {
            activeConversationState = state;
            renderedMessages.Clear();
            chatbox.Clear();
            chatbox.Font = new Font(fontFamily, fontSize);

            if (state.Messages.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(currentPerson) && IsChatBotContact(currentPerson))
                {
                    AppendConversationStatus("Starting a ChatBot conversation. Messages in this chat are processed on the server using model " + GetSelectedChatBotModelDisplayName() + ".");
                }
                else if (!string.IsNullOrWhiteSpace(currentPerson))
                {
                    AppendConversationStatus("No messages yet. Say hello to start the conversation.");
                }

                UpdatePinnedMessagePanel();
                return;
            }

            foreach (ConversationMessage message in state.Messages)
            {
                int start = chatbox.TextLength;
                bool isCurrentUser = string.Equals(message.Sender, currentUserName, StringComparison.OrdinalIgnoreCase);
                Color senderColor = isCurrentUser ? currentAccentColor : currentTextColor;

                chatbox.SelectionStart = chatbox.TextLength;
                chatbox.SelectionColor = senderColor;
                chatbox.SelectionFont = new Font(fontFamily, fontSize, FontStyle.Bold);
                chatbox.AppendText(message.Sender);

                string statusText = GetMessageStatusText(message);
                chatbox.SelectionColor = currentMutedTextColor;
                chatbox.SelectionFont = new Font(fontFamily, Math.Max(fontSize - 1f, 9f), FontStyle.Regular);
                chatbox.AppendText("  " + message.Timestamp);
                if (!string.IsNullOrWhiteSpace(statusText))
                {
                    chatbox.AppendText("  •  " + statusText);
                }
                chatbox.AppendText(Environment.NewLine);

                if (!string.IsNullOrWhiteSpace(message.ReplyToId) && state.ById.TryGetValue(message.ReplyToId, out ConversationMessage? repliedMessage))
                {
                    chatbox.SelectionColor = currentMutedTextColor;
                    chatbox.SelectionFont = new Font(fontFamily, Math.Max(fontSize - 1f, 9f), FontStyle.Italic);
                    string replySnippet = repliedMessage.IsDeleted ? "Deleted message" : NormalizePreview(repliedMessage.Body);
                    chatbox.AppendText("Reply to " + repliedMessage.Sender + ": " + replySnippet + Environment.NewLine);
                }

                chatbox.SelectionColor = message.IsDeleted ? currentMutedTextColor : currentTextColor;
                chatbox.SelectionFont = new Font(fontFamily, fontSize, message.IsDeleted ? FontStyle.Italic : FontStyle.Regular);
                chatbox.AppendText((message.IsDeleted ? "This message was retracted." : message.Body) + Environment.NewLine);

                string reactionsSummary = BuildReactionSummary(message);
                if (!string.IsNullOrWhiteSpace(reactionsSummary))
                {
                    chatbox.SelectionColor = currentMutedTextColor;
                    chatbox.SelectionFont = new Font(fontFamily, Math.Max(fontSize - 1.5f, 8.5f), FontStyle.Bold);
                    chatbox.AppendText(reactionsSummary + Environment.NewLine);
                }

                chatbox.AppendText(Environment.NewLine);
                renderedMessages.Add(new RenderedMessageRange
                {
                    MessageId = message.Id,
                    Start = start,
                    End = chatbox.TextLength
                });
            }

            UpdatePinnedMessagePanel();
            chatbox.SelectionStart = chatbox.TextLength;
            chatbox.ScrollToCaret();
        }

        private void RenderActiveConversation()
        {
            if (string.IsNullOrWhiteSpace(currentPerson))
            {
                ShowConversationPlaceholder();
                return;
            }

            string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUserName, currentPerson));
            ConversationState state = BuildConversationState(conversationFile);
            RenderConversationState(state);
            UpdateConversationHeader(currentPerson);
        }

        private void RefreshConversationMetadataForContact(string contactName)
        {
            string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUserName, contactName));
            ConversationState state = BuildConversationState(conversationFile);
            EnsureContactState(contactName);
            if (!string.IsNullOrWhiteSpace(state.LastPreview))
            {
                lastMessagePreviews[contactName] = state.LastPreview;
            }
            else if (IsChatBotContact(contactName))
            {
                lastMessagePreviews[contactName] = "Server assistant | Model: " + GetSelectedChatBotModelDisplayName();
            }
            else
            {
                lastMessagePreviews.Remove(contactName);
            }
        }

        private void HandleMessageInputChanged()
        {
            if (string.IsNullOrWhiteSpace(currentPerson) || IsChatBotContact(currentPerson))
            {
                StopLocalTypingIndicator();
                return;
            }

            if (string.IsNullOrWhiteSpace(messageInput.Text))
            {
                StopLocalTypingIndicator();
                return;
            }

            if (!localTypingStateSent || !string.Equals(localTypingRecipient, currentPerson, StringComparison.OrdinalIgnoreCase))
            {
                localTypingRecipient = currentPerson;
                SendTypingState(currentPerson, true);
                localTypingStateSent = true;
            }

            typingStopTimer.Stop();
            typingStopTimer.Start();
        }

        private void SendTypingState(string recipient, bool isTyping)
        {
            StreamWriter? pipeWriter = PipeConnectionManager.PipeWriter;
            if (pipeWriter == null)
            {
                return;
            }

            pipeWriter.WriteLine("TYPING:" + recipient + ":" + (isTyping ? "start" : "stop"));
            pipeWriter.Flush();
        }

        private void StopLocalTypingIndicator()
        {
            typingStopTimer?.Stop();
            if (!localTypingStateSent || string.IsNullOrWhiteSpace(localTypingRecipient))
            {
                return;
            }

            SendTypingState(localTypingRecipient, false);
            localTypingStateSent = false;
            localTypingRecipient = null;
        }

        private void RegisterTypingIndicator(string sender, bool isTyping)
        {
            if (isTyping)
            {
                typingIndicators[sender] = DateTime.UtcNow.AddSeconds(3);
            }
            else
            {
                typingIndicators.Remove(sender);
            }

            UpdateConversationHeader(currentPerson);
        }

        private void CleanupExpiredTypingIndicators()
        {
            DateTime now = DateTime.UtcNow;
            string[] expiredUsers = typingIndicators
                .Where(pair => pair.Value <= now)
                .Select(pair => pair.Key)
                .ToArray();
            if (expiredUsers.Length == 0)
            {
                return;
            }

            foreach (string user in expiredUsers)
            {
                typingIndicators.Remove(user);
            }

            UpdateConversationHeader(currentPerson);
        }
    }
}
