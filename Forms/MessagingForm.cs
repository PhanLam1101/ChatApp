using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MessagingApp
{
    public partial class MessagingForm : Form
    {
        private Panel chatPanelBorder = null!;
        private Panel contactPanelBorder = null!;
        private Panel sendMessageButtonBorder = null!;
        private Panel chatPanel = null!;
        private Panel contactPanel = null!;
        private Panel notificationPreferencesPanel = null!;
        private Panel templatePanel = null!;
        private Panel chatHeaderPanel = null!;
        private Panel messageInputCard = null!;
        private Panel sidebarSearchCard = null!;
        private Panel contactActionsPanel = null!;

        private Label sidebarTitleLabel = null!;
        private Label sidebarSubtitleLabel = null!;
        private Label contactsSummaryLabel = null!;
        private Label activeChatTitleLabel = null!;
        private Label activeChatSubtitleLabel = null!;
        private Label textInfoLabel = null!;

        private RichTextBox chatbox = null!;
        private TextBox messageInput = null!;
        private Button sendMessageButton = null!;
        private Button sendFileButton = null!;
        private Button refreshButton = null!;
        private Button removeContactButton = null!;
        private ListBox peopleList = null!;
        private TextBox filterTextBox = null!;

        private string fontFamily = "Segoe UI";
        private float fontSize = 12f;
        private readonly float[] fontSizes = { 10f, 12f, 14f, 16f, 18f, 20f, 22f, 24f };
        private readonly string delimiter = "{#EOM#}";
        private string themeSetting = AppThemeManager.DefaultThemeName;

        private Label addContactLabel = null!;
        private TextBox addContactInput = null!;
        private Button addContactButton = null!;

        private string? currentPerson;
        private readonly string currentUserName;
        private readonly string conversationFolder = "Conversations";
        private readonly string settingFolder = "Settings";
        private readonly string iconFolder = "Icons";
        private readonly string templatesFolder = "Templates";

        private Panel unaddedContactsPanel = null!;
        private ListBox unaddedContactsList = null!;
        private Button addSelectedContactButton = null!;
        private Label unaddedContactsLabel = null!;
        private readonly string unaddedContactsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UnaddedContact.txt");

        private List<string> people = new List<string>();

        private CheckBox enableSoundCheckBox = null!;
        private CheckBox enablePopupCheckBox = null!;
        private CheckBox enableStaticNotificationPanel = null!;

        private ToolStripMenuItem functionsMenu = null!;
        private ToolStripMenuItem templatesMenu = null!;

        private readonly Random random = new Random();

        private Panel addTemplatePanel = null!;
        private TextBox templateNameInput = null!;
        private RichTextBox templateContentInput = null!;

        private readonly OpenFileDialog fileDialog = new OpenFileDialog();

        private FileSystemWatcher? fileWatcher;
        private FileSystemWatcher? friendListWatcher;
        private long lastFileSize;
        private FileSystemWatcher? unaddedContactsWatcher;

        private Panel messageDetailsPanel = null!;
        private Panel chatSearchPanel = null!;
        private RichTextBox detailedMessageBox = null!;
        private List<string> searchResults = new List<string>();

        private System.Windows.Forms.Timer notificationTimer = null!;
        private Panel notificationPanel = null!;
        private Label notificationSenderLabel = null!;
        private Label notificationMessageLabel = null!;

        private Color currentBackgroundColor = Color.FromArgb(244, 246, 250);
        private Color currentSidebarColor = Color.FromArgb(31, 42, 68);
        private Color currentSurfaceColor = Color.White;
        private Color currentComposerColor = Color.FromArgb(248, 250, 253);
        private Color currentAccentColor = Color.FromArgb(46, 125, 255);
        private Color currentAccentSoftColor = Color.FromArgb(221, 233, 255);
        private Color currentTextColor = Color.FromArgb(24, 34, 52);
        private Color currentMutedTextColor = Color.FromArgb(111, 122, 144);
        private Color currentBorderColor = Color.FromArgb(216, 223, 233);
        private Color currentSidebarTextColor = Color.White;
        private Color currentSidebarMutedTextColor = Color.FromArgb(185, 193, 213);
        private Color currentDangerColor = Color.FromArgb(224, 94, 94);

        public MessagingForm(string userName)
        {
            currentUserName = userName;
            checkFolder(settingFolder);

            StartPosition = FormStartPosition.CenterScreen;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            UpdateStyles();

            LoadFontPreferences();
            InitializeComponent();
            InitializePanels();
            InitializeUIElements();
            InitializeUnaddedContactsPanel();
            CreateNotificationPanel();
            InitializeChatSearchPanel();
            LoadNotificationPreferences();

            PipeConnectionManager.OnNewMessageNotification += OnNewMessageReceived;
            Load += MessageForm_LoadTheme;
            FormClosing += MessageForm_FormClosing;

            InitializeConversationsFolder();
            LoadName();
        }
    }
}
