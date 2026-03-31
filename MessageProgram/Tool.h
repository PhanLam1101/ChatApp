#ifndef _TOOL_HEADER_
#define _TOOL_HEADER_
#include <iostream>
#include <cstdlib>
#include <fstream>
#include <string>
#include <string.h>
#include <filesystem>
#include <iomanip>
#include <chrono>
#include <ctime>
#include <sstream>
#include <codecvt>
#include <locale>
#include <regex>

#include <sstream>
#include <boost/asio.hpp>
#include <boost/filesystem.hpp>
namespace fss = std::filesystem;
using namespace std;
std::string ioContextToString(boost::asio::io_context& io_context);
std::string socketToString(boost::asio::ip::tcp::socket& socket);
std::string acceptorToString(boost::asio::ip::tcp::acceptor& acceptor);
std::string sanitizeFilename(const std::string& filename);
std::string replaceWhiteSpacesWithUnderscores(const std::string& str);
string QuoteRemoval(string sentence);
void CreateFolder(string folder_name);
string ReadIP(void);
std::string GetConversationFileName(const std::string& person1, const std::string& person2);
string StringReplace(string s, char replaced_char, char new_char);
void appendToFile(const std::string& file_name, const std::string& content);
std::string addDoubleBackslashes(const std::string& input);
std::string getCurrentDateTime();

#endif // !_TOOL_HEADER_
