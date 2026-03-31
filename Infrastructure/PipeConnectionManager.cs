using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Windows.Forms;

namespace MessagingApp
{
    public static class PipeConnectionManager
    {
        public static NamedPipeClientStream PipeClient { get; private set; }
        public static StreamWriter PipeWriter { get; private set; }

        public static NamedPipeClientStream NotificationPipe { get; private set; }
        public static StreamReader NotificationPipeReader { get; private set; }
        private static Thread notificationListenerThread;
        public static event Action<string> OnNewMessageNotification;
        public static string session;

        public static bool InitializePipe()
        {
            try
            {
                Thread.Sleep(2000);
                int retryCount = 5;
                while (!File.Exists("pipeConnection.bin") && retryCount > 0)
                {
                    Thread.Sleep(500);
                    retryCount--;
                }

                if (!File.Exists("pipeConnection.bin"))
                {
                    MessageBox.Show("Failed to find the pipeConnection.bin file.");
                    return false;
                }

                string pipeName = File.ReadAllText("pipeConnection.bin").Trim();
                PipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                PipeClient.Connect();
                PipeWriter = new StreamWriter(PipeClient) { AutoFlush = true };
                File.Delete("pipeConnection.bin");
                session = pipeName;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to the C++ program: {ex.Message}");
                File.Delete("pipeConnection.bin");
                return false;
            }
        }

        public static bool InitializeNotificationPipe()
        {
            try
            {
                Thread.Sleep(2000);
                int retryCount = 5;
                while (!File.Exists("notificationPipe.bin") && retryCount > 0)
                {
                    Thread.Sleep(500);
                    retryCount--;
                }

                if (!File.Exists("notificationPipe.bin"))
                {
                    MessageBox.Show("Failed to find the notificationPipe.bin file.");
                    return false;
                }

                string notificationPipeName = File.ReadAllText("notificationPipe.bin").Trim();

                NotificationPipe = new NamedPipeClientStream(".", notificationPipeName, PipeDirection.In);
                NotificationPipe.Connect();

                NotificationPipeReader = new StreamReader(NotificationPipe);

                File.Delete("notificationPipe.bin");

                StartListeningForNotifications();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect NotificationPipe: {ex.Message}");
                File.Delete("notificationPipe.bin");
                return false;
            }
        }

        private static void StartListeningForNotifications()
        {
            notificationListenerThread = new Thread(() =>
            {
                try
                {
                    while (NotificationPipe.IsConnected)
                    {
                        string message = NotificationPipeReader.ReadLine();
                        if (message != null && message.StartsWith("NEW_MESSAGE:"))
                        {
                            int firstColonIndex = message.IndexOf(':', "NEW_MESSAGE:".Length);
                            if (firstColonIndex != -1)
                            {
                                string sender = message.Substring("NEW_MESSAGE:".Length, firstColonIndex - "NEW_MESSAGE:".Length).Trim();
                                string content = message.Substring(firstColonIndex + 1).Trim();

                                OnNewMessageNotification?.Invoke($"{sender}:{content}");
                            }
                        }
                    }
                }
                catch (IOException ioEx)
                {
                    MessageBox.Show($"Notification pipe read error: {ioEx.Message}");
                }
            })
            {
                IsBackground = true
            };
            notificationListenerThread.Start();
        }

        public static void ClosePipe()
        {
            try
            {
                if (PipeWriter != null)
                {
                    PipeWriter.WriteLine("SHUTDOWN");
                    PipeWriter.Flush();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while closing pipe: " + ex);
            }

            PipeWriter?.Dispose();
            PipeClient?.Dispose();

            NotificationPipeReader?.Dispose();
            NotificationPipe?.Dispose();
        }
    }
}
