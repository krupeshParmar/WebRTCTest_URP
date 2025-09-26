📌 Overview

This Unity project demonstrates how to integrate the WebRTCStreamer C++ plugin into Unity for real-time gameplay streaming.
It is a minimal sample project (no large assets, no LFS) designed to show how Unity interacts with the native DLL.

👉 Note: This project is not part of VelEngine. It is a standalone sample created to showcase SDK-style streaming integration.

--------------------------------------------------------------------------------------------------------------

⚙️ Architecture

- Unity (C# layer)

- Captures gameplay frames from the Direct3D11 render pipeline.

- Passes texture pointers and input data into the C++ DLL via Unity’s plugin interface.

- C++ Plugin (WebRTCStreamer)

- Encodes GPU textures using NVIDIA NVENC (H.264).

- Handles WebRTC peer connections.

- Routes input events back into Unity scripts.

- Node.js Signaling Server

- Manages WebRTC offer/answer exchange and ICE candidate negotiation.

- Browser/Client

- Receives the low-latency video stream.

- Sends keyboard/mouse/gamepad inputs back to Unity.

--------------------------------------------------------------------------------------------------------------

🚀 Features

- Direct3D11 texture capture from Unity.

- Native C++ plugin integration.

- NVENC GPU encoding for real-time streaming.

- WebRTC data/media channel transport.

- Input round-trip: browser → Node → DLL → Unity.

--------------------------------------------------------------------------------------------------------------

🔧 How to Use
- Requirements

- Unity 202x.x.x (Direct3D11).

- Prebuilt WebRTCStreamer.dll

- Node.js vXX for the signaling server.

- Windows 10/11 + NVIDIA GPU.

Setup Steps

- Clone this repo.

- Clone the WebRTCStreamer repo and build it. Copy the dll from Release and paste it as shown below.

- Assets/Plugins/x64/WebRTCStreamer.dll

- Open the Unity project, load the sample scene, and press Play.

- Connect with a WebRTC-capable browser → see the streamed gameplay.

--------------------------------------------------------------------------------------------------------------

📂 Project Structure
Assets/
  _MyAssets/1_Scripts/      # Unity <-> DLL interop scripts
  Scenes/                   # Minimal sample scene
  Plugins/                  # Place WebRTCStreamer.dll here
Packages/
ProjectSettings/

--------------------------------------------------------------------------------------------------------------

🐞 Current Work

- Investigating a stability bug in frame pipeline.

- Improving reconnection and error handling.

- Expanding support for multiple clients (planned).

--------------------------------------------------------------------------------------------------------------

📜 License

MIT License © 2025 Krupesh Parmar (Demo Only)

🧑‍💻 Author

Krupesh Parmar – Indie game developer & engine programmer LinkedIn: www.linkedin.com/in/krupesh-parmar
