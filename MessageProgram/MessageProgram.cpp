#include <iostream>
#include <fstream>
#include <string>
#include <string.h>
#include <filesystem>
#include <sstream>
#include <boost/asio.hpp>
#include <boost/filesystem.hpp>
#include <thread>
#include <windows.h>
#include <mutex>
#include <condition_variable>
#include <queue>
#include <chrono>
#include <ctime>
#include "Tool.h"
#include "Security.h"
#include <atomic>
#include <unordered_set>

using boost::asio::ip::tcp;
using namespace std;
namespace fs = boost::filesystem;
namespace fss = std::filesystem;
const int PORT = 80;
const int BUFFER_SIZE = 4096;
std::queue<std::string> commandQueue;
std::mutex mtx;
std::condition_variable cv;
bool running = true;
string ip_server = ReadIP();
string userName;
string delimiter = "{#EOM#}";
bool isReceiving = false;  // Global or member variable to track the state of async function
boost::asio::io_context io_context;
std::atomic<bool> stop_asynch_reading_;
bool isReceivingAllowed = true;  // Flag to control whether async receive should be active
HANDLE hNotificationPipe;
enum MessageType { TEXT = 1, UPDATE_Message = 2, File = 3, UPDATE_File = 4, RECEIVE_TEXT = 5, RECEIVE_File = 6, REGISTRATION = 7, NOT_REGISTRATION = 8, END_UPDATE = 9, REFRESH = 10, ADD_FRIEND = 11, CHANGE_PASSWORD = 12 };
// ! Encrypt from the third line
bool registration(tcp::socket& socket) {
    string register_file_name = "register.bin";
    string ID, password;

    // Open the file and read the ID and password
    ifstream register_file(register_file_name);
    if (!register_file) {
        cerr << "Error: Could not open " << register_file_name << endl;
        return false;
    }
    register_file >> ID >> password;

    // Send the message type (REGISTRATION)
    uint32_t message_type = MessageType::REGISTRATION;
    boost::asio::write(socket, boost::asio::buffer(&message_type, sizeof(message_type)));

    // Send the ID and password lengths and contents
    uint32_t id_length = ID.size();
    uint32_t password_length = password.size();

    boost::asio::write(socket, boost::asio::buffer(&id_length, sizeof(id_length)));
    boost::asio::write(socket, boost::asio::buffer(ID));

    boost::asio::write(socket, boost::asio::buffer(&password_length, sizeof(password_length)));
    boost::asio::write(socket, boost::asio::buffer(password));

    // Wait for the server response
    uint32_t response;
    boost::system::error_code error;

    // Reading response from the server
    boost::asio::read(socket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        if (error == boost::asio::error::eof) {
            cerr << "Error: Server closed the connection (End of file)" << endl;
        }
        else {
            cerr << "Error receiving signal from server: " << error.message() << endl;
        }
        return false;
    }

    if (response == 1) {
        cout << "Registration successful!" << endl;
        return true;
    }
    else {
        cout << "Registration failed: ID may already exist." << endl;
        return false;
    }
}

void checkAndAddSender(const std::string& sender) {
    std::unordered_set<std::string> contactList;
    std::ifstream friendFile("Friend_List.txt");

    if (friendFile.is_open()) {
        std::string contact;
        while (std::getline(friendFile, contact)) {
            contactList.insert(contact);
        }
        friendFile.close();
    }
    else {
        std::cerr << "Error opening Friend_List.txt!" << std::endl;
        return;
    }

    // Check if the sender is already in the contact list
    if (contactList.find(sender) == contactList.end()) {
        // If sender is not in the contact list, add them to UnaddedContact.txt
        std::ofstream unaddedFile("UnaddedContact.txt", std::ios::app);
        if (unaddedFile.is_open()) {
            unaddedFile << sender << std::endl;
            unaddedFile.close();
            std::cout << "Sender " << sender << " added to UnaddedContact.txt." << std::endl;
        }
        else {
            std::cerr << "Error opening UnaddedContact.txt!" << std::endl;
        }
    }
    else {
        std::cout << "Sender " << sender << " is already in the contact list." << std::endl;
    }
}

