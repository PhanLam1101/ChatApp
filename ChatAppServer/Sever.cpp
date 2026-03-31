#include <iostream>
#include <fstream>
#include <sstream>
#include <cstdio>

#include <boost/asio.hpp>
#include <boost/filesystem.hpp>
#include <boost/asio/deadline_timer.hpp>

#include <thread>
#include <vector>
#include <unordered_map>
#include <filesystem>
#include <string>
#include <mutex>
#include <map>
#include <algorithm>
#include <ctime>
#include <set>
#include <memory>
#include "Huffman.h"
#include "ForServer.h"

using boost::asio::ip::tcp;
namespace fs = boost::filesystem;

const int PORT = 80;
const int BUFFER_SIZE = 4096;
const std::string CREDENTIALS_FILE = "Account.txt";
const std::string PENDING_MESSAGE = "PendingMessages";
enum MessageType { TEXT = 1, UPDATE_Message = 2, File = 3, UPDATE_File = 4, RECEIVE_TEXT = 5, RECEIVE_File = 6, REGISTRATION = 7, NOT_REGISTRATION = 8, END_UPDATE = 9, REFRESH = 10, ADD_FRIENDS = 11, CHANGE_PASSWORD = 12 };

std::unordered_map<std::string, std::string> client_credentials;
std::unordered_map<std::string, tcp::socket*> send_sockets;  // Stores send sockets
std::unordered_map<std::string, tcp::socket*> receive_sockets;  // Stores receive sockets
//std::unordered_map<std::string, tcp::acceptor*> acceptors_map;
//std::unordered_map<std::string, boost::asio::io_context*> io_context_map;

std::mutex credentials_mutex;
std::mutex socket_mutex;

