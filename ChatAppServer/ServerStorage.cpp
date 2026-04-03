#include "ServerStorage.h"

#include "ServerProtocol.h"
#include "ForServer.h"

#include <boost/filesystem.hpp>

#include <fstream>
#include <iostream>
#include <set>
#include <vector>

namespace MessagingServer {
namespace fs = boost::filesystem;

void LoadCredentials(ServerContext& context) {
    std::ifstream infile(kCredentialsFile);
    if (!infile.is_open()) {
        return;
    }

    std::string id;
    std::string password;
    while (infile >> id >> password) {
        context.clientCredentials[id] = password;
    }
}

void SaveCredentials(const ServerContext& context) {
    std::ofstream outfile(kCredentialsFile);
    for (const auto& pair : context.clientCredentials) {
        outfile << pair.first << " " << pair.second << std::endl;
    }
}

void LoadPublicKeys(ServerContext& context) {
    std::ifstream infile(kPublicKeysFile);
    if (!infile.is_open()) {
        return;
    }

    std::string id;
    std::string publicKey;
    while (infile >> id >> publicKey) {
        context.clientPublicKeys[id] = publicKey;
    }
}

void SavePublicKeys(const ServerContext& context) {
    std::ofstream outfile(kPublicKeysFile);
    for (const auto& pair : context.clientPublicKeys) {
        outfile << pair.first << " " << pair.second << std::endl;
    }
}

void StoreMessagesInServer(const std::string& senderName, const std::string& recipientName, const std::string& message) {
    fs::create_directory(kConversationsDirectory);
    std::string filePath = std::string(kConversationsDirectory) + "/" + GetConversationFileName(senderName, recipientName) + ".bin";

    std::ofstream outfile(filePath, std::ios::app);
    outfile << message << "\n";
}

void WriteToFile(const std::string& filename, const std::string& content) {
    std::ofstream file(filename, std::ios::app);
    if (file.is_open()) {
        file << content << "\n";
    }
}

void CreateUpdateFile(const std::string& previousFile, const std::string& offlineFile, const std::string& updateFile) {
    std::ifstream previous(previousFile);
    std::ifstream offline(offlineFile);
    std::ofstream update(updateFile);

    std::set<std::string> previousMessages;
    std::string line;

    while (std::getline(previous, line)) {
        previousMessages.insert(line);
    }

    while (std::getline(offline, line)) {
        if (previousMessages.find(line) == previousMessages.end()) {
            update << line << "\n";
        }
    }
}

void HandleChangePassword(ServerContext& context, tcp::socket& socket, const std::string& currentUserId) {
    boost::system::error_code error;

    std::uint32_t currentPasswordLength = 0;
    boost::asio::read(socket, boost::asio::buffer(&currentPasswordLength, sizeof(currentPasswordLength)), error);
    if (error) {
        std::cerr << "Error receiving current password length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> currentPasswordBuffer(currentPasswordLength);
    boost::asio::read(socket, boost::asio::buffer(currentPasswordBuffer), error);
    if (error) {
        std::cerr << "Error receiving current password: " << error.message() << std::endl;
        return;
    }
    std::string currentPassword(currentPasswordBuffer.begin(), currentPasswordBuffer.end());

    std::uint32_t newPasswordLength = 0;
    boost::asio::read(socket, boost::asio::buffer(&newPasswordLength, sizeof(newPasswordLength)), error);
    if (error) {
        std::cerr << "Error receiving new password length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> newPasswordBuffer(newPasswordLength);
    boost::asio::read(socket, boost::asio::buffer(newPasswordBuffer), error);
    if (error) {
        std::cerr << "Error receiving new password: " << error.message() << std::endl;
        return;
    }
    std::string newPassword(newPasswordBuffer.begin(), newPasswordBuffer.end());

    {
        std::lock_guard<std::mutex> lock(context.credentialsMutex);
        auto userIt = context.clientCredentials.find(currentUserId);
        if (userIt == context.clientCredentials.end() || userIt->second != currentPassword) {
            std::uint32_t response = 0;
            boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
            std::cout << "Password change failed for user " << currentUserId << std::endl;
            return;
        }

        context.clientCredentials[currentUserId] = newPassword;
        SaveCredentials(context);
    }

    std::uint32_t response = 1;
    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
    std::cout << "Password successfully changed for user " << currentUserId << std::endl;
}

void HandleAddFriend(ServerContext& context, tcp::socket& socket) {
    boost::system::error_code error;

    std::uint32_t nameLength = 0;
    boost::asio::read(socket, boost::asio::buffer(&nameLength, sizeof(nameLength)), error);
    if (error) {
        std::cerr << "Error reading friend's name length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> nameBuffer(nameLength);
    boost::asio::read(socket, boost::asio::buffer(nameBuffer), error);
    if (error) {
        std::cerr << "Error reading friend's name: " << error.message() << std::endl;
        return;
    }
    std::string friendName(nameBuffer.begin(), nameBuffer.end());

    std::uint32_t response = 0;
    std::string publicKey;
    bool friendExists = false;
    {
        std::lock_guard<std::mutex> credentialLock(context.credentialsMutex);
        friendExists = context.clientCredentials.find(friendName) != context.clientCredentials.end();
    }

    if (friendExists) {
        std::lock_guard<std::mutex> keyLock(context.publicKeysMutex);
        auto keyIt = context.clientPublicKeys.find(friendName);
        if (keyIt != context.clientPublicKeys.end() && !keyIt->second.empty()) {
            response = 1;
            publicKey = keyIt->second;
        }
        else {
            response = 2;
        }
    }

    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error sending response to client: " << error.message() << std::endl;
        return;
    }

    if (response == 1) {
        WriteSizedString(socket, publicKey, error);
        if (error) {
            std::cerr << "Error sending the friend's public key: " << error.message() << std::endl;
            return;
        }
    }

    if (response == 1) {
        std::cout << "Friend " << friendName << " added successfully." << std::endl;
    }
    else if (response == 2) {
        std::cout << "Friend " << friendName << " exists but has no uploaded public key yet." << std::endl;
    }
    else {
        std::cout << "Friend " << friendName << " not found in system." << std::endl;
    }
}

void HandlePublicKeySync(ServerContext& context, tcp::socket& socket, const std::string& currentUserId) {
    boost::system::error_code error;
    std::string publicKey;
    if (!ReadSizedString(socket, publicKey, error)) {
        std::cerr << "Error reading synced public key: " << error.message() << std::endl;
        return;
    }

    {
        std::lock_guard<std::mutex> keyLock(context.publicKeysMutex);
        context.clientPublicKeys[currentUserId] = publicKey;
        SavePublicKeys(context);
    }

    std::uint32_t response = 1;
    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error acknowledging the public-key sync: " << error.message() << std::endl;
    }
}

void HandlePublicKeyRequest(ServerContext& context, tcp::socket& socket) {
    boost::system::error_code error;
    std::string requestedUser;
    if (!ReadSizedString(socket, requestedUser, error)) {
        std::cerr << "Error reading public-key request target: " << error.message() << std::endl;
        return;
    }

    std::uint32_t response = 0;
    std::string publicKey;
    bool userExists = false;
    {
        std::lock_guard<std::mutex> credentialLock(context.credentialsMutex);
        userExists = context.clientCredentials.find(requestedUser) != context.clientCredentials.end();
    }

    if (userExists) {
        std::lock_guard<std::mutex> keyLock(context.publicKeysMutex);
        auto keyIt = context.clientPublicKeys.find(requestedUser);
        if (keyIt != context.clientPublicKeys.end() && !keyIt->second.empty()) {
            response = 1;
            publicKey = keyIt->second;
        }
        else {
            response = 2;
        }
    }

    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error sending public-key request response: " << error.message() << std::endl;
        return;
    }

    if (response == 1) {
        WriteSizedString(socket, publicKey, error);
        if (error) {
            std::cerr << "Error sending the requested public key: " << error.message() << std::endl;
        }
    }
}

void RefreshConversations(tcp::socket& sendSocket, const std::string& userName) {
    boost::system::error_code error;

    if (!fs::exists(kConversationsDirectory)) {
        std::cerr << "Conversations directory not found." << std::endl;
        return;
    }

    for (const auto& entry : fs::directory_iterator(kConversationsDirectory)) {
        std::string filename = entry.path().filename().string();
        size_t userPosition = filename.find(userName);
        if (userPosition == std::string::npos) {
            continue;
        }

        std::string otherUser;
        if (userPosition == 0) {
            otherUser = filename.substr(userName.size() + 1, filename.find('.') - userName.size() - 1);
        }
        else {
            otherUser = filename.substr(0, userPosition - 1);
        }

        std::ifstream conversationFile(entry.path().string());
        std::string message;
        while (std::getline(conversationFile, message)) {
            std::uint32_t messageType = MessageType::RECEIVE_TEXT;
            boost::asio::write(sendSocket, boost::asio::buffer(&messageType, sizeof(messageType)), error);
            if (error) {
                std::cerr << "Error sending message type: " << error.message() << std::endl;
                return;
            }

            std::uint32_t nameLength = static_cast<std::uint32_t>(otherUser.size());
            boost::asio::write(sendSocket, boost::asio::buffer(&nameLength, sizeof(nameLength)), error);
            boost::asio::write(sendSocket, boost::asio::buffer(otherUser), error);

            std::uint32_t messageLength = static_cast<std::uint32_t>(message.size());
            boost::asio::write(sendSocket, boost::asio::buffer(&messageLength, sizeof(messageLength)), error);
            boost::asio::write(sendSocket, boost::asio::buffer(message), error);

            if (error) {
                std::cerr << "Error sending message content: " << error.message() << std::endl;
                return;
            }
        }
    }

    std::uint32_t endUpdateSignal = MessageType::END_UPDATE;
    boost::asio::write(sendSocket, boost::asio::buffer(&endUpdateSignal, sizeof(endUpdateSignal)), error);
    if (error) {
        std::cerr << "Error sending END_UPDATE signal: " << error.message() << std::endl;
    }
}

bool AuthenticateClient(ServerContext& context, tcp::socket& socket, std::string& clientId) {
    boost::system::error_code error;

    std::uint32_t idLength = 0;
    boost::asio::read(socket, boost::asio::buffer(&idLength, sizeof(idLength)), error);
    if (error) {
        return false;
    }

    std::vector<char> idBuffer(idLength);
    boost::asio::read(socket, boost::asio::buffer(idBuffer), error);
    if (error) {
        return false;
    }
    clientId.assign(idBuffer.begin(), idBuffer.end());

    std::uint32_t passwordLength = 0;
    boost::asio::read(socket, boost::asio::buffer(&passwordLength, sizeof(passwordLength)), error);
    if (error) {
        return false;
    }

    std::vector<char> passwordBuffer(passwordLength);
    boost::asio::read(socket, boost::asio::buffer(passwordBuffer), error);
    if (error) {
        return false;
    }
    std::string password(passwordBuffer.begin(), passwordBuffer.end());

    bool authenticated = false;
    {
        std::lock_guard<std::mutex> lock(context.credentialsMutex);
        auto clientIt = context.clientCredentials.find(clientId);
        authenticated = clientIt != context.clientCredentials.end() && clientIt->second == password;
    }

    std::uint32_t response = authenticated ? 1U : 0U;
    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)));
    if (authenticated) {
        std::cout << "Authenticated client: " << clientId << std::endl;
    }

    return authenticated;
}

