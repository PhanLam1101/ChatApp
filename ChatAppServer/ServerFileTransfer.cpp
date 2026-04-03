#include "ServerFileTransfer.h"

#include "ServerConfig.h"
#include "ForServer.h"

#include <boost/asio/deadline_timer.hpp>
#include <boost/filesystem.hpp>

#include <algorithm>
#include <chrono>
#include <fstream>
#include <iostream>
#include <thread>

namespace MessagingServer {
namespace fs = boost::filesystem;

void StoreFileForOfflineRecipient(
    tcp::socket& socket,
    const std::string& recipient,
    const std::string& sender,
    const std::string& fileName,
    const std::uint32_t& fileSize) {
    boost::system::error_code error;
    fs::create_directory(kOfflineFilesDirectory);

    std::string filePath = std::string(kOfflineFilesDirectory) + "/" + recipient + "-" + sender + "-" + fileName;
    std::ofstream outfile(filePath, std::ios::binary);

    char buffer[kBufferSize];
    std::uint32_t bytesRead = 0;
    while (bytesRead < fileSize) {
        size_t toRead = std::min(static_cast<size_t>(kBufferSize), static_cast<size_t>(fileSize - bytesRead));
        size_t length = socket.read_some(boost::asio::buffer(buffer, toRead), error);
        if (error && error != boost::asio::error::eof) {
            std::cerr << "Error reading file data: " << error.message() << std::endl;
            return;
        }
        outfile.write(buffer, length);
        bytesRead += static_cast<std::uint32_t>(length);
    }

    std::cout << "Received file: " << fileName << " from client: " << sender << std::endl;
    std::cout << "Stored file for offline recipient: " << filePath << std::endl;

    std::uint32_t fileSent = MessageType::File;
    boost::asio::write(socket, boost::asio::buffer(&fileSent, sizeof(fileSent)), error);
}

void StoreFileForOnlineRecipient(
    tcp::socket& socket,
    const std::string& recipient,
    const std::string& sender,
    const std::string& fileName,
    const std::uint32_t& fileSize) {
    boost::system::error_code error;
    fs::create_directory(kOnlineFilesDirectory);

    std::string filePath = std::string(kOnlineFilesDirectory) + "/" + recipient + "-" + sender + "-" + fileName;
    std::ofstream outfile(filePath, std::ios::binary);

    char buffer[kBufferSize];
    std::uint32_t bytesRead = 0;
    while (bytesRead < fileSize) {
        size_t toRead = std::min(static_cast<size_t>(kBufferSize), static_cast<size_t>(fileSize - bytesRead));
        size_t length = socket.read_some(boost::asio::buffer(buffer, toRead), error);
        if (error && error != boost::asio::error::eof) {
            std::cerr << "Error reading file data: " << error.message() << std::endl;
            return;
        }
        outfile.write(buffer, length);
        bytesRead += static_cast<std::uint32_t>(length);
    }

    std::cout << "Received file: " << fileName << " from client: " << sender << std::endl;
    std::cout << "Stored file for online recipient: " << filePath << std::endl;

    std::uint32_t fileSent = MessageType::File;
    boost::asio::write(socket, boost::asio::buffer(&fileSent, sizeof(fileSent)), error);
}

void DeliverPendingFiles(
    boost::asio::io_context& ioContext,
    tcp::acceptor& acceptor,
    tcp::socket& recipientSocket,
    const std::string& recipient,
    const std::string& directoryName) {
    std::cout << "Delivering pending files to " << recipient << std::endl;
    std::cout << "io_context: " << ioContextToString(ioContext) << std::endl;
    std::cout << "sender_socket: " << socketToString(recipientSocket) << std::endl;
    std::cout << "acceptor: " << acceptorToString(acceptor) << std::endl;

    boost::system::error_code error;
    if (!recipientSocket.is_open()) {
        std::cerr << "Error: recipient_socket is not open." << std::endl;
        return;
    }

    std::uint32_t messageType = MessageType::UPDATE_File;
    boost::asio::write(recipientSocket, boost::asio::buffer(&messageType, sizeof(messageType)), error);
    if (error) {
        std::cerr << "Error sending UPDATE_File signal: " << error.message() << std::endl;
        return;
    }

    if (!fs::exists(directoryName)) {
        std::uint32_t endUpdateSignal = MessageType::END_UPDATE;
        boost::asio::write(recipientSocket, boost::asio::buffer(&endUpdateSignal, sizeof(endUpdateSignal)), error);
        return;
    }

    for (const auto& entry : fs::directory_iterator(directoryName)) {
        std::string filename = entry.path().filename().string();
        if (filename.find(recipient + "-") != 0) {
            continue;
        }

        size_t firstHyphen = filename.find("-");
        size_t secondHyphen = filename.find("-", firstHyphen + 1);
        if (firstHyphen == std::string::npos || secondHyphen == std::string::npos) {
            std::cerr << "Error: Invalid filename format: " << filename << std::endl;
            continue;
        }

        std::string sender = filename.substr(firstHyphen + 1, secondHyphen - firstHyphen - 1);
        std::string actualFileName = filename.substr(secondHyphen + 1);
        std::uint32_t fileNameLength = static_cast<std::uint32_t>(actualFileName.size());
        std::uint32_t fileSize = static_cast<std::uint32_t>(fs::file_size(entry.path()));

        std::cout << "Sending file metadata for: " << actualFileName << std::endl;
        std::cout << "Parsed filename: " << filename << "\n";
        std::cout << "Recipient: " << recipient << "\n";
        std::cout << "Sender: " << sender << "\n";
        std::cout << "Actual File Name: " << actualFileName << "\n";

        std::uint32_t responseType = MessageType::UPDATE_File;
        boost::asio::write(recipientSocket, boost::asio::buffer(&responseType, sizeof(responseType)), error);
        if (error) {
            std::cerr << "Error sending response: " << error.message() << std::endl;
            return;
        }
        boost::asio::write(recipientSocket, boost::asio::buffer(&fileNameLength, sizeof(fileNameLength)), error);
        if (error) {
            std::cerr << "Error sending file_name_length: " << error.message() << std::endl;
            return;
        }
        boost::asio::write(recipientSocket, boost::asio::buffer(actualFileName), error);
        if (error) {
            std::cerr << "Error sending file name: " << error.message() << std::endl;
            return;
        }
        boost::asio::write(recipientSocket, boost::asio::buffer(&fileSize, sizeof(fileSize)), error);
        if (error) {
            std::cerr << "Error sending file size: " << error.message() << std::endl;
            return;
        }

        tcp::socket fileSocket(ioContext);
        boost::asio::deadline_timer timer(ioContext, boost::posix_time::seconds(5));
        bool connectionEstablished = false;

        acceptor.async_accept(fileSocket, [&connectionEstablished, &error](const boost::system::error_code& ec) {
            error = ec;
            connectionEstablished = !error;
        });

        ioContext.restart();
        while (ioContext.run_one()) {
            if (connectionEstablished) {
                std::cout << "File socket established successfully for file transfer." << std::endl;
                break;
            }
            if (timer.expires_from_now() <= boost::posix_time::seconds(0)) {
                std::cerr << "Accept operation timed out." << std::endl;
                acceptor.cancel();
                return;
            }
        }

        if (!connectionEstablished) {
            std::cerr << "Failed to establish file socket connection after timeout." << std::endl;
            return;
        }

        std::ifstream file(entry.path().string(), std::ios::binary);
        char transferBuffer[kBufferSize];
        while (file.read(transferBuffer, sizeof(transferBuffer)) || file.gcount() > 0) {
            boost::asio::write(fileSocket, boost::asio::buffer(transferBuffer, file.gcount()), error);
            if (error) {
                std::cerr << "Error sending file content: " << error.message() << std::endl;
                return;
            }
        }

        file.close();
        fileSocket.close();
        fs::remove(entry.path());
        std::cout << "Delivered and removed pending file: " << filename << std::endl;
    }

    std::uint32_t endUpdateSignal = MessageType::END_UPDATE;
    boost::asio::write(recipientSocket, boost::asio::buffer(&endUpdateSignal, sizeof(endUpdateSignal)), error);
    if (error) {
        std::cerr << "Error sending END_UPDATE signal: " << error.message() << std::endl;
    }
    else {
        std::cout << "All files delivered, sent END_UPDATE signal." << std::endl;
    }
}

void RelayFileToRecipient(
    boost::asio::io_context& ioContext,
    tcp::acceptor& acceptor,
    tcp::socket& senderSocket,
    const std::string& sender,
    const std::string& recipient,
    const std::string& fileName,
    std::uint32_t fileSize,
    tcp::socket& recipientSocket) {
    StoreFileForOnlineRecipient(senderSocket, recipient, sender, fileName, fileSize);

    std::this_thread::sleep_for(std::chrono::milliseconds(500));
    DeliverPendingFiles(ioContext, acceptor, recipientSocket, recipient, kOnlineFilesDirectory);
}

} // namespace MessagingServer
