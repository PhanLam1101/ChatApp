using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Media;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace MessagingApp
{
    public partial class MessagingForm : Form
    {
	private void OnUnaddedContactsChanged(object sender, FileSystemEventArgs e)
	{
		if (File.Exists(unaddedContactsFile))
		{
			Invoke((MethodInvoker)delegate
			{
				LoadUnaddedContacts();
			});
		}
		else
		{
			Invoke((MethodInvoker)delegate
			{
				unaddedContactsList.Items.Clear();
			});
		}
	}

	private void InitializeFileWatcher()
	{
		string directoryPath = Path.GetDirectoryName(unaddedContactsFile) ?? AppPaths.CurrentWorkspaceRoot;
		if (!Directory.Exists(directoryPath))
		{
			Directory.CreateDirectory(directoryPath);
		}
		if (!File.Exists(unaddedContactsFile))
		{
			File.Create(unaddedContactsFile).Dispose();
		}
		unaddedContactsWatcher = new FileSystemWatcher
		{
			Path = directoryPath,
			Filter = Path.GetFileName(unaddedContactsFile),
			NotifyFilter = (NotifyFilters.FileName | NotifyFilters.LastWrite)
		};
		unaddedContactsWatcher.Changed += OnUnaddedContactsChanged;
		unaddedContactsWatcher.Created += OnUnaddedContactsChanged;
		unaddedContactsWatcher.Deleted += OnUnaddedContactsChanged;
		unaddedContactsWatcher.EnableRaisingEvents = true;
	}

	private void LoadUnaddedContacts()
	{
		if (!File.Exists(unaddedContactsFile))
		{
			File.Create(unaddedContactsFile).Dispose();
			MessageBox.Show("Unadded contacts file was missing and has now been created.");
		}
		string[] unaddedContacts = File.ReadAllLines(unaddedContactsFile)
			.Where(contact => !IsChatBotContact(contact))
			.ToArray();
		unaddedContactsList.Items.Clear();
		ListBox.ObjectCollection items = unaddedContactsList.Items;
		object[] items2 = unaddedContacts;
		items.AddRange(items2);
	}

	private void AddSelectedContact()
	{
		if (unaddedContactsList.SelectedItem != null)
		{
			string? selectedContact = unaddedContactsList.SelectedItem.ToString();
			if (string.IsNullOrWhiteSpace(selectedContact))
			{
				return;
			}

			AddContact(selectedContact);
			unaddedContactsList.Items.Remove(selectedContact);
			File.WriteAllLines(unaddedContactsFile, unaddedContactsList.Items.Cast<string>());
		}
		else
		{
			MessageBox.Show("Please select a contact to add.");
		}
	}

	private void ReplaceSpacesWithUnderscores(TextBox textBox)
	{
		int cursorPosition = textBox.SelectionStart;
		textBox.Text = textBox.Text.Replace(" ", "_");
		textBox.Text = textBox.Text.Replace("\t", "_");
		textBox.SelectionStart = cursorPosition;
	}

	private void AddContact(string contactName)
	{
		if (IsChatBotContact(contactName))
		{
			MessageBox.Show("ChatBot is already built into the app. Select it from the contact list to start chatting.", "ChatBot", MessageBoxButtons.OK, MessageBoxIcon.Information);
			currentPerson = ChatBotContactName;
			UpdatePeopleList();
			LoadConversation(currentUserName, ChatBotContactName);
			SetupFileWatcher(currentUserName, ChatBotContactName);
			return;
		}

		foreach (object item in peopleList.Items)
		{
			if (string.Equals(item?.ToString(), contactName, StringComparison.Ordinal))
			{
				MessageBox.Show("This contact already exists!", "Duplicate Contact", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
		}
		string nameFile = AppPaths.AddContactRequestFile;
		File.WriteAllText(nameFile, contactName);
		StreamWriter? pipeWriter = PipeConnectionManager.PipeWriter;
		if (pipeWriter != null)
		{
			pipeWriter.WriteLine("ADD_FRIEND");
			pipeWriter.Flush();
			string resultFilePath = AppPaths.AddContactResultFile;
			Thread.Sleep(500);
			if (File.Exists(resultFilePath))
			{
				string result = File.ReadAllText(resultFilePath).Trim();
				if (result == "success")
				{
					MessageBox.Show("The new contact has been added successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					File.Delete(resultFilePath);
					File.Delete(nameFile);
				}
				else if (result == "fail")
				{
					MessageBox.Show("The contact does not exist!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
					File.Delete(resultFilePath);
					File.Delete(nameFile);
				}
				else if (result == "no_key")
				{
					MessageBox.Show("This user exists, but they have not uploaded an encryption key yet. Ask them to sign in with the upgraded app first.", "Encryption Not Ready", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					File.Delete(resultFilePath);
					File.Delete(nameFile);
				}
			}
		}
		else
		{
			File.Delete(nameFile);
			MessageBox.Show("Pipe connection is not established.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void ChangeFontSize(float newFontSize)
	{
		chatbox.Font = new Font(chatbox.Font.FontFamily, newFontSize);
		messageInput.Font = new Font(messageInput.Font.FontFamily, newFontSize);
		fontSize = newFontSize;
		UpdateTextInfo();
		SaveFontPreferences(fontFamily, fontSize);
		if (!string.IsNullOrWhiteSpace(currentPerson))
		{
			LoadConversation(currentUserName, currentPerson);
			return;
		}

		ShowConversationPlaceholder();
	}

	private void ChangeTheme(string theme)
	{
		AppThemePalette palette = AppThemeManager.ResolveTheme(theme);
		themeSetting = palette.ThemeSelection;
		AppThemeManager.SaveThemeSelection(themeSetting);
		currentBackgroundColor = palette.BackgroundColor;
		currentSidebarColor = palette.SidebarColor;
		currentSurfaceColor = palette.SurfaceColor;
		currentComposerColor = palette.ComposerColor;
		currentAccentColor = palette.AccentColor;
		currentAccentSoftColor = palette.AccentSoftColor;
		currentTextColor = palette.TextColor;
		currentMutedTextColor = palette.MutedTextColor;
		currentBorderColor = palette.BorderColor;
		currentSidebarTextColor = palette.SidebarTextColor;
		currentSidebarMutedTextColor = palette.SidebarMutedTextColor;
		currentDangerColor = palette.DangerColor;
		ApplyCurrentThemeStyles();
		if (!string.IsNullOrWhiteSpace(currentPerson))
		{
			LoadConversation(currentUserName, currentPerson);
			return;
		}

		ShowConversationPlaceholder();
	}

	private void ApplyCurrentThemeStyles()
	{
		BackColor = currentBackgroundColor;
		chatPanelBorder.BackColor = currentBorderColor;
		contactPanelBorder.BackColor = currentBorderColor;
		chatPanel.BackColor = currentSurfaceColor;
		contactPanel.BackColor = currentSidebarColor;
		chatbox.BackColor = currentSurfaceColor;
		chatbox.ForeColor = currentTextColor;
		messageInput.BackColor = currentComposerColor;
		messageInput.ForeColor = currentTextColor;
		textInfoLabel.BackColor = currentComposerColor;
		textInfoLabel.ForeColor = currentMutedTextColor;
		sidebarTitleLabel.BackColor = currentSidebarColor;
		sidebarTitleLabel.ForeColor = currentSidebarTextColor;
		sidebarSubtitleLabel.BackColor = currentSidebarColor;
		sidebarSubtitleLabel.ForeColor = currentSidebarMutedTextColor;
		contactsSummaryLabel.BackColor = currentSidebarColor;
		contactsSummaryLabel.ForeColor = currentSidebarMutedTextColor;
		activeChatTitleLabel.BackColor = currentSurfaceColor;
		activeChatTitleLabel.ForeColor = currentTextColor;
		activeChatSubtitleLabel.BackColor = currentSurfaceColor;
		activeChatSubtitleLabel.ForeColor = currentMutedTextColor;
		sidebarSearchCard.BackColor = BlendColors(currentSidebarColor, Color.White, 0.1f);
		if (filterTextBox.Parent != null)
		{
			filterTextBox.Parent.BackColor = sidebarSearchCard.BackColor;
		}

		filterTextBox.BackColor = sidebarSearchCard.BackColor;
		filterTextBox.ForeColor = currentSidebarTextColor;
		peopleList.BackColor = currentSidebarColor;
		peopleList.ForeColor = currentSidebarTextColor;
		if (addContactInput.Parent != null)
		{
			addContactInput.Parent.BackColor = BlendColors(currentSidebarColor, Color.White, 0.1f);
		}

		addContactLabel.BackColor = currentSidebarColor;
		addContactLabel.ForeColor = currentSidebarTextColor;
		addContactInput.BackColor = addContactInput.Parent?.BackColor ?? currentSidebarColor;
		addContactInput.ForeColor = currentSidebarTextColor;
		unaddedContactsPanel.BackColor = BlendColors(currentSidebarColor, Color.White, 0.06f);
		unaddedContactsLabel.BackColor = unaddedContactsPanel.BackColor;
		unaddedContactsLabel.ForeColor = currentSidebarTextColor;
		unaddedContactsList.BackColor = unaddedContactsPanel.BackColor;
		unaddedContactsList.ForeColor = currentSidebarTextColor;
		messageInputCard.BackColor = currentComposerColor;
		sendMessageButtonBorder.BackColor = currentComposerColor;
		mainMenu.BackColor = currentSurfaceColor;
		mainMenu.ForeColor = currentTextColor;
		if (replyPreviewPanel != null)
		{
			replyPreviewPanel.BackColor = BlendColors(currentComposerColor, currentAccentColor, 0.12f);
			replyPreviewLabel.ForeColor = currentTextColor;
			ConfigureActionButton(clearReplyButton, BlendColors(currentComposerColor, currentDangerColor, 0.2f), currentTextColor);
		}

		if (pinnedMessagePanel != null)
		{
			pinnedMessagePanel.BackColor = BlendColors(currentSurfaceColor, currentAccentColor, 0.06f);
			pinnedMessageLabel.ForeColor = currentTextColor;
			ConfigureActionButton(unpinMessageButton, BlendColors(currentSurfaceColor, currentAccentColor, 0.14f), currentTextColor);
		}

		ConfigureActionButton(sendMessageButton, currentAccentColor, GetReadableTextColor(currentAccentColor));
		sendMessageButton.BackgroundImage = null;
		sendMessageButton.Text = "Send";
		ConfigureActionButton(sendFileButton, BlendColors(currentSurfaceColor, currentAccentColor, 0.1f), currentTextColor);
		sendFileButton.BackgroundImage = null;
		ConfigureActionButton(refreshButton, BlendColors(currentSurfaceColor, currentAccentColor, 0.1f), currentTextColor);
		ConfigureActionButton(addContactButton, currentAccentColor, GetReadableTextColor(currentAccentColor));
		ConfigureActionButton(addSelectedContactButton, currentAccentColor, GetReadableTextColor(currentAccentColor));
		ConfigureActionButton(removeContactButton, currentDangerColor, GetReadableTextColor(currentDangerColor));

		ApplyMenuColors(mainMenu.Items);
		if (notificationPreferencesPanel != null)
		{
			ApplyThemeToPanel(notificationPreferencesPanel, currentSurfaceColor, currentTextColor, currentMutedTextColor);
		}

		if (chatSearchPanel != null)
		{
			ApplyThemeToPanel(chatSearchPanel, currentSurfaceColor, currentTextColor, currentMutedTextColor);
		}

		if (messageDetailsPanel != null)
		{
			ApplyThemeToPanel(messageDetailsPanel, currentSurfaceColor, currentTextColor, currentMutedTextColor);
		}

		if (templatePanel != null)
		{
			ApplyThemeToPanel(templatePanel, currentSurfaceColor, currentTextColor, currentMutedTextColor);
		}

		if (addTemplatePanel != null)
		{
			ApplyThemeToPanel(addTemplatePanel, currentSurfaceColor, currentTextColor, currentMutedTextColor);
		}

		if (notificationPanel != null)
		{
			notificationPanel.BackColor = currentSidebarColor;
			notificationSenderLabel.ForeColor = currentSidebarTextColor;
			notificationMessageLabel.ForeColor = currentSidebarMutedTextColor;
		}

		peopleList.Invalidate();
		unaddedContactsList.Invalidate();
		UpdateTextInfo();
		UpdateConversationHeader(currentPerson);
	}

	private void ApplyMenuColors(ToolStripItemCollection items)
	{
		foreach (ToolStripItem item in items)
		{
			item.BackColor = currentSurfaceColor;
			item.ForeColor = currentTextColor;
			if (item is ToolStripDropDownItem dropDownItem)
			{
				ApplyMenuColors(dropDownItem.DropDownItems);
			}
		}
	}

	private void ApplyThemeToPanel(Control control, Color backgroundColor, Color primaryTextColor, Color mutedTextColor)
	{
		control.BackColor = backgroundColor;
		foreach (Control child in control.Controls)
		{
			switch (child)
			{
			case Label label:
				label.BackColor = backgroundColor;
				label.ForeColor = label.Font.Bold ? primaryTextColor : mutedTextColor;
				break;
			case TextBox textBox:
				textBox.BackColor = backgroundColor;
				textBox.ForeColor = primaryTextColor;
				break;
			case RichTextBox richTextBox:
				richTextBox.BackColor = backgroundColor;
				richTextBox.ForeColor = primaryTextColor;
				break;
			case ListBox listBox:
				listBox.BackColor = backgroundColor;
				listBox.ForeColor = primaryTextColor;
				break;
			case CheckBox checkBox:
				checkBox.BackColor = backgroundColor;
				checkBox.ForeColor = primaryTextColor;
				break;
			case Button button:
				ConfigureActionButton(button, BlendColors(backgroundColor, currentAccentColor, 0.1f), primaryTextColor);
				break;
			case Panel panel:
				ApplyThemeToPanel(panel, backgroundColor, primaryTextColor, mutedTextColor);
				break;
			}
		}
	}

	private Color GetReadableTextColor(Color backgroundColor)
	{
		double luminance = ((0.299 * backgroundColor.R) + (0.587 * backgroundColor.G) + (0.114 * backgroundColor.B)) / 255d;
		return luminance > 0.6 ? Color.FromArgb(24, 34, 52) : Color.White;
	}

	private void EnsureContactState(string contactName)
	{
		if (string.IsNullOrWhiteSpace(contactName))
		{
			return;
		}

		if (!contactOnlineStates.ContainsKey(contactName))
		{
			contactOnlineStates[contactName] = IsChatBotContact(contactName);
		}

		if (!unreadCounts.ContainsKey(contactName))
		{
			unreadCounts[contactName] = 0;
		}
	}

	private void RefreshConversationMetadata()
	{
		foreach (string person in people)
		{
			RefreshConversationMetadataForContact(person);
		}
	}

	private string NormalizePreview(string value)
	{
		string singleLine = value.Replace("\r\n", " ").Replace('\n', ' ').Trim();
		if (singleLine.Length > 60)
		{
			return singleLine.Substring(0, 57) + "...";
		}

		return singleLine;
	}

	private string ExtractConversationPreview(string rawMessage)
	{
		ParsedChatEntry entry = ParseChatEntry(rawMessage.Trim());
		if (entry.IsControl)
		{
			return string.Empty;
		}

		string normalizedBody = NormalizePreview(entry.Body);
		if (string.IsNullOrWhiteSpace(normalizedBody))
		{
			return entry.Sender == currentUserName ? "You sent a message" : entry.Sender + " sent a message";
		}

		return entry.Sender == currentUserName ? "You: " + normalizedBody : normalizedBody;
	}

	private void UpdateConversationPreview(string contactName, string rawMessage)
	{
		if (string.IsNullOrWhiteSpace(contactName))
		{
			return;
		}

		EnsureContactState(contactName);
		lastMessagePreviews[contactName] = ExtractConversationPreview(rawMessage);
	}

	private void IncrementUnreadCount(string contactName)
	{
		if (string.IsNullOrWhiteSpace(contactName))
		{
			return;
		}

		EnsureContactState(contactName);
		if (string.Equals(currentPerson, contactName, StringComparison.OrdinalIgnoreCase))
		{
			unreadCounts[contactName] = 0;
			return;
		}

		unreadCounts[contactName] = unreadCounts.GetValueOrDefault(contactName) + 1;
	}

	private void MarkConversationAsRead(string contactName)
	{
		if (string.IsNullOrWhiteSpace(contactName))
		{
			return;
		}

		EnsureContactState(contactName);
		if (unreadCounts.GetValueOrDefault(contactName) != 0)
		{
			unreadCounts[contactName] = 0;
			UpdatePeopleList();
		}
	}

	private string GetContactSubtitle(string contactName)
	{
		if (lastMessagePreviews.TryGetValue(contactName, out string? preview) && !string.IsNullOrWhiteSpace(preview))
		{
			return preview;
		}

		if (IsChatBotContact(contactName))
		{
			return "Server assistant | Model: " + GetSelectedChatBotModelDisplayName();
		}

		bool isOnline = contactOnlineStates.TryGetValue(contactName, out bool online) && online;
		return isOnline ? "Online now" : "No messages yet";
	}

	private void ShowConversationPlaceholder()
	{
		activeConversationState = null;
		ClearReplyTarget();
		chatbox.Clear();
		UpdateConversationHeader(null);
		AppendConversationStatus("Choose a conversation on the left to start chatting. User chats are end-to-end encrypted.");
	}

	private void UpdateConversationHeader(string? selectedPerson)
	{
		bool hasSelection = !string.IsNullOrWhiteSpace(selectedPerson);
		activeChatTitleLabel.Text = hasSelection ? selectedPerson : "Select a chat";
		if (hasSelection)
		{
			if (IsChatBotContact(selectedPerson))
			{
				activeChatSubtitleLabel.Text = "Server-side assistant | Model: " + GetSelectedChatBotModelDisplayName() + " | Not end-to-end encrypted";
			}
			else
			{
				bool isTyping = selectedPerson != null &&
					typingIndicators.TryGetValue(selectedPerson, out DateTime typingUntil) &&
					typingUntil > DateTime.UtcNow;
				bool isOnline = contactOnlineStates.TryGetValue(selectedPerson!, out bool online) && online;
				activeChatSubtitleLabel.Text = isTyping
					? "Typing... | End-to-end encrypted"
					: isOnline
						? "Online now | End-to-end encrypted"
						: $"Offline | End-to-end encrypted with {selectedPerson}";
			}
		}
		else
		{
			activeChatSubtitleLabel.Text = "Choose a conversation from the left to start chatting.";
		}
		sendMessageButton.Enabled = hasSelection;
		sendFileButton.Enabled = hasSelection && !IsChatBotContact(selectedPerson);
		UpdatePinnedMessagePanel();
	}

	private void AppendConversationStatus(string message)
	{
		chatbox.SelectionStart = chatbox.TextLength;
		chatbox.SelectionColor = currentMutedTextColor;
		chatbox.SelectionFont = new Font(fontFamily, fontSize, FontStyle.Italic);
		chatbox.AppendText(message + Environment.NewLine + Environment.NewLine);
		chatbox.SelectionColor = currentTextColor;
		chatbox.SelectionFont = new Font(fontFamily, fontSize, FontStyle.Regular);
	}

	private bool TryParseChatMessage(string message, out string sender, out string timestamp, out string body)
	{
		TryExtractMetadata(message, out _, out string cleanMessage);
		Match match = Regex.Match(cleanMessage, "^(?<sender>.+?) \\[(?<timestamp>[^\\]]+)\\]:\\s?(?<body>[\\s\\S]*)$");
		if (match.Success)
		{
			sender = match.Groups["sender"].Value.Trim();
			timestamp = match.Groups["timestamp"].Value.Trim();
			body = match.Groups["body"].Value.Trim();
			return true;
		}

		sender = string.Empty;
		timestamp = string.Empty;
		body = string.Empty;
		return false;
	}

	private string GetConversationFileName(string person1, string person2)
	{
		if (string.Compare(person1, person2) <= 0)
		{
			return person2 + "-" + person1 + ".bin";
		}
		return person1 + "-" + person2 + ".bin";
	}

	private string GetName(string person1, string person2)
	{
		if (string.Compare(person1, person2) <= 0)
		{
			return person2 + "-" + person1 + ".bin";
		}
		return person1 + "-" + person2 + ".bin";
	}

	private void LoadName()
	{
		string friendListFilePath = AppPaths.FriendListFile;
		if (!File.Exists(friendListFilePath))
		{
			File.Create(friendListFilePath).Close();
		}
		people = File.ReadAllLines(friendListFilePath)
			.Where(static line => !string.IsNullOrWhiteSpace(line))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToList();
		if (!people.Contains(ChatBotContactName, StringComparer.OrdinalIgnoreCase))
		{
			people.Add(ChatBotContactName);
		}
		RefreshConversationMetadata();
		UpdatePeopleList();
		if (string.IsNullOrWhiteSpace(currentPerson))
		{
			ShowConversationPlaceholder();
		}

		friendListWatcher = new FileSystemWatcher
		{
			Path = Path.GetDirectoryName(friendListFilePath) ?? AppPaths.CurrentWorkspaceRoot,
			Filter = Path.GetFileName(friendListFilePath),
			NotifyFilter = (NotifyFilters.Size | NotifyFilters.LastWrite)
		};
		friendListWatcher.Changed += delegate
		{
			Invoke((MethodInvoker)delegate
			{
				people = File.ReadAllLines(friendListFilePath)
					.Where(static line => !string.IsNullOrWhiteSpace(line))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				if (!people.Contains(ChatBotContactName, StringComparer.OrdinalIgnoreCase))
				{
					people.Add(ChatBotContactName);
				}
				RefreshConversationMetadata();
				UpdatePeopleList();
			});
		};
		friendListWatcher.EnableRaisingEvents = true;
	}

	private void UpdatePeopleList()
	{
		if (InvokeRequired)
		{
			BeginInvoke((MethodInvoker)delegate
			{
				UpdatePeopleList();
			});
			return;
		}

		foreach (string person in people)
		{
			EnsureContactState(person);
		}

		string filter = filterTextBox?.Text.Trim() ?? string.Empty;
		IEnumerable<string> orderedPeople = people
			.OrderByDescending((string person) => pinnedChats.Contains(person))
			.ThenByDescending((string person) => unreadCounts.GetValueOrDefault(person))
			.ThenByDescending((string person) => contactOnlineStates.GetValueOrDefault(person))
			.ThenBy((string person) => person, StringComparer.OrdinalIgnoreCase);
		string[] visiblePeople = string.IsNullOrWhiteSpace(filter)
			? orderedPeople.ToArray()
			: orderedPeople.Where((string person) => person.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

		suppressPeopleSelectionChange = true;
		peopleList.BeginUpdate();
		try
		{
			peopleList.Items.Clear();
			peopleList.Items.AddRange(visiblePeople);

			if (!string.IsNullOrWhiteSpace(currentPerson) && visiblePeople.Contains(currentPerson))
			{
				string? selectedValue = peopleList.SelectedItem?.ToString();
				if (!string.Equals(selectedValue, currentPerson, StringComparison.Ordinal))
				{
					peopleList.SelectedItem = currentPerson;
				}
			}
			else if (peopleList.SelectedItem != null)
			{
				peopleList.ClearSelected();
			}
		}
		finally
		{
			peopleList.EndUpdate();
			suppressPeopleSelectionChange = false;
		}

		int total = people.Count;
		int onlineCount = people.Count((string person) => contactOnlineStates.GetValueOrDefault(person));
		int unreadTotal = people.Sum((string person) => unreadCounts.GetValueOrDefault(person));
		contactsSummaryLabel.Text = total == 0
			? "No contacts yet"
			: visiblePeople.Length == total
				? $"{total} contact{(total == 1 ? string.Empty : "s")} | {onlineCount} online | {unreadTotal} unread"
				: $"Showing {visiblePeople.Length} of {total} | {onlineCount} online | {unreadTotal} unread";
		peopleList.Invalidate();
	}

	private void SendMessage(object? sender, EventArgs e)
	{
		if (currentPerson == null)
		{
			MessageBox.Show("Please select a person to chat with.");
			return;
		}
		string message = messageInput.Text.Trim();
		if (string.IsNullOrEmpty(message))
		{
			MessageBox.Show("Cannot send an empty message.");
			return;
		}
		if (ContainsUndisplayableCharacters(message))
		{
			MessageBox.Show("The message contains characters that are not supported . Please check the message to use only standard characters.", "Invalid File Name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		string messageId = Guid.NewGuid().ToString("N");
		string completedMessage = CreateTextMessagePayload(message, messageId);
		SendConversationPayload(currentPerson, completedMessage, messageId);
		MarkConversationAsRead(currentPerson);
		StopLocalTypingIndicator();
		messageInput.Clear();
		ClearReplyTarget();
		UpdateTextInfo();
		messageInput.Focus();
	}

	private void SendFile(object? sender, EventArgs e)
	{
		if (currentPerson == null)
		{
			MessageBox.Show("Please select a person to send a file.");
		}
		else if (IsChatBotContact(currentPerson))
		{
			MessageBox.Show("File sending is not supported for ChatBot conversations.", "ChatBot", MessageBoxButtons.OK, MessageBoxIcon.Information);
		}
		else
		{
			if (fileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			string tempFile = AppPaths.SendFileNowFile;
			string filePath = fileDialog.FileName;
			string recipient = currentPerson;
			Path.GetFileName(filePath);
			if (!IsFileNameUtf7Compliant(filePath))
			{
				MessageBox.Show("The file path contains characters that are not supported (such as Ã¼, Ã¶, Ã¤, Ã¡, Ã³, Ãº, ...). Please rename the file to use only standard characters.", "Invalid File Name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			File.WriteAllText(tempFile, recipient + "\n" + filePath);
			StreamWriter? pipeWriter = PipeConnectionManager.PipeWriter;
			if (pipeWriter != null)
			{
				pipeWriter.WriteLine("SEND_FILE");
				pipeWriter.Flush();
				MessageBox.Show("Sending file '" + filePath + "' to " + recipient);
			}
			else
			{
				MessageBox.Show("Pipe connection is not established.");
			}
		}
	}

	private bool IsFileNameUtf7Compliant(string fileName)
	{
		foreach (char c in fileName)
		{
			if (c < ' ' || c > '~')
			{
				return false;
			}
		}
		return true;
	}

	private bool ContainsUndisplayableCharacters(string input)
	{
		foreach (char c in input)
		{
			if ((c < ' ' && c != '\r' && c != '\n' && c != '\t') || (c >= '\u007f' && c < '\u00a0') || char.IsSurrogate(c))
			{
				return true;
			}
		}
		return false;
	}

	private void refreshConversations(object? sender, EventArgs e)
	{
		StreamWriter? pipeWriter = PipeConnectionManager.PipeWriter;
		if (pipeWriter != null)
		{
			pipeWriter.WriteLine("REFRESH");
			pipeWriter.Flush();
			currentPerson = null;
			peopleList.ClearSelected();
			ShowConversationPlaceholder();
			fileWatcher?.Dispose();
		}
		else
		{
			MessageBox.Show("Pipe connection is not established.");
		}
	}

	private void SetupFileWatcher(string currentUser, string recipient)
	{
		fileWatcher?.Dispose();
		pendingConversationChunk = string.Empty;
		string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUser, recipient));
		fileWatcher = new FileSystemWatcher
		{
			Path = conversationFolder,
			Filter = Path.GetFileName(conversationFile),
			NotifyFilter = (NotifyFilters.Size | NotifyFilters.LastWrite)
		};
		fileWatcher.Changed += delegate
		{
			LoadNewMessages(conversationFile);
		};
		fileWatcher.EnableRaisingEvents = true;
	}

	private void LoadNewMessages(string conversationFile)
	{
		try
		{
			if (skipNextConversationWatcherChange)
			{
				skipNextConversationWatcherChange = false;
				return;
			}

			long newFileSize = new FileInfo(conversationFile).Length;
			lastFileSize = newFileSize;

			if (IsDisposed || !IsHandleCreated)
			{
				return;
			}

			BeginInvoke((MethodInvoker)delegate
			{
				if (IsDisposed)
				{
					return;
				}

				if (!string.IsNullOrWhiteSpace(currentPerson))
				{
					RenderActiveConversation();
					MarkConversationAsRead(currentPerson);
					if (activeConversationState != null)
					{
						SendPendingReadReceipts(currentPerson, activeConversationState);
					}
					UpdatePeopleList();
				}
			});
		}
		catch (IOException ex)
		{
			Console.WriteLine("Error reading new messages: " + ex.Message);
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private void LoadConversation(string currentUser, string recipient)
	{
		EnsureContactState(recipient);
		InitializeConversationUiForContact(recipient);
		UpdateConversationHeader(recipient);
		string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUser, recipient));
		if (!File.Exists(conversationFile))
		{
			File.Create(conversationFile).Close();
		}

		ConversationState state = BuildConversationState(conversationFile);
		RenderConversationState(state);
		lastFileSize = new FileInfo(conversationFile).Length;
		MarkConversationAsRead(recipient);
		SendPendingReadReceipts(recipient, state);
		UpdatePeopleList();
		chatbox.SelectionStart = chatbox.Text.Length;
		chatbox.ScrollToCaret();
	}

	private void PersonSelected(object? sender, EventArgs e)
	{
		if (suppressPeopleSelectionChange)
		{
			return;
		}

		if (peopleList.SelectedItem != null)
		{
			StopLocalTypingIndicator();
			string selectedPerson = peopleList.SelectedItem.ToString() ?? string.Empty;
			currentPerson = selectedPerson;
			LoadConversation(currentUserName, selectedPerson);
			SetupFileWatcher(currentUserName, selectedPerson);
		}
		else
		{
			StopLocalTypingIndicator();
			currentPerson = null;
			ShowConversationPlaceholder();
		}
	}

	private void FilterPeople(object? sender, EventArgs e)
	{
		UpdatePeopleList();
	}
    }
}
