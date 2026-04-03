using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MessagingApp
{
    internal static class AppPaths
    {
        private const string AppFolderName = "MessagingApp";
        private static bool initialized;
        private static string sessionId = string.Empty;
        private static string? activeProfileRoot;

        internal static string AppRoot => AppContext.BaseDirectory;
        internal static string SharedDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppFolderName);

        internal static string ProfilesRoot => Path.Combine(SharedDataRoot, "Profiles");
        internal static string SessionsRoot => Path.Combine(SharedDataRoot, "Sessions");
        internal static string SessionRoot => Path.Combine(SessionsRoot, sessionId);
        internal static string DataRoot => SessionRoot;
        internal static string CurrentWorkspaceRoot => activeProfileRoot ?? SessionRoot;

        internal static string ConversationsDirectory => Path.Combine(CurrentWorkspaceRoot, "Conversations");
        internal static string SettingsDirectory => Path.Combine(SharedDataRoot, "Settings");
        internal static string TemplatesDirectory => Path.Combine(CurrentWorkspaceRoot, "Templates");
        internal static string DownloadsDirectory => Path.Combine(CurrentWorkspaceRoot, "Downloads");
        internal static string IconsDirectory => Path.Combine(AppRoot, "Icons");
        internal static string SoundEffectsDirectory => Path.Combine(AppRoot, "SoundEffects");

        internal static string FriendListFile => Path.Combine(CurrentWorkspaceRoot, "Friend_List.txt");
        internal static string UnaddedContactsFile => Path.Combine(CurrentWorkspaceRoot, "UnaddedContact.txt");
        internal static string ServerIpFile => Path.Combine(SharedDataRoot, "IP_Server.txt");
        internal static string RegisterRequestFile => Path.Combine(SessionRoot, "register.bin");
        internal static string RegisterResultFile => Path.Combine(SessionRoot, "register_result.bin");
        internal static string LoginRequestFile => Path.Combine(SessionRoot, "login.bin");
        internal static string LoginResultFile => Path.Combine(SessionRoot, "login_result.bin");
        internal static string ChangePasswordRequestFile => Path.Combine(CurrentWorkspaceRoot, "change_password.bin");
        internal static string ChangePasswordResultFile => Path.Combine(CurrentWorkspaceRoot, "change_password_result.bin");
        internal static string AddContactRequestFile => Path.Combine(CurrentWorkspaceRoot, "add_contact.bin");
        internal static string AddContactResultFile => Path.Combine(CurrentWorkspaceRoot, "add_contact_result.bin");
        internal static string MessageNowFile => Path.Combine(CurrentWorkspaceRoot, "message-now.bin");
        internal static string SendFileNowFile => Path.Combine(CurrentWorkspaceRoot, "send-file-now.bin");
        internal static string ChatBotModelSelectionFile => Path.Combine(CurrentWorkspaceRoot, "SelectedChatBotModel.txt");
        internal static string PipeConnectionFile => Path.Combine(SessionRoot, "pipeConnection.bin");
        internal static string NotificationPipeFile => Path.Combine(SessionRoot, "notificationPipe.bin");
        internal static string ChatBotModelsFile => Path.Combine(SettingsDirectory, "ChatBotModels.txt");
        internal static string NotificationSoundFile => Path.Combine(SoundEffectsDirectory, "Notification-01.wav");
        internal static string AppIconFile => Path.Combine(AppRoot, "app-icon1.ico");

        internal static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            sessionId = $"{Environment.ProcessId}_{Guid.NewGuid():N}";

            Directory.CreateDirectory(SharedDataRoot);
            Directory.CreateDirectory(SettingsDirectory);
            Directory.CreateDirectory(ProfilesRoot);
            Directory.CreateDirectory(SessionsRoot);
            EnsureWorkspaceDirectories(SessionRoot);

            MigrateSharedData();

            EnsureFileExists(ServerIpFile, "127.0.0.1");
            SyncSessionServerIp();

            initialized = true;
        }

        internal static void ActivateUserProfile(string userId)
        {
            if (!initialized)
            {
                Initialize();
            }

            string profileRoot = GetProfileRoot(userId);
            EnsureWorkspaceDirectories(profileRoot);
            MigrateLegacyProfileData(userId, profileRoot);
            activeProfileRoot = profileRoot;
        }

        internal static void PrepareUserProfile(string userId)
        {
            if (!initialized)
            {
                Initialize();
            }

            string profileRoot = GetProfileRoot(userId);
            EnsureWorkspaceDirectories(profileRoot);
            MigrateLegacyProfileData(userId, profileRoot);
        }

        internal static void SyncSessionServerIp()
        {
            string value = File.Exists(ServerIpFile)
                ? File.ReadAllText(ServerIpFile).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                value = "127.0.0.1";
            }

            string bridgeServerIpFile = Path.Combine(SessionRoot, "IP_Server.txt");
            EnsureDirectoryExists(Path.GetDirectoryName(bridgeServerIpFile));
            File.WriteAllText(bridgeServerIpFile, value);
        }

        internal static string ResolveBridgeExecutablePath()
        {
            string[] candidates =
            {
                Path.Combine(AppRoot, "MessageProgram", "MessageProgram", "x64", "Release", "MessageProgram.exe"),
                Path.Combine(AppRoot, "MessageProgram", "MessageProgram", "x64", "Debug", "MessageProgram.exe"),
                Path.Combine(AppRoot, "MessageProgram", "x64", "Release", "MessageProgram.exe"),
                Path.Combine(AppRoot, "MessageProgram", "x64", "Debug", "MessageProgram.exe"),
                Path.Combine(AppRoot, "MessageProgram", "MessageProgram.exe"),
                Path.Combine(AppRoot, "MessageProgram.exe")
            };

            foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Directory.Exists(AppRoot)
                ? Directory.EnumerateFiles(AppRoot, "MessageProgram.exe", SearchOption.AllDirectories).FirstOrDefault() ?? string.Empty
                : string.Empty;
        }

        private static string GetProfileRoot(string userId)
        {
            string safeUserId = SanitizePathSegment(userId);
            if (string.IsNullOrWhiteSpace(safeUserId))
            {
                safeUserId = "default";
            }

            return Path.Combine(ProfilesRoot, safeUserId);
        }

        private static void MigrateSharedData()
        {
            MergeDirectoryIfExists(Path.Combine(AppRoot, "Settings"), SettingsDirectory);

            if (!File.Exists(ServerIpFile))
            {
                string[] ipCandidates =
                {
                    Path.Combine(AppRoot, "IP_Server.txt"),
                    Path.Combine(AppRoot, "MessageProgram", "x64", "Release", "IP_Server.txt"),
                    Path.Combine(AppRoot, "MessageProgram", "x64", "Debug", "IP_Server.txt")
                };

                foreach (string candidate in ipCandidates)
                {
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    string content = File.ReadAllText(candidate).Trim();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        File.WriteAllText(ServerIpFile, content);
                        break;
                    }
                }
            }
        }

        private static void MigrateLegacyProfileData(string userId, string profileRoot)
        {
            CopyFileIfMissing(Path.Combine(SharedDataRoot, "Friend_List.txt"), Path.Combine(profileRoot, "Friend_List.txt"));
            CopyFileIfMissing(Path.Combine(SharedDataRoot, "UnaddedContact.txt"), Path.Combine(profileRoot, "UnaddedContact.txt"));
            MergeDirectoryIfExists(Path.Combine(SharedDataRoot, "Conversations"), Path.Combine(profileRoot, "Conversations"));
            MergeDirectoryIfExists(Path.Combine(SharedDataRoot, "Templates"), Path.Combine(profileRoot, "Templates"));
            MergeDirectoryIfExists(Path.Combine(SharedDataRoot, "Downloads"), Path.Combine(profileRoot, "Downloads"));
            MergeDirectoryIfExists(
                Path.Combine(SharedDataRoot, "Keys", SanitizePathSegment(userId)),
                Path.Combine(profileRoot, "Keys"));
        }

        private static void EnsureWorkspaceDirectories(string root)
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "Conversations"));
            Directory.CreateDirectory(Path.Combine(root, "Templates"));
            Directory.CreateDirectory(Path.Combine(root, "Downloads"));

            EnsureFileExists(Path.Combine(root, "Friend_List.txt"));
            EnsureFileExists(Path.Combine(root, "UnaddedContact.txt"));
        }

        private static void MergeDirectoryIfExists(string sourceDirectory, string targetDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                return;
            }

            Directory.CreateDirectory(targetDirectory);

            foreach (string directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, directory);
                Directory.CreateDirectory(Path.Combine(targetDirectory, relativePath));
            }

            foreach (string file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceDirectory, file);
                string destinationPath = Path.Combine(targetDirectory, relativePath);
                EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));

                if (!File.Exists(destinationPath))
                {
                    File.Copy(file, destinationPath);
                }
            }
        }

        private static void CopyFileIfMissing(string sourcePath, string destinationPath)
        {
            if (!File.Exists(sourcePath) || File.Exists(destinationPath))
            {
                return;
            }

            EnsureDirectoryExists(Path.GetDirectoryName(destinationPath));
            File.Copy(sourcePath, destinationPath);
        }

        private static void EnsureFileExists(string filePath, string defaultContent = "")
        {
            if (File.Exists(filePath))
            {
                return;
            }

            EnsureDirectoryExists(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, defaultContent);
        }

        private static void EnsureDirectoryExists(string? directory)
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            HashSet<char> invalidChars = Path.GetInvalidFileNameChars().ToHashSet();
            char[] cleaned = value
                .Trim()
                .Select(ch => invalidChars.Contains(ch) ? '_' : ch)
                .ToArray();
            return new string(cleaned);
        }
    }
}
