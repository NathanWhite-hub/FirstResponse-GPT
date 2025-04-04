
# FirstResponse GPT
<p align="center">
  <img src="https://img.shields.io/badge/OpenRouter-LLM API-blueviolet" height="30">
  <img src="https://img.shields.io/badge/TTS-ElevenLabs-000000?logo=elevenlabs" height="30">
  <img src="https://img.shields.io/badge/STS-Lemonfox.ai-fd7e14?logo=elevenlabs" height="30">
  <img src="https://img.shields.io/badge/Built with-.NET-512BD4?logo=dotnet" height="30">
  <img src="https://img.shields.io/badge/Audio Processing-JUCE-8DC63F?logo=juce" height="30">
  <img src="https://img.shields.io/badge/For-LSPDFR-blue" height="30">
</p>

**FirstResponse GPT** is a plugin for the Grand Theft Auto V modification [LSPDFR](https://www.lcpdfr.com/lspdfr) that creates a dynamic police simulation environment using Large Language Models (LLM), Text-to-Speech (TTS), and Speech-to-Text (STT) services.

## Features

- **Live Dispatcher Audio**: Dynamically generated and voiced dispatch responses.
- **Dynamic NPC Interactions**: Utilizes LLMs to generate realistic and context-aware NPC dialogues.
- **Voice Input Recognition**: Integrates STT services to process officer voice commands.
- **Realistic Dispatcher Responses**: Employs TTS to produce lifelike dispatcher communications.
- **Callout Awareness**: Automatically generates dispatcher speech when new callouts are triggered.
- **Hooking Support**: Seamless integration with UltimateBackup for use with its additional backup types.
- **Immersive Radio Effects**: Applies audio filters to simulate authentic police radio transmissions using the [companion JUCE framework plugin built for FirstResponseGPT](https://github.com/NathanWhite-hub/FirstResponse-GPT-JUCE-Radio-Processor).

## Use Case

This plugin is designed for content creators, immersive players, and LEO roleplay communities who want their in-game dispatcher to behave and sound more like a real person.

For example:
- You say: "Control, I'm on scene."
- The plugin sends your speech to the LLM.
- The LLM responds with something like: `Copy that, 10:22 hours.` voiced through ElevenLabs with a radio-filtered effect.
Or
- You say: "Start me another unit! Fight in progress!"
- The plugin sends your speech to the LLM.
- LLM determines that not only are you requesting backup, but that this is a priority request.
- The LLM initiates a priority signal sound effect over the radio, responds with something like: `All units, officer requesting assistance. Any available officer response.` voiced through ElevenLabs with a radio-filtered effect.
- Other officers that the LLM generated over the radio begin responding to the dispatchers traffic, indicating they are responding and a backup request is triggered in game for each officer.

## Installation

This is currently still a WIP and in development. Installation instructions are not provided at this time.

## License

This project is licensed under the MIT License. See the [LICENSE.txt](https://github.com/NathanWhite-hub/FirstResponse-GPT/blob/master/LICENSE.txt) file for details.

---

*Note: This project is in active development. Contributions and feedback are welcome!*


