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
	private void InitializeChatSearchPanel()
	{
		chatSearchPanel = new Panel
		{
			Location = new Point(10, 40),
			Size = new Size(300, 200),
			BackColor = Color.LightGray,
			Visible = false,
			BorderStyle = BorderStyle.FixedSingle
		};
		TextBox searchTextBox = new TextBox
		{
			PlaceholderText = "Enter keyword...",
			Location = new Point(10, 10),
			Size = new Size(280, 30),
			Font = new Font(fontFamily, 12f)
		};
		chatSearchPanel.Controls.Add(searchTextBox);
		Button searchButton = new Button
		{
			Text = "Search",
			Location = new Point(10, 50),
			Size = new Size(120, 30),
			BackColor = Color.LightBlue,
			Font = new Font(fontFamily, 10f, FontStyle.Bold)
		};
		chatSearchPanel.Controls.Add(searchButton);
		Button resetButton = new Button
		{
			Text = "Reset",
			Location = new Point(150, 50),
			Size = new Size(120, 30),
			BackColor = Color.LightCoral,
			Font = new Font(fontFamily, 10f, FontStyle.Bold)
		};
		chatSearchPanel.Controls.Add(resetButton);
		ListBox searchResultsList = new ListBox
		{
			Location = new Point(10, 90),
			Size = new Size(280, 100),
			Font = new Font(fontFamily, 10f),
			BackColor = Color.White
		};
		chatSearchPanel.Controls.Add(searchResultsList);
		searchButton.Click += delegate
		{
			PerformChatSearch(searchTextBox.Text, searchResultsList);
		};
		resetButton.Click += delegate
		{
			if (!string.IsNullOrWhiteSpace(currentPerson))
			{
				LoadConversation(currentUserName, currentPerson);
			}
		};
		searchResultsList.MouseDoubleClick += delegate
		{
			if (searchResultsList.SelectedItem != null)
			{
				int selectedIndex = searchResultsList.SelectedIndex;
				if (selectedIndex >= 0 && selectedIndex < searchResults.Count)
				{
					ShowDetailedMessage(searchResults[selectedIndex]);
				}
			}
		};
		base.Controls.Add(chatSearchPanel);
		messageDetailsPanel = new Panel
		{
			Size = new Size(400, 300),
			Location = new Point(200, 100),
			BackColor = Color.WhiteSmoke,
			BorderStyle = BorderStyle.FixedSingle,
			AutoScroll = true,
			Visible = false
		};
		base.Controls.Add(messageDetailsPanel);
		Label titleLabel = new Label
		{
			Text = "Message Details",
			Dock = DockStyle.Top,
			Height = 35,
			TextAlign = ContentAlignment.MiddleLeft,
			Font = new Font(fontFamily, 12f, FontStyle.Regular),
			BackColor = Color.LightGray,
			ForeColor = Color.Black,
			Padding = new Padding(10, 0, 0, 0)
		};
		messageDetailsPanel.Controls.Add(titleLabel);
		detailedMessageBox = new RichTextBox
		{
			Location = new Point(0, 35),
			Size = new Size(400, 200),
			ReadOnly = true,
			BorderStyle = BorderStyle.None,
			Font = new Font(fontFamily, 12f),
			BackColor = Color.White,
			ForeColor = Color.Black,
			Margin = new Padding(10)
		};
		messageDetailsPanel.Controls.Add(detailedMessageBox);
		Button closeButton = new Button
		{
			Text = "Close",
			Dock = DockStyle.Bottom,
			Height = 35,
			BackColor = Color.LightGray,
			ForeColor = Color.Black,
			FlatStyle = FlatStyle.Flat,
			Font = new Font(fontFamily, 10f, FontStyle.Regular)
		};
		closeButton.FlatAppearance.BorderSize = 0;
		closeButton.Click += delegate
		{
			messageDetailsPanel.Visible = false;
		};
		messageDetailsPanel.Controls.Add(closeButton);
	}

	private void RemoveSelectedContact()
	{
		if (peopleList.SelectedItem == null)
		{
			MessageBox.Show("Please select a contact to remove.");
			return;
		}
		string? selectedContact = peopleList.SelectedItem.ToString();
		if (string.IsNullOrWhiteSpace(selectedContact))
		{
			return;
		}

		string friendListFilePath = "Friend_List.txt";
		try
		{
			List<string> currentContacts = File.ReadAllLines(friendListFilePath).ToList();
			if (!currentContacts.Contains(selectedContact))
			{
				MessageBox.Show("Contact '" + selectedContact + "' not found in the friend list.");
				return;
			}
			currentContacts.Remove(selectedContact);
			File.WriteAllLines(friendListFilePath, currentContacts);
			people.Remove(selectedContact);
			UpdatePeopleList();
			MessageBox.Show("Contact '" + selectedContact + "' has been removed.");
			currentPerson = null;
			peopleList.ClearSelected();
			ShowConversationPlaceholder();
			fileWatcher?.Dispose();
		}
		catch (Exception ex)
		{
			MessageBox.Show("An error occurred while removing the contact: " + ex.Message);
		}
	}

	private void FilterTextInput(TextBox textBox)
	{
		string validChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 '(),-./:?!";
		string originalText = textBox.Text;
		string filteredText = string.Concat(originalText.Where((char c) => validChars.Contains(c)));
		if (filteredText != originalText)
		{
			int cursorPosition = textBox.SelectionStart;
			textBox.Text = filteredText;
			textBox.SelectionStart = Math.Min(cursorPosition, textBox.Text.Length);
		}
	}

	private void ShowDetailedMessage(string fullMessage)
	{
		if (!string.IsNullOrEmpty(fullMessage))
		{
			detailedMessageBox.Text = fullMessage;
			messageDetailsPanel.Visible = true;
			messageDetailsPanel.BringToFront();
		}
	}

	private void ToggleChatSearchPanel()
	{
		chatSearchPanel.Visible = !chatSearchPanel.Visible;
		if (chatSearchPanel.Visible)
		{
			chatSearchPanel.BringToFront();
		}
	}

	private void PerformChatSearch(string keyword, ListBox resultsList)
	{
		if (currentPerson == null)
		{
			MessageBox.Show("Please select a person to search.");
			return;
		}
		if (string.IsNullOrWhiteSpace(keyword))
		{
			MessageBox.Show("Please enter a keyword to search!");
			return;
		}
		string conversationFileName = "Conversations\\" + GetConversationFileName(currentPerson, currentUserName);
		searchResults = SearchMessagesInFile(conversationFileName, keyword);
		if (searchResults.Count > 0)
		{
			resultsList.Items.Clear();
			{
				foreach (string result in searchResults)
				{
					resultsList.Items.Add(result);
				}
				return;
			}
		}
		MessageBox.Show("No messages found containing '" + keyword + "'");
	}

	private List<string> SearchMessagesInFile(string conversationFileName, string keyword)
	{
		List<string> matchingMessages = new List<string>();
		if (!File.Exists(conversationFileName))
		{
			MessageBox.Show("Conversation file '" + conversationFileName + "' not found.");
			return matchingMessages;
		}
		try
		{
			string[] array = File.ReadAllText(conversationFileName).Split(new string[1] { delimiter }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string message in array)
			{
				if (message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					string cleanMessage = message.Trim();
					matchingMessages.Add(cleanMessage);
				}
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error while searching messages in '" + conversationFileName + "': " + ex.Message);
		}
		return matchingMessages;
	}

	private void OnNewMessageReceived(string notification)
	{
		if (!string.IsNullOrEmpty(notification))
		{
			int firstColonIndex = notification.IndexOf(':');
			int secondColonIndex = notification.IndexOf(':', firstColonIndex + 1);
			int thirdColonIndex = notification.IndexOf(':', secondColonIndex + 1);
			if (firstColonIndex != -1 && secondColonIndex != -1 && thirdColonIndex != -1)
			{
				string sender = notification.Substring(0, firstColonIndex).Trim();
				string messageBody = notification.Substring(thirdColonIndex + 1).Trim();
				ShowNotification(sender, messageBody);
			}
			else
			{
				ShowNotification("New message", "Unknown content");
			}
		}
	}

	private void CreateNotificationPanel()
	{
		notificationPanel = new Panel
		{
			Size = new Size(300, 70),
			BackColor = Color.FromArgb(50, 50, 50),
			ForeColor = Color.White,
			Visible = false,
			BorderStyle = BorderStyle.FixedSingle,
			Anchor = (AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right)
		};
		notificationSenderLabel = new Label
		{
			AutoSize = true,
			Font = new Font(fontFamily, 10f, FontStyle.Bold),
			Location = new Point(10, 5)
		};
		notificationMessageLabel = new Label
		{
			AutoSize = true,
			Font = new Font(fontFamily, 9f),
			Location = new Point(10, 25)
		};
		notificationPanel.Controls.Add(notificationSenderLabel);
		notificationPanel.Controls.Add(notificationMessageLabel);
		notificationPanel.Location = new Point(base.ClientSize.Width - notificationPanel.Width - 10, base.ClientSize.Height - notificationPanel.Height);
		notificationPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
		base.Controls.Add(notificationPanel);
		notificationTimer = new System.Windows.Forms.Timer();
		notificationTimer.Interval = 6000;
		notificationTimer.Tick += NotificationTimer_Tick;
	}

	private void ShowNotification(string sender, string message)
	{
		string cleanMessage = message.Replace("\0", delimiter);
		if (notificationPanel.InvokeRequired)
		{
			notificationPanel.Invoke(delegate
			{
				if (ArePopupNotificationsEnabled())
				{
					notificationSenderLabel.Text = "from " + sender;
					notificationMessageLabel.Text = cleanMessage;
					notificationPanel.Visible = true;
					notificationPanel.BringToFront();
				}
				if (AreSoundNotificationsEnabled())
				{
					PlayNotificationSound();
				}
				if (!IsNotificationPanelStatic())
				{
					notificationTimer.Start();
				}
			});
		}
		else
		{
			if (ArePopupNotificationsEnabled())
			{
				notificationSenderLabel.Text = "from " + sender;
				notificationMessageLabel.Text = cleanMessage;
				notificationPanel.Visible = true;
				notificationPanel.BringToFront();
			}
			if (AreSoundNotificationsEnabled())
			{
				PlayNotificationSound();
			}
			if (!IsNotificationPanelStatic())
			{
				notificationTimer.Start();
			}
		}
	}

	private void NotificationTimer_Tick(object? sender, EventArgs e)
	{
		notificationPanel.Visible = false;
		notificationTimer.Stop();
	}

	private void PlayNotificationSound()
	{
		try
		{
			new SoundPlayer("SoundEffects\\\\Notification-01.wav").Play();
		}
		catch (Exception ex)
		{
			MessageBox.Show("Failed to play sound: " + ex.Message);
		}
	}

    }
}