void HandleUpdateMessage(tcp::socket& socket, const std::string& recipient) {
    std::cout << "Delivering pending messages to " << recipient << std::endl;
    boost::system::error_code error;

    std::uint32_t messageType = MessageType::UPDATE_Message;
    boost::asio::write(socket, boost::asio::buffer(&messageType, sizeof(messageType)), error);
    if (error) {
        std::cerr << "Error sending update notification: " << error.message() << std::endl;
        return;
    }

    if (!fs::exists(kPendingMessagesDirectory)) {
        std::uint32_t endSignal = MessageType::END_UPDATE;
        boost::asio::write(socket, boost::asio::buffer(&endSignal, sizeof(endSignal)), error);
        return;
    }

    for (const auto& entry : fs::directory_iterator(kPendingMessagesDirectory)) {
        std::string filename = entry.path().filename().string();
        std::cout << "Checking for offline messages for: " << recipient << std::endl;

        std::size_t recipientPos = filename.find(recipient + "-");
        std::size_t senderPos = filename.find("-during-offline.bin");
        if (recipientPos != 0 || senderPos == std::string::npos) {
            continue;
        }

        std::string sender = filename.substr(recipient.size() + 1, senderPos - recipient.size() - 1);
        std::ifstream file(entry.path().string());
        std::string offlineMessage;
        while (std::getline(file, offlineMessage)) {
            if (offlineMessage.empty()) {
                continue;
            }

            std::uint32_t responseType = MessageType::UPDATE_Message;
            boost::asio::write(socket, boost::asio::buffer(&responseType, sizeof(responseType)), error);
            if (error) {
                std::cerr << "Error sending update notification: " << error.message() << std::endl;
                return;
            }

            std::uint32_t senderSize = static_cast<std::uint32_t>(sender.size());
            boost::asio::write(socket, boost::asio::buffer(&senderSize, sizeof(senderSize)), error);
            if (error) {
                std::cerr << "Error sending sender size: " << error.message() << std::endl;
                return;
            }
            boost::asio::write(socket, boost::asio::buffer(sender), error);
            if (error) {
                std::cerr << "Error sending sender name: " << error.message() << std::endl;
                return;
            }

            std::uint32_t messageLength = static_cast<std::uint32_t>(offlineMessage.size());
            boost::asio::write(socket, boost::asio::buffer(&messageLength, sizeof(messageLength)), error);
            if (error) {
                std::cerr << "Error sending message length: " << error.message() << std::endl;
                return;
            }

            boost::asio::write(socket, boost::asio::buffer(offlineMessage), error);
            if (error) {
                std::cerr << "Error sending update message: " << error.message() << std::endl;
                return;
            }

            std::cout << "Sent offline message from " << sender << " to " << recipient << std::endl;
        }

        file.close();
        fs::remove(entry.path());
        std::cout << "Deleted offline message file: " << filename << std::endl;
    }

    std::uint32_t responseType = MessageType::END_UPDATE;
    boost::asio::write(socket, boost::asio::buffer(&responseType, sizeof(responseType)), error);
    if (error) {
        std::cerr << "Error sending end-of-updates signal: " << error.message() << std::endl;
    }
    std::cout << "Finished sending all updates for " << recipient << std::endl;
}

