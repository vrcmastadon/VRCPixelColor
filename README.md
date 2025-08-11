# VRCPixelColor

VRCPixelColor is a Unity tool for VRChat worlds that dynamically applies pixel color data from a [VideoTXL](https://github.com/vrctxl/VideoTXL) video player to lights, materials, particles, and [VRCLightVolumes](https://github.com/REDSIM/VRCLightVolumes) in real time. Designed for immersive and responsive environments, with flexible integration for [AudioLink](https://github.com/llealloo/audiolink)-based effects.

## Requirements
- Unity **2022.3.22f1**
- [VideoTXL Player](https://github.com/vrctxl/VideoTXL)
- [AudioLink](https://github.com/llealloo/audiolink)
- [VRCLightVolumes 2.0](https://github.com/REDSIM/VRCLightVolumes) (OPTIONAL)

## Installation
1. Place the **VideoTXL** player in your scene and create a **CRT (Custom Render Texture)** via the *Screen Manager* component of the player.
2. Place the **AudioLink** prefab in your scene and link it to the player.
3. Add the **PixelColorController** prefab to the scene and assign the CRT to it.  
   - The prefab includes the controller, a receiver, and 4 example objects with different output configurations.
4. Use as many **PixelColorReceivers** as you want.
5. Done.

## Features
- Real-time environment lighting based on a pixel color from the video player.
- Support for Unity Lights, Renderers, Particle Systems, and VRCLightVolumes.
- Flexible AudioLink integration affecting only intensity, preserving existing color logic.
- Brightness and saturation boosting, black-to-white thresholding, and global intensity control.
- Automatic or manual target assignment for controlled objects.

## License
**CC0 1.0 Universal** — No rights reserved.  
You are free to copy, modify, distribute, and use this project, even for commercial purposes, without asking permission.