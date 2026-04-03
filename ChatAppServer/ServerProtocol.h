#pragma once

#include "ServerContext.h"

#include <boost/system/error_code.hpp>

namespace MessagingServer {

bool ReadSizedString(tcp::socket& socket, std::string& value, boost::system::error_code& error);
bool WriteSizedString(tcp::socket& socket, const std::string& value, boost::system::error_code& error);

void SendPresenceUpdate(tcp::socket& socket, const std::string& userName, bool isOnline);
void SendTypingIndicator(tcp::socket& socket, const std::string& userName, bool isTyping);
void SendOnlineUsersSnapshot(ServerContext& context, tcp::socket& socket, const std::string& currentUserId);
void BroadcastPresenceUpdate(ServerContext& context, const std::string& changedUserId, bool isOnline);

bool SendTextPayloadToClient(tcp::socket& socket, const std::string& senderName, const std::string& messagePayload);

} // namespace MessagingServer
