#ifndef _SECURITY_
#define _SECURITY_
#include <iostream>
#include <cstdlib>
#include <fstream>
#include <string>
#include <string.h>
#include <filesystem>
#include <vector>
#include <algorithm>
#include <codecvt>
#include <locale>
//#include <cryptlib.h>
//#include <aes.h>
//#include <modes.h>
//#include <filters.h>
//#include <stdexcept>


void Copy(const std::string& file_name, const std::string& message_now_file_name);
void Encryption(const std::string& encrypted_file_name, const std::string& message_now_file_name);
void Decryption(const std::string& encrypted_file_name, const std::string& conversation_file_name);
void DecryptMessage(const std::string& message, std::string& decrypted_message);
void EncryptMessage(const std::string& message, std::string& encrypted_message);

#endif // !_SECURITY_

