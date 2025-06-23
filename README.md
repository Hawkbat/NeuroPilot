# NeuroPilot

> [!CAUTION]
> This is **not** a general-purpose mod for Outer Wilds. It will not function properly without the Neuro-sama AI agent or a compatible implementation running on your machine.

A mod for Outer Wilds that uses the [Neuro Game API](https://github.com/VedalAI/neuro-game-sdk) to expose the ship's autopilot feature to the [Neuro-sama](https://www.twitch.tv/vedal987) AI agent. It also disables several means of manual control to force the player to rely on the AI agent for ship navigation.

## Setup

It's not as complicated as it looks, I promise!

1. Download the latest mod .zip file from the [Releases](https://github.com/Hawkbat/NeuroPilot/releases) tab of this repository.
2. Download and install the [Outer Wilds Mod Manager](https://outerwildsmods.com/mod-manager/) if you don't already have it.
3. Launch the Outer Wilds Mod Manager and select the "Install From" option from the "..." menu at the top of the manager.
4. Select "Zip File" in the "Install From" dropdown, then browse to the downloaded zip file.
5. Click "Install" to install the mod.
6. Set the `NEURO_SDK_WS_URL` environment variable to the WebSocket URL of the Neuro-sama Game API server.
	- This can also be set in-game if necessary, but will require the game to be restarted for changes to take effect.
7. Start the Neuro Game API server if it is not already running.
8. Launch the game using the "Run Game" button at the top of the mod manager.
9. If everything is set up correctly, the game will connect to the Neuro Game API server, and register commands for the AI agent to control the ship's autopilot feature.

## Credits

- Acamaeda: Initial concept and feature proposal
- Hawkbar: Mod development and implementation

Special Thanks:
- Vedal: Creator of Neuro-sama
- Alexvoid: Creator of the Neuro Game SDK
