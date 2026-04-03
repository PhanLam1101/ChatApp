#pragma once

#include "ServerContext.h"

#include <boost/asio.hpp>

namespace MessagingServer {

void StoreFileForOfflineRecipient(
    tcp::socket& socket,
    const std::string& recipient,
    const std::string& sender,
    const std::string& fileName,
    const std::uint32_t& fileSize);

void StoreFileForOnlineRecipient(
    tcp::socket& socket,
    const std::string& recipient,
    const std::string& sender,
    const std::string& fileName,
    const std::uint32_t& fileSize);

void DeliverPendingFiles(
    boost::asio::io_context& ioContext,
    tcp::acceptor& acceptor,
    tcp::socket& recipientSocket,
    const std::string& recipient,
    const std::string& directoryName);

void RelayFileToRecipient(
    boost::asio::io_context& ioContext,
    tcp::acceptor& acceptor,
    tcp::socket& senderSocket,
    const std::string& sender,
    const std::string& recipient,
    const std::string& fileName,
    std::uint32_t fileSize,
    tcp::socket& recipientSocket);

} // namespace MessagingServer
