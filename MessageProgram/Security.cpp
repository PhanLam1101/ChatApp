#include "Security.h"
void Copy(const std::string& file_name, const std::string& message_now_file_name) {
    std::ifstream message_file(message_now_file_name);
    if (!message_file) {
        std::cerr << "Error opening file: " << message_now_file_name << std::endl;
        return;
    }

    std::ofstream encrypted_file(file_name); // Open in append mode
    if (!encrypted_file) {
        std::cerr << "Error opening file: " << file_name << std::endl;
        return;
    }

    std::string temp;
    int line_number = 0;

    while (getline(message_file, temp)) {
        ++line_number;
        encrypted_file << temp << std::endl;
    }

    message_file.close();
    encrypted_file.close();
}
void Encryption(const std::string& encrypted_file_name, const std::string& message_now_file_name) {
    std::ifstream message_file(message_now_file_name);
    if (!message_file) {
        std::cerr << "Error opening file: " << message_now_file_name << std::endl;
        return;
    }

    std::ofstream encrypted_file(encrypted_file_name); // Open in append mode
    if (!encrypted_file) {
        std::cerr << "Error opening file: " << encrypted_file_name << std::endl;
        return;
    }

    std::string temp;
    int line_number = 0;

    while (getline(message_file, temp)) {
        ++line_number;
        encrypted_file << temp << std::endl;
    }

    message_file.close();
    encrypted_file.close();
}
void Decryption(const std::string& encrypted_file_name, const std::string& conversation_file_name) {
    std::ifstream encrypted_file(encrypted_file_name);
    if (!encrypted_file) {
        std::cerr << "Error opening file: " << encrypted_file_name << std::endl;
        return;
    }

    std::ofstream conversation_file(conversation_file_name, std::ios::app); // Open in append mode
    if (!conversation_file) {
        std::cerr << "Error opening file: " << conversation_file_name << std::endl;
        return;
    }

    std::string temp;
    while (getline(encrypted_file, temp)) {
        conversation_file << temp << std::endl;
    }

    encrypted_file.close();
    conversation_file.close();
}
// Predefined AES key and IV (Initialization Vector)
//const CryptoPP::byte key[CryptoPP::AES::DEFAULT_KEYLENGTH] = {
//    0x60, 0x3d, 0xeb, 0x10, 0x15, 0xca, 0x71, 0xbe,
//    0x2b, 0x73, 0xae, 0xf0, 0x85, 0x7d, 0x77, 0x81
//};
//
//const CryptoPP::byte iv[CryptoPP::AES::BLOCKSIZE] = {
//    0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07,
//    0x08, 0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x0e, 0x0f
//};

//void EncryptMessage(const std::string& message, std::string& encrypted_message) {
//    try {
//        CryptoPP::CBC_Mode<CryptoPP::AES>::Encryption encryptor(key, sizeof(key), iv);
//
//        // Encrypt with padding
//        CryptoPP::StringSource encryptorStream(
//            message,
//            true,
//            new CryptoPP::StreamTransformationFilter(
//                encryptor,
//                new CryptoPP::StringSink(encrypted_message),
//                CryptoPP::BlockPaddingSchemeDef::PKCS_PADDING
//            )
//        );
//    }
//    catch (const CryptoPP::Exception& ex) {
//        std::cerr << "Encryption failed: " << ex.what() << std::endl;
//        throw std::runtime_error("Encryption failed.");
//    }
//}
//
//void DecryptMessage(const std::string& message, std::string& decrypted_message) {
//    try {
//        CryptoPP::CBC_Mode<CryptoPP::AES>::Decryption decryptor(key, sizeof(key), iv);
//
//        // Decrypt with padding
//        CryptoPP::StringSource decryptorStream(
//            message,
//            true,
//            new CryptoPP::StreamTransformationFilter(
//                decryptor,
//                new CryptoPP::StringSink(decrypted_message),
//                CryptoPP::BlockPaddingSchemeDef::PKCS_PADDING
//            )
//        );
//    }
//    catch (const CryptoPP::Exception& ex) {
//        std::cerr << "Decryption failed: " << ex.what() << std::endl;
//        throw std::runtime_error("Decryption failed.");
//    }
//}

