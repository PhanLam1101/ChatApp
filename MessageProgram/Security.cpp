#include "Security.h"

#include <bcrypt.h>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <iomanip>
#include <sstream>

#pragma comment(lib, "bcrypt.lib")

namespace
{
    constexpr size_t AesKeySize = 32;
    constexpr size_t GcmNonceSize = 12;
    constexpr size_t GcmTagSize = 16;
    const std::string PayloadPrefix = "E2EE1";
    const std::string ConversationPayloadPrefix = "E2EE2";
    const std::string PlaintextPayloadPrefix = "PLAIN1";

    bool IsSuccess(NTSTATUS status)
    {
        return status >= 0;
    }

    std::string TrimCopy(const std::string& value)
    {
        size_t start = value.find_first_not_of(" \t\r\n");
        if (start == std::string::npos)
        {
            return std::string();
        }

        size_t end = value.find_last_not_of(" \t\r\n");
        return value.substr(start, end - start + 1);
    }

    std::string BytesToHex(const std::vector<unsigned char>& bytes)
    {
        std::ostringstream output;
        output << std::hex << std::setfill('0');
        for (unsigned char value : bytes)
        {
            output << std::setw(2) << static_cast<int>(value);
        }

        return output.str();
    }

    bool HexToBytes(const std::string& hex, std::vector<unsigned char>& bytes)
    {
        if ((hex.size() % 2) != 0)
        {
            return false;
        }

        bytes.clear();
        bytes.reserve(hex.size() / 2);
        for (size_t index = 0; index < hex.size(); index += 2)
        {
            unsigned int value = 0;
            std::istringstream input(hex.substr(index, 2));
            input >> std::hex >> value;
            if (input.fail())
            {
                return false;
            }

            bytes.push_back(static_cast<unsigned char>(value));
        }

        return true;
    }

    std::vector<std::string> Split(const std::string& value, char delimiter)
    {
        std::vector<std::string> parts;
        std::stringstream stream(value);
        std::string part;
        while (std::getline(stream, part, delimiter))
        {
            parts.push_back(part);
        }

        return parts;
    }

    std::string ReadEnvironmentVariable(const char* variableName)
    {
        char* value = nullptr;
        size_t length = 0;
        if (_dupenv_s(&value, &length, variableName) != 0 || value == nullptr)
        {
            return std::string();
        }

        std::string result(value);
        std::free(value);
        return result;
    }

    std::filesystem::path GetProfilesRootPath()
    {
        std::string configured_root = ReadEnvironmentVariable("MESSAGINGAPP_PROFILES_ROOT");
        if (!configured_root.empty())
        {
            return std::filesystem::path(configured_root);
        }

        return std::filesystem::current_path();
    }

    std::filesystem::path GetUserProfileRoot(const std::string& user_id)
    {
        return GetProfilesRootPath() / user_id;
    }

    std::filesystem::path GetUserKeyDirectory(const std::string& user_id)
    {
        return GetUserProfileRoot(user_id) / "Keys";
    }

    std::filesystem::path GetUserPrivateKeyPath(const std::string& user_id)
    {
        return GetUserKeyDirectory(user_id) / "private.key";
    }

    std::filesystem::path GetUserPublicKeyPath(const std::string& user_id)
    {
        return GetUserKeyDirectory(user_id) / "public.key";
    }

    std::filesystem::path GetContactKeysDirectory(const std::string& owner_user_id)
    {
        return GetUserKeyDirectory(owner_user_id) / "Contacts";
    }

    std::filesystem::path GetContactPublicKeyPath(const std::string& owner_user_id, const std::string& contact_id)
    {
        return GetContactKeysDirectory(owner_user_id) / (contact_id + ".public.key");
    }

    bool ReadTextFile(const std::filesystem::path& path, std::string& content)
    {
        std::ifstream input(path, std::ios::binary);
        if (!input.is_open())
        {
            return false;
        }

        content.assign(std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>());
        content = TrimCopy(content);
        return true;
    }

