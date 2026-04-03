#pragma once

#include "ServerConfig.h"

#include <boost/asio.hpp>

#include <mutex>
#include <set>
#include <string>
#include <unordered_map>

namespace MessagingServer {

using boost::asio::ip::tcp;

struct ServerContext {
    std::unordered_map<std::string, std::string> clientCredentials;
    std::unordered_map<std::string, std::string> clientPublicKeys;
    std::unordered_map<std::string, tcp::socket*> sendSockets;
    std::unordered_map<std::string, tcp::socket*> receiveSockets;
    std::unordered_map<std::string, std::string> activeChatBotModelByUser;
    std::unordered_map<std::string, std::size_t> activeChatBotUsersByModel;

    std::mutex credentialsMutex;
    std::mutex publicKeysMutex;
    std::mutex socketMutex;
    std::mutex chatBotStateMutex;
};

} // namespace MessagingServer