void addFriend(tcp::socket& socket) {
    // Open "add_contact.txt" and read the friend's name
    std::ifstream contactFile("add_contact.bin");
    std::string friendName;
    if (contactFile.is_open()) {
        std::getline(contactFile, friendName);
        contactFile.close();
    }
    else {
        std::cerr << "Error opening add_contact.bin" << std::endl;
        return;
    }

    // Step 1: Send ADD_CONTACT request type to server
    uint32_t requestType = MessageType::ADD_FRIEND;
    boost::system::error_code error;
    boost::asio::write(socket, boost::asio::buffer(&requestType, sizeof(requestType)), error);
    if (error) {
        std::cerr << "Error sending request type: " << error.message() << std::endl;
        return;
    }

    // Step 2: Send friend's name length and friend's name
    uint32_t nameLength = friendName.size();
    boost::asio::write(socket, boost::asio::buffer(&nameLength, sizeof(nameLength)), error);
    boost::asio::write(socket, boost::asio::buffer(friendName), error);
    if (error) {
        std::cerr << "Error sending friend's name: " << error.message() << std::endl;
        return;
    }

    // Step 3: Receive response from server
    uint32_t response;
    boost::asio::read(socket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error receiving response from server: " << error.message() << std::endl;
        return;
    }

    // Step 4: Process response and update files accordingly
    if (response == 1) {
        // Success: Add the friend to "Friend_List.txt" and write "success" to "add_contact_result.txt"
        std::ofstream friendList("Friend_List.txt", std::ios::app);
        if (friendList.is_open()) {
            friendList << friendName << std::endl;
            friendList.close();
        }

        std::ofstream resultFile("add_contact_result.bin");
        if (resultFile.is_open()) {
            resultFile << "success" << std::endl;
            resultFile.close();
        }
    }
    else {
        // Failure: Write "fail" to "add_contact_result.txt"
        std::ofstream resultFile("add_contact_result.bin");
        if (resultFile.is_open()) {
            resultFile << "fail" << std::endl;
            resultFile.close();
        }
    }
}
void changePassword(tcp::socket& socket, const string& currentPassword, const string& newPassword)
{
    boost::system::error_code error;

    // Step 1: Send command type for password change
    uint32_t message_type = MessageType::CHANGE_PASSWORD;
    boost::asio::write(socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
    if (error) {
        std::cerr << "Error sending CHANGE_PASSWORD command: " << error.message() << std::endl;
        return;
    }

    // Step 2: Send current password length and current password
    uint32_t currentPasswordLength = currentPassword.size();
    boost::asio::write(socket, boost::asio::buffer(&currentPasswordLength, sizeof(currentPasswordLength)), error);
    boost::asio::write(socket, boost::asio::buffer(currentPassword), error);
    if (error) {
        std::cerr << "Error sending current password: " << error.message() << std::endl;
        return;
    }

    // Step 3: Send new password length and new password
    uint32_t newPasswordLength = newPassword.size();
    boost::asio::write(socket, boost::asio::buffer(&newPasswordLength, sizeof(newPasswordLength)), error);
    boost::asio::write(socket, boost::asio::buffer(newPassword), error);
    if (error) {
        std::cerr << "Error sending new password: " << error.message() << std::endl;
        return;
    }

    // Step 4: Wait for server's response (1 for success, 0 for failure)
    uint32_t response;
    boost::asio::read(socket, boost::asio::buffer(&response, sizeof(response)), error);
    if (error) {
        std::cerr << "Error receiving response: " << error.message() << std::endl;
        return;
    }
    ofstream changeResultFile("change_password_result.bin");
    // Step 5: Output the result based on server response
    if (response == 1) {
        std::cout << "Password changed successfully." << std::endl;
        changeResultFile << "success";
    }
    else {
        std::cout << "Failed to change password. Please check your current password and try again." << std::endl;
        changeResultFile << "fail";
    }
    changeResultFile.close();
}
// Sends a "NEW_MESSAGE" notification through the notification pipe
void SendNewMessageNotification(const std::string& sender, const std::string& content) {
    if (ConnectNamedPipe(hNotificationPipe, NULL) || GetLastError() == ERROR_PIPE_CONNECTED) {
        // Construct the notification string
        std::string notification = "NEW_MESSAGE:" + sender + ":" + content + "\n"; // Add newline
        DWORD bytesWritten;

        // Write the full notification to the pipe
        if (WriteFile(hNotificationPipe, notification.c_str(), notification.size(), &bytesWritten, NULL)) {
            std::cout << "Sent NEW_MESSAGE notification: " << notification << std::endl;
        }
        else {
            std::cerr << "Error writing to notification pipe. Error: " << GetLastError() << std::endl;
        }
    }
    else {
        std::cerr << "Failed to connect to notification pipe. Error: " << GetLastError() << std::endl;
    }
}
void receiveMessageContent(tcp::socket& receive_socket, const std::string& sender) {
    uint32_t message_length;
    boost::system::error_code error;

    boost::asio::read(receive_socket, boost::asio::buffer(&message_length, sizeof(message_length)), error);
    if (error) {
        std::cerr << "Error reading message length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> message_buffer(message_length);
    boost::asio::read(receive_socket, boost::asio::buffer(message_buffer), error);
    if (error) {
        std::cerr << "Error reading message content: " << error.message() << std::endl;
        return;
    }

    std::string message(message_buffer.begin(), message_buffer.end());
    std::cout << "Received message: " << message << " from: " << sender << std::endl;

    string decrypted_message;
    DecryptMessage(message, decrypted_message);

    std::string conversationFileName = "Conversations\\" + GetConversationFileName(sender, userName) + ".bin";
    std::ofstream conversationFile(conversationFileName, std::ios::app);
    if (conversationFile.is_open()) {
        conversationFile << decrypted_message << std::endl;
        conversationFile.close();
    }
    else {
        std::cerr << "Error opening conversation file: " << conversationFileName << std::endl;
    }
    cout << "sender: " << sender << endl;
    cout << "decrypted_message: " << decrypted_message << endl;
    SendNewMessageNotification(sender, decrypted_message);
}

void receiveSender(tcp::socket& receive_socket) {
    uint32_t sender_length;
    boost::system::error_code error;

    boost::asio::read(receive_socket, boost::asio::buffer(&sender_length, sizeof(sender_length)), error);
    if (error) {
        std::cerr << "Error reading sender length: " << error.message() << std::endl;
        return;
    }

    std::vector<char> sender_buffer(sender_length);
    boost::asio::read(receive_socket, boost::asio::buffer(sender_buffer), error);
    if (error) {
        std::cerr << "Error reading sender name: " << error.message() << std::endl;
        return;
    }

    std::string sender(sender_buffer.begin(), sender_buffer.end());
    std::cout << "Sender: " << sender << std::endl;

    checkAndAddSender(sender);
    receiveMessageContent(receive_socket, sender);

}

bool Login(tcp::socket& send_socket) {
    CreateFolder("Downloads");
    std::string Login = "login.bin";
    try {
        std::ifstream LoginFile(Login);
        uint32_t temp_message = 8; // To notify server action is not registration
        boost::asio::write(send_socket, boost::asio::buffer(&temp_message, sizeof(temp_message)));

        std::string id, password;
        LoginFile >> id >> password;
        LoginFile.close();

        uint32_t id_length = id.size();
        boost::asio::write(send_socket, boost::asio::buffer(&id_length, sizeof(id_length)));
        boost::asio::write(send_socket, boost::asio::buffer(id));

        uint32_t password_length = password.size();
        boost::asio::write(send_socket, boost::asio::buffer(&password_length, sizeof(password_length)));
        boost::asio::write(send_socket, boost::asio::buffer(password));

        userName = id;
        return true;
    }
    catch (std::exception& e) {
        std::cerr << "Exception: " << e.what() << std::endl;
        return false;
    }
}

bool SendFileToServer(tcp::socket& send_socket, const std::string& recipient, std::string& file_path) {
    file_path = addDoubleBackslashes(file_path);
    std::ifstream infile(file_path, std::ios::binary);

    if (!infile) {
        std::cerr << "Error opening file: " << file_path << std::endl;
        return false;
    }

    infile.seekg(0, std::ios::end);
    uint32_t file_size = static_cast<uint32_t>(infile.tellg());
    infile.seekg(0, std::ios::beg);

    std::string file_name = file_path.substr(file_path.find_last_of("/\\") + 1);

    uint32_t message_type = MessageType::File;
    boost::asio::write(send_socket, boost::asio::buffer(&message_type, sizeof(message_type)));

    uint32_t recipient_length = recipient.size();
    boost::asio::write(send_socket, boost::asio::buffer(&recipient_length, sizeof(recipient_length)));
    boost::asio::write(send_socket, boost::asio::buffer(recipient));

    uint32_t file_name_length = file_name.size();
    boost::asio::write(send_socket, boost::asio::buffer(&file_name_length, sizeof(file_name_length)));
    file_name = sanitizeFilename(file_name);
    cout << "file_name_length: " << file_name_length << endl;
    boost::asio::write(send_socket, boost::asio::buffer(file_name));

    boost::asio::write(send_socket, boost::asio::buffer(&file_size, sizeof(file_size)));

    char buffer[BUFFER_SIZE];
    while (infile.read(buffer, BUFFER_SIZE) || infile.gcount() > 0) {
        boost::asio::write(send_socket, boost::asio::buffer(buffer, infile.gcount()));
    }
    infile.close();
    uint32_t response;
    boost::asio::read(send_socket, boost::asio::buffer(&response, sizeof(response)));
    if (response == MessageType::File)
    {
        return true;
    }
    else
    {
        return false;
    }
    return false;
}

void UpdateOfflineFiles(boost::asio::io_context& io_context, tcp::socket& control_socket) {
    std::cout << "Updating offline files..." << std::endl;
    std::string download_dir = "Downloads";
    fs::create_directory(download_dir);
    boost::system::error_code error;

    while (true) {
        uint32_t response_type;
        boost::asio::read(control_socket, boost::asio::buffer(&response_type, sizeof(response_type)), error);
        if (error) break;

        if (response_type == MessageType::END_UPDATE) {
            std::cout << "End of file update signals received." << std::endl;
            break;
        }

        if (response_type == MessageType::UPDATE_File) {
            uint32_t file_name_length, file_size;
            std::string file_name;

            // Receive file metadata
            boost::asio::read(control_socket, boost::asio::buffer(&file_name_length, sizeof(file_name_length)), error);
            cout << "file_name_length: " << file_name_length << endl;

            std::vector<char> file_name_buffer(file_name_length);
            boost::asio::read(control_socket, boost::asio::buffer(file_name_buffer), error);
            file_name.assign(file_name_buffer.begin(), file_name_buffer.end());
            cout << "file_name: " << file_name << endl;

            boost::asio::read(control_socket, boost::asio::buffer(&file_size, sizeof(file_size)), error);
            cout << "file_size: " << file_size << endl;

            // Generate a unique file name
            std::string final_file_name = file_name;
            int counter = 1;
            while (fs::exists(download_dir + "\\" + final_file_name)) {
                std::ostringstream unique_name;
                unique_name << fs::path(file_name).stem().string() << "(" << counter << ")"
                    << fs::path(file_name).extension().string();
                final_file_name = unique_name.str();
                counter++;
            }

            // Connect to the shared file transfer socket
            tcp::socket file_socket(io_context);
            int attempts = 50;
            bool connected = false;
            for (int i = 0; i < attempts; ++i) {
                file_socket.connect(tcp::endpoint(control_socket.remote_endpoint().address(), PORT), error);
                if (!error) {
                    connected = true;
                    std::cout << "File socket established successfully on client side." << std::endl;
                    break;
                }
                else {
                    std::cerr << "Attempt " << (i + 1) << " failed to connect: " << error.message() << std::endl;
                    std::this_thread::sleep_for(std::chrono::milliseconds(500));  // Wait before retrying
                }
            }

            if (!connected) {
                std::cerr << "Error connecting to file transfer port after multiple attempts: " << error.message() << std::endl;
                break;
            }

            // Prepare to receive file
            std::ofstream file(download_dir + "\\" + final_file_name, std::ios::binary);
            int packet = 1;
            char buffer[BUFFER_SIZE];
            uint32_t total_bytes_read = 0;
            while (total_bytes_read < file_size) {
                size_t bytes_to_read = std::min<size_t>(BUFFER_SIZE, static_cast<size_t>(file_size - total_bytes_read));
                size_t bytes_read = boost::asio::read(file_socket, boost::asio::buffer(buffer, bytes_to_read), error);

                packet++;
                if (error) {
                    // Only break if it's not an expected EOF error
                    if (error != boost::asio::error::eof) {
                        std::cerr << "Error receiving file content: " << error.message() << std::endl;
                    }
                    break;
                }

                file.write(buffer, bytes_read);
                total_bytes_read += bytes_read;
            }
            cout << "total of packets: " << packet << endl;
            file.close();
            file_socket.close();
            std::cout << "File " << file_name << " received and saved." << std::endl;
        }
    }
}

void TextMessage(tcp::socket& socket, const std::string& filename) {
    isReceivingAllowed = false;  // Temporarily disable async receive

    string finishedFileName = "finished-message-now.bin";
    //Encrypt file: 
    Copy(finishedFileName, filename);
    try {
        std::ifstream infile(finishedFileName);
        if (!infile.is_open()) {
            std::cerr << "Could not open file: " << filename << std::endl;
            isReceivingAllowed = true;
            return;
        }

        uint32_t temp_message = MessageType::TEXT; // To notify the server that the action is sending message
        boost::asio::write(socket, boost::asio::buffer(&temp_message, sizeof(temp_message)));

        std::string recipient, sender, message;
        std::getline(infile, recipient);
        std::getline(infile, sender);
        message.assign((std::istreambuf_iterator<char>(infile)), std::istreambuf_iterator<char>());
        infile.close();

        string encrypted_message;

        EncryptMessage(message, encrypted_message);

        std::cout << "send to: " << recipient << " message: " << encrypted_message << std::endl;

        std::string formattedMessage = recipient + "-" + sender + "\n" + encrypted_message;

        uint32_t filename_length = filename.size();
        /*boost::asio::write(socket, boost::asio::buffer(&filename_length, sizeof(filename_length)));
        boost::asio::write(socket, boost::asio::buffer(filename));*/

        boost::system::error_code error;
        uint32_t message_length = formattedMessage.size();
        boost::asio::write(socket, boost::asio::buffer(&message_length, sizeof(message_length)), error);
        if (error) {
            std::cerr << "Error sending message length: " << error.message() << std::endl;
            return; // Or handle the error appropriately
        }

        boost::asio::write(socket, boost::asio::buffer(formattedMessage), error);
        if (error) {
            std::cerr << "Error sending message content: " << error.message() << std::endl;
            return; // Or handle the error appropriately
        }

        uint32_t response;
        boost::asio::read(socket, boost::asio::buffer(&response, sizeof(response)));
        cout << "filename: " << filename << endl;
        cout << "formattedMessage: " << formattedMessage << endl;
        cout << "{in TextMessage} response: " << response << endl;
        if (response == 1) {
            std::cout << "Message successfully sent to server." << std::endl;
            /*delete(filename.c_str());
            delete(finishedFileName.c_str());*/
        }
        else {
            std::cerr << "Server failed to receive the message." << std::endl;
        }
    }
    catch (std::exception& e) {
        std::cerr << "Exception: " << e.what() << std::endl;
    }
    isReceivingAllowed = true;
    // You might need to call asyncReceiveMessage(socket) again here
}

void UpdateOfflineMessages(tcp::socket& socket) {
    isReceivingAllowed = false;  // Temporarily disable async receive

    std::cout << "Updating offline messages..." << std::endl;
    uint32_t request_type; // MessageType to request updates
    boost::system::error_code error;
    string sender_names;
    string notification;
    bool hasUpdate = false;
    while (true) {
        // Receive the response type from the server
        uint32_t response_type;
        boost::asio::read(socket, boost::asio::buffer(&response_type, sizeof(response_type)), error);
        if (error) {
            std::cerr << "Error reading response type: " << error.message() << std::endl;
            notification = "System [00:00-00/00/0000]: Error during update message! ";
            break;
        }

        // Check if the server has indicated that no more messages are pending
        if (response_type == MessageType::END_UPDATE) {
            std::cout << "No more offline messages to update. Exiting message update process." << std::endl;
            if (!hasUpdate)
            {
                notification = "System [00:00-00/00/0000]: There is no new message!";
            }
            break;
        }

        // If there's an update available
        if (response_type == MessageType::UPDATE_Message) {
            hasUpdate = true;
            uint32_t sender_length;
            boost::asio::read(socket, boost::asio::buffer(&sender_length, sizeof(sender_length)), error);
            if (error) {
                std::cerr << "Error reading sender length: " << error.message() << std::endl;
                return;
            }

            std::vector<char> sender_buffer(sender_length);
            boost::asio::read(socket, boost::asio::buffer(sender_buffer), error);
            if (error) {
                std::cerr << "Error reading sender name: " << error.message() << std::endl;
                return;
            }
            std::string sender(sender_buffer.begin(), sender_buffer.end());

            uint32_t message_length;
            boost::asio::read(socket, boost::asio::buffer(&message_length, sizeof(message_length)), error);
            if (error) {
                std::cerr << "Error reading message length: " << error.message() << std::endl;
                return;
            }

            std::vector<char> message_buffer(message_length);
            boost::asio::read(socket, boost::asio::buffer(message_buffer), error);
            if (error) {
                std::cerr << "Error reading update message content: " << error.message() << std::endl;
                return;
            }

            std::string update_message(message_buffer.begin(), message_buffer.end());
            std::string conversationFileName = "Conversations\\" + GetConversationFileName(sender, userName) + ".bin";
            std::string encryptedFileName = userName + "-" + sender + "-encrypted.bin";

            std::cout << "Received offline message from: " << sender << std::endl;

            string plain_message;
            DecryptMessage(update_message, plain_message);
            // Decrypt the message and save it to the conversation file:
            cout << "plain_message: " << plain_message << endl;
            std::ofstream conversation_file(conversationFileName, std::ios::app); // Open in append mode
            if (!conversation_file) {
                std::cerr << "Error opening file: " << conversationFileName << std::endl;
                return;
            }
            conversation_file << plain_message << std::endl;
            conversation_file.close();
            checkAndAddSender(sender);
            sender_names += sender + ", ";
        }
    }
    if (sender_names.empty())
    {
        SendNewMessageNotification("", ""); //dump message
        SendNewMessageNotification("System", notification);
    }
    else
    {
        sender_names = sender_names.substr(0, sender_names.size() - 2);  // Remove trailing ", "
        notification = "System [00:00-00/00/0000]: New messages from " + sender_names;
        SendNewMessageNotification("", ""); //dump message
        SendNewMessageNotification("System", notification);
    }
    std::cout << "Finished updating offline messages." << std::endl;

    isReceivingAllowed = true;  // Re-enable async receive
}

void asyncReceiveMessage(boost::asio::io_context& io_context, tcp::socket& receive_socket) {
    boost::system::error_code error;
    uint32_t signal;

    // Attempt to read the signal/message type from the server
    boost::asio::read(receive_socket, boost::asio::buffer(&signal, sizeof(signal)), error);

    // Check if there was an error during the read
    if (error) {
        std::cerr << "Error receiving signal from server: " << error.message() << std::endl;
        return; // Stop further recursion on error
    }

    std::cout << "Signal from server: " << signal << std::endl;

    // Process the signal based on its type
    if (signal == MessageType::RECEIVE_TEXT) {
        receiveSender(receive_socket);
    }
    else if (signal == MessageType::UPDATE_Message) {
        UpdateOfflineMessages(receive_socket);
    }
    else if (signal == MessageType::UPDATE_File) {
        UpdateOfflineFiles(io_context, receive_socket);
    }
    else if (signal == MessageType::END_UPDATE) {
        std::cout << "No more updates from the server." << std::endl;
        return;  // Stop recursion for end-of-update signal
    }
    else {
        std::cerr << "Unexpected message type received: " << signal << std::endl;
    }

    // Recursively call asyncReceiveMessage to wait for the next signal
    asyncReceiveMessage(io_context, receive_socket);
}
void clearConversationsFolder(const std::string& folder) {
    try {
        for (boost::filesystem::directory_iterator endDirIt, it(folder); it != endDirIt; ++it) {
            if (boost::filesystem::is_regular_file(it->status())) {
                boost::filesystem::remove(it->path());
            }
        }
        std::cout << "Cleared existing conversations in " << folder << std::endl;
    }
    catch (const boost::filesystem::filesystem_error& ex) {
        std::cerr << "Error clearing conversations folder: " << ex.what() << std::endl;
    }
}
// Helper function to display messages in the client chatbox
void displayMessage(const std::string& sender, const std::string& message) {
    std::cout << sender << ": " << message << std::endl;
}

void refreshConversations(tcp::socket& send_socket) {
    boost::system::error_code error;

    std::string conversationFolder = "Conversations";
    clearConversationsFolder(conversationFolder);

    // Step 1: Send the REFRESH command to the server
    uint32_t refresh_command = MessageType::REFRESH;
    boost::asio::write(send_socket, boost::asio::buffer(&refresh_command, sizeof(refresh_command)), error);
    if (error) {
        std::cerr << "Error sending REFRESH command: " << error.message() << std::endl;
        return;
    }

    // Step 2: Listen for responses from the server
    while (true) {
        uint32_t message_type = 0;
        boost::asio::read(send_socket, boost::asio::buffer(&message_type, sizeof(message_type)), error);
        if (error) {
            std::cerr << "Error reading message type: " << error.message() << std::endl;
            break;
        }

        // Step 3: Check if server has finished sending messages
        if (message_type == MessageType::END_UPDATE) {
            std::cout << "All conversations refreshed." << std::endl;
            break;
        }

        // Step 4: Process each received message
        if (message_type == MessageType::RECEIVE_TEXT) {
            try {
                uint32_t sender_size = 0, message_size = 0;

                // Receive sender's name size and name
                boost::asio::read(send_socket, boost::asio::buffer(&sender_size, sizeof(sender_size)), error);
                if (error) throw std::runtime_error("Error reading sender size: " + error.message());

                std::vector<char> sender_buffer(sender_size);
                boost::asio::read(send_socket, boost::asio::buffer(sender_buffer), error);
                if (error) throw std::runtime_error("Error reading sender name: " + error.message());
                std::string sender(sender_buffer.begin(), sender_buffer.end());

                // Receive message size and content
                boost::asio::read(send_socket, boost::asio::buffer(&message_size, sizeof(message_size)), error);
                if (error) throw std::runtime_error("Error reading message size: " + error.message());

                std::vector<char> message_buffer(message_size);
                boost::asio::read(send_socket, boost::asio::buffer(message_buffer), error);
                if (error) throw std::runtime_error("Error reading message content: " + error.message());
                std::string message(message_buffer.begin(), message_buffer.end());

                // Decrypt the message
                std::string decrypted_message;
                DecryptMessage(message, decrypted_message);

                // Save the decrypted message to the conversation file
                std::string conversationFileName = conversationFolder + "/" + GetConversationFileName(sender, userName) + ".bin";
                std::ofstream conversationFile(conversationFileName, std::ios::app);
                if (conversationFile.is_open()) {
                    conversationFile << decrypted_message << std::endl;
                    conversationFile.close();
                }
                else {
                    std::cerr << "Error opening conversation file: " << conversationFileName << std::endl;
                }

                // Display the message
                displayMessage(sender, decrypted_message);

            }
            catch (const std::exception& ex) {
                std::cerr << "Exception during message processing: " << ex.what() << std::endl;
            }
        }
    }
}


void sendMessageForFiles(tcp::socket& send_socket, string& sender, string& recipient, string& content)
{
    string conversation_dir = "Conversations";
    string file_name = GetConversationFileName(sender, recipient) + ".bin";
    string file_path = conversation_dir + "\\" + file_name;
    appendToFile(file_path, content);

    string temp_name = "message-now.bin";
    ofstream temp_file(temp_name);
    temp_file << recipient + "\n" + sender + "\n" + content;
    temp_file.close();

    if (std::ifstream("message-now.bin")) {
        TextMessage(send_socket, "message-now.bin");
    }
    else {
        std::cerr << "File 'message-now.bin' not found." << std::endl;
    }
}

void cleanup(tcp::socket& send_socket, tcp::socket& receive_socket, HANDLE hPipe, boost::asio::io_context& io_context) {
    send_socket.close();
    receive_socket.close();
    io_context.stop();

    if (hPipe != INVALID_HANDLE_VALUE) {
        DisconnectNamedPipe(hPipe);
        CloseHandle(hPipe);
        CloseHandle(hNotificationPipe);
    }

    std::cout << "Cleanup completed. Exiting program." << std::endl;
}
// Utility function to convert std::string to std::wstring
std::wstring stringToWstring(const std::string& str) {
    return std::wstring(str.begin(), str.end());
}
// Creates and initializes the notification pipe with a unique name
HANDLE InitializeNotificationPipe() {
    // Get the current time to generate a unique pipe name
    time_t currentTime = time(0);

    // Generate the pipe name and write it to notificationPipe.txt
    std::ofstream pipeConnection("notificationPipe.bin");
    pipeConnection << "notificationPipe_" << currentTime;
    pipeConnection.close();

    // Read back the pipe name
    std::ifstream readPipe("notificationPipe.bin");
    std::string pipeName;
    readPipe >> pipeName;
    readPipe.close();

    // Convert the pipe name to a wide string format suitable for CreateNamedPipe
    std::wstring formattedPipeName = L"\\\\.\\pipe\\" + std::wstring(pipeName.begin(), pipeName.end());
    std::wcout << L"Notification pipe name is: " << formattedPipeName << std::endl;

    // Create the named pipe with the formatted name
    HANDLE hPipe = CreateNamedPipe(
        formattedPipeName.c_str(),
        PIPE_ACCESS_OUTBOUND,
        PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
        1, 1024 * 16, 1024 * 16, 0, NULL
    );

    if (hPipe == INVALID_HANDLE_VALUE) {
        std::cerr << "Error creating notification pipe. Error: " << GetLastError() << std::endl;
        return nullptr;
    }

    return hPipe;
}

// Main command listener function to handle user commands and control the socket
void commandListener(tcp::socket& send_socket, tcp::socket& receive_socket) {
    std::cout << "() Start commandListener in its own thread" << std::endl;
    time_t currentTime = time(0);
    std::ofstream pipeConnection("pipeConnection.bin");
    pipeConnection << currentTime;
    pipeConnection.close();
    std::ifstream readPipe("pipeConnection.bin");
    std::string pipeName;
    readPipe >> pipeName;
    readPipe.close();

    std::wstring formattedPipeName = L"\\\\.\\pipe\\" + std::wstring(pipeName.begin(), pipeName.end());
    std::cout << "pipe_name is: " << pipeName << std::endl;
    HANDLE hPipe = CreateNamedPipe(
        formattedPipeName.c_str(), PIPE_ACCESS_DUPLEX,
        PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
        1, 1024 * 16, 1024 * 16, 0, NULL
    );

    if (hPipe == INVALID_HANDLE_VALUE) {
        std::cerr << "Error creating named pipe. Error: " << GetLastError() << std::endl;
        return;
    }

    std::cout << "Connecting... Please wait." << std::endl;

    // Initialize the notification pipe
    hNotificationPipe = InitializeNotificationPipe();
    if (hNotificationPipe == nullptr) {
        std::cerr << "Failed to initialize notification pipe." << std::endl;
    }

    while (running) {
        BOOL isConnected = ConnectNamedPipe(hPipe, NULL) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED);
        if (isConnected) {
            char buffer[1024];
            DWORD bytesRead;

            while (ReadFile(hPipe, buffer, sizeof(buffer) - 1, &bytesRead, NULL)) {
                buffer[bytesRead] = '\0';
                std::string command(buffer);
                command.erase(std::remove(command.begin(), command.end(), '\r'), command.end());
                command.erase(std::remove(command.begin(), command.end(), '\n'), command.end());
                command.erase(std::remove(command.begin(), command.end(), '\0'), command.end());
                std::cout << "Received command: " << command << std::endl;

                if (command == "LOGIN") {
                    std::string LoginAuth = "login_result.bin";
                    std::ofstream LoginAuthFile(LoginAuth);
                    cout << "Logining..." << endl;
                    if (Login(send_socket)) {
                        uint32_t response;
                        boost::asio::read(send_socket, boost::asio::buffer(&response, sizeof(response)));
                        cout << "(2) Login!" << endl;
                        if (response == 1) {
                            LoginAuthFile << "success" << std::endl;
                        }
                        else if (response == 0) {
                            LoginAuthFile << "fail" << std::endl;
                        }
                    }
                    else {
                        LoginAuthFile << "fail?" << std::endl;
                    }
                    std::cout << "The app is running..." << std::endl;
                    LoginAuthFile.close();
                }
                else if (command == "TEXT_MESSAGE") {
                    if (std::ifstream("message-now.bin")) {
                        TextMessage(send_socket, "message-now.bin");
                    }
                    else {
                        std::cerr << "File 'message-now.bin' not found." << std::endl;
                    }
                }
                else if (command == "SEND_FILE") {
                    std::string temp_file_name = "send-file-now.bin";
                    std::string recipient, file_path;
                    std::ifstream tempFile(temp_file_name);
                    if (tempFile.is_open()) {
                        std::getline(tempFile, recipient);
                        std::getline(tempFile, file_path);
                        std::cout << "Recipient: " << recipient << std::endl;
                        std::cout << "File path: " << file_path << std::endl;
                        if (SendFileToServer(send_socket, recipient, file_path))
                        {
                            std::string file_name = file_path.substr(file_path.find_last_of("/\\") + 1);
                            string content = "System " + getCurrentDateTime() + ": " + userName + " has sent the file " + file_name + " to " + recipient + ".\n" + delimiter ;
                            sendMessageForFiles(send_socket, userName, recipient, content);
                            cout << "the file has been sent successfully!" << endl;
                        }
                    }
                    else {
                        std::string file_name = file_path.substr(file_path.find_last_of("/\\") + 1);
                        string content = "System " + getCurrentDateTime() + ": The file cannot be sent. Please try again! \n" + delimiter ;
                        appendToFile(file_name, content);
                        std::cerr << "File 'send-file-now.bin' not found." << std::endl;
                    }
                }
                // Additional commands (e.g., SEND_FILE) use send_socket
                else if (command == "SHUTDOWN") {
                    std::cout << "Exiting..." << std::endl;
                    running = false;
                    //break;
                }
                else if (command == "REFRESH")
                {
                    refreshConversations(send_socket);
                }
                else if (command == "REGISTER") {
                    const std::string LoginAuth = "register_result.bin";
                    std::ofstream LoginAuthFile(LoginAuth);

                    cout << "Registering..." << endl;
                    if (registration(send_socket)) {
                        // Registration succeeded
                        cout << "(2) Registration success!" << endl;
                        LoginAuthFile << "success" << std::endl;
                    }
                    else {
                        // Registration failed
                        cout << "(2) Registration failed!" << endl;
                        LoginAuthFile << "fail" << std::endl;
                    }
                }
                else if (command == "CHANGE_PASSWORD")
                {
                    string nameFile = "change_password.bin";
                    ifstream passwordFile(nameFile);
                    string currentPassword;
                    string newPassword;
                    passwordFile >> currentPassword >> newPassword;
                    cout << "Old password: " << currentPassword << endl;
                    cout << "New password: " << newPassword << endl;

                    // Call the changePassword function
                    changePassword(send_socket, currentPassword, newPassword);
                }
                else if (command == "ADD_FRIEND")
                {
                    addFriend(send_socket);
                }
                else {
                    std::cerr << "Unknown command: " << command << std::endl;
                }
            }
        }

        if (!isConnected && GetLastError() != ERROR_PIPE_CONNECTED) {
            std::cerr << "ConnectNamedPipe failed with error: " << GetLastError() << std::endl;
            continue;
        }

        if (!running) {
            break;
        }
    }
    cleanup(send_socket, receive_socket, hPipe, io_context);
}

int main() {
    cout << "IP_SERVER: " << ip_server << endl;
    // Initialize resolver and endpoints for both sockets
    tcp::resolver resolver(io_context);
    tcp::resolver::results_type endpoints = resolver.resolve(ip_server, std::to_string(PORT));

    // Initialize the send and receive sockets
    tcp::socket send_socket(io_context);
    tcp::socket receive_socket(io_context);

    // Connect both sockets
    boost::asio::connect(send_socket, endpoints);
    cout << "send_socket connected" << endl;
    boost::asio::connect(receive_socket, endpoints);
    cout << "receive_socket connected" << endl;
    // Start the async receive loop on the receiving socket
    
    // Start commandListener in a separate thread to handle commands via the send socket
    std::thread command_thread([&]() {
        commandListener(send_socket, receive_socket);
        });
    std::cout << "Start commandListener in its own thread" << std::endl;

    // Run io_context in another thread to handle async operations
    std::thread io_thread([&]() {
        asyncReceiveMessage(io_context, receive_socket);
        io_context.run();
        });
    std::cout << "Start io_context.run() in a separate thread for async operations" << std::endl;

    command_thread.join();
    io_thread.join();

    return 0;
}