    bool WriteTextFile(const std::filesystem::path& path, const std::string& content, std::string& error_message)
    {
        try
        {
            std::filesystem::create_directories(path.parent_path());
            std::ofstream output(path, std::ios::binary | std::ios::trunc);
            if (!output.is_open())
            {
                error_message = "Unable to open " + path.string() + " for writing.";
                return false;
            }

            output << content;
            return true;
        }
        catch (const std::exception& exception)
        {
            error_message = exception.what();
            return false;
        }
    }

    bool GenerateRandomBytes(size_t byte_count, std::vector<unsigned char>& bytes, std::string& error_message)
    {
        bytes.resize(byte_count);
        NTSTATUS status = BCryptGenRandom(nullptr, bytes.data(), static_cast<ULONG>(bytes.size()), BCRYPT_USE_SYSTEM_PREFERRED_RNG);
        if (!IsSuccess(status))
        {
            error_message = "Failed to generate secure random bytes.";
            return false;
        }

        return true;
    }

    bool ExportKeyBlob(BCRYPT_KEY_HANDLE key_handle, LPCWSTR blob_type, std::vector<unsigned char>& blob, std::string& error_message)
    {
        ULONG blob_size = 0;
        ULONG bytes_copied = 0;
        NTSTATUS status = BCryptExportKey(key_handle, nullptr, blob_type, nullptr, 0, &blob_size, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to determine key blob size.";
            return false;
        }

        blob.resize(blob_size);
        status = BCryptExportKey(key_handle, nullptr, blob_type, blob.data(), blob_size, &bytes_copied, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to export key blob.";
            return false;
        }

        blob.resize(bytes_copied);
        return true;
    }

