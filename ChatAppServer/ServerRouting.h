#pragma once

#include "ServerContext.h"

namespace MessagingServer {

bool HandleReceivedMessage(ServerContext& context, tcp::socket& senderSocket, const std::string& message);

} // namespace MessagingServer