void RegisterClient(ServerContext& context, tcp::socket& socket) {
    boost::system::error_code error;

    std::uint32_t idLength = 0;
    boost::asio::read(socket, boost::asio::buffer(&idLength, sizeof(idLength)), error);
    if (error) {
        std::cerr << "Error reading ID length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> idBuffer(idLength);
    boost::asio::read(socket, boost::asio::buffer(idBuffer), error);
    if (error) {
        std::cerr << "Error reading ID: " << error.message() << std::endl;
        return;
    }
    std::string clientId(idBuffer.begin(), idBuffer.end());

    std::uint32_t passwordLength = 0;
    boost::asio::read(socket, boost::asio::buffer(&passwordLength, sizeof(passwordLength)), error);
    if (error) {
        std::cerr << "Error reading password length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> passwordBuffer(passwordLength);
    boost::asio::read(socket, boost::asio::buffer(passwordBuffer), error);
    if (error) {
        std::cerr << "Error reading password: " << error.message() << std::endl;
        return;
    }
    std::string password(passwordBuffer.begin(), passwordBuffer.end());

    std::string publicKey;
    if (!ReadSizedString(socket, publicKey, error)) {
        std::cerr << "Error reading public key during registration: " << error.message() << std::endl;
        return;
    }

    {
        std::lock_guard<std::mutex> lock(context.credentialsMutex);
        if (context.clientCredentials.find(clientId) == context.clientCredentials.end()) {
            context.clientCredentials[clientId] = password;
            SaveCredentials(context);

            {
                std::lock_guard<std::mutex> keyLock(context.publicKeysMutex);
                context.clientPublicKeys[clientId] = publicKey;
                SavePublicKeys(context);
            }

            std::uint32_t response = 1;
            boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)));
            std::cout << "Registered new client: " << clientId << std::endl;
            return;
        }
    }

    std::uint32_t response = 0;
    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)));
    std::cout << "Registration failed: Client ID '" << clientId << "' already exists." << std::endl;
}

} // namespace MessagingServer
