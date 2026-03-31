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
