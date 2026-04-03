#include "ClientSession.h"
#include "ServerChatBot.h"
#include "ServerConfig.h"
#include "ServerContext.h"
#include "ServerStorage.h"

#include <boost/asio.hpp>

#include <iostream>
#include <thread>
#include <utility>
#include <windows.h>

namespace MessagingServer {

namespace {

ServerContext* g_serverContext = nullptr;

BOOL WINAPI HandleServerConsoleSignal(DWORD signal) {
    switch (signal) {
    case CTRL_C_EVENT:
    case CTRL_BREAK_EVENT:
    case CTRL_CLOSE_EVENT:
    case CTRL_LOGOFF_EVENT:
    case CTRL_SHUTDOWN_EVENT:
        if (g_serverContext != nullptr) {
            ReleaseAllChatBotResources(*g_serverContext);
        }
        return FALSE;
    default:
        return FALSE;
    }
}

} // namespace

int RunServer() {
    ServerContext context;
    g_serverContext = &context;
    SetConsoleCtrlHandler(HandleServerConsoleSignal, TRUE);
    LoadCredentials(context);
    LoadPublicKeys(context);

    try {
        boost::asio::io_context ioContext;
        tcp::acceptor acceptor(ioContext, tcp::endpoint(tcp::v4(), kPort));
        std::cout << "Server is listening on port " << kPort << std::endl;

        while (true) {
            tcp::socket sendSocket(ioContext);
            tcp::socket receiveSocket(ioContext);

            acceptor.accept(sendSocket);
            acceptor.accept(receiveSocket);

            std::thread clientThread(
                HandleClient,
                std::ref(context),
                std::ref(ioContext),
                std::ref(acceptor),
                std::move(sendSocket),
                std::move(receiveSocket));
            clientThread.detach();

            std::cout << "Client connected..." << std::endl;
        }
    }
    catch (const std::exception& exception) {
        ReleaseAllChatBotResources(context);
        g_serverContext = nullptr;
        std::cerr << "Exception in server: " << exception.what() << std::endl;
        return 1;
    }

    ReleaseAllChatBotResources(context);
    g_serverContext = nullptr;
    return 0;
}

} // namespace MessagingServer

int main() {
    return MessagingServer::RunServer();
}
