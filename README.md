# Baldur's Gate 3 Inworld Mod Source Code

This repository houses the source code for a Baldur's Gate 3 mod that empowers players to engage in conversation with their AI companions.

**Target Audience:**

This repository is geared towards developers, not players. It presupposes familiarity with C# application development and Baldur's Gate 3 modding. 
Players without development experience should simply search for the packaged version on Nexus Mods https://www.nexusmods.com/baldursgate3. 
Developers unfamiliar with the concepts presented here are encouraged to delve into independent research and build simple mods first before exploring this source code.

**Component Breakdown:**

The mod comprises two key components:

1. **Desktop Application:**
   - Written in C#
   - Interacts with the game (hooks)
   - Initiates a fastify NodeJS socket server
   - Connects to socket server for bidirectional connection
   - Communicates with Inworld Backend via gRPC Streaming
   - Records and listens audio during open session and transmits data to Inworld for AI communication.

2. **Mod:**
   - Contains Lua code for creating the mod's `.pak` file
   - Refer to https://steamcommunity.com/sharedfiles/filedetails/?id=2381865525 for a general overview of the process.

**System Functionality:**

- The desktop application retrieves the API key and secret from the Windows Credential Manager.
- It continuously monitors running applications, specifically checking for Baldur's Gate 3.
- Upon detecting Baldur's Gate 3, it establishes a hook within the DirectX layer and launches the NodeJS connector.
- The NodeJS connector creates a local socket connection, which the desktop application also establishes a connection to.
- When a player starts a session, the application communicates with the connector, which interacts with Inworld using a gRPC connection.
- Responses and UI elements are rendered on the DirectX layer.
- The layer additionally listens for mouse events to enable 2D raycasting. This calculates bounding boxes to prevent unintended clicks from passing through.


## Special Thanks
Fararagi for his Configurable Movement Speed Mod (https://www.nexusmods.com/baldursgate3/users/64167336)
Open Faerûn Enjoyer for giving some insights about existing mods (From Larian Modding Discord)
Inworld Team for their development key for me to fool around