void EncryptMessage(const std::string& message, std::string& encrypted_message) {
    encrypted_message = "";
    int key = 3; // You can change the key here
    for (char ch : message) {
        if (isalpha(ch)) {
            char base = islower(ch) ? 'a' : 'A';
            encrypted_message += base + (ch - base + key) % 26;
        }
        else {
            encrypted_message += ch;
        }
    }
}

void DecryptMessage(const std::string& message, std::string& decrypted_message) {
    decrypted_message = "";
    int key = 3; // Make sure to use the same key for decryption
    for (char ch : message) {
        if (isalpha(ch)) {
            char base = islower(ch) ? 'a' : 'A';
            decrypted_message += base + (ch - base - key + 26) % 26;
        }
        else {
            decrypted_message += ch;
        }
    }
}
// 
//int key = 5;
//// Encrypt message using a Caesar cipher
//void EncryptMessage(const std::string& message, std::string& encrypted_message) {
//    encrypted_message.clear();
//
//    for (char ch : message) {
//        // Shift alphabetic characters
//        if (isalpha(ch)) {
//            char base = islower(ch) ? 'a' : 'A';
//            encrypted_message += base + (ch - base + key) % 26;
//        }
//        // Shift numeric characters
//        else if (isdigit(ch)) {
//            encrypted_message += '0' + (ch - '0' + key) % 10;
//        }
//        // Shift other characters within the UTF-7 safe range (32 to 126)
//        else if (ch >= 32 && ch <= 126) {
//            encrypted_message += 32 + (ch - 32 + key) % 95;
//        }
//        // Leave unsupported characters unchanged
//        else {
//            encrypted_message += ch;
//        }
//    }
//}
//
//// Decrypt message using a Caesar cipher
//void DecryptMessage(const std::string& encrypted_message, std::string& decrypted_message) {
//    decrypted_message.clear();
//
//    for (char ch : encrypted_message) {
//        // Unshift alphabetic characters
//        if (isalpha(ch)) {
//            char base = islower(ch) ? 'a' : 'A';
//            decrypted_message += base + (ch - base - key + 26) % 26;
//        }
//        // Unshift numeric characters
//        else if (isdigit(ch)) {
//            decrypted_message += '0' + (ch - '0' - key + 10) % 10;
//        }
//        // Unshift other characters within the UTF-7 safe range (32 to 126)
//        else if (ch >= 32 && ch <= 126) {
//            decrypted_message += 32 + (ch - 32 - key + 95) % 95;
//        }
//        // Leave unsupported characters unchanged
//        else {
//            decrypted_message += ch;
//        }
//    }
//}
const std::string KEY = "hMv68ZbA"; // 16 characters (128 bits)

// Generate a pseudo-random key sequence
//std::vector<int> GenerateKeySequence(const std::string& key, size_t length) {
//    std::vector<int> key_sequence;
//    for (size_t i = 0; i < length; ++i) {
//        key_sequence.push_back(static_cast<int>(key[i % key.length()]) % 256);
//    }
//    return key_sequence;
//}
//
//void EncryptMessage(const std::string& message, std::string& encrypted_message) {
//    encrypted_message.clear();
//    std::vector<int> key_sequence = GenerateKeySequence(KEY, message.length());
//
//    for (size_t i = 0; i < message.length(); ++i) {
//        char ch = message[i];
//        encrypted_message += static_cast<char>((ch + key_sequence[i]) % 256);
//    }
//}
//
//void DecryptMessage(const std::string& encrypted_message, std::string& decrypted_message) {
//    decrypted_message.clear();
//    std::vector<int> key_sequence = GenerateKeySequence(KEY, encrypted_message.length());
//
//    for (size_t i = 0; i < encrypted_message.length(); ++i) {
//        char ch = encrypted_message[i];
//        decrypted_message += static_cast<char>((ch - key_sequence[i] + 256) % 256);
//    }
//}