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
		string directoryPath = Path.GetDirectoryName(unaddedContactsFile) ?? AppDomain.CurrentDomain.BaseDirectory;
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
		string[] unaddedContacts = File.ReadAllLines(unaddedContactsFile);
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
		foreach (object item in peopleList.Items)
		{
			if (string.Equals(item?.ToString(), contactName, StringComparison.Ordinal))
			{
				MessageBox.Show("This contact already exists!", "Duplicate Contact", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
		}
		string nameFile = "add_contact.bin";
		File.WriteAllText(nameFile, contactName);
		if (PipeConnectionManager.PipeWriter != null)
		{
			PipeConnectionManager.PipeWriter.WriteLine("ADD_FRIEND");
			PipeConnectionManager.PipeWriter.Flush();
			string resultFilePath = "add_contact_result.bin";
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

	private void ShowConversationPlaceholder()
	{
		chatbox.Clear();
		UpdateConversationHeader(null);
		AppendConversationStatus("Choose a conversation on the left to start messaging.");
	}

	private void UpdateConversationHeader(string? selectedPerson)
	{
		bool hasSelection = !string.IsNullOrWhiteSpace(selectedPerson);
		activeChatTitleLabel.Text = hasSelection ? selectedPerson : "Select a chat";
		activeChatSubtitleLabel.Text = hasSelection
			? $"Secure conversation with {selectedPerson}"
			: "Choose a conversation from the left to start messaging.";
		sendMessageButton.Enabled = hasSelection;
		sendFileButton.Enabled = hasSelection;
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
		Match match = Regex.Match(message, "^(?<sender>.+?) \\[(?<timestamp>[^\\]]+)\\]:\\s?(?<body>[\\s\\S]*)$");
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
		string friendListFilePath = "Friend_List.txt";
		if (!File.Exists(friendListFilePath))
		{
			File.Create(friendListFilePath).Close();
		}
		people = File.ReadAllLines(friendListFilePath).ToList();
		UpdatePeopleList();
		if (string.IsNullOrWhiteSpace(currentPerson))
		{
			ShowConversationPlaceholder();
		}

		friendListWatcher = new FileSystemWatcher
		{
			Path = AppDomain.CurrentDomain.BaseDirectory,
			Filter = Path.GetFileName(friendListFilePath),
			NotifyFilter = (NotifyFilters.Size | NotifyFilters.LastWrite)
		};
		friendListWatcher.Changed += delegate
		{
			Invoke((MethodInvoker)delegate
			{
				people = File.ReadAllLines(friendListFilePath).ToList();
				UpdatePeopleList();
			});
		};
		friendListWatcher.EnableRaisingEvents = true;
	}

	private void UpdatePeopleList()
	{
		string filter = filterTextBox?.Text.Trim() ?? string.Empty;
		string[] visiblePeople = string.IsNullOrWhiteSpace(filter)
			? people.ToArray()
			: people.Where((string person) => person.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

		peopleList.BeginUpdate();
		peopleList.Items.Clear();
		peopleList.Items.AddRange(visiblePeople);
		peopleList.EndUpdate();

		if (!string.IsNullOrWhiteSpace(currentPerson) && visiblePeople.Contains(currentPerson))
		{
			peopleList.SelectedItem = currentPerson;
		}
		else if (!visiblePeople.Contains(currentPerson ?? string.Empty))
		{
			peopleList.ClearSelected();
		}

		int total = people.Count;
		contactsSummaryLabel.Text = total == 0
			? "No contacts yet"
			: visiblePeople.Length == total
				? $"{total} contact{(total == 1 ? string.Empty : "s")}"
				: $"Showing {visiblePeople.Length} of {total}";
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
		string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUserName, currentPerson));
		string timeStamp = DateTime.Now.ToString("HH:mm-dd/MM/yyyy");
		string text = currentUserName + " [" + timeStamp + "]: ";
		string formattedMessage = message + "\n";
		string completedMessage = text + formattedMessage + delimiter;
		string tempFile = "message-now.bin";
		string sentMessage = currentPerson + "\n" + currentUserName + "\n" + completedMessage;
		File.WriteAllText(tempFile, sentMessage);
		File.AppendAllText(conversationFile, completedMessage + "\n");
		Thread.Sleep(1000);
		try
		{
			if (PipeConnectionManager.PipeWriter != null)
			{
				PipeConnectionManager.PipeWriter.WriteLine("TEXT_MESSAGE");
				PipeConnectionManager.PipeWriter.Flush();
			}
			else
			{
				MessageBox.Show("PipeWriter is not connected.");
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error communicating with C++ program: " + ex.Message);
		}
		messageInput.Clear();
		UpdateTextInfo();
		messageInput.Focus();
	}

	private void SendFile(object? sender, EventArgs e)
	{
		if (currentPerson == null)
		{
			MessageBox.Show("Please select a person to send a file.");
		}
		else
		{
			if (fileDialog.ShowDialog() != DialogResult.OK)
			{
				return;
			}
			string tempFile = "send-file-now.bin";
			string filePath = fileDialog.FileName;
			string recipient = currentPerson;
			Path.GetFileName(filePath);
			if (!IsFileNameUtf7Compliant(filePath))
			{
				MessageBox.Show("The file path contains characters that are not supported (such as Ã¼, Ã¶, Ã¤, Ã¡, Ã³, Ãº, ...). Please rename the file to use only standard characters.", "Invalid File Name", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			File.WriteAllText(tempFile, recipient + "\n" + filePath);
			if (PipeConnectionManager.PipeWriter != null)
			{
				PipeConnectionManager.PipeWriter.WriteLine("SEND_FILE");
				PipeConnectionManager.PipeWriter.Flush();
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
		if (PipeConnectionManager.PipeWriter != null)
		{
			PipeConnectionManager.PipeWriter.WriteLine("REFRESH");
			PipeConnectionManager.PipeWriter.Flush();
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
			using (FileStream fs = new FileStream(conversationFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
			{
				fs.Seek(lastFileSize, SeekOrigin.Begin);
				using StreamReader reader = new StreamReader(fs);
				string? newLine;
				while ((newLine = reader.ReadLine()) != null)
				{
					DisplayMessageInChatbox(newLine);
				}
				lastFileSize = fs.Length;
			}
			chatbox.Invoke((MethodInvoker)delegate
			{
				chatbox.SelectionStart = chatbox.Text.Length;
				chatbox.ScrollToCaret();
			});
		}
		catch (IOException ex)
		{
			Console.WriteLine("Error reading new messages: " + ex.Message);
		}
	}

	private void DisplayMessageInChatbox(string message)
	{
		if (chatbox.InvokeRequired)
		{
			chatbox.Invoke(delegate
			{
				DisplayMessageInChatbox(message);
			});
			return;
		}
		try
		{
			if (message.EndsWith(delimiter))
			{
				message = message.Substring(0, message.Length - delimiter.Length);
			}
			if (!TryParseChatMessage(message, out string sender, out string timestamp, out string messageBody))
			{
				AppendConversationStatus(message.Trim());
				return;
			}

			Color senderColor = string.Equals(sender, currentUserName, StringComparison.OrdinalIgnoreCase)
				? currentAccentColor
				: currentTextColor;

			chatbox.SelectionStart = chatbox.TextLength;
			chatbox.SelectionColor = senderColor;
			chatbox.SelectionFont = new Font(fontFamily, fontSize, FontStyle.Bold);
			chatbox.AppendText(sender);
			chatbox.SelectionColor = currentMutedTextColor;
			chatbox.SelectionFont = new Font(fontFamily, Math.Max(fontSize - 1f, 9f), FontStyle.Regular);
			chatbox.AppendText("  " + timestamp + Environment.NewLine);
			chatbox.SelectionColor = currentTextColor;
			chatbox.SelectionFont = new Font(fontFamily, fontSize, FontStyle.Regular);
			chatbox.AppendText(messageBody + Environment.NewLine + Environment.NewLine);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error displaying message: " + ex.Message);
		}
	}

	private void LoadConversation(string currentUser, string recipient)
	{
		chatbox.Clear();
		chatbox.Font = new Font(fontFamily, fontSize);
		UpdateConversationHeader(recipient);
		string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentUser, recipient));
		if (File.Exists(conversationFile))
		{
			try
			{
				string[] messages = File.ReadAllText(conversationFile).Split(new string[1] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
				if (messages.Length != 0)
				{
					string[] array = messages;
					foreach (string message in array)
					{
						DisplayMessageInChatbox(message.Trim());
					}
				}
				else
				{
					AppendConversationStatus("No messages yet. Say hello to start the conversation.");
				}
				lastFileSize = new FileInfo(conversationFile).Length;
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error loading conversation: " + ex.Message);
			}
		}
		else
		{
			File.Create(conversationFile).Close();
			AppendConversationStatus("Starting a new conversation with " + recipient + ".");
			lastFileSize = 0L;
		}
		chatbox.SelectionStart = chatbox.Text.Length;
		chatbox.ScrollToCaret();
	}

	private void PersonSelected(object? sender, EventArgs e)
	{
		if (peopleList.SelectedItem != null)
		{
			string selectedPerson = peopleList.SelectedItem.ToString() ?? string.Empty;
			currentPerson = selectedPerson;
			LoadConversation(currentUserName, selectedPerson);
			SetupFileWatcher(currentUserName, selectedPerson);
		}
		else
		{
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
