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
	private void IncreaseFontSize(object? sender, EventArgs e)
	{
		int currentIndex = Array.IndexOf(fontSizes, fontSize);
		if (currentIndex == -1)
		{
			MessageBox.Show("Current font size is invalid!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		else if (currentIndex < fontSizes.Length - 1)
		{
			float newFontSize = fontSizes[currentIndex + 1];
			ChangeFontSize(newFontSize);
		}
		else
		{
			MessageBox.Show("Font size is already at the maximum!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void DecreaseFontSize(object? sender, EventArgs e)
	{
		int currentIndex = Array.IndexOf(fontSizes, fontSize);
		if (currentIndex == -1)
		{
			MessageBox.Show("Current font size is invalid!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
		else if (currentIndex > 0)
		{
			float newFontSize = fontSizes[currentIndex - 1];
			ChangeFontSize(newFontSize);
		}
		else
		{
			MessageBox.Show("Font size is already at the minimum!", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
	}

	private void MessageForm_LoadTheme(object? sender, EventArgs e)
	{
		themeSetting = AppThemeManager.LoadSavedThemeSelection();
		ChangeTheme(themeSetting);
	}

	private void MessageForm_FormClosing(object? sender, FormClosingEventArgs e)
	{
		StopLocalTypingIndicator();
		typingIndicatorTimer?.Stop();
		typingStopTimer?.Stop();
		PipeConnectionManager.OnNotificationReceived -= OnNotificationReceived;
		PipeConnectionManager.ClosePipe();
	}

	private void ToggleNotificationPreferencesPanel()
	{
		if (notificationPreferencesPanel == null)
		{
			return;
		}

		notificationPreferencesPanel.Visible = !notificationPreferencesPanel.Visible;
		if (notificationPreferencesPanel.Visible)
		{
			notificationPreferencesPanel.BringToFront();
		}
	}

	private void SaveNotificationPreferences()
	{
		bool soundEnabled = enableSoundCheckBox?.Checked ?? true;
		bool popupEnabled = enablePopupCheckBox?.Checked ?? true;
		bool staticPanelEnabled = enableStaticNotificationPanel?.Checked ?? false;
		File.WriteAllText(Path.Combine(settingFolder, "NotificationPreferences.txt"), $"{soundEnabled},{popupEnabled},{staticPanelEnabled}");
	}

	private void LoadNotificationPreferences()
	{
		string filePath = Path.Combine(settingFolder, "NotificationPreferences.txt");
		if (File.Exists(filePath))
		{
			string[] array = File.ReadAllText(filePath).Split(',');
			if (enableSoundCheckBox != null && enablePopupCheckBox != null && enableStaticNotificationPanel != null && array.Length >= 3)
			{
				enableSoundCheckBox.Checked = bool.Parse(array[0]);
				enablePopupCheckBox.Checked = bool.Parse(array[1]);
				enableStaticNotificationPanel.Checked = bool.Parse(array[2].Trim());
			}
		}
		else
		{
			File.WriteAllText(filePath, "True,True,False");
			if (enableSoundCheckBox != null)
			{
				enableSoundCheckBox.Checked = true;
			}
			if (enablePopupCheckBox != null)
			{
				enablePopupCheckBox.Checked = true;
			}
			if (enableStaticNotificationPanel != null)
			{
				enableStaticNotificationPanel.Checked = false;
			}
		}
	}

	private bool ArePopupNotificationsEnabled()
	{
		return enablePopupCheckBox?.Checked ?? true;
	}

	private bool AreSoundNotificationsEnabled()
	{
		return enableSoundCheckBox?.Checked ?? true;
	}

	private bool IsNotificationPanelStatic()
	{
		return enableStaticNotificationPanel?.Checked ?? false;
	}

	private void OpenDownloadsFolder()
	{
		try
		{
			string downloadsPath = AppPaths.DownloadsDirectory;
			if (!Directory.Exists(downloadsPath))
			{
				Directory.CreateDirectory(downloadsPath);
			}
			Process.Start(new ProcessStartInfo
			{
				FileName = downloadsPath,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to open Downloads folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void ExportConversation()
	{
		if (currentPerson == null)
		{
			MessageBox.Show("Please select a contact to export the conversation.", "No Conversation", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		SaveFileDialog saveFileDialog = new SaveFileDialog
		{
			Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
			Title = "Export Conversation",
			FileName = currentPerson + "_Conversation.txt"
		};
		if (saveFileDialog.ShowDialog() != DialogResult.OK)
		{
			return;
		}
		try
		{
			string conversationFile = Path.Combine(conversationFolder, GetConversationFileName(currentPerson, currentUserName));
			if (!File.Exists(conversationFile))
			{
				MessageBox.Show("No conversation found for the selected contact.", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			File.WriteAllText(saveFileDialog.FileName, chatbox.Text);
			MessageBox.Show("Conversation exported successfully!", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to export conversation: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void ShowAppInfo()
	{
		MessageBox.Show("ChatApp Prototype Version\nDeveloped by: Phan Duy Lam\nText chats are end-to-end encrypted in this build.\nChatBot conversations are processed on the server and are not end-to-end encrypted.", "About ChatApp", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
	}

	private bool IsChatBotContact(string? contactName)
	{
		return string.Equals(contactName, ChatBotContactName, StringComparison.OrdinalIgnoreCase);
	}

	private void LoadChatBotModelSettings()
	{
		EnsureChatBotModelsFile();
		chatBotModels = File.ReadAllLines(AppPaths.ChatBotModelsFile)
			.Select(ParseChatBotModelOption)
			.Where(static option => option != null)
			.Cast<ChatBotModelOption>()
			.GroupBy(static option => option.ModelName, StringComparer.OrdinalIgnoreCase)
			.Select(static group => group.First())
			.ToList();

		if (chatBotModels.Count == 0)
		{
			chatBotModels.Add(new ChatBotModelOption
			{
				DisplayName = "Gemma 3 1B",
				ModelName = "gemma3:1b"
			});
		}

		if (File.Exists(AppPaths.ChatBotModelSelectionFile))
		{
			string selectedModel = File.ReadAllText(AppPaths.ChatBotModelSelectionFile).Trim();
			if (!string.IsNullOrWhiteSpace(selectedModel))
			{
				selectedChatBotModel = selectedModel;
			}
		}

		if (!chatBotModels.Any(option => string.Equals(option.ModelName, selectedChatBotModel, StringComparison.OrdinalIgnoreCase)))
		{
			selectedChatBotModel = chatBotModels[0].ModelName;
			SaveSelectedChatBotModel();
		}
	}

	private void EnsureChatBotModelsFile()
	{
		string filePath = AppPaths.ChatBotModelsFile;
		string? directory = Path.GetDirectoryName(filePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		if (!File.Exists(filePath))
		{
			File.WriteAllLines(filePath, new[]
			{
				"# One model per line in the format: Display Name|ollama-model-name",
				"# After editing this file, open Settings > ChatBot Model > Reload Model List.",
				"Gemma 3 270M|gemma3:270m",
				"Gemma 3 1B|gemma3:1b"
			});
		}
	}

	private ChatBotModelOption? ParseChatBotModelOption(string line)
	{
		string trimmed = line.Trim();
		if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
		{
			return null;
		}

		string[] parts = trimmed.Split('|', 2);
		string modelName = (parts.Length == 2 ? parts[1] : parts[0]).Trim();
		if (string.IsNullOrWhiteSpace(modelName))
		{
			return null;
		}

		string displayName = (parts.Length == 2 ? parts[0] : parts[0]).Trim();
		if (string.IsNullOrWhiteSpace(displayName))
		{
			displayName = modelName;
		}

		return new ChatBotModelOption
		{
			DisplayName = displayName,
			ModelName = modelName
		};
	}

	private void PopulateChatBotModelMenu()
	{
		if (chatBotModelMenu == null)
		{
			return;
		}

		chatBotModelMenu.DropDownItems.Clear();
		foreach (ChatBotModelOption option in chatBotModels)
		{
			ToolStripMenuItem item = new ToolStripMenuItem(option.DisplayName)
			{
				Checked = string.Equals(option.ModelName, selectedChatBotModel, StringComparison.OrdinalIgnoreCase)
			};
			item.Click += (_, _) => SelectChatBotModel(option.ModelName);
			chatBotModelMenu.DropDownItems.Add(item);
		}

		chatBotModelMenu.DropDownItems.Add(new ToolStripSeparator());

		ToolStripMenuItem reloadModelsItem = new ToolStripMenuItem("Reload Model List");
		reloadModelsItem.Click += (_, _) => ReloadChatBotModels();
		chatBotModelMenu.DropDownItems.Add(reloadModelsItem);

		ToolStripMenuItem editModelsItem = new ToolStripMenuItem("Open Models File");
		editModelsItem.Click += (_, _) => OpenChatBotModelsFile();
		chatBotModelMenu.DropDownItems.Add(editModelsItem);
	}

	private void ReloadChatBotModels()
	{
		LoadChatBotModelSettings();
		PopulateChatBotModelMenu();
		UpdateConversationHeader(currentPerson);
		MessageBox.Show("ChatBot models have been reloaded from ChatBotModels.txt.", "ChatBot Models", MessageBoxButtons.OK, MessageBoxIcon.Information);
	}

	private void OpenChatBotModelsFile()
	{
		EnsureChatBotModelsFile();
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = AppPaths.ChatBotModelsFile,
				UseShellExecute = true
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to open ChatBotModels.txt: " + ex.Message, "ChatBot Models", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}
	}

	private void SelectChatBotModel(string modelName)
	{
		if (string.IsNullOrWhiteSpace(modelName))
		{
			return;
		}

		selectedChatBotModel = modelName.Trim();
		SaveSelectedChatBotModel();
		PopulateChatBotModelMenu();
		UpdateConversationHeader(currentPerson);
	}

	private void SaveSelectedChatBotModel()
	{
		File.WriteAllText(AppPaths.ChatBotModelSelectionFile, selectedChatBotModel);
	}

	private string GetSelectedChatBotModelDisplayName()
	{
		ChatBotModelOption? option = chatBotModels.FirstOrDefault(option => string.Equals(option.ModelName, selectedChatBotModel, StringComparison.OrdinalIgnoreCase));
		return option?.DisplayName ?? selectedChatBotModel;
	}

	private void ChangeFont(string fontName)
	{
		try
		{
			Font labelFont = new Font(fontName, 12f);
			Font chatFont = new Font(fontName, fontSize);
			chatbox.Font = chatFont;
			messageInput.Font = chatFont;
			sendMessageButton.Font = labelFont;
			sendFileButton.Font = labelFont;
			refreshButton.Font = labelFont;
			filterTextBox.Font = labelFont;
			addContactLabel.Font = labelFont;
			addContactInput.Font = labelFont;
			addContactButton.Font = labelFont;
			removeContactButton.Font = labelFont;
			unaddedContactsList.Font = labelFont;
			addSelectedContactButton.Font = labelFont;
			enableSoundCheckBox.Font = labelFont;
			enablePopupCheckBox.Font = labelFont;
			enableStaticNotificationPanel.Font = labelFont;
			peopleList.Font = labelFont;
			unaddedContactsLabel.Font = labelFont;
			sidebarSubtitleLabel.Font = new Font(fontName, 10.5f);
			contactsSummaryLabel.Font = new Font(fontName, 9.5f);
			activeChatSubtitleLabel.Font = new Font(fontName, 10.5f);
			textInfoLabel.Font = new Font(fontName, 9.25f);
			fontFamily = fontName;
			UpdateTextInfo();
			if (!string.IsNullOrWhiteSpace(currentPerson))
			{
				LoadConversation(currentUserName, currentPerson);
			}
			else
			{
				ShowConversationPlaceholder();
			}

			SaveFontPreferences(fontFamily, fontSize);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error applying font: " + ex.Message);
		}
	}

	private void SaveFontPreferences(string newFontName, float newSize)
	{
		using StreamWriter writer = new StreamWriter(Path.Combine(settingFolder, "FontSettings.txt"));
		writer.WriteLine($"{newFontName},{newSize}");
	}

	private void LoadFontPreferences()
	{
		string filePath = Path.Combine(settingFolder, "FontSettings.txt");
		if (File.Exists(filePath))
		{
			string[] settings = File.ReadAllText(filePath).Split(',');
			if (settings.Length == 2)
			{
				fontFamily = settings[0];
				if (float.TryParse(settings[1], out var size))
				{
					fontSize = size;
				}
			}
		}
		else
		{
			File.Create(filePath).Dispose();
			using (StreamWriter writer = new StreamWriter(filePath))
			{
				writer.WriteLine($"{fontFamily},{fontSize}");
			}
		}
		if (chatbox != null)
		{
			chatbox.Font = new Font(fontFamily, fontSize);
		}
	}

	private void ChooseBackgroundImage()
	{
		using OpenFileDialog openFileDialog = new OpenFileDialog();
		openFileDialog.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp";
		if (openFileDialog.ShowDialog() == DialogResult.OK)
		{
			BackgroundImage = Image.FromFile(openFileDialog.FileName);
			BackgroundImageLayout = ImageLayout.Stretch;
		}
	}

	private void ResetBackgroundImage()
	{
		BackgroundImage = null;
		BackColor = Color.WhiteSmoke;
	}

	private void ShowChangePasswordForm()
	{
		new ChangePasswordForm().ShowDialog(this);
	}

    }
}
