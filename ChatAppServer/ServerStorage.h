#pragma once

#include "ServerContext.h"

namespace MessagingServer {

void LoadCredentials(ServerContext& context);
void SaveCredentials(const ServerContext& context);
void LoadPublicKeys(ServerContext& context);
void SavePublicKeys(const ServerContext& context);

void StoreMessagesInServer(const std::string& senderName, const std::string& recipientName, const std::string& message);
void WriteToFile(const std::string& filename, const std::string& content);
void CreateUpdateFile(const std::string& previousFile, const std::string& offlineFile, const std::string& updateFile);

void HandleChangePassword(ServerContext& context, tcp::socket& socket, const std::string& currentUserId);
void HandleAddFriend(ServerContext& context, tcp::socket& socket);
void HandlePublicKeySync(ServerContext& context, tcp::socket& socket, const std::string& currentUserId);
void HandlePublicKeyRequest(ServerContext& context, tcp::socket& socket);
void RefreshConversations(tcp::socket& sendSocket, const std::string& userName);
bool AuthenticateClient(ServerContext& context, tcp::socket& socket, std::string& clientId);
void HandleUpdateMessage(tcp::socket& socket, const std::string& recipient);
void RegisterClient(ServerContext& context, tcp::socket& socket);

} // namespace MessagingServer