    bool GenerateRsaKeyPair(std::vector<unsigned char>& public_blob, std::vector<unsigned char>& private_blob, std::string& error_message)
    {
        BCRYPT_ALG_HANDLE algorithm_handle = nullptr;
        BCRYPT_KEY_HANDLE key_handle = nullptr;

        NTSTATUS status = BCryptOpenAlgorithmProvider(&algorithm_handle, BCRYPT_RSA_ALGORITHM, nullptr, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to open the RSA provider.";
            return false;
        }

        status = BCryptGenerateKeyPair(algorithm_handle, &key_handle, 2048, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to generate the RSA key pair.";
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        status = BCryptFinalizeKeyPair(key_handle, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to finalize the RSA key pair.";
            BCryptDestroyKey(key_handle);
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        bool public_ok = ExportKeyBlob(key_handle, BCRYPT_RSAPUBLIC_BLOB, public_blob, error_message);
        bool private_ok = public_ok && ExportKeyBlob(key_handle, BCRYPT_RSAFULLPRIVATE_BLOB, private_blob, error_message);

        BCryptDestroyKey(key_handle);
        BCryptCloseAlgorithmProvider(algorithm_handle, 0);
        return public_ok && private_ok;
    }

    bool ImportRsaPublicKey(const std::string& public_key_hex, BCRYPT_KEY_HANDLE& key_handle, std::string& error_message)
    {
        key_handle = nullptr;
        std::vector<unsigned char> public_blob;
        if (!HexToBytes(public_key_hex, public_blob))
        {
            error_message = "The stored public key is not valid hex.";
            return false;
        }

        BCRYPT_ALG_HANDLE algorithm_handle = nullptr;
        NTSTATUS status = BCryptOpenAlgorithmProvider(&algorithm_handle, BCRYPT_RSA_ALGORITHM, nullptr, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to open the RSA provider.";
            return false;
        }

        status = BCryptImportKeyPair(
            algorithm_handle,
            nullptr,
            BCRYPT_RSAPUBLIC_BLOB,
            &key_handle,
            public_blob.data(),
            static_cast<ULONG>(public_blob.size()),
            0);
        BCryptCloseAlgorithmProvider(algorithm_handle, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to import the public key.";
            return false;
        }

        return true;
    }

    bool ImportRsaPrivateKey(const std::string& private_key_hex, BCRYPT_KEY_HANDLE& key_handle, std::string& error_message)
    {
        key_handle = nullptr;
        std::vector<unsigned char> private_blob;
        if (!HexToBytes(private_key_hex, private_blob))
        {
            error_message = "The stored private key is not valid hex.";
            return false;
        }

        BCRYPT_ALG_HANDLE algorithm_handle = nullptr;
        NTSTATUS status = BCryptOpenAlgorithmProvider(&algorithm_handle, BCRYPT_RSA_ALGORITHM, nullptr, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to open the RSA provider.";
            return false;
        }

        status = BCryptImportKeyPair(
            algorithm_handle,
            nullptr,
            BCRYPT_RSAFULLPRIVATE_BLOB,
            &key_handle,
            private_blob.data(),
            static_cast<ULONG>(private_blob.size()),
            0);
        BCryptCloseAlgorithmProvider(algorithm_handle, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to import the private key.";
            return false;
        }

        return true;
    }

    bool EncryptAesKeyWithRsa(const std::string& public_key_hex, const std::vector<unsigned char>& aes_key, std::vector<unsigned char>& encrypted_key, std::string& error_message)
    {
        BCRYPT_KEY_HANDLE public_key_handle = nullptr;
        if (!ImportRsaPublicKey(public_key_hex, public_key_handle, error_message))
        {
            return false;
        }

        BCRYPT_OAEP_PADDING_INFO padding_info = { BCRYPT_SHA256_ALGORITHM, nullptr, 0 };
        ULONG encrypted_key_size = 0;
        NTSTATUS status = BCryptEncrypt(
            public_key_handle,
            const_cast<PUCHAR>(aes_key.data()),
            static_cast<ULONG>(aes_key.size()),
            &padding_info,
            nullptr,
            0,
            nullptr,
            0,
            &encrypted_key_size,
            BCRYPT_PAD_OAEP);
        if (!IsSuccess(status))
        {
            error_message = "Failed to determine the wrapped AES key size.";
            BCryptDestroyKey(public_key_handle);
            return false;
        }

        encrypted_key.resize(encrypted_key_size);
        status = BCryptEncrypt(
            public_key_handle,
            const_cast<PUCHAR>(aes_key.data()),
            static_cast<ULONG>(aes_key.size()),
            &padding_info,
            nullptr,
            0,
            encrypted_key.data(),
            encrypted_key_size,
            &encrypted_key_size,
            BCRYPT_PAD_OAEP);
        BCryptDestroyKey(public_key_handle);
        if (!IsSuccess(status))
        {
            error_message = "Failed to wrap the AES key.";
            return false;
        }

        encrypted_key.resize(encrypted_key_size);
        return true;
    }

    bool DecryptAesKeyWithRsa(const std::string& private_key_hex, const std::vector<unsigned char>& encrypted_key, std::vector<unsigned char>& aes_key, std::string& error_message)
    {
        BCRYPT_KEY_HANDLE private_key_handle = nullptr;
        if (!ImportRsaPrivateKey(private_key_hex, private_key_handle, error_message))
        {
            return false;
        }

        BCRYPT_OAEP_PADDING_INFO padding_info = { BCRYPT_SHA256_ALGORITHM, nullptr, 0 };
        ULONG aes_key_size = 0;
        NTSTATUS status = BCryptDecrypt(
            private_key_handle,
            const_cast<PUCHAR>(encrypted_key.data()),
            static_cast<ULONG>(encrypted_key.size()),
            &padding_info,
            nullptr,
            0,
            nullptr,
            0,
            &aes_key_size,
            BCRYPT_PAD_OAEP);
        if (!IsSuccess(status))
        {
            error_message = "Failed to determine the AES key size during unwrap.";
            BCryptDestroyKey(private_key_handle);
            return false;
        }

        aes_key.resize(aes_key_size);
        status = BCryptDecrypt(
            private_key_handle,
            const_cast<PUCHAR>(encrypted_key.data()),
            static_cast<ULONG>(encrypted_key.size()),
            &padding_info,
            nullptr,
            0,
            aes_key.data(),
            aes_key_size,
            &aes_key_size,
            BCRYPT_PAD_OAEP);
        BCryptDestroyKey(private_key_handle);
        if (!IsSuccess(status))
        {
            error_message = "Failed to unwrap the AES key.";
            return false;
        }

        aes_key.resize(aes_key_size);
        return true;
    }

    bool EncryptWithAesGcm(
        const std::vector<unsigned char>& aes_key,
        const std::string& plaintext,
        std::vector<unsigned char>& nonce,
        std::vector<unsigned char>& tag,
        std::vector<unsigned char>& ciphertext,
        std::string& error_message)
    {
        if (!GenerateRandomBytes(GcmNonceSize, nonce, error_message))
        {
            return false;
        }

        BCRYPT_ALG_HANDLE algorithm_handle = nullptr;
        BCRYPT_KEY_HANDLE key_handle = nullptr;
        NTSTATUS status = BCryptOpenAlgorithmProvider(&algorithm_handle, BCRYPT_AES_ALGORITHM, nullptr, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to open the AES provider.";
            return false;
        }

        status = BCryptSetProperty(
            algorithm_handle,
            BCRYPT_CHAINING_MODE,
            reinterpret_cast<PUCHAR>(const_cast<wchar_t*>(BCRYPT_CHAIN_MODE_GCM)),
            static_cast<ULONG>((wcslen(BCRYPT_CHAIN_MODE_GCM) + 1) * sizeof(wchar_t)),
            0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to enable AES-GCM mode.";
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        ULONG key_object_size = 0;
        ULONG bytes_copied = 0;
        status = BCryptGetProperty(algorithm_handle, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&key_object_size), sizeof(key_object_size), &bytes_copied, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to read the AES key object size.";
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        std::vector<unsigned char> key_object(key_object_size);
        status = BCryptGenerateSymmetricKey(
            algorithm_handle,
            &key_handle,
            key_object.data(),
            key_object_size,
            const_cast<PUCHAR>(aes_key.data()),
            static_cast<ULONG>(aes_key.size()),
            0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to create the AES key handle.";
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        tag.resize(GcmTagSize);
        ciphertext.resize(plaintext.size());
        BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO auth_info;
        BCRYPT_INIT_AUTH_MODE_INFO(auth_info);
        auth_info.pbNonce = nonce.data();
        auth_info.cbNonce = static_cast<ULONG>(nonce.size());
        auth_info.pbTag = tag.data();
        auth_info.cbTag = static_cast<ULONG>(tag.size());

        ULONG ciphertext_size = 0;
        status = BCryptEncrypt(
            key_handle,
            reinterpret_cast<PUCHAR>(const_cast<char*>(plaintext.data())),
            static_cast<ULONG>(plaintext.size()),
            &auth_info,
            nullptr,
            0,
            ciphertext.data(),
            static_cast<ULONG>(ciphertext.size()),
            &ciphertext_size,
            0);

        BCryptDestroyKey(key_handle);
        BCryptCloseAlgorithmProvider(algorithm_handle, 0);
        if (!IsSuccess(status))
        {
            error_message = "AES-GCM encryption failed.";
            return false;
        }

        ciphertext.resize(ciphertext_size);
        return true;
    }

    bool DecryptWithAesGcm(
        const std::vector<unsigned char>& aes_key,
        const std::vector<unsigned char>& nonce,
        const std::vector<unsigned char>& tag,
        const std::vector<unsigned char>& ciphertext,
        std::string& plaintext,
        std::string& error_message)
    {
        BCRYPT_ALG_HANDLE algorithm_handle = nullptr;
        BCRYPT_KEY_HANDLE key_handle = nullptr;
        NTSTATUS status = BCryptOpenAlgorithmProvider(&algorithm_handle, BCRYPT_AES_ALGORITHM, nullptr, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to open the AES provider.";
            return false;
        }

        status = BCryptSetProperty(
            algorithm_handle,
            BCRYPT_CHAINING_MODE,
            reinterpret_cast<PUCHAR>(const_cast<wchar_t*>(BCRYPT_CHAIN_MODE_GCM)),
            static_cast<ULONG>((wcslen(BCRYPT_CHAIN_MODE_GCM) + 1) * sizeof(wchar_t)),
            0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to enable AES-GCM mode.";
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        ULONG key_object_size = 0;
        ULONG bytes_copied = 0;
        status = BCryptGetProperty(algorithm_handle, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&key_object_size), sizeof(key_object_size), &bytes_copied, 0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to read the AES key object size.";
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        std::vector<unsigned char> key_object(key_object_size);
        status = BCryptGenerateSymmetricKey(
            algorithm_handle,
            &key_handle,
            key_object.data(),
            key_object_size,
            const_cast<PUCHAR>(aes_key.data()),
            static_cast<ULONG>(aes_key.size()),
            0);
        if (!IsSuccess(status))
        {
            error_message = "Failed to create the AES key handle.";
            BCryptCloseAlgorithmProvider(algorithm_handle, 0);
            return false;
        }

        std::vector<unsigned char> plaintext_buffer(ciphertext.size());
        BCRYPT_AUTHENTICATED_CIPHER_MODE_INFO auth_info;
        BCRYPT_INIT_AUTH_MODE_INFO(auth_info);
        auth_info.pbNonce = const_cast<PUCHAR>(nonce.data());
        auth_info.cbNonce = static_cast<ULONG>(nonce.size());
        auth_info.pbTag = const_cast<PUCHAR>(tag.data());
        auth_info.cbTag = static_cast<ULONG>(tag.size());

        ULONG plaintext_size = 0;
        status = BCryptDecrypt(
            key_handle,
            const_cast<PUCHAR>(ciphertext.data()),
            static_cast<ULONG>(ciphertext.size()),
            &auth_info,
            nullptr,
            0,
            plaintext_buffer.data(),
            static_cast<ULONG>(plaintext_buffer.size()),
            &plaintext_size,
            0);

        BCryptDestroyKey(key_handle);
        BCryptCloseAlgorithmProvider(algorithm_handle, 0);
        if (!IsSuccess(status))
        {
            error_message = "AES-GCM decryption failed or the message was tampered with.";
            return false;
        }

        plaintext.assign(reinterpret_cast<char*>(plaintext_buffer.data()), plaintext_size);
        return true;
    }

    std::string DecryptLegacyMessage(const std::string& message)
    {
        std::string decrypted_message;
        const int key = 3;
        for (char character : message)
        {
            if (isalpha(static_cast<unsigned char>(character)))
            {
                char base = islower(static_cast<unsigned char>(character)) ? 'a' : 'A';
                decrypted_message += base + (character - base - key + 26) % 26;
            }
            else
            {
                decrypted_message += character;
            }
        }

        return decrypted_message;
    }

    bool LoadOwnPublicKey(const std::string& user_id, std::string& public_key_hex, std::string& error_message)
    {
        return EnsureUserKeys(user_id, public_key_hex, error_message);
    }

    bool EncryptMessageWithPublicKeyHex(const std::string& public_key_hex, const std::string& message, std::string& encrypted_message, std::string& error_message)
    {
        encrypted_message.clear();
        error_message.clear();

        std::vector<unsigned char> aes_key;
        if (!GenerateRandomBytes(AesKeySize, aes_key, error_message))
        {
            return false;
        }

        std::vector<unsigned char> encrypted_key;
        if (!EncryptAesKeyWithRsa(public_key_hex, aes_key, encrypted_key, error_message))
        {
            return false;
        }

        std::vector<unsigned char> nonce;
        std::vector<unsigned char> tag;
        std::vector<unsigned char> ciphertext;
        if (!EncryptWithAesGcm(aes_key, message, nonce, tag, ciphertext, error_message))
        {
            return false;
        }

        encrypted_message =
            PayloadPrefix + "|" +
            BytesToHex(encrypted_key) + "|" +
            BytesToHex(nonce) + "|" +
            BytesToHex(tag) + "|" +
            BytesToHex(ciphertext);

        return true;
    }

    bool TryDecryptSinglePayload(const std::string& recipient_id, const std::string& sender_id, const std::string& payload, std::string& decrypted_message, std::string& error_message)
    {
        decrypted_message.clear();
        error_message.clear();

        if (payload.rfind(PayloadPrefix + "|", 0) != 0)
        {
            error_message = "The encrypted payload format is invalid.";
            return false;
        }

        std::vector<std::string> parts = Split(payload, '|');
        if (parts.size() != 5)
        {
            error_message = "The encrypted payload format is invalid.";
            return false;
        }

        std::vector<unsigned char> encrypted_key;
        std::vector<unsigned char> nonce;
        std::vector<unsigned char> tag;
        std::vector<unsigned char> ciphertext;
        if (!HexToBytes(parts[1], encrypted_key) ||
            !HexToBytes(parts[2], nonce) ||
            !HexToBytes(parts[3], tag) ||
            !HexToBytes(parts[4], ciphertext))
        {
            error_message = "The encrypted payload contains invalid hex data.";
            return false;
        }

        std::string private_key_hex;
        if (!ReadTextFile(GetUserPrivateKeyPath(recipient_id), private_key_hex))
        {
            error_message = "The private key for " + recipient_id + " could not be loaded.";
            return false;
        }

        std::vector<unsigned char> aes_key;
        if (!DecryptAesKeyWithRsa(private_key_hex, encrypted_key, aes_key, error_message))
        {
            error_message = "Unable to decrypt a message from " + sender_id + ". " + error_message;
            return false;
        }

        return DecryptWithAesGcm(aes_key, nonce, tag, ciphertext, decrypted_message, error_message);
    }
}

void Copy(const std::string& file_name, const std::string& message_now_file_name)
{
    std::ifstream message_file(message_now_file_name);
    if (!message_file)
    {
        std::cerr << "Error opening file: " << message_now_file_name << std::endl;
        return;
    }

    std::ofstream encrypted_file(file_name);
    if (!encrypted_file)
    {
        std::cerr << "Error opening file: " << file_name << std::endl;
        return;
    }

    std::string temp;
    while (getline(message_file, temp))
    {
        encrypted_file << temp << std::endl;
    }

    message_file.close();
    encrypted_file.close();
}

void Encryption(const std::string& encrypted_file_name, const std::string& message_now_file_name)
{
    Copy(encrypted_file_name, message_now_file_name);
}

void Decryption(const std::string& encrypted_file_name, const std::string& conversation_file_name)
{
    std::ifstream encrypted_file(encrypted_file_name);
    if (!encrypted_file)
    {
        std::cerr << "Error opening file: " << encrypted_file_name << std::endl;
        return;
    }

    std::ofstream conversation_file(conversation_file_name, std::ios::app);
    if (!conversation_file)
    {
        std::cerr << "Error opening file: " << conversation_file_name << std::endl;
        return;
    }

    std::string temp;
    while (getline(encrypted_file, temp))
    {
        conversation_file << temp << std::endl;
    }

    encrypted_file.close();
    conversation_file.close();
}

bool EnsureUserKeys(const std::string& user_id, std::string& public_key_hex, std::string& error_message)
{
    error_message.clear();
    std::filesystem::path private_key_path = GetUserPrivateKeyPath(user_id);
    std::filesystem::path public_key_path = GetUserPublicKeyPath(user_id);

    std::string existing_public_key;
    std::string existing_private_key;
    if (ReadTextFile(public_key_path, existing_public_key) && ReadTextFile(private_key_path, existing_private_key))
    {
        public_key_hex = existing_public_key;
        std::filesystem::create_directories(GetContactKeysDirectory(user_id));
        return true;
    }

    std::vector<unsigned char> public_blob;
    std::vector<unsigned char> private_blob;
    if (!GenerateRsaKeyPair(public_blob, private_blob, error_message))
    {
        return false;
    }

    public_key_hex = BytesToHex(public_blob);
    std::string private_key_hex = BytesToHex(private_blob);
    if (!WriteTextFile(public_key_path, public_key_hex, error_message))
    {
        return false;
    }

    if (!WriteTextFile(private_key_path, private_key_hex, error_message))
    {
        return false;
    }

    std::filesystem::create_directories(GetContactKeysDirectory(user_id));
    return true;
}

bool CacheContactPublicKey(const std::string& owner_user_id, const std::string& contact_id, const std::string& public_key_hex, std::string& warning_message, std::string& error_message)
{
    warning_message.clear();
    error_message.clear();

    if (public_key_hex.empty())
    {
        error_message = "The contact public key is empty.";
        return false;
    }

    std::filesystem::path contact_key_path = GetContactPublicKeyPath(owner_user_id, contact_id);
    std::string existing_public_key;
    if (ReadTextFile(contact_key_path, existing_public_key))
    {
        if (existing_public_key == public_key_hex)
        {
            return true;
        }

        warning_message =
            "The public key for " + contact_id +
            " changed on the server. Updating the cached key. "
            "Previous fingerprint: " + GetPublicKeyFingerprint(existing_public_key) +
            " | New fingerprint: " + GetPublicKeyFingerprint(public_key_hex);
    }

    return WriteTextFile(contact_key_path, public_key_hex, error_message);
}

bool LoadContactPublicKey(const std::string& owner_user_id, const std::string& contact_id, std::string& public_key_hex, std::string& error_message)
{
    error_message.clear();
    if (!ReadTextFile(GetContactPublicKeyPath(owner_user_id, contact_id), public_key_hex))
    {
        error_message = "No trusted public key is cached for " + contact_id + ".";
        return false;
    }

    if (public_key_hex.empty())
    {
        error_message = "The cached public key for " + contact_id + " is empty.";
        return false;
    }

    return true;
}

std::string GetPublicKeyFingerprint(const std::string& public_key_hex)
{
    std::vector<unsigned char> public_key_blob;
    if (!HexToBytes(public_key_hex, public_key_blob))
    {
        return "invalid";
    }

    BCRYPT_ALG_HANDLE algorithm_handle = nullptr;
    BCRYPT_HASH_HANDLE hash_handle = nullptr;
    ULONG object_length = 0;
    ULONG hash_length = 0;
    ULONG bytes_copied = 0;
    NTSTATUS status = BCryptOpenAlgorithmProvider(&algorithm_handle, BCRYPT_SHA256_ALGORITHM, nullptr, 0);
    if (!IsSuccess(status))
    {
        return "unavailable";
    }

    if (!IsSuccess(BCryptGetProperty(algorithm_handle, BCRYPT_OBJECT_LENGTH, reinterpret_cast<PUCHAR>(&object_length), sizeof(object_length), &bytes_copied, 0)) ||
        !IsSuccess(BCryptGetProperty(algorithm_handle, BCRYPT_HASH_LENGTH, reinterpret_cast<PUCHAR>(&hash_length), sizeof(hash_length), &bytes_copied, 0)))
    {
        BCryptCloseAlgorithmProvider(algorithm_handle, 0);
        return "unavailable";
    }

    std::vector<unsigned char> hash_object(object_length);
    std::vector<unsigned char> hash_value(hash_length);
    status = BCryptCreateHash(algorithm_handle, &hash_handle, hash_object.data(), object_length, nullptr, 0, 0);
    if (!IsSuccess(status))
    {
        BCryptCloseAlgorithmProvider(algorithm_handle, 0);
        return "unavailable";
    }

    status = BCryptHashData(hash_handle, public_key_blob.data(), static_cast<ULONG>(public_key_blob.size()), 0);
    if (IsSuccess(status))
    {
        status = BCryptFinishHash(hash_handle, hash_value.data(), hash_length, 0);
    }

    BCryptDestroyHash(hash_handle);
    BCryptCloseAlgorithmProvider(algorithm_handle, 0);
    if (!IsSuccess(status))
    {
        return "unavailable";
    }

    std::string full_hex = BytesToHex(hash_value);
    return full_hex.substr(0, std::min<size_t>(16, full_hex.size()));
}

bool EncryptMessageForConversation(const std::string& sender_id, const std::string& recipient_id, const std::string& message, std::string& encrypted_message, std::string& error_message)
{
    encrypted_message.clear();
    error_message.clear();

    std::string sender_public_key;
    if (!LoadOwnPublicKey(sender_id, sender_public_key, error_message))
    {
        return false;
    }

    std::string recipient_public_key;
    if (!LoadContactPublicKey(sender_id, recipient_id, recipient_public_key, error_message))
    {
        return false;
    }

    std::string sender_payload;
    if (!EncryptMessageWithPublicKeyHex(sender_public_key, message, sender_payload, error_message))
    {
        return false;
    }

    std::string recipient_payload;
    if (!EncryptMessageWithPublicKeyHex(recipient_public_key, message, recipient_payload, error_message))
    {
        return false;
    }

    std::vector<unsigned char> sender_payload_bytes(sender_payload.begin(), sender_payload.end());
    std::vector<unsigned char> recipient_payload_bytes(recipient_payload.begin(), recipient_payload.end());
    encrypted_message =
        ConversationPayloadPrefix + "|" +
        BytesToHex(sender_payload_bytes) + "|" +
        BytesToHex(recipient_payload_bytes);

    return true;
}

bool EncryptMessageForRecipient(const std::string& sender_id, const std::string& recipient_id, const std::string& message, std::string& encrypted_message, std::string& error_message)
{
    encrypted_message.clear();
    error_message.clear();

    std::string recipient_public_key;
    if (!LoadContactPublicKey(sender_id, recipient_id, recipient_public_key, error_message))
    {
        return false;
    }

    return EncryptMessageWithPublicKeyHex(recipient_public_key, message, encrypted_message, error_message);
}

bool WrapPlaintextMessage(const std::string& message, std::string& wrapped_message, std::string& error_message)
{
    wrapped_message.clear();
    error_message.clear();

    std::vector<unsigned char> message_bytes(message.begin(), message.end());
    wrapped_message = PlaintextPayloadPrefix + "|" + BytesToHex(message_bytes);
    return true;
}

bool UnwrapPlaintextMessage(const std::string& message, std::string& plaintext_message, std::string& error_message)
{
    plaintext_message.clear();
    error_message.clear();

    if (message.rfind(PlaintextPayloadPrefix + "|", 0) != 0)
    {
        error_message = "The plaintext payload format is invalid.";
        return false;
    }

    std::vector<std::string> parts = Split(message, '|');
    if (parts.size() != 2)
    {
        error_message = "The plaintext payload format is invalid.";
        return false;
    }

    std::vector<unsigned char> plaintext_bytes;
    if (!HexToBytes(parts[1], plaintext_bytes))
    {
        error_message = "The plaintext payload contains invalid hex data.";
        return false;
    }

    plaintext_message.assign(plaintext_bytes.begin(), plaintext_bytes.end());
    return true;
}

bool DecryptMessageForUser(const std::string& recipient_id, const std::string& sender_id, const std::string& message, std::string& decrypted_message, std::string& error_message)
{
    decrypted_message.clear();
    error_message.clear();

    if (message.rfind(PlaintextPayloadPrefix + "|", 0) == 0)
    {
        return UnwrapPlaintextMessage(message, decrypted_message, error_message);
    }

    if (message.rfind(ConversationPayloadPrefix + "|", 0) == 0)
    {
        std::vector<std::string> parts = Split(message, '|');
        if (parts.size() != 3)
        {
            error_message = "The encrypted conversation payload format is invalid.";
            return false;
        }

        std::vector<unsigned char> sender_payload_bytes;
        std::vector<unsigned char> recipient_payload_bytes;
        if (!HexToBytes(parts[1], sender_payload_bytes) || !HexToBytes(parts[2], recipient_payload_bytes))
        {
            error_message = "The encrypted conversation payload contains invalid hex data.";
            return false;
        }

        std::string sender_payload(sender_payload_bytes.begin(), sender_payload_bytes.end());
        std::string recipient_payload(recipient_payload_bytes.begin(), recipient_payload_bytes.end());
        const bool current_user_is_sender = (recipient_id == sender_id);
        const std::string& primary_payload = current_user_is_sender ? sender_payload : recipient_payload;
        const std::string& fallback_payload = current_user_is_sender ? recipient_payload : sender_payload;

        std::string primary_error;
        if (TryDecryptSinglePayload(recipient_id, sender_id, primary_payload, decrypted_message, primary_error))
        {
            return true;
        }

        std::string fallback_error;
        if (TryDecryptSinglePayload(recipient_id, sender_id, fallback_payload, decrypted_message, fallback_error))
        {
            return true;
        }

        error_message = fallback_error.empty() ? primary_error : fallback_error;
        return false;
    }

    if (message.rfind(PayloadPrefix + "|", 0) != 0)
    {
        decrypted_message = DecryptLegacyMessage(message);
        return true;
    }

    return TryDecryptSinglePayload(recipient_id, sender_id, message, decrypted_message, error_message);
}
