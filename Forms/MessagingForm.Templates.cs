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
	private void ShowTemplatePanel()
	{
		if (templatePanel == null)
		{
			templatePanel = new Panel
			{
				Size = new Size(450, 300),
				BackColor = Color.WhiteSmoke,
				BorderStyle = BorderStyle.FixedSingle,
				Visible = false,
				AutoScroll = true
			};
			base.Controls.Add(templatePanel);
		}
		LoadTemplateButtons();
		templatePanel.Location = new Point((base.ClientSize.Width - templatePanel.Width) / 2, (base.ClientSize.Height - templatePanel.Height) / 2);
		templatePanel.BringToFront();
		templatePanel.Visible = true;
	}

	private void LoadTemplateButtons()
	{
		if (!Directory.Exists(templatesFolder))
		{
			Directory.CreateDirectory(templatesFolder);
		}
		string[] files = Directory.GetFiles(templatesFolder, "*.txt");
		templatePanel.Controls.Clear();
		Label titleLabel = new Label
		{
			Text = "Select a Template",
			Location = new Point(10, 10),
			Font = new Font("Segoe UI", 12f, FontStyle.Bold),
			Size = new Size(300, 20)
		};
		templatePanel.Controls.Add(titleLabel);
		int yOffset = 40;
		string[] array = files;
		foreach (string file in array)
		{
			string templateName = Path.GetFileNameWithoutExtension(file);
			int red = random.Next(128, 256);
			int green = random.Next(128, 256);
			int blue = random.Next(128, 256);
			Color randomColor = Color.FromArgb(red, green, blue);
			Button templateButton = new Button
			{
				Text = templateName,
				Font = new Font(fontFamily, 12f, FontStyle.Bold),
				Location = new Point(10, yOffset),
				Size = new Size(410, 30),
				BackColor = randomColor,
				Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right),
				Tag = file
			};
			templateButton.Click += delegate
			{
				UseTemplate(file);
			};
			templatePanel.Controls.Add(templateButton);
			yOffset += 40;
		}
		Button closeButton = new Button
		{
			Text = "Close",
			Font = new Font(fontFamily, 12f, FontStyle.Bold),
			Location = new Point(340, 1),
			Size = new Size(80, 40),
			BackColor = Color.LightCoral,
			Anchor = (AnchorStyles.Top | AnchorStyles.Right)
		};
		closeButton.Click += delegate
		{
			templatePanel.Visible = false;
		};
		templatePanel.Controls.Add(closeButton);
	}

	private void OpenTemplate(string filePath)
	{
		try
		{
			MessageBox.Show(File.ReadAllText(filePath), Path.GetFileNameWithoutExtension(filePath), MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error opening template: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void UseTemplate(string filePath)
	{
		try
		{
			string[] content = File.ReadAllText(filePath).Split(new string[1] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
			if (currentPerson != null && content.Length != 0)
			{
				string[] array = content;
				foreach (string line in array)
				{
					TextBox textBox = messageInput;
					textBox.Text = textBox.Text + line + Environment.NewLine;
				}
			}
			else
			{
				MessageBox.Show("Please select a person to use template!.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
			templatePanel.Visible = false;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error loading template: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void ShowAddTemplatePanel()
	{
		Label textInfo;
		if (addTemplatePanel == null)
		{
			addTemplatePanel = new Panel
			{
				Size = new Size(760, 570),
				BackColor = Color.WhiteSmoke,
				BorderStyle = BorderStyle.FixedSingle,
				Visible = false,
				Anchor = (AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right)
			};
			Label titleLabel = new Label
			{
				Text = "Create a New Template",
				Location = new Point(10, 10),
				AutoSize = true,
				Font = new Font("Segoe UI", 16f, FontStyle.Bold),
				ForeColor = Color.DarkSlateGray
			};
			addTemplatePanel.Controls.Add(titleLabel);
			Label nameLabel = new Label
			{
				Text = "Template Name:",
				Location = new Point(10, 60),
				AutoSize = true,
				Font = new Font("Segoe UI", 12f, FontStyle.Regular),
				ForeColor = Color.Black
			};
			addTemplatePanel.Controls.Add(nameLabel);
			templateNameInput = new TextBox
			{
				Location = new Point(150, 55),
				Size = new Size(580, 30),
				Font = new Font("Segoe UI", 12f, FontStyle.Regular),
				BorderStyle = BorderStyle.FixedSingle
			};
			addTemplatePanel.Controls.Add(templateNameInput);
			Label contentLabel = new Label
			{
				Text = "Template Content:",
				Location = new Point(10, 110),
				AutoSize = true,
				Font = new Font("Segoe UI", 12f, FontStyle.Regular),
				ForeColor = Color.Black
			};
			addTemplatePanel.Controls.Add(contentLabel);
			textInfo = new Label
			{
				Location = new Point(10, 495),
				Size = new Size(540, 20),
				Font = new Font(fontFamily, 10f),
				ForeColor = Color.Gray,
				Text = "Word Count: 0 | Characters: 0 | Line: 1, Column: 1"
			};
			addTemplatePanel.Controls.Add(textInfo);
			textInfo.Show();
			textInfo.BringToFront();
			templateContentInput = new RichTextBox
			{
				Location = new Point(10, 140),
				Size = new Size(730, 350),
				Font = new Font("Segoe UI", 12f, FontStyle.Regular),
				BorderStyle = BorderStyle.FixedSingle,
				Anchor = (AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right)
			};
			addTemplatePanel.Controls.Add(templateContentInput);
			templateContentInput.TextChanged += delegate
			{
				UpdateTextInfo();
			};
			templateContentInput.KeyUp += delegate
			{
				UpdateTextInfo();
			};
			templateContentInput.MouseClick += delegate
			{
				UpdateTextInfo();
			};
			Button saveButton = new Button
			{
				Text = "Save",
				Location = new Point(200, 520),
				Size = new Size(130, 40),
				Font = new Font(fontFamily, 12f),
				BackColor = Color.LightGreen,
				ForeColor = Color.Black,
				FlatStyle = FlatStyle.Flat,
				Anchor = (AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right)
			};
			saveButton.FlatAppearance.BorderSize = 1;
			saveButton.Click += SaveTemplate;
			addTemplatePanel.Controls.Add(saveButton);
			Button cancelButton = new Button
			{
				Text = "Cancel",
				Location = new Point(400, 520),
				Size = new Size(130, 40),
				Font = new Font(fontFamily, 12f),
				BackColor = Color.LightCoral,
				ForeColor = Color.Black,
				FlatStyle = FlatStyle.Flat,
				Anchor = (AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right)
			};
			cancelButton.FlatAppearance.BorderSize = 1;
			cancelButton.Click += delegate
			{
				addTemplatePanel.Visible = false;
			};
			addTemplatePanel.Controls.Add(cancelButton);
			ToolTip toolTip = new ToolTip();
			toolTip.ShowAlways = true;
			toolTip.SetToolTip(templateNameInput, "Enter a unique name for your template.");
			toolTip.SetToolTip(templateContentInput, "Type or paste the content for your template here.");
			toolTip.SetToolTip(saveButton, "Click to save your new template.");
			toolTip.SetToolTip(cancelButton, "Click to cancel and close this panel.");
			base.Controls.Add(addTemplatePanel);
		}
		addTemplatePanel.Location = new Point((base.ClientSize.Width - addTemplatePanel.Width) / 2, (base.ClientSize.Height - addTemplatePanel.Height) / 2);
		addTemplatePanel.BringToFront();
		addTemplatePanel.Visible = true;
		static int CountWords(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
			{
				return 0;
			}
			return Regex.Matches(text, "\\b\\w+\\b").Count;
		}
		static (int, int) GetCursorPosition(RichTextBox richTextBox)
		{
			int index = richTextBox.SelectionStart;
			int item = richTextBox.GetLineFromCharIndex(index) + 1;
			int col = index - richTextBox.GetFirstCharIndexOfCurrentLine() + 1;
			return (item, col);
		}
		void UpdateTextInfo()
		{
			int wordCount = CountWords(templateContentInput.Text);
			int charCount = templateContentInput.Text.Length;
			var (line, col) = GetCursorPosition(templateContentInput);
			textInfo.Text = $"Word Count: {wordCount} | Characters: {charCount} | Line: {line}, Column: {col}";
		}
	}

	private void SaveTemplate(object? sender, EventArgs e)
	{
		if (templateNameInput == null || templateContentInput == null)
		{
			MessageBox.Show("Template input fields are not initialized.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			return;
		}
		string? templateName = templateNameInput.Text?.Trim();
		if (string.IsNullOrEmpty(templateName))
		{
			MessageBox.Show("Template name cannot be empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			return;
		}
		string templateContent = templateContentInput.Text;
		try
		{
			if (!Directory.Exists(templatesFolder))
			{
				Directory.CreateDirectory(templatesFolder);
			}
			File.WriteAllText(Path.Combine(templatesFolder, templateName + ".txt"), templateContent);
			MessageBox.Show("Template '" + templateName + "' saved successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			addTemplatePanel.Visible = false;
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error saving template: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void InsertTemplate(string filePath)
	{
		try
		{
			string templateContent = File.ReadAllText(filePath);
			messageInput.Text = templateContent;
			MessageBox.Show("Template '" + Path.GetFileNameWithoutExtension(filePath) + "' loaded successfully.", "Template Loaded", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
		}
		catch (Exception ex)
		{
			MessageBox.Show("Error loading template: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
		}
	}

	private void checkFolder(string folder)
	{
		if (!Directory.Exists(folder))
		{
			Directory.CreateDirectory(folder);
		}
	}

    }
}
