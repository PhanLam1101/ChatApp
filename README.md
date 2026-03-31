# ChatApp
A C# WinForms chat application prototype with login, contacts, themes, notifications, templates, file sending, and C++ named-pipe backend integration.
## How to Use

### 1. Start the server

On the server machine, run the C++ server executable:

`ChatAppServer\Sever.exe`

Keep the server window open while clients are using the app.

### 2. Make sure the server is reachable

- The server and client should be on the same network, or otherwise be able to reach each other.
- Make sure the server machine allows incoming connections through Windows Firewall.
- The server uses port `80`, so that port must be available.

### 3. Launch the desktop app

On the client machine, run:

`UserInterface.exe`

### 4. Enter the server IP address

On the sign-in screen, find the `Server IP` field.

- If the client and server are on the same PC, enter: `127.0.0.1`
- If the server is on another PC in the same network, enter that PC’s local IP address
- Click `Change` to save the IP address

### 5. Register or sign in

- New users can create an account in the registration form
- Existing users can sign in with their ID and password

After sign-in or registration, the app automatically starts the communication bridge and connects to the server.

### 6. Start chatting

Once login succeeds, the messaging window opens. From there, users can:

- view contacts
- send messages
- send files
- search chats
- use templates
- change theme settings

## Caution

After you sign in or register, the application may open an additional console window:

- **User Interface:** the main desktop application built with C# WinForms
- **C++ Program:** a helper program used for communication with the server and background processing

Do not close the C++ program window while the application is running. If it is closed, the chat app may stop working correctly. If this happens, close the application and start it again.

## Troubleshooting

If you have trouble starting or using the application, try the following:

- **Start the server first:** Make sure the C++ server is already running before attempting to sign in.
- **Check the server IP:** In the `Server IP` field on the login screen, enter the correct IP address of the server. Use `127.0.0.1` if the server is running on the same computer. To get Server IP, on Server machine, open command line, type 'ipconfig' and then look for IPv4 Address. . . . . . . . . . . : 
- **Install Visual C++ Redistributable:** If the C++ components do not start, install the required runtime libraries using `VC_redist.x64.exe`.
- **Use a writable folder:** Run the application from a normal folder such as `Desktop` or `Documents`. Avoid protected folders such as `Program Files`, since the app needs to create and modify local files.
- **File access errors:** If you see errors such as `"file-name.bin" cannot be opened`, first move the app to a writable folder. If needed, try `Run as administrator`.
- **Slow first startup:** `UserInterface.exe` is a bundled all-in-one build, so the first launch may take a little longer than expected.
- **C++ helper path issues:** If the helper program cannot be launched, check the `program.txt` file in the application folder and confirm it points to:
  `MessageProgram\x64\Release\MessageProgram.exe`

## Debug Option

If you are testing the application and want more detailed error output, you can switch the helper program to the Debug build:

1. Open `program.txt` in the application directory.
2. Replace its content with:
   `MessageProgram\x64\Debug\MessageProgram.exe`
3. Save the file.
4. Start the application again.

This is mainly for troubleshooting and development.