void handleChangePassword(tcp::socket& socket, string& current_user_id) {
    boost::system::error_code error;

    // Step 1: Receive current password length and current password
    uint32_t currentPasswordLength;
    boost::asio::read(socket, boost::asio::buffer(&currentPasswordLength, sizeof(currentPasswordLength)), error);
    if (error) {
        std::cerr << "Error receiving current password length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> currentPasswordBuffer(currentPasswordLength);
    boost::asio::read(socket, boost::asio::buffer(currentPasswordBuffer), error);
    if (error) {
        std::cerr << "Error receiving current password: " << error.message() << std::endl;
        return;
    }
    std::string currentPassword(currentPasswordBuffer.begin(), currentPasswordBuffer.end());

    // Step 2: Receive new password length and new password
    uint32_t newPasswordLength;
    boost::asio::read(socket, boost::asio::buffer(&newPasswordLength, sizeof(newPasswordLength)), error);
    if (error) {
        std::cerr << "Error receiving new password length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> newPasswordBuffer(newPasswordLength);
    boost::asio::read(socket, boost::asio::buffer(newPasswordBuffer), error);
    if (error) {
        std::cerr << "Error receiving new password: " << error.message() << std::endl;
        return;
    }
    std::string newPassword(newPasswordBuffer.begin(), newPasswordBuffer.end());

    // Step 3: Validate current password
    {
        std::lock_guard<std::mutex> lock(credentials_mutex); // Lock for thread safety
        auto userIt = client_credentials.find(current_user_id); // Retrieve user ID for the current session
        if (userIt == client_credentials.end() || userIt->second != currentPassword) {
            // If user not found or password doesn't match, send failure response
            uint32_t response = 0;
            boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
            std::cout << "Password change failed for user " << current_user_id << std::endl;
            return;
        }
    }

    // Step 4: Update password in memory and in Account.txt
    client_credentials[current_user_id] = newPassword;

    // Update Account.txt file
    std::ofstream accountFile("Account.txt");
    if (accountFile.is_open()) {
        for (const auto& entry : client_credentials) {
            accountFile << entry.first << " " << entry.second << std::endl;
        }
        accountFile.close();
    }
    else {
        std::cerr << "Failed to open Account.txt for updating password." << std::endl;
    }

    // Step 5: Send success response
    uint32_t response = 1;
    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
    std::cout << "Password successfully changed for user " << current_user_id << std::endl;
}

void load_credentials() {
    std::ifstream infile(CREDENTIALS_FILE.c_str());
    if (infile.is_open()) {
        std::string id, password;
        while (infile >> id >> password) {
            client_credentials[id] = password;
        }
        infile.close();
    }
}

void save_credentials() {
    std::ofstream outfile(CREDENTIALS_FILE.c_str());
    for (const auto& pair : client_credentials) {
        outfile << pair.first << " " << pair.second << std::endl;
    }
    outfile.close();
}

void storeMessagesInServer(const std::string& sender_name, const std::string& recipient_name, const std::string& message) {
    std::string conv_dir = "Conversations";
    fs::create_directory(conv_dir);  // Create directory if it doesn’t exist
    std::string file_path = conv_dir + "/" + GetConversationFileName(sender_name, recipient_name) + ".bin";

    std::ofstream outfile(file_path, std::ios::app);
    outfile << message << "\n";
    outfile.close();
}
void handleAddFriend(tcp::socket& socket) {
    boost::system::error_code error;

    // Step 1: Read the length of the friend's name
    uint32_t nameLength;
    boost::asio::read(socket, boost::asio::buffer(&nameLength, sizeof(nameLength)), error);
    if (error) {
        std::cerr << "Error reading friend's name length: " << error.message() << std::endl;
        return;
    }

    // Step 2: Read the friend's name
    std::vector<char> nameBuffer(nameLength);
    boost::asio::read(socket, boost::asio::buffer(nameBuffer), error);
    if (error) {
        std::cerr << "Error reading friend's name: " << error.message() << std::endl;
        return;
    }
    std::string friendName(nameBuffer.begin(), nameBuffer.end());

    // Step 3: Check if friend exists in the credentials map
    uint32_t response;
    if (client_credentials.find(friendName) != client_credentials.end()) {
        // Friend exists, send success response
        response = 1;
    }
    else {
        // Friend does not exist, send failure response
        response = 0;
    }

    // Step 4: Send the response back to the client
    boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error sending response to client: " << error.message() << std::endl;
    }

    // If successful, log the friend addition (optional)
    if (response == 1) {
        std::cout << "Friend " << friendName << " added successfully." << std::endl;
    }
    else {
        std::cout << "Friend " << friendName << " not found in system." << std::endl;
    }
}
void refreshConversations(tcp::socket& send_socket, const std::string& user_name) {
    boost::system::error_code error;
    std::string conv_dir = "Conversations";

    // Ensure the conversations directory exists
    if (!fs::exists(conv_dir)) {
        std::cerr << "Conversations directory not found." << std::endl;
        return;
    }

    // Iterate over all files in the Conversations directory
    for (const auto& entry : fs::directory_iterator(conv_dir)) {
        std::string filename = entry.path().filename().string();

        // Check if the file name contains the user's name
        size_t user_pos = filename.find(user_name);
        if (user_pos == std::string::npos) continue;

        // Identify the other participant in the conversation
        std::string other_user;
        if (user_pos == 0) {
            other_user = filename.substr(user_name.size() + 1, filename.find('.') - user_name.size() - 1);
        }
        else {
            other_user = filename.substr(0, user_pos - 1);
        }

        // Read each line (message) from the conversation file
        std::ifstream conv_file(entry.path().string());
        std::string message;
        while (std::getline(conv_file, message)) {
            // Send `UPDATE_Message` type signal
            uint32_t message_type = MessageType::RECEIVE_TEXT;
            boost::asio::write(send_socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
            if (error) {
                std::cerr << "Error sending message type: " << error.message() << std::endl;
                return;
            }

            // Send the other participant's name length and name
            uint32_t name_length = static_cast<uint32_t>(other_user.size());
            boost::asio::write(send_socket, boost::asio::buffer(&name_length, sizeof(name_length)), error);
            boost::asio::write(send_socket, boost::asio::buffer(other_user), error);

            // Send message length and message content
            uint32_t message_length = static_cast<uint32_t>(message.size());
            boost::asio::write(send_socket, boost::asio::buffer(&message_length, sizeof(message_length)), error);
            boost::asio::write(send_socket, boost::asio::buffer(message), error);

            if (error) {
                std::cerr << "Error sending message content: " << error.message() << std::endl;
                return;
            }
        }
    }

    // Send END_UPDATE signal to indicate completion
    uint32_t end_update_signal = MessageType::END_UPDATE;
    boost::asio::write(send_socket, boost::asio::buffer(&end_update_signal, sizeof(end_update_signal)), error);
    if (error) {
        std::cerr << "Error sending END_UPDATE signal: " << error.message() << std::endl;
    }
}
void storeFileForOfflineRecipient(tcp::socket& socket, const std::string& recipient, const std::string& sender, const std::string& file_name, const uint32_t& file_size) {
    boost::system::error_code error;
    std::string offline_dir = "OfflineFiles";
    fs::create_directory(offline_dir);  // Create directory if it doesn’t exist

    std::string file_path = offline_dir + "/" + recipient + "-" + sender + "-" + file_name;
    std::ofstream outfile(file_path, std::ios::binary);

    char buffer[BUFFER_SIZE];

    uint32_t bytes_read = 0;
    while (bytes_read < file_size) {
        size_t to_read = std::min(static_cast<size_t>(BUFFER_SIZE), static_cast<size_t>(file_size - bytes_read));
        size_t len = socket.read_some(boost::asio::buffer(buffer, to_read), error);
        if (error && error != boost::asio::error::eof) {
            std::cerr << "Error reading file data: " << error.message() << std::endl;
            return;
        }
        outfile.write(buffer, len);
        bytes_read += len;
    }
    outfile.close();
    std::cout << "Received file: " << file_name << " from client: " << sender << std::endl;

    std::cout << "Stored file for offline recipient: " << file_path << std::endl;
    uint32_t file_sent = MessageType::File;
    boost::asio::write(socket, boost::asio::buffer(&file_sent, sizeof(file_sent)), error);
}
void storeFileForOnlineRecipient(tcp::socket& socket, const std::string& recipient, const std::string& sender, const std::string& file_name, const uint32_t& file_size) {
    boost::system::error_code error;
    std::string offline_dir = "OnlineFiles";
    fs::create_directory(offline_dir);  // Create directory if it doesn’t exist

    std::string file_path = offline_dir + "/" + recipient + "-" + sender + "-" + file_name;
    std::ofstream outfile(file_path, std::ios::binary);

    char buffer[BUFFER_SIZE];

    uint32_t bytes_read = 0;
    while (bytes_read < file_size) {
        size_t to_read = std::min(static_cast<size_t>(BUFFER_SIZE), static_cast<size_t>(file_size - bytes_read));
        size_t len = socket.read_some(boost::asio::buffer(buffer, to_read), error);
        if (error && error != boost::asio::error::eof) {
            std::cerr << "Error reading file data: " << error.message() << std::endl;
            return;
        }
        outfile.write(buffer, len);
        bytes_read += len;
    }
    outfile.close();
    std::cout << "Received file: " << file_name << " from client: " << sender << std::endl;

    std::cout << "Stored file for online recipient: " << file_path << std::endl;
    uint32_t file_sent = MessageType::File;
    boost::asio::write(socket, boost::asio::buffer(&file_sent, sizeof(file_sent)), error);
}
// Modified function to handle file delivery with a timeout on accept
void deliverPendingFiles(boost::asio::io_context& io_context, tcp::acceptor& acceptor,
    tcp::socket& recipient_socket, const std::string& recipient, string& dir_name) {
    std::cout << "Delivering pending files to " << recipient << std::endl;
    cout << "io_context: " << ioContextToString(io_context) << endl;
    cout << "sender_socket: " << socketToString(recipient_socket) << endl;
    cout << "acceptor: " << acceptorToString(acceptor) << endl;
    boost::system::error_code error;

    if (!recipient_socket.is_open()) {
        std::cerr << "Error: recipient_socket is not open." << std::endl;
        return;
    }

    uint32_t message_type = MessageType::UPDATE_File;
    boost::asio::write(recipient_socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
    if (error) {
        std::cerr << "Error sending UPDATE_File signal: " << error.message() << std::endl;
        return;
    }

    for (const auto& entry : fs::directory_iterator(dir_name)) {
        std::string filename = entry.path().filename().string();

        if (filename.find(recipient + "-") == 0) {
            // Locate the positions of the first and last hyphens
            size_t first_hyphen = filename.find("-");
            size_t second_hyphen = filename.find("-", first_hyphen + 1);

            // Validate positions
            if (first_hyphen == std::string::npos || second_hyphen == std::string::npos) {
                std::cerr << "Error: Invalid filename format: " << filename << std::endl;
                continue;
            }

            std::string sender = filename.substr(first_hyphen + 1, second_hyphen - first_hyphen - 1);
            std::string actual_file_name = filename.substr(second_hyphen + 1);
            /*std::string sender = filename.substr(filename.find("-") + 1, filename.rfind("-") - filename.find("-") - 1);
            std::string actual_file_name = filename.substr(filename.rfind("-") + 1);*/

            uint32_t file_name_length = static_cast<uint32_t>(actual_file_name.size());
            uint32_t file_size = static_cast<uint32_t>(fs::file_size(entry.path()));
            cout << "Sending file metadata for: " << actual_file_name << endl;
            // Debugging information
            std::cout << "Parsed filename: " << filename << "\n";
            std::cout << "Recipient: " << recipient << "\n";
            std::cout << "Sender: " << sender << "\n";
            std::cout << "Actual File Name: " << actual_file_name << "\n";

            uint32_t response_type = MessageType::UPDATE_File;
            boost::asio::write(recipient_socket, boost::asio::buffer(&response_type, sizeof(response_type)), error);
            if (error) {
                std::cerr << "Error sending response: " << error.message() << std::endl;
                return;
            }
            boost::asio::write(recipient_socket, boost::asio::buffer(&file_name_length, sizeof(file_name_length)), error);
            if (error) {
                std::cerr << "Error sending file_name_length: " << error.message() << std::endl;
                return;
            }
            boost::asio::write(recipient_socket, boost::asio::buffer(actual_file_name), error);
            if (error) {
                std::cerr << "Error sending file name: " << error.message() << std::endl;
                return;
            }
            boost::asio::write(recipient_socket, boost::asio::buffer(&file_size, sizeof(file_size)), error);

            if (error) {
                std::cerr << "Error sending file size: " << error.message() << std::endl;
                return;
            }

            tcp::socket file_socket(io_context);
            boost::asio::deadline_timer timer(io_context, boost::posix_time::seconds(5));  // 5-second timeout

            bool connection_established = false;

            // Start the async_accept operation
            acceptor.async_accept(file_socket, [&connection_established, &error](const boost::system::error_code& ec) {
                error = ec;
                connection_established = !error;
                });

            // Run the io_context and wait for either the accept to complete or the timer to expire
            io_context.restart();
            while (io_context.run_one()) {
                if (connection_established) {
                    std::cout << "File socket established successfully for file transfer." << std::endl;
                    break;
                }
                if (timer.expires_from_now() <= boost::posix_time::seconds(0)) {
                    std::cerr << "Accept operation timed out." << std::endl;
                    acceptor.cancel();  // Cancel the accept operation if it timed out
                    return;
                }
            }

            if (!connection_established) {
                std::cerr << "Failed to establish file socket connection after timeout." << std::endl;
                return;
            }

            // Transfer file data if the connection was established
            std::ifstream file(entry.path().string(), std::ios::binary);
            char buffer[BUFFER_SIZE];
            while (file.read(buffer, sizeof(buffer)) || file.gcount() > 0) {
                boost::asio::write(file_socket, boost::asio::buffer(buffer, file.gcount()), error);
                if (error) {
                    std::cerr << "Error sending file content: " << error.message() << std::endl;
                    return;
                }
            }
            file.close();
            file_socket.close();
            fs::remove(entry.path());
            std::cout << "Delivered and removed pending file: " << filename << std::endl;
        }
    }

    uint32_t end_update_signal = MessageType::END_UPDATE;
    boost::asio::write(recipient_socket, boost::asio::buffer(&end_update_signal, sizeof(end_update_signal)), error);
    if (error) {
        std::cerr << "Error sending END_UPDATE signal: " << error.message() << std::endl;
    }
    else {
        std::cout << "All files delivered, sent END_UPDATE signal." << std::endl;
    }
}
void relayFileToRecipient(boost::asio::io_context& io_context, tcp::acceptor& acceptor, tcp::socket& sender_socket, const std::string& sender,
    const std::string& recipient, const std::string& file_name, uint32_t file_size, tcp::socket& recipient_socket) {
    // Store file for the recipient
    storeFileForOnlineRecipient(sender_socket, recipient, sender, file_name, file_size);

    // Deliver files using the shared deliverPendingFiles
    std::string online_dir = "OnlineFiles";
    
    std::this_thread::sleep_for(std::chrono::milliseconds(500));
    deliverPendingFiles(io_context, acceptor, *receive_sockets[recipient], recipient, online_dir);
}

void writeToFile(const std::string& filename, const std::string& content) {
    std::ofstream file(filename, std::ios::app); // Append mode
    if (file.is_open()) {
        file << content << "\n";
    }
    file.close();
}

void createUpdateFile(const std::string& prevFile, const std::string& offlineFile, const std::string& updateFile) {
    std::ifstream prev(prevFile);
    std::ifstream offline(offlineFile);
    std::ofstream update(updateFile);

    std::set<std::string> prevMessages;
    std::string line;

    while (std::getline(prev, line)) {
        prevMessages.insert(line);
    }

    while (std::getline(offline, line)) {
        if (prevMessages.find(line) == prevMessages.end()) {
            update << line << "\n";
        }
    }
}

bool handleReceivedMessage(tcp::socket& sender_socket, const std::string& message) {
    boost::system::error_code error;

    // Extract recipient and sender from the message
    size_t delimPos = message.find('-');
    if (delimPos == std::string::npos) return false;
    std::string recipient = message.substr(0, delimPos);

    size_t msgStart = message.find('\n', delimPos + 1);
    if (msgStart == std::string::npos) return false;
    std::string sender = message.substr(delimPos + 1, msgStart - delimPos - 1);
    std::string completedMessage = message.substr(msgStart + 1);

    std::lock_guard<std::mutex> lock(socket_mutex);
    std::cout << "Message from: " << sender << " - Content: " << completedMessage << " - to: " << recipient << std::endl;

    if (receive_sockets.find(recipient) != receive_sockets.end()) {
        auto recipient_socket = receive_sockets[recipient];

        if (!recipient_socket || !recipient_socket->is_open()) {
            std::cerr << "Error: recipient's receive socket is not open or invalid." << std::endl;
            return false;
        }

        uint32_t message_type = MessageType::RECEIVE_TEXT;
        boost::asio::write(*recipient_socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
        if (error) {
            std::cerr << "Error sending message type: " << error.message() << std::endl;
            return false;
        }

        // Send sender name size and name
        uint32_t sender_size = static_cast<uint32_t>(sender.size());
        boost::asio::write(*recipient_socket, boost::asio::buffer(&sender_size, sizeof(sender_size)), error);
        if (error) {
            std::cerr << "Error sending sender size: " << error.message() << std::endl;
            return false;
        }
        boost::asio::write(*recipient_socket, boost::asio::buffer(sender, sender_size), error);
        if (error) {
            std::cerr << "Error sending sender name: " << error.message() << std::endl;
            return false;
        }

        uint32_t completedMessage_size = static_cast<uint32_t>(completedMessage.size());
        boost::asio::write(*recipient_socket, boost::asio::buffer(&completedMessage_size, sizeof(completedMessage_size)), error);
        if (error) {
            std::cerr << "Error sending message size: " << error.message() << std::endl;
            return false;
        }
        boost::asio::write(*recipient_socket, boost::asio::buffer(completedMessage, completedMessage_size), error);
        if (error) {
            std::cerr << "Error sending message content: " << error.message() << std::endl;
            return false;
        }

        uint32_t response = 1;
        boost::asio::write(sender_socket, boost::asio::buffer(&response, sizeof(response)), error);
        if (error) {
            std::cerr << "Error sending confirmation to sender: " << error.message() << std::endl;
            return false;
        }
        storeMessagesInServer(sender, recipient, completedMessage);

    }
    else {
        // Store offline message in "PendingMessages" folder
        std::string pending_dir = "PendingMessages";
        fs::create_directory(pending_dir);
        std::string offlineFile = pending_dir + "/" + recipient + "-" + sender + "-during-offline.bin";
        writeToFile(offlineFile, completedMessage);

        uint32_t response = 1;
        boost::asio::write(sender_socket, boost::asio::buffer(&response, sizeof(response)), error);
        if (error) {
            std::cerr << "Error sending confirmation to sender: " << error.message() << std::endl;
            return false;
        }

        storeMessagesInServer(sender, recipient, completedMessage);
    }

    return true;
}



void handleOfflineMessages(const std::string& recipient, const std::string& sender) {
    std::string offlineFile = recipient + "-" + sender + "-during-offline.bin";
    std::string encryptedFile = recipient + "-" + sender + "-encrypted.bin";
    std::string updateFile = recipient + "-" + sender + "-update-message.bin";

    createUpdateFile(encryptedFile, offlineFile, updateFile);

    std::ifstream update(updateFile);
    std::string line;
    while (std::getline(update, line)) {
        boost::asio::write(*receive_sockets[recipient], boost::asio::buffer(line));
    }
}

bool authenticate_client(tcp::socket& socket, std::string& client_id) {
    boost::system::error_code error;

    uint32_t id_length;
    boost::asio::read(socket, boost::asio::buffer(&id_length, sizeof(id_length)), error);
    if (error) return false;

    std::vector<char> id_buffer(id_length);
    boost::asio::read(socket, boost::asio::buffer(id_buffer), error);
    if (error) return false;
    client_id = std::string(id_buffer.begin(), id_buffer.end());

    uint32_t password_length;
    boost::asio::read(socket, boost::asio::buffer(&password_length, sizeof(password_length)), error);
    if (error) return false;

    std::vector<char> password_buffer(password_length);
    boost::asio::read(socket, boost::asio::buffer(password_buffer), error);
    if (error) return false;
    std::string password(password_buffer.begin(), password_buffer.end());

    if (client_credentials.find(client_id) != client_credentials.end() && client_credentials[client_id] == password) {
        uint32_t response = 1;
        boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)));
        cout << "Authenticated client: " << client_id << endl;
        return true;
    }
    else {
        uint32_t response = 0;
        boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)));
        return false;
    }
}

