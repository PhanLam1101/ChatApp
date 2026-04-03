#ifndef _TOOLs_HEADER_
#define _TOOLs_HEADER_
#include <iostream>
#include <cstdlib>
#include <fstream>
#include <thread>
#include <unordered_map>
#include <string>
#include <string.h>
#include <boost/asio.hpp>
#include <boost/filesystem.hpp>
#include <boost/asio/deadline_timer.hpp>


using namespace std;
inline unordered_map<string, string> GetAccounts(string file_account);
inline unordered_map<string, string> GetAccounts(string file_account)
{
	ifstream File;
	unordered_map<string, string>Account;
	File.open(file_account.c_str());
	if (!File.is_open())
	{
		cerr << "Error: cannot open Account file" << endl;
	}
	string ID;
	string password;
	while (File >> ID >> password)
	{
		Account[ID] = password;
	}
	File.close();
	/*for (const auto& pair : Account) {
		std::cout << pair.first << ": " << pair.second << std::endl;
	}*/
	return Account;
}
inline std::string ToLower(const std::string& str) {
	std::string lower = str;
	std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
	return lower;
}

inline std::string GetConversationFileName(const std::string& person1, const std::string& person2) {
	std::string lower1 = ToLower(person1);
	std::string lower2 = ToLower(person2);
	return (lower1 > lower2)
		? (person1 + "-" + person2)
		: (person2 + "-" + person1);
}

//std::string GetConversationFileName(const std::string& person1, const std::string& person2)
//{
//	return (person1 > person2)
//		? (person1 + "-" + person2)
//		: (person2 + "-" + person1);
//}
inline std::string ioContextToString(boost::asio::io_context& io_context) {
	std::ostringstream oss;
	oss << "IO Context: " << &io_context;  // Print the address for debugging
	return oss.str();
}
inline std::string socketToString(boost::asio::ip::tcp::socket& socket) {
	std::ostringstream oss;
	try {
		auto local_endpoint = socket.local_endpoint();
		auto remote_endpoint = socket.remote_endpoint();

		oss << "Socket: Local [" << local_endpoint.address().to_string()
			<< ":" << local_endpoint.port() << "] Remote ["
			<< remote_endpoint.address().to_string()
			<< ":" << remote_endpoint.port() << "]";
	}
	catch (const boost::system::system_error& e) {
		oss << "Socket: Not connected (" << e.what() << ")";
	}
	return oss.str();
}
inline std::string acceptorToString(boost::asio::ip::tcp::acceptor& acceptor) {
	std::ostringstream oss;
	try {
		auto local_endpoint = acceptor.local_endpoint();
		oss << "Acceptor: Local [" << local_endpoint.address().to_string()
			<< ":" << local_endpoint.port() << "]";
	}
	catch (const boost::system::system_error& e) {
		oss << "Acceptor: Not open (" << e.what() << ")";
	}
	return oss.str();
}

#endif // !_TOOLs_HEADER_
