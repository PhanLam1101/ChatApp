#pragma once

#include "ServerContext.h"

#include <boost/asio.hpp>

namespace MessagingServer {

void HandleClient(
    ServerContext& context,
    boost::asio::io_context& ioContext,
    tcp::acceptor& acceptor,
    tcp::socket sendSocket,
    tcp::socket receiveSocket);

} // namespace MessagingServer
