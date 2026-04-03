#pragma once

#include <cstddef>
#include <cstdint>
#include <string>

namespace MessagingServer {

inline constexpr int kPort = 80;
inline constexpr int kBufferSize = 4096;

inline constexpr char kCredentialsFile[] = "Account.txt";
inline constexpr char kPublicKeysFile[] = "PublicKeys.txt";
inline constexpr char kPendingMessagesDirectory[] = "PendingMessages";
inline constexpr char kConversationsDirectory[] = "Conversations";
inline constexpr char kOfflineFilesDirectory[] = "OfflineFiles";
inline constexpr char kOnlineFilesDirectory[] = "OnlineFiles";

inline constexpr char kChatBotUser[] = "ChatBot";
inline constexpr char kChatBotDefaultModel[] = "gemma3:270m";
inline constexpr int kChatBotKeepAlive = -1;
inline constexpr char kPlaintextPayloadPrefix[] = "PLAIN1";
inline constexpr char kOllamaHost[] = "127.0.0.1";
inline constexpr char kOllamaPort[] = "11434";

inline constexpr std::size_t kChatBotHistoryMessageLimit = 12;
inline constexpr std::size_t kChatBotHistoryContentLimit = 320;

enum MessageType : std::uint32_t {
    TEXT = 1,
    UPDATE_Message = 2,
    File = 3,
    UPDATE_File = 4,
    RECEIVE_TEXT = 5,
    RECEIVE_File = 6,
    REGISTRATION = 7,
    NOT_REGISTRATION = 8,
    END_UPDATE = 9,
    REFRESH = 10,
    ADD_FRIENDS = 11,
    CHANGE_PASSWORD = 12,
    PRESENCE_UPDATE = 13,
    PUBLIC_KEY_REQUEST = 14,
    PUBLIC_KEY_SYNC = 15,
    TYPING_INDICATOR = 16,
    CLEAR_CHATBOT_MEMORY = 17
};

struct OllamaChatMessage {
    std::string role;
    std::string content;
};

} // namespace MessagingServer
