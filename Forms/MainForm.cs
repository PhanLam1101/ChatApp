using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MessagingApp
{
    public partial class MainForm : Form
    {
        private string currentUserName = "";
        private readonly string iconFolder = AppPaths.IconsDirectory;

        public MainForm()
        {
            InitializeComponent();
        }

        private void ReplaceSpacesWithUnderscores(TextBox textBox)
        {
            int cursorPosition = textBox.SelectionStart;

            var newText = new System.Text.StringBuilder();
            foreach (char ch in textBox.Text)
            {
                if (IsInvalidCharacter(ch))
                {
                    newText.Append('_');
                }
                else
                {
                    newText.Append(ch);
                }
            }

            textBox.Text = newText.ToString();
            textBox.SelectionStart = Math.Min(cursorPosition, textBox.Text.Length);
        }

        private bool IsInvalidCharacter(char ch)
        {
            bool isPrintableUtf7 = (ch >= 48 && ch <= 57) || (ch >= 65 && ch <= 90) || (ch >= 97 && ch <= 122);
            return !isPrintableUtf7 || char.IsControl(ch);
        }

        private void InitializeComponent()
        {
            AppThemePalette themePalette = AppThemeManager.ResolveTheme(AppThemeManager.LoadSavedThemeSelection());
            Color cardColor = themePalette.SurfaceColor;
            Color inputColor = themePalette.ComposerColor;
            Color textColor = themePalette.TextColor;
            Color mutedTextColor = themePalette.MutedTextColor;
            Color accentColor = themePalette.AccentColor;
            Color accentSoftColor = themePalette.AccentSoftColor;

            this.Text = "Sign In / Register";
            this.ClientSize = new System.Drawing.Size(450, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = themePalette.BackgroundColor;
            this.BackgroundImage = null;
            this.MaximizeBox = false;
            string iconPath = AppPaths.AppIconFile;
            if (File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }

            Label headerLabel = new Label()
            {
                Text = "Welcome to ChatApp",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = textColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Top,
                BackColor = themePalette.BackgroundColor,
                Height = 50,
            };
            this.Controls.Add(headerLabel);

            Panel signInPanel = new Panel()
            {
                Location = new Point(50, 70),
                Size = new Size(350, 180),
                BackColor = cardColor,
                BorderStyle = BorderStyle.FixedSingle,
            };
            this.Controls.Add(signInPanel);

            Label idLabel = new Label()
            {
                Text = "ID:",
                Location = new Point(6, 20),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = textColor,
                BackColor = cardColor
            };
            signInPanel.Controls.Add(idLabel);

            TextBox idInput = new TextBox()
            {
                Location = new Point(130, 20),
                Width = 200,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "Enter your ID",
                BackColor = inputColor,
                ForeColor = textColor
            };
            idInput.TextChanged += (sender, e) => ReplaceSpacesWithUnderscores(idInput);
            signInPanel.Controls.Add(idInput);

            Label passwordLabel = new Label()
            {
                Text = "Password:",
                Location = new Point(6, 60),
                ForeColor = textColor,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
                ,
                BackColor = cardColor
            };
            signInPanel.Controls.Add(passwordLabel);

            TextBox passwordInput = new TextBox()
            {
                Location = new Point(130, 60),
                Width = 200,
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "Enter your password",
                BackColor = inputColor,
                ForeColor = textColor
            };
            passwordInput.TextChanged += (sender, e) => ReplaceSpacesWithUnderscores(passwordInput);
            signInPanel.Controls.Add(passwordInput);

            Button signInButton = new Button()
            {
                Text = "Sign In",
                Location = new Point(100, 110),
                Size = new Size(150, 40),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            signInButton.FlatAppearance.BorderColor = themePalette.BorderColor;
            signInButton.Click += (sender, e) => SignIn(idInput.Text, passwordInput.Text);
            signInPanel.Controls.Add(signInButton);

            Panel registerPanel = new Panel()
            {
                Location = new Point(50, 270),
                Size = new Size(350, 200),
                BackColor = cardColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(registerPanel);

            Label newIdLabel = new Label()
            {
                Text = "New ID:",
                AutoSize = true,
                Location = new Point(6, 20),
                ForeColor = textColor,
                BackColor = cardColor,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            registerPanel.Controls.Add(newIdLabel);

            TextBox newIdInput = new TextBox()
            {
                Location = new Point(130, 20),
                Width = 200,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "New ID",
                BackColor = inputColor,
                ForeColor = textColor
            };
            newIdInput.TextChanged += (sender, e) => ReplaceSpacesWithUnderscores(newIdInput);
            registerPanel.Controls.Add(newIdInput);

            Label newPasswordLabel = new Label()
            {
                Text = "New Password:",
                Location = new Point(6, 60),
                AutoSize = true,
                ForeColor = textColor,
                BackColor = cardColor,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
            };
            registerPanel.Controls.Add(newPasswordLabel);

            TextBox newPasswordInput = new TextBox()
            {
                Location = new Point(130, 60),
                Width = 200,
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "New password",
                BackColor = inputColor,
                ForeColor = textColor
            };
            newPasswordInput.TextChanged += (sender, e) => ReplaceSpacesWithUnderscores(newPasswordInput);
            registerPanel.Controls.Add(newPasswordInput);

            Label confirmPasswordLabel = new Label()
            {
                Text = "Confirm Password:",
                AutoSize = true,
                Location = new Point(6, 100),
                ForeColor = textColor,
                BackColor = cardColor,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            registerPanel.Controls.Add(confirmPasswordLabel);

            TextBox confirmPasswordInput = new TextBox()
            {
                Location = new Point(130, 100),
                Width = 200,
                UseSystemPasswordChar = true,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "Confirm new password",
                BackColor = inputColor,
                ForeColor = textColor
            };
            confirmPasswordInput.TextChanged += (sender, e) => ReplaceSpacesWithUnderscores(confirmPasswordInput);
            registerPanel.Controls.Add(confirmPasswordInput);

            Button registerButton = new Button()
            {
                Text = "Register",
                Location = new Point(100, 140),
                Size = new Size(150, 40),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            registerButton.FlatAppearance.BorderColor = themePalette.BorderColor;
            registerButton.Click += (sender, e) => Register(newIdInput.Text, newPasswordInput.Text, confirmPasswordInput.Text);
            registerPanel.Controls.Add(registerButton);

            Button hintButton = new Button()
            {
                Location = new Point(270, 140),
                Size = new Size(40, 40),
                BackgroundImage = LoadImage(Path.Combine(iconFolder, "hint.bmp")),
                BackgroundImageLayout = ImageLayout.Stretch,
                BackColor = accentSoftColor,
                FlatStyle = FlatStyle.Flat
            };
            hintButton.FlatAppearance.BorderColor = themePalette.BorderColor;
            hintButton.Click += (sender, e) =>
            {
                MessageBox.Show(
                    "Tips for creating a secure ID and password:\n\n" +
                    "1. **ID Guidelines**:\n" +
                    "   - Use only alphanumeric characters (A-Z, 0-9).\n" +
                    "   - Avoid using spaces or special characters.\n" +
                    "   - Keep it between 6 and 20 characters long.\n\n" +
                    "2. **Password Guidelines**:\n" +
                    "   - Use at least 8 characters.\n" +
                    "   - Include uppercase letters, lowercase letters, numbers, and symbols.\n" +
                    "   - Avoid using obvious words like 'password' or '123456'.\n" +
                    "   - Do not reuse passwords from other accounts.\n" +
                    "   - Regularly update your password.\n\n" +
                    "Example:\n" +
                    "   - ID: User123\n" +
                    "   - Password: StrongPass@123",
                    "Guidelines for ID and Password",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            };
            registerPanel.Controls.Add(hintButton);

            Label IPLabel = new Label()
            {
                Text = "Server IP:",
                AutoSize = true,
                Location = new Point(50, 500),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = themePalette.BackgroundColor,
                ForeColor = textColor
            };
            this.Controls.Add(IPLabel);
            string IPServer = File.ReadAllText(AppPaths.ServerIpFile).Trim();
            TextBox IPInput = new TextBox()
            {
                Location = new Point(150, 500),
                Width = 150,
                Text = IPServer,
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "Enter the server IP",
                BackColor = inputColor,
                ForeColor = textColor
            };
            this.Controls.Add(IPInput);

            Button IPButton = new Button()
            {
                Text = "Change",
                Location = new Point(320, 500),
                Size = new Size(60, 25),
                Font = new Font("Segoe UI", 9),
                BackColor = accentSoftColor,
                ForeColor = textColor,
                FlatStyle = FlatStyle.Flat
            };
            IPButton.FlatAppearance.BorderColor = themePalette.BorderColor;
            IPButton.Click += (sender, e) => IPChange(IPInput.Text);
            this.Controls.Add(IPButton);

            Label footerLabel = new Label()
            {
                Text = "Version: Prototype",
                Font = new Font("Segoe UI", 10, FontStyle.Italic),
                ForeColor = mutedTextColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Bottom,
                BackColor = themePalette.BackgroundColor,
                Height = 40
            };
            this.Controls.Add(footerLabel);
        }

        private void IPChange(string newIP)
        {
            if (string.IsNullOrWhiteSpace(newIP))
            {
                MessageBox.Show("Enter IP of the server", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            File.WriteAllText(AppPaths.ServerIpFile, newIP.Trim());

            MessageBox.Show("IP Address has been changed!");
        }

        private void Register(string id, string password, string confirm_password)
        {
            if (id == "")
            {
                MessageBox.Show("Please enter the new ID", "Remind", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (password == "")
            {
                MessageBox.Show("Please enter the new password", "Remind", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (confirm_password == "")
            {
                MessageBox.Show("Please confirm the new password", "Remind", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (password != confirm_password)
            {
                MessageBox.Show("Please try again! Confirmed password does not match!", "Remind", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            else
            {
                string registerFilePath = AppPaths.RegisterRequestFile;
                string resultFilePath = AppPaths.RegisterResultFile;
                File.WriteAllText(registerFilePath, $"{id}\n{password}");
                Thread.Sleep(500);

                Process? process = StartBridgeProcess();
                if (process == null)
                {
                    DeleteFileIfExists(registerFilePath);
                    return;
                }

                if (TrySendPipeCommand("REGISTER", process))
                {
                    Thread.Sleep(500);
                }
                else
                {
                    DeleteFileIfExists(registerFilePath);
                    return;
                }

                if (File.Exists(resultFilePath))
                {
                    if (!TryReadResultFile(resultFilePath, out string loginResult))
                    {
                        MessageBox.Show("Could not read the registration result from the C++ helper. Please try again.");
                        DeleteFileIfExists(registerFilePath);
                        ShutdownBridgeProcess(process);
                        return;
                    }

                    if (loginResult == "success")
                    {
                        MessageBox.Show("Register successful! \nPlease use this ID and password to sign in");
                        currentUserName = id;
                        DeleteFileIfExists(registerFilePath);
                        DeleteFileIfExists(resultFilePath);
                        ShutdownBridgeProcess(process);
                    }
                    else if (loginResult == "fail")
                    {
                        MessageBox.Show("Register failed. Please try again. \nSomeone used this ID before!");
                        DeleteFileIfExists(resultFilePath);
                        DeleteFileIfExists(registerFilePath);
                        ShutdownBridgeProcess(process);
                    }
                }
            }
        }

        private void SignIn(string id, string password)
        {
            if (id == "")
            {
                MessageBox.Show("Please enter your ID", "Remind", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (password == "")
            {
                MessageBox.Show("Please enter your password", "Remind", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string loginFilePath = AppPaths.LoginRequestFile;
            string resultFilePath = AppPaths.LoginResultFile;
            AppPaths.PrepareUserProfile(id);
            File.WriteAllText(loginFilePath, $"{id}\n{password}");
            Thread.Sleep(500);

            Process? process = StartBridgeProcess();
            if (process == null)
            {
                DeleteFileIfExists(loginFilePath);
                return;
            }

            if (TrySendPipeCommand("LOGIN", process))
            {
                Thread.Sleep(500);
            }
            else
            {
                DeleteFileIfExists(loginFilePath);
                return;
            }

            if (File.Exists(resultFilePath))
            {
                if (!TryReadResultFile(resultFilePath, out string loginResult))
                {
                    MessageBox.Show("Could not read the login result from the C++ helper. Please try again.");
                    DeleteFileIfExists(loginFilePath);
                    ShutdownBridgeProcess(process);
                    return;
                }

                if (loginResult == "success")
                {
                    MessageBox.Show("Login successful!");
                    currentUserName = id;
                    DeleteFileIfExists(loginFilePath);
                    DeleteFileIfExists(resultFilePath);
                    if (!PipeConnectionManager.InitializeNotificationPipe())
                    {
                        MessageBox.Show("Unable to establish a connection with the C++ program.");
                        StopProcess(process);
                        return;
                    }

                    AppPaths.ActivateUserProfile(currentUserName);
                    OpenMessagingForm();
                }
                else if (loginResult == "fail")
                {
                    MessageBox.Show("Login failed. Please try again.");
                    DeleteFileIfExists(resultFilePath);
                    DeleteFileIfExists(loginFilePath);
                    ShutdownBridgeProcess(process);
                }
            }
        }

        private static bool TryReadResultFile(string path, out string result)
        {
            const int maxAttempts = 20;
            result = string.Empty;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (!File.Exists(path))
                {
                    Thread.Sleep(150);
                    continue;
                }

                try
                {
                    result = File.ReadAllText(path).Trim();
                    return true;
                }
                catch (IOException)
                {
                    Thread.Sleep(150);
                }
                catch (UnauthorizedAccessException)
                {
                    Thread.Sleep(150);
                }
            }

            return false;
        }

        private Image? LoadImage(string imagePath)
        {
            return File.Exists(imagePath) ? Image.FromFile(imagePath) : null;
        }

        private Process? StartBridgeProcess()
        {
            string programPath = AppPaths.ResolveBridgeExecutablePath();
            if (string.IsNullOrWhiteSpace(programPath))
            {
                MessageBox.Show(
                    "MessageProgram.exe could not be found. Keep the MessageProgram folder next to UserInterface.exe.",
                    "Missing Helper Program",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return null;
            }

            try
            {
                DeleteFileIfExists(AppPaths.PipeConnectionFile);
                DeleteFileIfExists(AppPaths.NotificationPipeFile);
                AppPaths.SyncSessionServerIp();

                Process process = new Process();
                process.StartInfo.FileName = programPath;
                process.StartInfo.WorkingDirectory = AppPaths.SessionRoot;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.Environment["MESSAGINGAPP_PROFILES_ROOT"] = AppPaths.ProfilesRoot;
                process.Start();
                Thread.Sleep(1000);
                return process;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to start the C++ helper program: {ex.Message}", "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        private bool TrySendPipeCommand(string command, Process process)
        {
            if (!PipeConnectionManager.InitializePipe())
            {
                MessageBox.Show("Unable to establish a connection with the C++ program.");
                StopProcess(process);
                return false;
            }

            StreamWriter? pipeWriter = PipeConnectionManager.PipeWriter;
            if (pipeWriter == null)
            {
                MessageBox.Show("The connection to the C++ program was created, but no pipe writer is available.");
                StopProcess(process);
                return false;
            }

            pipeWriter.WriteLine(command);
            pipeWriter.Flush();
            return true;
        }

        private static void StopProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(1000);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static void ShutdownBridgeProcess(Process? process)
        {
            PipeConnectionManager.ClosePipe();

            if (process == null)
            {
                return;
            }

            try
            {
                if (process.HasExited)
                {
                    return;
                }

                if (process.WaitForExit(1500))
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }

            StopProcess(process);
        }

        private static void DeleteFileIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private void OpenMessagingForm()
        {
            this.Hide();

            MessagingForm messagingForm = new MessagingForm(currentUserName);
            messagingForm.Show();
            messagingForm.FormClosed += MainForm_Close;
        }

        private void MainForm_Close(object? sender, FormClosedEventArgs e)
        {
            this.Close();
            Environment.Exit(0);
        }
    }
}
