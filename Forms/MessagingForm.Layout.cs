using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace MessagingApp
{
    public partial class MessagingForm : Form
    {
        private const int SidebarWidth = 336;
        private const int SidebarActionsHeight = 152;
        private const int UnaddedContactsHeight = 184;
        private const int ChatHeaderHeight = 122;
        private const int ComposerHeight = 204;

        private TableLayoutPanel sidebarLayout = null!;
        private TableLayoutPanel chatLayout = null!;
        private MenuStrip mainMenu = null!;

        private void InitializePanels()
        {
            int menuHeight = MainMenuStrip?.Height ?? 30;

            Panel contentHost = new Panel
            {
                Location = new Point(0, menuHeight),
                Size = new Size(ClientSize.Width, ClientSize.Height - menuHeight),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = currentBackgroundColor,
                Padding = new Padding(20, 18, 20, 20)
            };

            TableLayoutPanel shellLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = currentBackgroundColor
            };
            shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
            shellLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            shellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            contactPanelBorder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentBorderColor,
                Padding = new Padding(1),
                Margin = new Padding(0, 0, 18, 0)
            };

            contactPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentSidebarColor
            };

            sidebarLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = currentSidebarColor,
                Padding = new Padding(18)
            };
            sidebarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, SidebarActionsHeight));
            sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, UnaddedContactsHeight));

            sidebarSearchCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BlendColors(currentSidebarColor, Color.White, 0.1f),
                Padding = new Padding(14, 12, 14, 10),
                Margin = new Padding(0, 8, 0, 6)
            };

            contactActionsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentSidebarColor,
                Padding = new Padding(0, 16, 0, 0),
                Margin = new Padding(0, 8, 0, 4)
            };

            unaddedContactsPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BlendColors(currentSidebarColor, Color.White, 0.06f),
                Padding = new Padding(14),
                Margin = new Padding(0, 8, 0, 0)
            };

            contactPanel.Controls.Add(sidebarLayout);
            contactPanelBorder.Controls.Add(contactPanel);

            chatPanelBorder = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentBorderColor,
                Padding = new Padding(1),
                Margin = new Padding(0)
            };

            chatPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentSurfaceColor
            };

            chatLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = currentSurfaceColor,
                Padding = new Padding(24)
            };
            chatLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ChatHeaderHeight));
            chatLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            chatLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, ComposerHeight));

            chatHeaderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentSurfaceColor,
                Padding = new Padding(0, 0, 0, 12),
                Margin = new Padding(0, 0, 0, 16)
            };

            messageInputCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentComposerColor,
                Padding = new Padding(18, 16, 18, 14),
                Margin = new Padding(0, 16, 0, 0)
            };

            sendMessageButtonBorder = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 0, 0, 0),
                BackColor = currentComposerColor
            };

            chatPanel.Controls.Add(chatLayout);
            chatPanelBorder.Controls.Add(chatPanel);

            shellLayout.Controls.Add(contactPanelBorder, 0, 0);
            shellLayout.Controls.Add(chatPanelBorder, 1, 0);
            contentHost.Controls.Add(shellLayout);
            Controls.Add(contentHost);

            mainMenu?.BringToFront();
        }

        private void InitializeUIElements()
        {
            ToolTip toolTip = new ToolTip
            {
                ShowAlways = true,
                AutoPopDelay = 6000,
                InitialDelay = 200
            };

            sidebarTitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 34,
                Text = "Chats",
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                ForeColor = currentSidebarTextColor,
                TextAlign = ContentAlignment.BottomLeft
            };

            sidebarSubtitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = $"Signed in as {currentUserName}",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                ForeColor = currentSidebarMutedTextColor,
                TextAlign = ContentAlignment.TopLeft
            };

            Panel sidebarHeaderPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentSidebarColor,
                Padding = new Padding(0, 0, 0, 8),
                Margin = new Padding(0)
            };
            sidebarHeaderPanel.Controls.Add(sidebarSubtitleLabel);
            sidebarHeaderPanel.Controls.Add(sidebarTitleLabel);

            filterTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font(fontFamily, 11f),
                PlaceholderText = "Search contacts",
                BackColor = sidebarSearchCard.BackColor,
                ForeColor = currentSidebarTextColor
            };
            filterTextBox.TextChanged += FilterPeople;
            toolTip.SetToolTip(filterTextBox, "Search your contact list.");
            sidebarSearchCard.Controls.Add(filterTextBox);

            contactsSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "No contacts yet",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = currentSidebarMutedTextColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, 4)
            };

            peopleList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 64,
                IntegralHeight = false,
                Font = new Font(fontFamily, 10.5f),
                BackColor = currentSidebarColor,
                ForeColor = currentSidebarTextColor,
                Margin = new Padding(0)
            };
            peopleList.DrawItem += DrawPeopleListItem;
            peopleList.SelectedIndexChanged += PersonSelected;
            peopleList.MouseUp += PeopleListMouseUp;
            toolTip.SetToolTip(peopleList, "Open a conversation.");

            addContactLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "Start a new conversation",
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                ForeColor = currentSidebarTextColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Panel addContactRow = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = currentSidebarColor,
                Margin = new Padding(0, 10, 0, 0)
            };

            Panel addContactInputCard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = BlendColors(currentSidebarColor, Color.White, 0.1f),
                Padding = new Padding(12, 10, 12, 8),
                Margin = new Padding(0, 0, 10, 0)
            };

            addContactInput = new TextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                Font = new Font(fontFamily, 10.5f),
                PlaceholderText = "Enter a username",
                BackColor = addContactInputCard.BackColor,
                ForeColor = currentSidebarTextColor
            };
            addContactInput.TextChanged += (_, _) => ReplaceSpacesWithUnderscores(addContactInput);

            addContactButton = new Button
            {
                Dock = DockStyle.Right,
                Width = 120,
                Text = "Add Contact",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
            };
            addContactButton.Click += (_, _) => AddContact(addContactInput.Text.Trim());
            ConfigureActionButton(addContactButton, currentAccentColor, Color.White);
            toolTip.SetToolTip(addContactButton, "Send a contact request.");

            removeContactButton = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Remove Selected",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Margin = new Padding(0)
            };
            removeContactButton.Click += (_, _) => RemoveSelectedContact();
            ConfigureActionButton(removeContactButton, currentDangerColor, Color.White);
            toolTip.SetToolTip(removeContactButton, "Remove the selected contact.");

            Panel removeContactContainer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = currentSidebarColor,
                Padding = new Padding(0, 14, 0, 0),
                Margin = new Padding(0)
            };
            removeContactContainer.Controls.Add(removeContactButton);

            addContactInputCard.Controls.Add(addContactInput);
            addContactRow.Controls.Add(addContactInputCard);
            addContactRow.Controls.Add(addContactButton);
            contactActionsPanel.Controls.Add(removeContactContainer);
            contactActionsPanel.Controls.Add(addContactRow);
            contactActionsPanel.Controls.Add(addContactLabel);

            activeChatTitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Text = "Select a chat",
                Font = new Font("Segoe UI Semibold", 20f, FontStyle.Bold),
                ForeColor = currentTextColor,
                TextAlign = ContentAlignment.BottomLeft
            };

            activeChatSubtitleLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "Choose a conversation from the left to start messaging.",
                Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
                ForeColor = currentMutedTextColor,
                TextAlign = ContentAlignment.TopLeft
            };

            Panel chatHeaderInfoPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentSurfaceColor,
                Padding = new Padding(0, 0, 0, 4)
            };
            chatHeaderInfoPanel.Controls.Add(activeChatSubtitleLabel);
            chatHeaderInfoPanel.Controls.Add(activeChatTitleLabel);
            InitializePinnedMessagePanel(chatHeaderInfoPanel);

            FlowLayoutPanel headerActionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                BackColor = currentSurfaceColor,
                Padding = new Padding(0, 6, 0, 0)
            };

            refreshButton = new Button
            {
                Size = new Size(106, 38),
                Text = "Refresh",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Margin = new Padding(10, 0, 0, 0)
            };
            refreshButton.Click += refreshConversations;
            ConfigureActionButton(refreshButton, BlendColors(currentSurfaceColor, currentAccentColor, 0.08f), currentTextColor);
            toolTip.SetToolTip(refreshButton, "Refresh messages from the server.");

            sendFileButton = new Button
            {
                Size = new Size(112, 38),
                Text = "Send File",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Margin = new Padding(10, 0, 0, 0),
                Enabled = false
            };
            sendFileButton.Click += SendFile;
            ConfigureActionButton(sendFileButton, BlendColors(currentSurfaceColor, currentAccentColor, 0.08f), currentTextColor);
            toolTip.SetToolTip(sendFileButton, "Send a file to the active chat.");

            headerActionsPanel.Controls.Add(refreshButton);
            headerActionsPanel.Controls.Add(sendFileButton);
            chatHeaderPanel.Controls.Add(chatHeaderInfoPanel);
            chatHeaderPanel.Controls.Add(headerActionsPanel);

            chatbox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                DetectUrls = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font(fontFamily, fontSize),
                BackColor = currentSurfaceColor,
                ForeColor = currentTextColor,
                Margin = new Padding(0)
            };
            toolTip.SetToolTip(chatbox, "Conversation history.");
            chatbox.MouseUp += ChatboxMouseUp;

            TableLayoutPanel composerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = currentComposerColor
            };
            composerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            composerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146f));
            composerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Panel messageEditorPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = currentComposerColor
            };

            textInfoLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                Font = new Font("Segoe UI", 9.25f, FontStyle.Regular),
                ForeColor = currentMutedTextColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "0 words • 0 characters • Line 1, Col 1 • Ctrl+Enter to send"
            };

            messageInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None,
                Font = new Font(fontFamily, fontSize),
                PlaceholderText = "Type a thoughtful message...",
                WordWrap = true,
                BackColor = currentComposerColor,
                ForeColor = currentTextColor
            };
            messageInput.TextChanged += (_, _) =>
            {
                UpdateTextInfo();
                HandleMessageInputChanged();
            };
            messageInput.KeyUp += (_, _) => UpdateTextInfo();
            messageInput.MouseClick += (_, _) => UpdateTextInfo();
            messageInput.KeyDown += HandleMessageInputKeyDown;
            toolTip.SetToolTip(messageInput, "Write your message here.");

            sendMessageButton = new Button
            {
                Dock = DockStyle.Fill,
                Text = "Send",
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                Enabled = false
            };
            sendMessageButton.Click += SendMessage;
            ConfigureActionButton(sendMessageButton, currentAccentColor, Color.White);
            toolTip.SetToolTip(sendMessageButton, "Send the current message.");

            messageEditorPanel.Controls.Add(messageInput);
            messageEditorPanel.Controls.Add(textInfoLabel);
            InitializeReplyPreviewPanel(messageEditorPanel);
            sendMessageButtonBorder.Controls.Add(sendMessageButton);
            composerLayout.Controls.Add(messageEditorPanel, 0, 0);
            composerLayout.Controls.Add(sendMessageButtonBorder, 1, 0);
            messageInputCard.Controls.Add(composerLayout);

            sidebarLayout.Controls.Add(sidebarHeaderPanel, 0, 0);
            sidebarLayout.Controls.Add(sidebarSearchCard, 0, 1);
            sidebarLayout.Controls.Add(contactsSummaryLabel, 0, 2);
            sidebarLayout.Controls.Add(peopleList, 0, 3);
            sidebarLayout.Controls.Add(contactActionsPanel, 0, 4);
            sidebarLayout.Controls.Add(unaddedContactsPanel, 0, 5);

            chatLayout.Controls.Add(chatHeaderPanel, 0, 0);
            chatLayout.Controls.Add(chatbox, 0, 1);
            chatLayout.Controls.Add(messageInputCard, 0, 2);

            InitializeNotificationPreferencesPanel();
            InitializeAdvancedChatFeatures();
            UpdateTextInfo();
            UpdateConversationHeader(null);
        }

        private void InitializeNotificationPreferencesPanel()
        {
            notificationPreferencesPanel = new Panel
            {
                Size = new Size(320, 210),
                BackColor = currentSurfaceColor,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            notificationPreferencesPanel.Location = new Point(ClientSize.Width - notificationPreferencesPanel.Width - 30, (MainMenuStrip?.Height ?? 30) + 26);

            Label titleLabel = new Label
            {
                Text = "Notification Preferences",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Segoe UI Semibold", 11f, FontStyle.Bold),
                ForeColor = currentTextColor,
                BackColor = currentSurfaceColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 0, 0)
            };

            Label helperLabel = new Label
            {
                Text = "Choose how new messages should alert you.",
                Dock = DockStyle.Top,
                Height = 32,
                Font = new Font("Segoe UI", 9f),
                ForeColor = currentMutedTextColor,
                BackColor = currentSurfaceColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 12, 0)
            };

            enableSoundCheckBox = new CheckBox
            {
                Text = "Play a sound for new messages",
                AutoSize = false,
                Height = 30,
                Dock = DockStyle.Top,
                Checked = true,
                Font = new Font(fontFamily, 10f),
                ForeColor = currentTextColor,
                BackColor = currentSurfaceColor,
                Padding = new Padding(12, 0, 0, 0)
            };

            enablePopupCheckBox = new CheckBox
            {
                Text = "Show popup notification cards",
                AutoSize = false,
                Height = 30,
                Dock = DockStyle.Top,
                Checked = true,
                Font = new Font(fontFamily, 10f),
                ForeColor = currentTextColor,
                BackColor = currentSurfaceColor,
                Padding = new Padding(12, 0, 0, 0)
            };

            enableStaticNotificationPanel = new CheckBox
            {
                Text = "Keep notification card visible",
                AutoSize = false,
                Height = 30,
                Dock = DockStyle.Top,
                Checked = false,
                Font = new Font(fontFamily, 10f),
                ForeColor = currentTextColor,
                BackColor = currentSurfaceColor,
                Padding = new Padding(12, 0, 0, 0)
            };

            Button savePreferencesButton = new Button
            {
                Text = "Save Preferences",
                Dock = DockStyle.Bottom,
                Height = 40,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold)
            };
            savePreferencesButton.Click += (_, _) =>
            {
                SaveNotificationPreferences();
                notificationPreferencesPanel.Visible = false;
            };
            ConfigureActionButton(savePreferencesButton, currentAccentColor, Color.White);

            notificationPreferencesPanel.Controls.Add(savePreferencesButton);
            notificationPreferencesPanel.Controls.Add(enableStaticNotificationPanel);
            notificationPreferencesPanel.Controls.Add(enablePopupCheckBox);
            notificationPreferencesPanel.Controls.Add(enableSoundCheckBox);
            notificationPreferencesPanel.Controls.Add(helperLabel);
            notificationPreferencesPanel.Controls.Add(titleLabel);
            Controls.Add(notificationPreferencesPanel);
            notificationPreferencesPanel.BringToFront();
        }

        private void InitializeConversationsFolder()
        {
            if (!Directory.Exists(conversationFolder))
            {
                Directory.CreateDirectory(conversationFolder);
            }
        }

        private void InitializeUnaddedContactsPanel()
        {
            ToolTip toolTip = new ToolTip
            {
                ShowAlways = true
            };

            unaddedContactsPanel.Controls.Clear();

            unaddedContactsLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Text = "Pending contacts",
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                ForeColor = currentSidebarTextColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            Label hintLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Text = "People who can be added to your chat list",
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = currentSidebarMutedTextColor,
                TextAlign = ContentAlignment.MiddleLeft
            };

            addSelectedContactButton = new Button
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                Text = "Add Selected",
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Enabled = false
            };
            addSelectedContactButton.Click += (_, _) => AddSelectedContact();
            ConfigureActionButton(addSelectedContactButton, currentAccentColor, Color.White);
            toolTip.SetToolTip(addSelectedContactButton, "Add the selected pending contact.");

            unaddedContactsList = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 40,
                IntegralHeight = false,
                Font = new Font(fontFamily, 10f),
                BackColor = unaddedContactsPanel.BackColor,
                ForeColor = currentSidebarTextColor
            };
            unaddedContactsList.DrawItem += DrawUnaddedContactItem;
            unaddedContactsList.SelectedIndexChanged += (_, _) =>
            {
                addSelectedContactButton.Enabled = unaddedContactsList.SelectedItem != null;
            };
            toolTip.SetToolTip(unaddedContactsList, "Select a pending contact to add.");

            unaddedContactsPanel.Controls.Add(unaddedContactsList);
            unaddedContactsPanel.Controls.Add(addSelectedContactButton);
            unaddedContactsPanel.Controls.Add(hintLabel);
            unaddedContactsPanel.Controls.Add(unaddedContactsLabel);

            LoadUnaddedContacts();
            InitializeFileWatcher();
        }

        private void InitializeTemplateMenu()
        {
            if (!Directory.Exists(templatesFolder))
            {
                Directory.CreateDirectory(templatesFolder);
            }

            templatesMenu = CreateMenuItem("Templates", "templates.bmp");
            ToolStripMenuItem viewTemplatesMenuItem = CreateMenuItem("Choose Template", "choose_template.bmp", (_, _) => ShowTemplatePanel());
            viewTemplatesMenuItem.ShortcutKeys = Keys.Control | Keys.T;

            ToolStripMenuItem addTemplateMenuItem = CreateMenuItem("Add or Edit Template", "add_edit_template.bmp", (_, _) => ShowAddTemplatePanel());
            addTemplateMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.T;

            templatesMenu.DropDownItems.Add(viewTemplatesMenuItem);
            templatesMenu.DropDownItems.Add(addTemplateMenuItem);
            functionsMenu.DropDownItems.Add(new ToolStripSeparator());
            functionsMenu.DropDownItems.Add(templatesMenu);
        }

        private void InitializeComponent()
        {
            Text = $"ChatApp - {currentUserName} - Session: {PipeConnectionManager.session}";
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(1120, 760);
            BackColor = currentBackgroundColor;
            string iconPath = AppPaths.AppIconFile;
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }

            mainMenu = new MenuStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden,
                Stretch = true,
                Padding = new Padding(10, 6, 10, 6),
                BackColor = currentSurfaceColor,
                ForeColor = currentTextColor
            };

            ToolStripMenuItem accountMenu = CreateMenuItem("Account", "account.bmp");
            ToolStripMenuItem changePasswordMenu = CreateMenuItem("Change Password", "password_change.bmp", (_, _) => ShowChangePasswordForm());
            accountMenu.DropDownItems.Add(changePasswordMenu);

            ToolStripMenuItem fileMenu = CreateMenuItem("File", "folder.bmp");
            ToolStripMenuItem openDownloadsMenuItem = CreateMenuItem("Open Downloads Folder", "download.bmp", (_, _) => OpenDownloadsFolder());
            openDownloadsMenuItem.ShortcutKeys = Keys.Control | Keys.D;
            ToolStripMenuItem exportConversationMenuItem = CreateMenuItem("Export Conversation", "export_file.bmp", (_, _) => ExportConversation());
            exportConversationMenuItem.ShortcutKeys = Keys.Control | Keys.P;
            ToolStripMenuItem pinCurrentChatMenuItem = CreateMenuItem("Pin or Unpin Current Chat", "choose_template.bmp", (_, _) => TogglePinnedCurrentChat());
            ToolStripMenuItem clearCurrentChatMenuItem = CreateMenuItem("Clear Current Chat Locally", "remove_contact.bmp", (_, _) => ClearCurrentChatFromMenu());
            ToolStripMenuItem clearChatBotMemoryMenuItem = CreateMenuItem("Clear ChatBot Memory", "remove_contact.bmp", (_, _) => ResetCurrentChatBotMemoryFromMenu());
            fileMenu.DropDownItems.Add(openDownloadsMenuItem);
            fileMenu.DropDownItems.Add(exportConversationMenuItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(pinCurrentChatMenuItem);
            fileMenu.DropDownItems.Add(clearCurrentChatMenuItem);
            fileMenu.DropDownItems.Add(clearChatBotMemoryMenuItem);

            functionsMenu = CreateMenuItem("Functions", "function.bmp");
            ToolStripMenuItem chatSearchMenu = CreateMenuItem("Chat Search", "search_chat.bmp", (_, _) => ToggleChatSearchPanel());
            chatSearchMenu.ShortcutKeys = Keys.Control | Keys.F;
            functionsMenu.DropDownItems.Add(chatSearchMenu);

            ToolStripMenuItem settingsMenu = CreateMenuItem("Settings", "settings.bmp");
            ToolStripMenuItem fontSizeMenu = CreateMenuItem("Font Size", "size_font_change.bmp");
            foreach (float size in fontSizes)
            {
                ToolStripMenuItem fontSizeOption = CreateMenuItem(size.ToString("0"), "font_size_change.bmp", (_, _) => ChangeFontSize(size));
                fontSizeMenu.DropDownItems.Add(fontSizeOption);
            }

            ToolStripMenuItem increaseFontSizeShortcut = new ToolStripMenuItem("Increase Font Size")
            {
                ShortcutKeys = Keys.Control | Keys.Oemplus
            };
            increaseFontSizeShortcut.Click += IncreaseFontSize;
            fontSizeMenu.DropDownItems.Add(new ToolStripSeparator());
            fontSizeMenu.DropDownItems.Add(increaseFontSizeShortcut);

            ToolStripMenuItem decreaseFontSizeShortcut = new ToolStripMenuItem("Decrease Font Size")
            {
                ShortcutKeys = Keys.Control | Keys.OemMinus
            };
            decreaseFontSizeShortcut.Click += DecreaseFontSize;
            fontSizeMenu.DropDownItems.Add(decreaseFontSizeShortcut);

            ToolStripMenuItem themeMenu = CreateMenuItem("Theme", "theme.bmp");
            foreach (string themeName in AppThemeManager.ThemeNames)
            {
                string label = themeName == "RandomCool" ? "Random Cool Theme" : themeName;
                ToolStripMenuItem themeOption = CreateMenuItem(label, "theme_colorful.bmp", (_, _) => ChangeTheme(themeName));
                if (themeName == "Random")
                {
                    themeOption.ShortcutKeys = Keys.Control | Keys.Shift | Keys.R;
                }
                else if (themeName == "RandomCool")
                {
                    themeOption.ShortcutKeys = Keys.Control | Keys.Shift | Keys.C;
                }

                themeMenu.DropDownItems.Add(themeOption);
            }

            ToolStripMenuItem fontSettingsMenu = CreateMenuItem("Change Font", "font_adjustment.bmp");
            foreach (string font in new[]
            {
                "Segoe UI",
                "Arial",
                "Calibri",
                "Times New Roman",
                "Comic Sans MS",
                "Verdana",
                "Microsoft Sans Serif",
                "Bahnschrift"
            })
            {
                ToolStripMenuItem fontOption = new ToolStripMenuItem(font == "Segoe UI" ? "Segoe UI (Default)" : font, LoadIcon("font_adjustment.bmp"))
                {
                    Tag = font
                };
                fontOption.Click += (_, _) => ChangeFont(font);
                fontSettingsMenu.DropDownItems.Add(fontOption);
            }

            ToolStripMenuItem notificationPreferencesMenuItem = CreateMenuItem("Notification Preferences", "notification_setting.bmp", (_, _) => ToggleNotificationPreferencesPanel());
            chatBotModelMenu = CreateMenuItem("ChatBot Model", "theme_colorful.bmp");
            PopulateChatBotModelMenu();
            ToolStripMenuItem aboutMenu = CreateMenuItem("About ChatApp", "information.bmp", (_, _) => ShowAppInfo());

            settingsMenu.DropDownItems.Add(fontSizeMenu);
            settingsMenu.DropDownItems.Add(themeMenu);
            settingsMenu.DropDownItems.Add(fontSettingsMenu);
            settingsMenu.DropDownItems.Add(chatBotModelMenu);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add(notificationPreferencesMenuItem);
            settingsMenu.DropDownItems.Add(aboutMenu);

            mainMenu.Items.Add(accountMenu);
            mainMenu.Items.Add(fileMenu);
            mainMenu.Items.Add(functionsMenu);
            mainMenu.Items.Add(settingsMenu);

            MainMenuStrip = mainMenu;
            Controls.Add(mainMenu);

            InitializeTemplateMenu();
        }

        private void ConfigureActionButton(Button button, Color backgroundColor, Color foregroundColor)
        {
            button.BackColor = backgroundColor;
            button.ForeColor = foregroundColor;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = BlendColors(backgroundColor, Color.Black, 0.18f);
            button.Cursor = Cursors.Hand;
        }

        private ToolStripMenuItem CreateMenuItem(string text, string iconFileName, EventHandler? clickHandler = null)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text, LoadIcon(iconFileName));
            if (clickHandler != null)
            {
                item.Click += clickHandler;
            }

            return item;
        }

        private Image? LoadIcon(string fileName)
        {
            string path = Path.Combine(iconFolder, fileName);
            return File.Exists(path) ? Image.FromFile(path) : null;
        }

        private void HandleMessageInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                SendMessage(sender ?? this, EventArgs.Empty);
            }
        }

        private void DrawPeopleListItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            e.DrawBackground();
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            string person = peopleList.Items[e.Index]?.ToString() ?? string.Empty;
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            bool isOnline = contactOnlineStates.TryGetValue(person, out bool online) && online;
            bool isPinned = pinnedChats.Contains(person);
            int unreadCount = unreadCounts.GetValueOrDefault(person);
            string subtitle = GetContactSubtitle(person);

            Rectangle itemBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 4, e.Bounds.Width - 8, e.Bounds.Height - 8);
            Color cardColor = isSelected
                ? BlendColors(currentAccentColor, Color.White, 0.18f)
                : BlendColors(currentSidebarColor, Color.White, 0.06f);
            Color subtitleColor = isSelected
                ? BlendColors(currentSidebarTextColor, currentSidebarMutedTextColor, 0.35f)
                : currentSidebarMutedTextColor;
            Color avatarColor = isSelected ? currentAccentColor : BlendColors(currentSidebarColor, currentAccentColor, 0.52f);

            using (SolidBrush cardBrush = new SolidBrush(cardColor))
            using (SolidBrush avatarBrush = new SolidBrush(avatarColor))
            {
                e.Graphics.FillRectangle(cardBrush, itemBounds);
                if (isSelected)
                {
                    using SolidBrush accentBrush = new SolidBrush(currentAccentColor);
                    e.Graphics.FillRectangle(accentBrush, new Rectangle(itemBounds.X, itemBounds.Y, 4, itemBounds.Height));
                }

                Rectangle avatarRect = new Rectangle(itemBounds.X + 14, itemBounds.Y + 12, 36, 36);
                e.Graphics.FillEllipse(avatarBrush, avatarRect);

                Rectangle presenceRect = new Rectangle(avatarRect.Right - 11, avatarRect.Bottom - 11, 11, 11);
                using SolidBrush presenceBrush = new SolidBrush(isOnline ? Color.FromArgb(74, 201, 126) : BlendColors(currentSidebarMutedTextColor, currentSidebarColor, 0.3f));
                using Pen presenceBorder = new Pen(cardColor, 2f);
                e.Graphics.FillEllipse(presenceBrush, presenceRect);
                e.Graphics.DrawEllipse(presenceBorder, presenceRect);

                using Font initialsFont = new Font("Segoe UI Semibold", 9.5f, FontStyle.Bold);
                TextRenderer.DrawText(
                    e.Graphics,
                    GetInitials(person),
                    initialsFont,
                    avatarRect,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                int unreadBubbleWidth = 0;
                if (unreadCount > 0)
                {
                    using Font unreadFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    unreadBubbleWidth = Math.Max(22, TextRenderer.MeasureText(unreadCount.ToString(), unreadFont).Width + 12);
                    Rectangle bubbleRect = new Rectangle(itemBounds.Right - unreadBubbleWidth - 12, itemBounds.Y + 18, unreadBubbleWidth, 22);
                    using SolidBrush bubbleBrush = new SolidBrush(currentAccentColor);
                    e.Graphics.FillEllipse(bubbleBrush, bubbleRect);
                    TextRenderer.DrawText(
                        e.Graphics,
                        unreadCount.ToString(),
                        unreadFont,
                        bubbleRect,
                        GetReadableTextColor(currentAccentColor),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                Rectangle titleRect = new Rectangle(itemBounds.X + 62, itemBounds.Y + 10, itemBounds.Width - 86 - unreadBubbleWidth, 24);
                Rectangle subtitleRect = new Rectangle(itemBounds.X + 62, itemBounds.Y + 30, itemBounds.Width - 74, 22);

                using Font titleFont = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
                using Font subtitleFont = new Font("Segoe UI", 8.75f, FontStyle.Regular);

                string titleText = isPinned ? "[Pinned] " + person : person;
                TextRenderer.DrawText(e.Graphics, titleText, titleFont, titleRect, currentSidebarTextColor, TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, subtitle, subtitleFont, subtitleRect, subtitleColor, TextFormatFlags.EndEllipsis);
            }

            e.DrawFocusRectangle();
        }

        private void DrawUnaddedContactItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0)
            {
                return;
            }

            e.DrawBackground();
            string person = unaddedContactsList.Items[e.Index]?.ToString() ?? string.Empty;
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            Rectangle itemBounds = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 3, e.Bounds.Width - 8, e.Bounds.Height - 6);
            Color cardColor = isSelected
                ? BlendColors(currentAccentColor, Color.White, 0.2f)
                : BlendColors(unaddedContactsPanel.BackColor, Color.White, 0.04f);

            using (SolidBrush cardBrush = new SolidBrush(cardColor))
            {
                e.Graphics.FillRectangle(cardBrush, itemBounds);
            }

            Rectangle tagRect = new Rectangle(itemBounds.Right - 58, itemBounds.Y + 8, 42, 22);
            using (SolidBrush tagBrush = new SolidBrush(BlendColors(currentAccentColor, Color.White, 0.1f)))
            {
                e.Graphics.FillRectangle(tagBrush, tagRect);
            }

            using Font nameFont = new Font("Segoe UI", 9.75f, FontStyle.Regular);
            using Font tagFont = new Font("Segoe UI Semibold", 8f, FontStyle.Bold);

            TextRenderer.DrawText(
                e.Graphics,
                person,
                nameFont,
                new Rectangle(itemBounds.X + 12, itemBounds.Y + 9, itemBounds.Width - 80, 22),
                currentSidebarTextColor,
                TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                e.Graphics,
                "ADD",
                tagFont,
                tagRect,
                currentAccentColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            e.DrawFocusRectangle();
        }

        private string GetInitials(string value)
        {
            string[] parts = value
                .Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToArray();

            if (parts.Length == 0)
            {
                return "?";
            }

            if (parts.Length == 1)
            {
                return parts[0].Length >= 2
                    ? parts[0][0].ToString().ToUpperInvariant() + parts[0][1].ToString().ToUpperInvariant()
                    : parts[0][0].ToString().ToUpperInvariant();
            }

            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        private void UpdateTextInfo()
        {
            if (textInfoLabel == null || messageInput == null)
            {
                return;
            }

            int wordCount = CountWords(messageInput.Text);
            int charCount = messageInput.Text.Length;
            (int line, int column) = GetCursorPosition(messageInput);
            textInfoLabel.Text = $"{wordCount} words - {charCount} characters - Line {line}, Col {column} - Ctrl+Enter to send";
        }

        private int CountWords(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? 0 : Regex.Matches(text, "\\b\\w+\\b").Count;
        }

        private (int line, int column) GetCursorPosition(TextBox textBox)
        {
            int index = textBox.SelectionStart;
            int line = textBox.GetLineFromCharIndex(index) + 1;
            int column = index - textBox.GetFirstCharIndexOfCurrentLine() + 1;
            return (line, column);
        }

        private Color BlendColors(Color baseColor, Color overlayColor, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            int r = (int)(baseColor.R + ((overlayColor.R - baseColor.R) * amount));
            int g = (int)(baseColor.G + ((overlayColor.G - baseColor.G) * amount));
            int b = (int)(baseColor.B + ((overlayColor.B - baseColor.B) * amount));
            return Color.FromArgb(r, g, b);
        }
    }
}
