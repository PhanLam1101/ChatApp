#include "ClientSession.h"

#include "ServerChatBot.h"
#include "ServerConfig.h"
#include "ServerFileTransfer.h"
#include "ServerProtocol.h"
#include "ServerRouting.h"
#include "ServerStorage.h"
#include "ForServer.h"

#include <iostream>
#include <memory>
#include <utility>
#include <vector>

namespace MessagingServer {

void HandleClient(
    ServerContext& context,
    boost::asio::io_context& ioContext,
    tcp::acceptor& acceptor,
    tcp::socket sendSocket,
    tcp::socket receiveSocket) {
    std::string clientId;
    bool isAuthenticated = false;

    try {
        boost::system::error_code error;

        std::uint32_t messageType = 0;
        boost::asio::read(sendSocket, boost::asio::buffer(&messageType, sizeof(messageType)), error);
        std::cout << "1st thing from client: " << messageType << std::endl;
        if (error == boost::asio::error::eof) {
            return;
        }
        if (error) {
            throw boost::system::system_error(error);
        }

        if (messageType == MessageType::REGISTRATION) {
            RegisterClient(context, sendSocket);
        }
        else {
            if (!AuthenticateClient(context, sendSocket, clientId)) {
                return;
            }

            {
                std::lock_guard<std::mutex> lock(context.socketMutex);
                std::cout << "\n#user_name: " << clientId << std::endl;
                std::cout << "io_context: " << ioContextToString(ioContext) << std::endl;
                std::cout << "send_socket: " << socketToString(sendSocket) << std::endl;
                std::cout << "receive_socket: " << socketToString(receiveSocket) << std::endl;
                std::cout << "acceptor: " << acceptorToString(acceptor) << "#\n" << std::endl;
                context.sendSockets[clientId] = &sendSocket;
                context.receiveSockets[clientId] = &receiveSocket;
            }

            isAuthenticated = true;
            SendOnlineUsersSnapshot(context, receiveSocket, clientId);
            BroadcastPresenceUpdate(context, clientId, true);

            std::cout << "Delivering updates to " << clientId << std::endl;
            HandleUpdateMessage(receiveSocket, clientId);
            DeliverPendingFiles(ioContext, acceptor, receiveSocket, clientId, kOfflineFilesDirectory);

            while (true) {
                std::uint32_t requestType = 0;
                boost::asio::read(sendSocket, boost::asio::buffer(&requestType, sizeof(requestType)), error);
                if (error == boost::asio::error::eof) {
                    break;
                }
                if (error) {
                    throw boost::system::system_error(error);
                }

                std::cout << "Received message type: " << requestType << std::endl;

                if (requestType == MessageType::TEXT) {
                    std::uint32_t messageLength = 0;
                    boost::asio::read(sendSocket, boost::asio::buffer(&messageLength, sizeof(messageLength)), error);
                    if (error) {
                        break;
                    }

                    std::vector<char> messageBuffer(messageLength);
                    boost::asio::read(sendSocket, boost::asio::buffer(messageBuffer), error);
                    if (error) {
                        break;
                    }

                    std::string message(messageBuffer.begin(), messageBuffer.end());
                    std::cout << "Received message from user: " << message << std::endl;
                    HandleReceivedMessage(context, sendSocket, message);
                }
                else if (requestType == MessageType::File) {
                    std::uint32_t recipientLength = 0;
                    boost::asio::read(sendSocket, boost::asio::buffer(&recipientLength, sizeof(recipientLength)), error);
                    if (error) {
                        break;
                    }

                    std::vector<char> recipientBuffer(recipientLength);
                    boost::asio::read(sendSocket, boost::asio::buffer(recipientBuffer), error);
                    if (error) {
                        break;
                    }
                    std::string recipient(recipientBuffer.begin(), recipientBuffer.end());
                    std::cout << "Recipient: " << recipient << std::endl;

                    std::uint32_t fileNameLength = 0;
                    boost::asio::read(sendSocket, boost::asio::buffer(&fileNameLength, sizeof(fileNameLength)), error);
                    if (error) {
                        break;
                    }

                    std::vector<char> fileNameBuffer(fileNameLength);
                    boost::asio::read(sendSocket, boost::asio::buffer(fileNameBuffer), error);
                    if (error) {
                        break;
                    }
                    std::string fileName(fileNameBuffer.begin(), fileNameBuffer.end());
                    std::cout << "File name: " << fileName << std::endl;

                    std::uint32_t fileSize = 0;
                    boost::asio::read(sendSocket, boost::asio::buffer(&fileSize, sizeof(fileSize)), error);
                    if (error) {
                        break;
                    }

                    tcp::socket* recipientSocket = nullptr;
                    {
                        std::lock_guard<std::mutex> lock(context.socketMutex);
                        auto recipientIt = context.receiveSockets.find(recipient);
                        if (recipientIt != context.receiveSockets.end()) {
                            recipientSocket = recipientIt->second;
                        }
                    }

                    if (recipientSocket != nullptr) {
                        RelayFileToRecipient(ioContext, acceptor, sendSocket, clientId, recipient, fileName, fileSize, *recipientSocket);
                    }
                    else {
                        StoreFileForOfflineRecipient(sendSocket, recipient, clientId, fileName, fileSize);
                    }
                }
                else if (requestType == MessageType::REFRESH) {
                    RefreshConversations(sendSocket, clientId);
                }
                else if (requestType == MessageType::CHANGE_PASSWORD) {
                    HandleChangePassword(context, sendSocket, clientId);
                }
                else if (requestType == MessageType::ADD_FRIENDS) {
                    HandleAddFriend(context, sendSocket);
                }
                else if (requestType == MessageType::PUBLIC_KEY_REQUEST) {
                    HandlePublicKeyRequest(context, sendSocket);
                }
                else if (requestType == MessageType::PUBLIC_KEY_SYNC) {
                    HandlePublicKeySync(context, sendSocket, clientId);
                }
                else if (requestType == MessageType::TYPING_INDICATOR) {
                    std::string recipient;
                    if (!ReadSizedString(sendSocket, recipient, error)) {
                        break;
                    }

                    std::uint32_t typingState = 0;
                    boost::asio::read(sendSocket, boost::asio::buffer(&typingState, sizeof(typingState)), error);
                    if (error) {
                        break;
                    }

                    tcp::socket* recipientSocket = nullptr;
                    {
                        std::lock_guard<std::mutex> lock(context.socketMutex);
                        auto recipientIt = context.receiveSockets.find(recipient);
                        if (recipientIt != context.receiveSockets.end()) {
                            recipientSocket = recipientIt->second;
                        }
                    }

                    if (recipientSocket != nullptr && recipientSocket->is_open()) {
                        SendTypingIndicator(*recipientSocket, clientId, typingState == 1U);
                    }
                }
                else if (requestType == MessageType::CLEAR_CHATBOT_MEMORY) {
                    const bool cleared = ClearChatBotConversation(clientId);
                    const std::uint32_t response = cleared ? 1U : 0U;
                    boost::asio::write(sendSocket, boost::asio::buffer(&response, sizeof(response)), error);
                    if (error) {
                        break;
                    }
                }
            }
        }
    }
    catch (const std::exception& exception) {
        std::cerr << "Exception in handling client: " << exception.what() << std::endl;
    }

    if (isAuthenticated && !clientId.empty()) {
        std::cout << "User " << clientId << " is disconnecting..." << std::endl;
        ReleaseChatBotResourcesForUser(context, clientId);
        {
            std::lock_guard<std::mutex> lock(context.socketMutex);
            context.sendSockets.erase(clientId);
            context.receiveSockets.erase(clientId);
        }
        BroadcastPresenceUpdate(context, clientId, false);
        std::cout << "User " << clientId << " is disconnected!" << std::endl;
    }
}

} // namespace MessagingServer
