# VRCPixelColor

VRCPixelColor is a Unity tool for VRChat worlds that dynamically applies pixel color data from a [VideoTXL](https://github.com/vrctxl/VideoTXL) video player to lights, materials, particles, and [VRCLightVolumes](https://github.com/REDSIM/VRCLightVolumes) in real time. Designed for immersive and responsive environments.

## Showcase
[![Showcase Video](https://img.youtube.com/vi/3hzO7frJHh4/0.jpg)](https://youtu.be/3hzO7frJHh4)

## Requirements
- Unity **2022.3.22f1**
- [VideoTXL Player](https://github.com/vrctxl/VideoTXL)
- [VRCLightVolumes 2.0](https://github.com/REDSIM/VRCLightVolumes)(OPTIONAL)

## Installation
1. Place the **VideoTXL** player in your scene and create a **CRT (Custom Render Texture)** via the *Screen Manager* component of the player.
2. Add the **PixelColorController** prefab to the scene and assign the CRT to it.  
   - The prefab includes the controller, a receiver, and 4 example objects with different output configurations.
3. Use as many **PixelColorReceivers** as you want.
4. Done.

## Features
- Real-time environment lighting based on a pixel color from the video player.
- Support for Unity Lights, Renderers, Particle Systems, and VRCLightVolumes.
- Brightness and saturation boosting, black-to-white thresholding, and global intensity control.
- Automatic or manual target assignment for controlled objects.

## License
**CC0 1.0 Universal** — No rights reserved.  
You are free to copy, modify, distribute, and use this project, even for commercial purposes, without asking permission.
