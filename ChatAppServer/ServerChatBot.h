#pragma once

#include "ServerContext.h"

namespace MessagingServer {

bool HandleChatBotMessage(
    ServerContext& context,
    tcp::socket& senderSocket,
    const std::string& sender,
    const std::string& recipientToken,
    const std::string& messagePayload);

void ReleaseChatBotResourcesForUser(ServerContext& context, const std::string& userName);
void ReleaseAllChatBotResources(ServerContext& context);
bool ClearChatBotConversation(const std::string& userName);

} // namespace MessagingServer
