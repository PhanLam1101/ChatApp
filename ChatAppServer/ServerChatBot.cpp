#include "ServerChatBot.h"

#include "ServerProtocol.h"
#include "ServerStorage.h"
#include "ForServer.h"

#include <boost/asio.hpp>
#include <boost/filesystem.hpp>

#include <algorithm>
#include <cctype>
#include <ctime>
#include <fstream>
#include <iostream>
#include <iterator>
#include <sstream>
#include <vector>

namespace MessagingServer {
namespace fs = boost::filesystem;

namespace {
constexpr const char* kMessageMetadataPrefix = "[#META#]";

std::string TrimCopy(const std::string& value) {
    size_t start = value.find_first_not_of(" \t\r\n");
    if (start == std::string::npos) {
        return std::string();
    }

    size_t end = value.find_last_not_of(" \t\r\n");
    return value.substr(start, end - start + 1);
}

bool StartsWith(const std::string& value, const std::string& prefix) {
    return value.rfind(prefix, 0) == 0;
}

std::string BytesToHex(const std::string& value) {
    static const char hexDigits[] = "0123456789abcdef";
    std::string hex;
    hex.reserve(value.size() * 2);
    for (unsigned char ch : value) {
        hex.push_back(hexDigits[(ch >> 4) & 0x0F]);
        hex.push_back(hexDigits[ch & 0x0F]);
    }

    return hex;
}

bool HexToString(const std::string& hex, std::string& value) {
    if ((hex.size() % 2) != 0) {
        return false;
    }

    value.clear();
    value.reserve(hex.size() / 2);
    for (size_t i = 0; i < hex.size(); i += 2) {
        auto hexValue = [](char ch) -> int {
            if (ch >= '0' && ch <= '9') {
                return ch - '0';
            }

            char lowered = static_cast<char>(std::tolower(static_cast<unsigned char>(ch)));
            if (lowered >= 'a' && lowered <= 'f') {
                return 10 + (lowered - 'a');
            }

            return -1;
        };

        int high = hexValue(hex[i]);
        int low = hexValue(hex[i + 1]);
        if (high < 0 || low < 0) {
            return false;
        }

        value.push_back(static_cast<char>((high << 4) | low));
    }

    return true;
}

bool WrapPlaintextPayload(const std::string& message, std::string& wrappedMessage) {
    wrappedMessage = std::string(kPlaintextPayloadPrefix) + "|" + BytesToHex(message);
    return true;
}

bool UnwrapPlaintextPayload(const std::string& message, std::string& plaintextMessage) {
    std::string prefix = std::string(kPlaintextPayloadPrefix) + "|";
    if (!StartsWith(message, prefix)) {
        return false;
    }

    size_t separator = message.find('|');
    if (separator == std::string::npos || separator + 1 >= message.size()) {
        return false;
    }

    return HexToString(message.substr(separator + 1), plaintextMessage);
}

bool TryReadMetadataValue(const std::string& formattedMessage, const std::string& key, std::string& value) {
    std::istringstream stream(formattedMessage);
    std::string line;
    const std::string prefix = std::string(kMessageMetadataPrefix) + key + "=";
    while (std::getline(stream, line)) {
        line = TrimCopy(line);
        if (StartsWith(line, prefix)) {
            value = line.substr(prefix.size());
            return true;
        }
    }

    return false;
}

std::string RemoveMetadataLines(const std::string& formattedMessage) {
    std::istringstream stream(formattedMessage);
    std::ostringstream cleaned;
    std::string line;
    bool firstLine = true;
    while (std::getline(stream, line)) {
        std::string trimmedLine = TrimCopy(line);
        if (StartsWith(trimmedLine, kMessageMetadataPrefix)) {
            continue;
        }

        if (!firstLine) {
            cleaned << '\n';
        }
        cleaned << line;
        firstLine = false;
    }

    return cleaned.str();
}

std::string GetChatBotTimestamp() {
    std::time_t now = std::time(nullptr);
    std::tm localTime {};
    localtime_s(&localTime, &now);
    char buffer[32];
    std::strftime(buffer, sizeof(buffer), "%H:%M-%d/%m/%Y", &localTime);
    return buffer;
}

std::string ExtractChatMessageBody(const std::string& formattedMessage) {
    std::string trimmed = TrimCopy(RemoveMetadataLines(formattedMessage));
    size_t headerEnd = trimmed.find("]:");
    if (headerEnd == std::string::npos) {
        return trimmed;
    }

    std::string body = TrimCopy(trimmed.substr(headerEnd + 2));
    const std::string endOfMessageMarker = "{#EOM#}";
    if (body.size() >= endOfMessageMarker.size()) {
        size_t markerPosition = body.rfind(endOfMessageMarker);
        if (markerPosition != std::string::npos && markerPosition + endOfMessageMarker.size() == body.size()) {
            body.erase(markerPosition);
        }
    }

    return TrimCopy(body);
}

std::string TruncateChatBotContent(const std::string& value, size_t maxLength) {
    std::string trimmed = TrimCopy(value);
    if (trimmed.size() <= maxLength) {
        return trimmed;
    }

    if (maxLength <= 3) {
        return trimmed.substr(0, maxLength);
    }

    return TrimCopy(trimmed.substr(0, maxLength - 3)) + "...";
}

bool TryParseChatBotStoredMessage(const std::string& storedPayload, const std::string& userName, OllamaChatMessage& parsedMessage) {
    std::string plainMessage;
    if (!UnwrapPlaintextPayload(storedPayload, plainMessage)) {
        return false;
    }

    std::string messageKind;
    if (TryReadMetadataValue(plainMessage, "kind", messageKind) && TrimCopy(messageKind) == "control") {
        return false;
    }

    std::string trimmed = TrimCopy(plainMessage);
    size_t senderSeparator = trimmed.find(" [");
    if (senderSeparator == std::string::npos) {
        return false;
    }

    std::string speaker = TrimCopy(trimmed.substr(0, senderSeparator));
    std::string body = TruncateChatBotContent(ExtractChatMessageBody(trimmed), kChatBotHistoryContentLimit);
    if (body.empty()) {
        return false;
    }

    if (speaker == kChatBotUser) {
        parsedMessage.role = "assistant";
    }
    else if (speaker == userName) {
        parsedMessage.role = "user";
    }
    else {
        return false;
    }

    parsedMessage.content = body;
    return true;
}

std::vector<OllamaChatMessage> LoadRecentChatBotHistory(const std::string& userName) {
    std::vector<OllamaChatMessage> history;
    std::string conversationPath = std::string(kConversationsDirectory) + "/" + GetConversationFileName(userName, kChatBotUser) + ".bin";
    std::ifstream conversationFile(conversationPath);
    if (!conversationFile.is_open()) {
        return history;
    }

    std::string storedPayload;
    while (std::getline(conversationFile, storedPayload)) {
        OllamaChatMessage parsedMessage;
        if (TryParseChatBotStoredMessage(storedPayload, userName, parsedMessage)) {
            history.push_back(parsedMessage);
        }
    }

    if (history.size() > kChatBotHistoryMessageLimit) {
        history.erase(history.begin(), history.end() - static_cast<std::ptrdiff_t>(kChatBotHistoryMessageLimit));
    }

    return history;
}

std::string JsonEscape(const std::string& value) {
    std::ostringstream escaped;
    for (unsigned char ch : value) {
        switch (ch) {
        case '\\':
            escaped << "\\\\";
            break;
        case '"':
            escaped << "\\\"";
            break;
        case '\n':
            escaped << "\\n";
            break;
        case '\r':
            escaped << "\\r";
            break;
        case '\t':
            escaped << "\\t";
            break;
        default:
            if (ch < 0x20) {
                escaped << ' ';
            }
            else {
                escaped << static_cast<char>(ch);
            }
            break;
        }
    }

    return escaped.str();
}

bool TryExtractJsonStringField(const std::string& json, const std::string& fieldName, std::string& value) {
    std::string token = "\"" + fieldName + "\":";
    size_t tokenPosition = json.find(token);
    if (tokenPosition == std::string::npos) {
        return false;
    }

    size_t quotePosition = json.find('"', tokenPosition + token.size());
    if (quotePosition == std::string::npos) {
        return false;
    }

    value.clear();
    bool escaping = false;
    for (size_t i = quotePosition + 1; i < json.size(); ++i) {
        char ch = json[i];
        if (escaping) {
            switch (ch) {
            case '\\':
                value.push_back('\\');
                break;
            case '"':
                value.push_back('"');
                break;
            case 'n':
                value.push_back('\n');
                break;
            case 'r':
                value.push_back('\r');
                break;
            case 't':
                value.push_back('\t');
                break;
            default:
                value.push_back(ch);
                break;
            }
            escaping = false;
            continue;
        }

        if (ch == '\\') {
            escaping = true;
            continue;
        }

        if (ch == '"') {
            return true;
        }

        value.push_back(ch);
    }

    return false;
}

std::string BuildOllamaChatRequestBody(const std::string& modelName, const std::vector<OllamaChatMessage>& messages) {
    std::ostringstream body;
    body << "{\"model\":\"" << JsonEscape(modelName)
         << "\",\"stream\":false,\"keep_alive\":" << kChatBotKeepAlive << ",\"messages\":[";

    for (size_t i = 0; i < messages.size(); ++i) {
        if (i > 0) {
            body << ",";
        }

        body << "{\"role\":\"" << JsonEscape(messages[i].role)
             << "\",\"content\":\"" << JsonEscape(messages[i].content) << "\"}";
    }

    body << "]}";
    return body.str();
}

bool CallOllamaChat(const std::string& modelName, const std::vector<OllamaChatMessage>& messages, std::string& responseText, std::string& errorMessage) {
    responseText.clear();
    errorMessage.clear();

    if (messages.empty()) {
        errorMessage = "No chat messages were provided to Ollama.";
        return false;
    }

    try {
        boost::asio::io_context ioContext;
        tcp::resolver resolver(ioContext);
        tcp::socket socket(ioContext);
        boost::system::error_code error;

        auto endpoints = resolver.resolve(kOllamaHost, kOllamaPort, error);
        if (error) {
            errorMessage = "Failed to resolve the local Ollama server: " + error.message();
            return false;
        }

        boost::asio::connect(socket, endpoints, error);
        if (error) {
            errorMessage = "Failed to connect to the local Ollama server: " + error.message();
            return false;
        }

        std::string body = BuildOllamaChatRequestBody(modelName, messages);
        std::ostringstream request;
        request << "POST /api/chat HTTP/1.1\r\n"
                << "Host: " << kOllamaHost << ":" << kOllamaPort << "\r\n"
                << "Content-Type: application/json\r\n"
                << "Content-Length: " << body.size() << "\r\n"
                << "Connection: close\r\n\r\n"
                << body;

        boost::asio::write(socket, boost::asio::buffer(request.str()), error);
        if (error) {
            errorMessage = "Failed to send the chat request to Ollama: " + error.message();
            return false;
        }

        boost::asio::streambuf responseBuffer;
        boost::asio::read(socket, responseBuffer, error);
        if (error && error != boost::asio::error::eof) {
            errorMessage = "Failed to read the Ollama chat response: " + error.message();
            return false;
        }

        std::string rawResponse((std::istreambuf_iterator<char>(&responseBuffer)), std::istreambuf_iterator<char>());
        size_t headerEnd = rawResponse.find("\r\n\r\n");
        if (headerEnd == std::string::npos) {
            errorMessage = "Received an invalid HTTP response from Ollama.";
            return false;
        }

        std::string statusLine = rawResponse.substr(0, rawResponse.find("\r\n"));
        std::string bodyText = rawResponse.substr(headerEnd + 4);
        if (statusLine.find("200") == std::string::npos) {
            std::string ollamaError;
            if (TryExtractJsonStringField(bodyText, "error", ollamaError)) {
                errorMessage = ollamaError;
            }
            else {
                errorMessage = "Ollama returned an unexpected status: " + statusLine;
            }
            return false;
        }

        if (!TryExtractJsonStringField(bodyText, "content", responseText)) {
            errorMessage = "Ollama returned a chat response, but the assistant content could not be parsed.";
            return false;
        }

        responseText = TrimCopy(responseText);
        return true;
    }
    catch (const std::exception& exception) {
        errorMessage = exception.what();
        return false;
    }
}

bool UnloadOllamaModel(const std::string& modelName, std::string& errorMessage) {
    errorMessage.clear();

    try {
        boost::asio::io_context ioContext;
        tcp::resolver resolver(ioContext);
        tcp::socket socket(ioContext);
        boost::system::error_code error;

        auto endpoints = resolver.resolve(kOllamaHost, kOllamaPort, error);
        if (error) {
            errorMessage = "Failed to resolve the local Ollama server: " + error.message();
            return false;
        }

        boost::asio::connect(socket, endpoints, error);
        if (error) {
            errorMessage = "Failed to connect to the local Ollama server: " + error.message();
            return false;
        }

        std::string body = std::string("{\"model\":\"") + JsonEscape(modelName) + "\",\"keep_alive\":0}";
        std::ostringstream request;
        request << "POST /api/generate HTTP/1.1\r\n"
                << "Host: " << kOllamaHost << ":" << kOllamaPort << "\r\n"
                << "Content-Type: application/json\r\n"
                << "Content-Length: " << body.size() << "\r\n"
                << "Connection: close\r\n\r\n"
                << body;

        boost::asio::write(socket, boost::asio::buffer(request.str()), error);
        if (error) {
            errorMessage = "Failed to send the unload request to Ollama: " + error.message();
            return false;
        }

        boost::asio::streambuf responseBuffer;
        boost::asio::read(socket, responseBuffer, error);
        if (error && error != boost::asio::error::eof) {
            errorMessage = "Failed to read the Ollama unload response: " + error.message();
            return false;
        }

        std::string rawResponse((std::istreambuf_iterator<char>(&responseBuffer)), std::istreambuf_iterator<char>());
        size_t headerEnd = rawResponse.find("\r\n\r\n");
        if (headerEnd == std::string::npos) {
            errorMessage = "Received an invalid HTTP response from Ollama during unload.";
            return false;
        }

        std::string statusLine = rawResponse.substr(0, rawResponse.find("\r\n"));
        if (statusLine.find("200") == std::string::npos) {
            std::string bodyText = rawResponse.substr(headerEnd + 4);
            std::string ollamaError;
            if (TryExtractJsonStringField(bodyText, "error", ollamaError)) {
                errorMessage = ollamaError;
            }
            else {
                errorMessage = "Ollama returned an unexpected unload status: " + statusLine;
            }
            return false;
        }

        return true;
    }
    catch (const std::exception& exception) {
        errorMessage = exception.what();
        return false;
    }
}

void ReleaseTrackedModel(ServerContext& context, const std::string& modelName) {
    if (modelName.empty()) {
        return;
    }

    bool shouldUnload = false;
    {
        std::lock_guard<std::mutex> lock(context.chatBotStateMutex);
        auto countIt = context.activeChatBotUsersByModel.find(modelName);
        if (countIt == context.activeChatBotUsersByModel.end()) {
            return;
        }

        if (countIt->second > 1) {
            --countIt->second;
            return;
        }

        context.activeChatBotUsersByModel.erase(countIt);
        shouldUnload = true;
    }

    if (shouldUnload) {
        std::string unloadError;
        if (!UnloadOllamaModel(modelName, unloadError)) {
            std::cerr << "Failed to unload Ollama model '" << modelName << "': " << unloadError << std::endl;
        }
        else {
            std::cout << "Unloaded Ollama model: " << modelName << std::endl;
        }
    }
}

void TrackChatBotModelUsage(ServerContext& context, const std::string& userName, const std::string& modelName) {
    std::string previousModel;
    {
        std::lock_guard<std::mutex> lock(context.chatBotStateMutex);
        auto previousIt = context.activeChatBotModelByUser.find(userName);
        if (previousIt != context.activeChatBotModelByUser.end()) {
            previousModel = previousIt->second;
            if (previousModel == modelName) {
                return;
            }
        }

        context.activeChatBotModelByUser[userName] = modelName;
        context.activeChatBotUsersByModel[modelName] = context.activeChatBotUsersByModel[modelName] + 1;
    }

    if (!previousModel.empty()) {
        ReleaseTrackedModel(context, previousModel);
    }
}

} // namespace

bool HandleChatBotMessage(
    ServerContext& context,
    tcp::socket& senderSocket,
    const std::string& sender,
    const std::string& recipientToken,
    const std::string& messagePayload) {
    std::string plainMessage;
    if (!UnwrapPlaintextPayload(messagePayload, plainMessage)) {
        plainMessage = messagePayload;
    }

    std::string requestedModel = kChatBotDefaultModel;
    size_t modelSeparator = recipientToken.find('|');
    if (modelSeparator != std::string::npos && modelSeparator + 1 < recipientToken.size()) {
        requestedModel = TrimCopy(recipientToken.substr(modelSeparator + 1));
    }
    if (requestedModel.empty()) {
        requestedModel = kChatBotDefaultModel;
    }

    TrackChatBotModelUsage(context, sender, requestedModel);

    std::string prompt = ExtractChatMessageBody(plainMessage);
    std::vector<OllamaChatMessage> chatMessages;
    chatMessages.push_back({
        "system",
        "You are ChatBot inside a desktop messaging application. "
        "Use the recent conversation when it is relevant, but prioritize the newest user request. "
        "Reply helpfully, clearly, and keep answers reasonably concise unless the user asks for more detail. "
        "Only the recent part of the conversation is provided, so do not pretend to remember details that are not in the visible history."
    });

    std::vector<OllamaChatMessage> recentHistory = LoadRecentChatBotHistory(sender);
    chatMessages.insert(chatMessages.end(), recentHistory.begin(), recentHistory.end());
    chatMessages.push_back({ "user", prompt });

    std::string botText;
    std::string ollamaError;
    if (!CallOllamaChat(requestedModel, chatMessages, botText, ollamaError)) {
        botText = "I couldn't generate a reply with model '" + requestedModel + "'. " + ollamaError;
    }

    std::string formattedReply = std::string(kChatBotUser) + " [" + GetChatBotTimestamp() + "]: " + botText + "\n{#EOM#}";
    std::string wrappedReply;
    WrapPlaintextPayload(formattedReply, wrappedReply);

    std::uint32_t response = 1;
    boost::system::error_code error;
    boost::asio::write(senderSocket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error sending confirmation to sender: " << error.message() << std::endl;
        return false;
    }

    StoreMessagesInServer(sender, kChatBotUser, messagePayload);
    StoreMessagesInServer(sender, kChatBotUser, wrappedReply);

    tcp::socket* senderReceiveSocket = nullptr;
    {
        std::lock_guard<std::mutex> lock(context.socketMutex);
        auto senderSocketIt = context.receiveSockets.find(sender);
        if (senderSocketIt != context.receiveSockets.end() && senderSocketIt->second != nullptr && senderSocketIt->second->is_open()) {
            senderReceiveSocket = senderSocketIt->second;
        }
    }

    if (senderReceiveSocket != nullptr) {
        return SendTextPayloadToClient(*senderReceiveSocket, kChatBotUser, wrappedReply);
    }

    fs::create_directory(kPendingMessagesDirectory);
    std::string offlineFile = std::string(kPendingMessagesDirectory) + "/" + sender + "-" + kChatBotUser + "-during-offline.bin";
    WriteToFile(offlineFile, wrappedReply);
    return true;
}

void ReleaseChatBotResourcesForUser(ServerContext& context, const std::string& userName) {
    std::string modelName;
    {
        std::lock_guard<std::mutex> lock(context.chatBotStateMutex);
        auto userIt = context.activeChatBotModelByUser.find(userName);
        if (userIt == context.activeChatBotModelByUser.end()) {
            return;
        }

        modelName = userIt->second;
        context.activeChatBotModelByUser.erase(userIt);
    }

    ReleaseTrackedModel(context, modelName);
}

void ReleaseAllChatBotResources(ServerContext& context) {
    std::vector<std::string> modelsToUnload;
    {
        std::lock_guard<std::mutex> lock(context.chatBotStateMutex);
        for (const auto& entry : context.activeChatBotUsersByModel) {
            modelsToUnload.push_back(entry.first);
        }

        context.activeChatBotUsersByModel.clear();
        context.activeChatBotModelByUser.clear();
    }

    for (const std::string& modelName : modelsToUnload) {
        std::string unloadError;
        if (!UnloadOllamaModel(modelName, unloadError)) {
            std::cerr << "Failed to unload Ollama model '" << modelName << "' during shutdown: " << unloadError << std::endl;
        }
        else {
            std::cout << "Unloaded Ollama model during shutdown: " << modelName << std::endl;
        }
    }
}

bool ClearChatBotConversation(const std::string& userName) {
    bool success = true;
    try {
        const std::string conversationFile =
            std::string(kConversationsDirectory) + "/" + GetConversationFileName(userName, kChatBotUser) + ".bin";
        if (fs::exists(conversationFile)) {
            fs::remove(conversationFile);
        }

        const std::string pendingFile =
            std::string(kPendingMessagesDirectory) + "/" + userName + "-" + kChatBotUser + "-during-offline.bin";
        if (fs::exists(pendingFile)) {
            fs::remove(pendingFile);
        }
    }
    catch (const fs::filesystem_error& ex) {
        std::cerr << "Failed to clear ChatBot conversation for " << userName << ": " << ex.what() << std::endl;
        success = false;
    }

    return success;
}

} // namespace MessagingServer