void handleUpdateMessage(tcp::socket& socket, const std::string& recipient) {
    std::cout << "Delivering pending messages to " << recipient << std::endl;
    boost::system::error_code error;
    bool updateSent = false;
    uint32_t message_type = MessageType::UPDATE_Message;

    boost::asio::write(socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
    if (error) {
        std::cerr << "Error sending update notification: " << error.message() << std::endl;
        return;
    }

    std::string pending_dir = "PendingMessages";
    for (const auto& entry : fs::directory_iterator(pending_dir)) {
        std::string filename = entry.path().filename().string();

        std::cout << "Checking for offline messages for: " << recipient << std::endl;

        // Check if the file matches "<recipient>-<sender>-during-offline.txt"
        std::size_t recipientPos = filename.find(recipient + "-");
        std::size_t senderPos = filename.find("-during-offline.bin");

        if (recipientPos == 0 && senderPos != std::string::npos) {
            std::string sender = filename.substr(recipient.size() + 1, senderPos - recipient.size() - 1);
            std::ifstream file(entry.path().string());
            std::string offlineMessage((std::istreambuf_iterator<char>(file)), std::istreambuf_iterator<char>());
            file.close();

            if (!offlineMessage.empty()) {
                uint32_t response_type = MessageType::UPDATE_Message;
                boost::asio::write(socket, boost::asio::buffer(&response_type, sizeof(response_type)), error);
                if (error) {
                    std::cerr << "Error sending update notification: " << error.message() << std::endl;
                    return;
                }

                uint32_t sender_size = static_cast<uint32_t>(sender.size());
                boost::asio::write(socket, boost::asio::buffer(&sender_size, sizeof(sender_size)), error);
                if (error) {
                    std::cerr << "Error sending sender size: " << error.message() << std::endl;
                    return;
                }
                boost::asio::write(socket, boost::asio::buffer(sender), error);
                if (error) {
                    std::cerr << "Error sending sender name: " << error.message() << std::endl;
                    return;
                }

                uint32_t message_length = static_cast<uint32_t>(offlineMessage.size());
                boost::asio::write(socket, boost::asio::buffer(&message_length, sizeof(message_length)), error);
                if (error) {
                    std::cerr << "Error sending message length: " << error.message() << std::endl;
                    return;
                }

                boost::asio::write(socket, boost::asio::buffer(offlineMessage), error);
                if (error) {
                    std::cerr << "Error sending update message: " << error.message() << std::endl;
                    return;
                }

                std::cout << "Sent offline message from " << sender << " to " << recipient << std::endl;

                // Delete the offline message file after successful transmission
                fs::remove(entry.path());
                std::cout << "Deleted offline message file: " << filename << std::endl;

                updateSent = true;
            }
        }
    }

    uint32_t response_type = MessageType::END_UPDATE;
    boost::asio::write(socket, boost::asio::buffer(&response_type, sizeof(response_type)), error);
    if (error) {
        std::cerr << "Error sending end-of-updates signal: " << error.message() << std::endl;
    }
    std::cout << "Finished sending all updates for " << recipient << std::endl;
}


void register_client(tcp::socket& socket) {
    boost::system::error_code error;

    // Read ID length
    uint32_t id_length;
    boost::asio::read(socket, boost::asio::buffer(&id_length, sizeof(id_length)), error);
    if (error) {
        cerr << "Error reading ID length: " << error.message() << endl;
        return;
    }

    // Read ID
    std::vector<char> id_buffer(id_length);
    boost::asio::read(socket, boost::asio::buffer(id_buffer), error);
    if (error) {
        cerr << "Error reading ID: " << error.message() << endl;
        return;
    }
    std::string client_id(id_buffer.begin(), id_buffer.end());

    // Read password length
    uint32_t password_length;
    boost::asio::read(socket, boost::asio::buffer(&password_length, sizeof(password_length)), error);
    if (error) {
        cerr << "Error reading password length: " << error.message() << endl;
        return;
    }

    // Read password
    std::vector<char> password_buffer(password_length);
    boost::asio::read(socket, boost::asio::buffer(password_buffer), error);
    if (error) {
        cerr << "Error reading password: " << error.message() << endl;
        return;
    }
    std::string password(password_buffer.begin(), password_buffer.end());

    // Register client
    {
        std::lock_guard<std::mutex> lock(credentials_mutex);
        if (client_credentials.find(client_id) == client_credentials.end()) {
            // If client ID is not in the map, add it
            client_credentials[client_id] = password;
            save_credentials();  // Save updated credentials

            uint32_t response = 1;  // Registration success
            boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)));
            cout << "Registered new client: " << client_id << endl;
        }
        else {
            uint32_t response = 0;  // Registration failed (ID already exists)
            boost::asio::write(socket, boost::asio::buffer(&response, sizeof(response)));
            cout << "Registration failed: Client ID '" << client_id << "' already exists." << endl;
        }
    }
}
void handle_client(boost::asio::io_context& io_context, tcp::acceptor& acceptor,
    tcp::socket send_socket, tcp::socket receive_socket) {
    try {
        std::string client_id;
        boost::system::error_code error;

        uint32_t message_type;
        boost::asio::read(send_socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
        cout << "1st thing from client: " << message_type << endl;
        if (error == boost::asio::error::eof) return;
        else if (error) throw boost::system::system_error(error);

        if (message_type == MessageType::REGISTRATION) {
            register_client(send_socket);
        }
        else {
            if (!authenticate_client(send_socket, client_id)) return;
            {
                std::lock_guard<std::mutex> lock(socket_mutex); // Synchronize access
                cout << "\n#user_name: " << client_id << endl;
                cout << "io_context: " << ioContextToString(io_context) << endl;
                cout << "send_socket: " << socketToString(send_socket) << endl;
                cout << "receive_socket: " << socketToString(receive_socket) << endl;
                cout << "acceptor: " << acceptorToString(acceptor)<<"#\n" << endl;
                send_sockets[client_id] = &send_socket;
                receive_sockets[client_id] = &receive_socket;
                /*acceptors_map[client_id] = &acceptor;
                io_context_map[client_id] = &io_context;*/
            }
            std::cout << "Delivering updates to " << client_id << std::endl;
            string offline_dir = "OfflineFiles";
            handleUpdateMessage(receive_socket, client_id);
            deliverPendingFiles(io_context, acceptor, receive_socket, client_id, offline_dir);

            while (true) {
                uint32_t message_type;
                boost::asio::read(send_socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
                if (error == boost::asio::error::eof) break;
                else if (error) throw boost::system::system_error(error);

                std::cout << "Received message type: " << message_type << std::endl;

                if (message_type == MessageType::TEXT) {
                    uint32_t message_length;
                    boost::asio::read(send_socket, boost::asio::buffer(&message_length, sizeof(message_length)), error);
                    if (error) break;

                    std::vector<char> message_buffer(message_length);
                    boost::asio::read(send_socket, boost::asio::buffer(message_buffer), error);
                    if (error) break;

                    std::string message(message_buffer.begin(), message_buffer.end());
                    std::cout << "Received message from user: " << message << std::endl;

                    handleReceivedMessage(send_socket, message);  // Send message to client
                }
                else if (message_type == MessageType::File) {
                    // Handle file reception
                    uint32_t recipient_length;
                    boost::asio::read(send_socket, boost::asio::buffer(&recipient_length, sizeof(recipient_length)), error);
                    if (error) break;

                    std::vector<char> recipient_buffer(recipient_length);
                    boost::asio::read(send_socket, boost::asio::buffer(recipient_buffer), error);
                    if (error) break;
                    std::string recipient(recipient_buffer.begin(), recipient_buffer.end());

                    std::cout << "Recipient: " << recipient << std::endl;

                    uint32_t file_name_length;
                    boost::asio::read(send_socket, boost::asio::buffer(&file_name_length, sizeof(file_name_length)), error);
                    if (error) break;

                    std::vector<char> file_name_buffer(file_name_length);
                    boost::asio::read(send_socket, boost::asio::buffer(file_name_buffer), error);
                    if (error) break;
                    std::string file_name(file_name_buffer.begin(), file_name_buffer.end());

                    std::cout << "File name: " << file_name << std::endl;

                    uint32_t file_size;
                    boost::asio::read(send_socket, boost::asio::buffer(&file_size, sizeof(file_size)), error);
                    if (error) break;

                    // Check if recipient is online
                    auto recipient_it = receive_sockets.find(recipient);
                    if (recipient_it != receive_sockets.end()) {
                        auto recipient_socket = receive_sockets[recipient];

                        /*auto recipient_io_context = std::make_shared<boost::asio::io_context>();
                        io_context_map[recipient] = recipient_io_context.get();*/

                        relayFileToRecipient(io_context, acceptor, send_socket, client_id, recipient, file_name, file_size, *recipient_socket);
                    }
                    else {
                        storeFileForOfflineRecipient(send_socket, recipient, client_id, file_name, file_size);
                    }
                }
                else if (message_type == MessageType::REFRESH)
                {
                    refreshConversations(send_socket, client_id);
                }
                else if (message_type == MessageType::CHANGE_PASSWORD)
                {
                    handleChangePassword(send_socket, client_id);
                }
                else if (message_type == MessageType::ADD_FRIENDS)
                {
                    handleAddFriend(send_socket);
                }
            }
        }
        cout << "User " << client_id << " is disconnecting..." << endl;
        send_sockets.erase(client_id);
        receive_sockets.erase(client_id);
        cout << "User " << client_id << " is disconnected!" << endl;
    }
    catch (std::exception& e) {
        std::cerr << "Exception in handling client: " << e.what() << std::endl;
    }
}

int main() {
    load_credentials();
    try {
        boost::asio::io_context io_context;
        tcp::acceptor acceptor(io_context, tcp::endpoint(tcp::v4(), PORT));
        std::cout << "Server is listening on port " << PORT << std::endl;

        while (true) {
            tcp::socket send_socket(io_context);
            tcp::socket receive_socket(io_context);

            // Accept both sending and receiving sockets
            acceptor.accept(send_socket);
            acceptor.accept(receive_socket);

            // Create a thread to handle the client using both sockets
            std::thread client_thread(handle_client, std::ref(io_context), std::ref(acceptor),
                std::move(send_socket), std::move(receive_socket));
            client_thread.detach();
            std::cout << "Client connected..." << std::endl;
        }
    }
    catch (std::exception& e) {
        std::cerr << "Exception in server: " << e.what() << std::endl;
    }

    return 0;
}