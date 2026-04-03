#include "ServerProtocol.h"

#include <vector>
#include <iostream>

namespace MessagingServer {

bool ReadSizedString(tcp::socket& socket, std::string& value, boost::system::error_code& error) {
    std::uint32_t length = 0;
    boost::asio::read(socket, boost::asio::buffer(&length, sizeof(length)), error);
    if (error) {
        return false;
    }

    std::vector<char> buffer(length);
    if (length > 0) {
        boost::asio::read(socket, boost::asio::buffer(buffer), error);
        if (error) {
            return false;
        }
    }

    value.assign(buffer.begin(), buffer.end());
    return true;
}

bool WriteSizedString(tcp::socket& socket, const std::string& value, boost::system::error_code& error) {
    std::uint32_t length = static_cast<std::uint32_t>(value.size());
    boost::asio::write(socket, boost::asio::buffer(&length, sizeof(length)), error);
    if (error) {
        return false;
    }

    if (length > 0) {
        boost::asio::write(socket, boost::asio::buffer(value), error);
    }

    return !error;
}

void SendPresenceUpdate(tcp::socket& socket, const std::string& userName, bool isOnline) {
    boost::system::error_code error;
    std::uint32_t messageType = MessageType::PRESENCE_UPDATE;
    boost::asio::write(socket, boost::asio::buffer(&messageType, sizeof(messageType)), error);
    if (error) {
        std::cerr << "Error sending presence message type: " << error.message() << std::endl;
        return;
    }

    std::uint32_t userNameLength = static_cast<std::uint32_t>(userName.size());
    boost::asio::write(socket, boost::asio::buffer(&userNameLength, sizeof(userNameLength)), error);
    if (error) {
        std::cerr << "Error sending presence user length: " << error.message() << std::endl;
        return;
    }

    boost::asio::write(socket, boost::asio::buffer(userName), error);
    if (error) {
        std::cerr << "Error sending presence user name: " << error.message() << std::endl;
        return;
    }

    std::uint32_t onlineState = isOnline ? 1U : 0U;
    boost::asio::write(socket, boost::asio::buffer(&onlineState, sizeof(onlineState)), error);
    if (error) {
        std::cerr << "Error sending presence state: " << error.message() << std::endl;
    }
}

void SendTypingIndicator(tcp::socket& socket, const std::string& userName, bool isTyping) {
    boost::system::error_code error;
    std::uint32_t messageType = MessageType::TYPING_INDICATOR;
    boost::asio::write(socket, boost::asio::buffer(&messageType, sizeof(messageType)), error);
    if (error) {
        std::cerr << "Error sending typing message type: " << error.message() << std::endl;
        return;
    }

    std::uint32_t userNameLength = static_cast<std::uint32_t>(userName.size());
    boost::asio::write(socket, boost::asio::buffer(&userNameLength, sizeof(userNameLength)), error);
    if (error) {
        std::cerr << "Error sending typing user length: " << error.message() << std::endl;
        return;
    }

    boost::asio::write(socket, boost::asio::buffer(userName), error);
    if (error) {
        std::cerr << "Error sending typing user name: " << error.message() << std::endl;
        return;
    }

    std::uint32_t typingState = isTyping ? 1U : 0U;
    boost::asio::write(socket, boost::asio::buffer(&typingState, sizeof(typingState)), error);
    if (error) {
        std::cerr << "Error sending typing state: " << error.message() << std::endl;
    }
}

void SendOnlineUsersSnapshot(ServerContext& context, tcp::socket& socket, const std::string& currentUserId) {
    std::vector<std::string> onlineUsers;
    {
        std::lock_guard<std::mutex> lock(context.socketMutex);
        for (const auto& entry : context.receiveSockets) {
            if (entry.first == currentUserId) {
                continue;
            }

            tcp::socket* onlineSocket = entry.second;
            if (onlineSocket != nullptr && onlineSocket->is_open()) {
                onlineUsers.push_back(entry.first);
            }
        }
    }

    for (const std::string& onlineUser : onlineUsers) {
        SendPresenceUpdate(socket, onlineUser, true);
    }
}

void BroadcastPresenceUpdate(ServerContext& context, const std::string& changedUserId, bool isOnline) {
    std::vector<tcp::socket*> targets;
    {
        std::lock_guard<std::mutex> lock(context.socketMutex);
        for (const auto& entry : context.receiveSockets) {
            if (entry.first == changedUserId) {
                continue;
            }

            tcp::socket* targetSocket = entry.second;
            if (targetSocket != nullptr && targetSocket->is_open()) {
                targets.push_back(targetSocket);
            }
        }
    }

    for (tcp::socket* targetSocket : targets) {
        SendPresenceUpdate(*targetSocket, changedUserId, isOnline);
    }
}

bool SendTextPayloadToClient(tcp::socket& socket, const std::string& senderName, const std::string& messagePayload) {
    boost::system::error_code error;
    std::uint32_t messageType = MessageType::RECEIVE_TEXT;
    boost::asio::write(socket, boost::asio::buffer(&messageType, sizeof(messageType)), error);
    if (error) {
        std::cerr << "Error sending message type: " << error.message() << std::endl;
        return false;
    }

    std::uint32_t senderLength = static_cast<std::uint32_t>(senderName.size());
    boost::asio::write(socket, boost::asio::buffer(&senderLength, sizeof(senderLength)), error);
    boost::asio::write(socket, boost::asio::buffer(senderName), error);
    if (error) {
        std::cerr << "Error sending sender name: " << error.message() << std::endl;
        return false;
    }

    std::uint32_t messageLength = static_cast<std::uint32_t>(messagePayload.size());
    boost::asio::write(socket, boost::asio::buffer(&messageLength, sizeof(messageLength)), error);
    boost::asio::write(socket, boost::asio::buffer(messagePayload), error);
    if (error) {
        std::cerr << "Error sending message payload: " << error.message() << std::endl;
        return false;
    }

    return true;
}

} // namespace MessagingServer
