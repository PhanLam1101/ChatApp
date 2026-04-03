using System;
using System.IO;
using System.IO.Pipes;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace MessagingApp
{
    public static class PipeConnectionManager
    {
        private const int PipeConnectTimeoutMs = 15000;
        public static NamedPipeClientStream? PipeClient { get; private set; }
        public static StreamWriter? PipeWriter { get; private set; }

        public static NamedPipeClientStream? NotificationPipe { get; private set; }
        public static StreamReader? NotificationPipeReader { get; private set; }
        private static Thread? notificationListenerThread;
        private static readonly object notificationEventLock = new object();
        private static readonly Queue<string> pendingNotifications = new Queue<string>();
        private static Action<string>? notificationReceivedHandlers;
        public static event Action<string>? OnNotificationReceived
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                List<string> backlog = new List<string>();
                lock (notificationEventLock)
                {
                    notificationReceivedHandlers += value;
                    while (pendingNotifications.Count > 0)
                    {
                        backlog.Add(pendingNotifications.Dequeue());
                    }
                }

                foreach (string notification in backlog)
                {
                    value(notification);
                }
            }
            remove
            {
                lock (notificationEventLock)
                {
                    notificationReceivedHandlers -= value;
                }
            }
        }
        public static string session = string.Empty;

        public static bool InitializePipe()
        {
            string pipeConnectionFile = AppPaths.PipeConnectionFile;
            try
            {
                Thread.Sleep(5000);
                int retryCount = 5;
                while (!File.Exists(pipeConnectionFile) && retryCount > 0)
                {
                    Thread.Sleep(2000);
                    retryCount--;
                }

                if (!File.Exists(pipeConnectionFile))
                {
                    MessageBox.Show("Failed to find the pipeConnection.bin file.");
                    return false;
                }

                string pipeName = File.ReadAllText(pipeConnectionFile).Trim();
                PipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                PipeClient.Connect(PipeConnectTimeoutMs);
                PipeWriter = new StreamWriter(PipeClient) { AutoFlush = true };
                TryDeleteFile(pipeConnectionFile);
                session = pipeName;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect to the C++ program: {ex.Message}");
                TryDeleteFile(pipeConnectionFile);
                return false;
            }
        }

        public static bool InitializeNotificationPipe()
        {
            string notificationPipeFile = AppPaths.NotificationPipeFile;
            try
            {
                Thread.Sleep(2000);
                int retryCount = 5;
                while (!File.Exists(notificationPipeFile) && retryCount > 0)
                {
                    Thread.Sleep(500);
                    retryCount--;
                }

                if (!File.Exists(notificationPipeFile))
                {
                    MessageBox.Show("Failed to find the notificationPipe.bin file.");
                    return false;
                }

                string notificationPipeName = File.ReadAllText(notificationPipeFile).Trim();

                NotificationPipe = new NamedPipeClientStream(".", notificationPipeName, PipeDirection.In);
                NotificationPipe.Connect(PipeConnectTimeoutMs);

                NotificationPipeReader = new StreamReader(NotificationPipe);

                TryDeleteFile(notificationPipeFile);

                StartListeningForNotifications();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to connect NotificationPipe: {ex.Message}");
                TryDeleteFile(notificationPipeFile);
                return false;
            }
        }

        private static void StartListeningForNotifications()
        {
            if (NotificationPipe == null || NotificationPipeReader == null)
            {
                return;
            }

            notificationListenerThread = new Thread(() =>
            {
                try
                {
                    while (NotificationPipe != null && NotificationPipeReader != null && NotificationPipe.IsConnected)
                    {
                        string? message = NotificationPipeReader.ReadLine();
                        if (message == null)
                        {
                        }

                        if (!string.IsNullOrWhiteSpace(message))
                        {
                            PublishNotification(message);
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
            PipeWriter = null;
            PipeClient = null;

            NotificationPipeReader?.Dispose();
            NotificationPipe?.Dispose();
            NotificationPipeReader = null;
            NotificationPipe = null;

            lock (notificationEventLock)
            {
                pendingNotifications.Clear();
            }
        }

        private static void PublishNotification(string message)
        {
            Action<string>? handlers;
            lock (notificationEventLock)
            {
                handlers = notificationReceivedHandlers;
                if (handlers == null)
                {
                    pendingNotifications.Enqueue(message);
                    return;
                }
            }

            handlers.Invoke(message);
        }

        private static void TryDeleteFile(string path)
        {
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
