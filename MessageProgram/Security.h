#ifndef _SECURITY_
#define _SECURITY_

#include <algorithm>
#include <codecvt>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <locale>
#include <string>
#include <string.h>
#include <vector>
#include <windows.h>

void Copy(const std::string& file_name, const std::string& message_now_file_name);
void Encryption(const std::string& encrypted_file_name, const std::string& message_now_file_name);
void Decryption(const std::string& encrypted_file_name, const std::string& conversation_file_name);
bool EnsureUserKeys(const std::string& user_id, std::string& public_key_hex, std::string& error_message);
bool CacheContactPublicKey(const std::string& owner_user_id, const std::string& contact_id, const std::string& public_key_hex, std::string& warning_message, std::string& error_message);
bool LoadContactPublicKey(const std::string& owner_user_id, const std::string& contact_id, std::string& public_key_hex, std::string& error_message);
std::string GetPublicKeyFingerprint(const std::string& public_key_hex);
bool EncryptMessageForConversation(const std::string& sender_id, const std::string& recipient_id, const std::string& message, std::string& encrypted_message, std::string& error_message);
bool EncryptMessageForRecipient(const std::string& sender_id, const std::string& recipient_id, const std::string& message, std::string& encrypted_message, std::string& error_message);
bool DecryptMessageForUser(const std::string& recipient_id, const std::string& sender_id, const std::string& message, std::string& decrypted_message, std::string& error_message);
bool WrapPlaintextMessage(const std::string& message, std::string& wrapped_message, std::string& error_message);
bool UnwrapPlaintextMessage(const std::string& message, std::string& plaintext_message, std::string& error_message);

#endif // !_SECURITY_
