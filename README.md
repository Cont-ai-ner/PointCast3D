# PointCast3D
Real-time dense point cloud streaming from Intel RealSense D435 to Meta Quest VR headsets (UDP + Unity)

### Prerequisites
- Unity 2021.3 LTS or 2022.3 LTS (recommended for Quest compatibility)
- Meta Quest 2 or Quest 3S headset
- Oculus Integration package (download from Meta's developer site)
- PC with Air Link or USB-C cable for development & testing
- Android Build Support module installed in Unity Hub

### Step-by-step: Create & configure the Unity project from scratch

1. **Create a new Unity project**
   - Open Unity Hub
   - Click **New project** → 3D Core template
   - Name it e.g. `PointCast 3D`
   - Choose a folder → Create

2. **Switch platform to Android**
   - File → Build Settings
   - Platform: Android → **Switch Platform**

3. **Install required packages**
   - Window → Package Manager
   - Search & install:
     - **Oculus XR Plugin** (or Meta XR Core SDK – newer versions)
     - **XR Interaction Toolkit** (optional but recommended for VR controls later)

4. **Configure Player Settings for Quest**
   - Edit → Project Settings → XR Plug-in Management
     - Android tab → Check **Oculus**
   - Player Settings → Android tab
     - **Minimum API Level**: Android 10 (API 29) or higher
     - **Scripting Backend**: IL2CPP
     - **Target Architectures**: ARM64 only (Quest is ARM64)
     - **Graphics APIs**: Vulkan (remove OpenGLES if present)

5. **Create the scene**
   - File → New Scene → Basic (Built-in)
   - Save as `Assets/Scenes/MainScene.unity`

6. **Add the receiver GameObject**
   - Hierarchy → right-click → 3D Object → Empty → rename to `PointCloudReceiver`
   - Add Component → **Mesh Filter**
   - Add Component → **Mesh Renderer**
   - Add Component → **UdpPointCloudSaver** (the script you copied from this repo)

7. **Assign a material with point rendering**
   - Create new Material → name `PointCloudMaterial`
   - Set Shader → **Particles/Standard Unlit** (or **Pcx/Disk** if you have the PCX package)
   - Drag this material onto the **Mesh Renderer** of `PointCloudReceiver`
   - (If using PCX/Disk: set Point Size to 1–3 in the material inspector)

8. **Add VR camera rig**
   - GameObject → XR → **XR Origin (VR)** (or **OVRCameraRig** if using older Oculus Integration)
   - Position it at `(0, 1, -2)` so the point cloud is in front of you

9. **Build & deploy to Quest**
   - File → Build Settings
   - Add current scene (`MainScene`) to Scenes In Build
   - Click **Build And Run** (connect Quest via cable or enable Air Link)
   - Install on Quest → Launch the app

10. **Run the sender**
    - On your laptop/Jetson:  
      ```bash
      python Sender/real_sense_udp_sender.py
      ```
    - Make sure the Quest and sender are on the **same Wi-Fi network**
    - Look at the Quest — the point cloud should appear in 3D space

### Troubleshooting
- **No points appear** → Check Unity Console for errors (e.g. port 5005 already used)
- **Big squares** → Use **Pcx/Disk** shader or Particles/Standard Unlit + small point size
- **Lag / low FPS** → Wi-Fi bottleneck → try wired connection or reduce depth/resolution
- **Portrait mode** → Make sure Oculus XR Plugin is active and you’re using XR Origin / OVRCameraRig

### Quick checklist for new users

- [ ] Unity 2021.3 / 2022.3 LTS installed
- [ ] Android Build Support + Oculus XR Plugin installed
- [ ] Quest 2/3S connected via cable or Air Link
- [ ] Sender script running on laptop/Jetson
- [ ] Same Wi-Fi network for both devices
