using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MessagingApp
{
    public partial class ChangePasswordForm : Form
    {
        public ChangePasswordForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AppThemePalette themePalette = AppThemeManager.ResolveTheme(AppThemeManager.LoadSavedThemeSelection());

            this.Text = "Change Password";
            this.ClientSize = new System.Drawing.Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = themePalette.BackgroundColor;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app-icon1.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }

            Label headerLabel = new Label()
            {
                Text = "Change Your Password",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = themePalette.TextColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = themePalette.BackgroundColor
            };
            this.Controls.Add(headerLabel);

            Panel inputPanel = new Panel()
            {
                Location = new Point(30, 70),
                Size = new Size(340, 150),
                BackColor = themePalette.SurfaceColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(inputPanel);

            Label currentPasswordLabel = new Label()
            {
                Text = "Current Password:",
                AutoSize = true,
                Location = new Point(10, 20),
                Font = new Font("Segoe UI", 10),
                ForeColor = themePalette.TextColor,
                BackColor = themePalette.SurfaceColor
            };
            inputPanel.Controls.Add(currentPasswordLabel);

            TextBox currentPasswordInput = new TextBox()
            {
                Location = new Point(140, 18),
                Width = 180,
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 10),
                BackColor = themePalette.ComposerColor,
                ForeColor = themePalette.TextColor
            };
            inputPanel.Controls.Add(currentPasswordInput);

            Label newPasswordLabel = new Label()
            {
                Text = "New Password:",
                AutoSize = true,
                Location = new Point(10, 60),
                Font = new Font("Segoe UI", 10),
                ForeColor = themePalette.TextColor,
                BackColor = themePalette.SurfaceColor
            };
            inputPanel.Controls.Add(newPasswordLabel);

            TextBox newPasswordInput = new TextBox()
            {
                Location = new Point(140, 58),
                Width = 180,
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 10),
                BackColor = themePalette.ComposerColor,
                ForeColor = themePalette.TextColor
            };
            inputPanel.Controls.Add(newPasswordInput);

            Label confirmPasswordLabel = new Label()
            {
                Text = "Confirm Password:",
                AutoSize = true,
                Location = new Point(10, 100),
                Font = new Font("Segoe UI", 10),
                ForeColor = themePalette.TextColor,
                BackColor = themePalette.SurfaceColor
            };
            inputPanel.Controls.Add(confirmPasswordLabel);

            TextBox confirmPasswordInput = new TextBox()
            {
                Location = new Point(140, 98),
                Width = 180,
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 10),
                BackColor = themePalette.ComposerColor,
                ForeColor = themePalette.TextColor
            };
            inputPanel.Controls.Add(confirmPasswordInput);

            Button saveButton = new Button()
            {
                Text = "Save",
                Location = new Point(80, 240),
                Size = new Size(100, 35),
                BackColor = themePalette.AccentColor,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            saveButton.FlatAppearance.BorderColor = themePalette.BorderColor;
            saveButton.Click += (sender, e) => SavePassword(currentPasswordInput.Text, newPasswordInput.Text, confirmPasswordInput.Text);
            this.Controls.Add(saveButton);

            Button cancelButton = new Button()
            {
                Text = "Cancel",
                Location = new Point(220, 240),
                Size = new Size(100, 35),
                BackColor = themePalette.AccentSoftColor,
                ForeColor = themePalette.TextColor,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            cancelButton.FlatAppearance.BorderColor = themePalette.BorderColor;
            cancelButton.Click += (sender, e) => this.Close();
            this.Controls.Add(cancelButton);
        }

        private void SavePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                MessageBox.Show("New password and confirmation do not match.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool passwordChanged = ChangeUserPassword(currentPassword, newPassword);

            if (passwordChanged)
            {
                MessageBox.Show("Your password has been changed successfully!!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            else
            {
                MessageBox.Show("Change password failed. Your current password is incorrect!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ChangeUserPassword(string currentPassword, string newPassword)
        {
            string nameFile = "change_password.bin";
            string passwordFile = currentPassword + "\n" + newPassword;

            File.WriteAllText(nameFile, passwordFile);
            if (PipeConnectionManager.PipeWriter != null)
            {
                PipeConnectionManager.PipeWriter.WriteLine("CHANGE_PASSWORD");
                PipeConnectionManager.PipeWriter.Flush();
            }
            else
            {
                File.Delete(nameFile);
                MessageBox.Show("Pipe connection is not established.");
                return false;
            }

            string resultFilePath = "change_password_result.bin";
            Thread.Sleep(500);
            if (File.Exists(resultFilePath))
            {
                string Result = File.ReadAllText(resultFilePath).Trim();
                if (Result == "success")
                {
                    File.Delete(resultFilePath);
                    File.Delete(nameFile);
                    return true;
                }
                else if (Result == "fail")
                {
                    File.Delete(resultFilePath);
                    File.Delete(nameFile);
                    return false;
                }
            }

            return false;
        }
    }
}
