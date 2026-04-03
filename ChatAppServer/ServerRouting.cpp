#include "ServerRouting.h"

#include "ServerChatBot.h"
#include "ServerProtocol.h"
#include "ServerStorage.h"

#include <boost/filesystem.hpp>

#include <iostream>

namespace MessagingServer {
namespace fs = boost::filesystem;

bool HandleReceivedMessage(ServerContext& context, tcp::socket& senderSocket, const std::string& message) {
    boost::system::error_code error;

    size_t delimiterPosition = message.find('-');
    if (delimiterPosition == std::string::npos) {
        return false;
    }
    std::string recipient = message.substr(0, delimiterPosition);

    size_t messageStart = message.find('\n', delimiterPosition + 1);
    if (messageStart == std::string::npos) {
        return false;
    }

    std::string sender = message.substr(delimiterPosition + 1, messageStart - delimiterPosition - 1);
    std::string completedMessage = message.substr(messageStart + 1);

    size_t modelSeparator = recipient.find('|');
    std::string recipientName = modelSeparator == std::string::npos
        ? recipient
        : recipient.substr(0, modelSeparator);
    if (recipientName == kChatBotUser) {
        return HandleChatBotMessage(context, senderSocket, sender, recipient, completedMessage);
    }

    tcp::socket* recipientSocket = nullptr;
    {
        std::lock_guard<std::mutex> lock(context.socketMutex);
        std::cout << "Message from: " << sender << " - Content: " << completedMessage << " - to: " << recipient << std::endl;

        auto recipientIt = context.receiveSockets.find(recipient);
        if (recipientIt != context.receiveSockets.end()) {
            recipientSocket = recipientIt->second;
        }
    }

    if (recipientSocket != nullptr) {
        if (!recipientSocket->is_open()) {
            std::cerr << "Error: recipient's receive socket is not open or invalid." << std::endl;
            return false;
        }

        if (!SendTextPayloadToClient(*recipientSocket, sender, completedMessage)) {
            return false;
        }

        std::uint32_t response = 1;
        boost::asio::write(senderSocket, boost::asio::buffer(&response, sizeof(response)), error);
        if (error) {
            std::cerr << "Error sending confirmation to sender: " << error.message() << std::endl;
            return false;
        }

        StoreMessagesInServer(sender, recipient, completedMessage);
        return true;
    }

    fs::create_directory(kPendingMessagesDirectory);
    std::string offlineFile = std::string(kPendingMessagesDirectory) + "/" + recipient + "-" + sender + "-during-offline.bin";
    WriteToFile(offlineFile, completedMessage);

    std::uint32_t response = 1;
    boost::asio::write(senderSocket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error sending confirmation to sender: " << error.message() << std::endl;
        return false;
    }

    StoreMessagesInServer(sender, recipient, completedMessage);
    return true;
}

} // namespace MessagingServer
