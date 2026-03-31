#include "Tool.h"
using namespace std;

std::string ioContextToString(boost::asio::io_context& io_context) {
	std::ostringstream oss;
	oss << "IO Context: " << &io_context;  // Print the address for debugging
	return oss.str();
}
std::string socketToString(boost::asio::ip::tcp::socket& socket) {
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
std::string acceptorToString(boost::asio::ip::tcp::acceptor& acceptor) {
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
std::string sanitizeFilename(const std::string& filename) {
	// Replace special characters with underscores
	std::string sanitized = std::regex_replace(filename, std::regex(R"([^\w\d\._-])"), "_");

	// Ensure the filename does not exceed 255 characters (common filesystem limit)
	constexpr size_t max_length = 255;
	if (sanitized.length() > max_length) {
		size_t extension_pos = sanitized.find_last_of('.');
		if (extension_pos == std::string::npos || sanitized.length() - extension_pos > max_length) {
			sanitized = sanitized.substr(0, max_length);
		}
		else {
			size_t trim_length = max_length - (sanitized.length() - extension_pos);
			sanitized = sanitized.substr(0, trim_length) + sanitized.substr(extension_pos);
		}
	}

	return sanitized;
}
std::string replaceWhiteSpacesWithUnderscores(const std::string& str) {
	std::string result;
	for (char ch : str) {
		if (isspace(ch)) {
			result += '_'; // Replace space with underscore
		}
		else {
			result += ch;
		}
	}
	return result;
}
std::string ToLower(const std::string& str) {
	std::string lower = str;
	std::transform(lower.begin(), lower.end(), lower.begin(), ::tolower);
	return lower;
}

std::string GetConversationFileName(const std::string& person1, const std::string& person2) {
	std::string lower1 = ToLower(person1);
	std::string lower2 = ToLower(person2);
	return (lower1 > lower2)
		? (person1 + "-" + person2)
		: (person2 + "-" + person1);
}

//std::string GetConversationFileName(const std::string& person1, const std::string& person2) {
//	return (person1 > person2)
//		? (person1 + "-" + person2)
//		: (person2 + "-" + person1);
//}
std::string ReadIP() {
	std::string nameFile = "IP_Server.txt";
	std::ifstream IPFile(nameFile);
	std::string IP;

	// Check if the file exists and is readable
	if (!IPFile.is_open()) {
		std::cerr << "Error: Could not open file " << nameFile << ". Creating a default IP file." << std::endl;

		// Create the file and write a default IP address
		std::ofstream defaultIPFile(nameFile);
		if (!defaultIPFile.is_open()) {
			std::cerr << "Error: Failed to create the file " << nameFile << std::endl;
			return "";
		}
		std::string defaultIP = "127.0.0.1"; // Default IP address
		defaultIPFile << defaultIP;
		defaultIPFile.close();

		// Return the default IP
		return defaultIP;
	}

	// Read the IP address
	std::getline(IPFile, IP);
	IPFile.close();

	// Trim whitespace (if any)
	IP.erase(0, IP.find_first_not_of(" \t\n\r"));
	IP.erase(IP.find_last_not_of(" \t\n\r") + 1);

	// Return the read IP address
	return IP;
}
string QuoteRemoval(string sentence)
{
	int i;
	int j = 0, count = 0;
	string temp;
	ofstream file_temp1;
	ifstream file_temp2;
	file_temp1.open("temp.txt");
	for (i = 0; i < (sentence.size() - 2); i++)
	{
		file_temp1 << '0';
	}
	file_temp1.close();
	file_temp2.open("temp.txt");
	file_temp2 >> temp;
	file_temp2.close();
	remove("temp.txt");
	for (i = 0; i < sentence.size() - 2; i++)
	{
		temp[i] = sentence[i + 1];
	}
	return temp;
}
void CreateFolder(string folder_name)
{
	if (fss::exists(folder_name))
	{
		if (fss::is_directory(folder_name))
		{
			//cout << "Alles gut!" << endl;
		}
		else
		{
			cout << "There is an error at folder Download, please check it!" << std::endl;
		}
	}
	else
	{
		try {
			if (fss::create_directory(folder_name)) {
				std::cout << "The folder Download has been created successfully!" << std::endl;
			}
			else {
				std::cerr << "Failed to create the folder Download." << std::endl;
			}
		}
		catch (const std::filesystem::filesystem_error& e) {
			std::cerr << "Filesystem error: " << e.what() << std::endl;
		}
	}
}
string StringReplace(string s,
	char replaced_char, char new_char)
{
	int l = s.length();

	for (int i = 0; i < l; i++)
	{
		if (s[i] == replaced_char)
			s[i] = new_char;

		else if (s[i] == new_char)
			s[i] = replaced_char;
	}
	return s;
}
void appendToFile(const std::string& file_name, const std::string& content) {
	std::ofstream file(file_name, std::ios::app);

	// Check if the file opened successfully
	if (!file) {
		std::cerr << "Error: Unable to open file for appending: " << file_name << std::endl;
		return;
	}

	// Append the content to the file
	file << content << std::endl;

	// Check if the write was successful
	if (!file) {
		std::cerr << "Error: Failed to write to file: " << file_name << std::endl;
	}

	file.close();
}
std::string addDoubleBackslashes(const std::string& input) {
	std::string result = input;
	size_t pos = 0;
	while ((pos = result.find("\\", pos)) != std::string::npos) {
		result.replace(pos, 1, "\\\\"); // Replaces '\' with '\\'
		pos += 2; // Move past the new double backslash
	}
	return result;
}
std::string getCurrentDateTime() {
	// Get current time
	auto now = std::chrono::system_clock::now();
	// Convert to time_t to use with std::localtime
	std::time_t now_time = std::chrono::system_clock::to_time_t(now);

	// Use localtime_s for thread-safe local time conversion on Windows
	std::tm local_time;
	localtime_s(&local_time, &now_time);

	// Format and store in a string
	std::ostringstream oss;
	oss << '['
		<< std::setw(2) << std::setfill('0') << local_time.tm_hour << ':'
		<< std::setw(2) << std::setfill('0') << local_time.tm_min << '-'
		<< std::setw(2) << std::setfill('0') << local_time.tm_mday << '/'
		<< std::setw(2) << std::setfill('0') << (local_time.tm_mon + 1) << '/'
		<< (local_time.tm_year + 1900) << ']';
	return oss.str();
}
