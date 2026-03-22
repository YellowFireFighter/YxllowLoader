# Yxllow.Tech — Rocket League SDK & Bot Bridge

![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6.svg)
![Language](https://img.shields.io/badge/Language-C++-00599C.svg)
![Build Status](https://img.shields.io/badge/Build-Stable-green.svg)

## 🚀 Overview

Yxllow.Tech is a C++ DLL-based SDK for Rocket League that provides real-time game data access, visual overlays, and serves as a high-performance bridge to external Python bot clients. The project exposes Rocket League's internal game state through a clean C++ API, enabling developers to build bots and analysis tools.

## ⚠️ **Important Disclaimers**

### **Bot Client Required**
**This repository contains only the C++ DLL component.** Yxllow.Tech acts as a bridge and data provider but requires a separate Python bot client to function as an actual bot.

**This repository won't be updated anymore.**

**What this project provides:**
- ✅ Real-time game data extraction *(100% accurate when GameEvent found)*
- ✅ Visual overlays and ESP features *(all work except boost pad timers)*
- ✅ TCP communication bridge *(mostly functional)*
- ✅ Memory management and safety *(solid implementation)*
- ❌ Boost pad tracking *(completely broken system)*

**What you need separately:**
- ❌ Bot logic and decision making (Python client)
- ❌ Controller input simulation
- ❌ Machine learning models

### **🚨 Current State & Code Quality**

**This codebase is experimental:**

- 🔴 **Code Quality**: Messy, poorly organized, and lacks proper documentation
- 🔴 **Hook Reliability**: Many function hooks are unstable and don't work consistently
- 🔴 **Memory Management**: Functional but needs cleanup
- 🔴 **Error Handling**: Insufficient in many critical sections
- 🔴 **Architecture**: Could be significantly improved

**Known Issues:**
- **Primary Issue**: Function hooks may fail to initialize, preventing GameEvent detection
- **When GameEvent is found**: All core game state reading works perfectly
- **Boost pad tracking**: Completely non-functional
- **TCP bridge**: Can be unreliable under certain conditions
- **Pattern scanning**: May fail on newer game versions requiring offset updates

**This project is shared for:**
- 📚 **Educational purposes** — Learning about game hacking techniques
- 🔧 **Reference implementation** — Base for building better tools
- 🤝 **Community contribution** — Hoping others can improve upon it

## ⚡ Key Features *(When They Work)*

### 🎯 **Game State Access** *(Reliable When GameEvent Found)*
- **Real-time Ball Data**: Position, velocity, angular velocity *(100% functional)*
- **Car Information**: Player positions, rotations, velocities, boost amounts *(100% functional)*
- **Match State**: Game time, score, team information *(100% functional)*
- **Boost Pad Tracking**: Live monitoring with respawn timers *(completely broken)*

### 🔧 **Memory Management** *(Solid When Initialized)*
- **Unreal Engine Integration**: Access to `GObjects` and `GNames` tables
- **Type-Safe Wrappers**: C++ object wrappers
- **Memory Reading**: Game state access *(100% accurate when GameEvent is found)*
- **Pattern Scanning**: Automatic offset resolution *(works but may fail on major updates)*

### 🎨 **Visual Overlays** *(Mostly Functional)*
- **Ball Prediction**: 3D trajectory visualization *(works well)*
- **Car Hitboxes**: Hitbox rendering *(functional)*
- **Velocity Indicators**: Movement arrows *(working)*
- **Boost Visualization**: Boost indicators above cars *(working)*
- **Team-Colored Elements**: Team color detection *(functional)*
- **Boost Pad Timers**: Countdown displays *(completely broken — don't use)*

### 🌐 **Networking** *(Sometimes Works)*
- **TCP Bridge**: Localhost server *(connection drops frequently)*
- **JSON Protocol**: Data exchange *(formatting inconsistencies)*
- **Real-time Updates**: Low latency *(when stable)*

### 🔩 **Function Hooking** *(Major Issues)*
- **MinHook Integration**: Function interception *(many hooks fail to install)*
- **ProcessEvent Hooking**: Event system integration *(very unreliable)*
- **DirectX 11 Rendering**: ImGui overlays *(may crash)*
- **Event System**: Pub/sub architecture *(events may not fire)*

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Python Bot   │◄──►│  Yxllow.Tech DLL │◄──►│  Rocket League  │
│     Client      │    │    (TCP Bridge)  │    │   (Injected)    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
        │                        │                        │
        │              ┌─────────▼─────────┐             │
        │              │  Memory Manager   │             │
        │              │  GNames/GObjects  │             │
        │              │  Function Hooks   │             │
        │              └───────────────────┘             │
        │                                                │
        └──── JSON Data Exchange ──────────────────────────┘
```

## 🛠️ Building the Project

### Prerequisites

- **Visual Studio 2019 or newer** with C++ Desktop Development workload
- **Windows 10/11 SDK**
- **[vcpkg](https://vcpkg.io/)** package manager (recommended)

### Dependencies

```bash
# Install required packages via vcpkg (STATIC linking required for DLL injection)
vcpkg install curl:x64-windows-static
vcpkg install nlohmann-json:x64-windows-static
```

**Included Dependencies:**
- **Kiero** — DirectX hooking framework
- **MinHook** — Function hooking library
- **ImGui** — Immediate mode GUI library

### Build Steps

1. **Clone the repository:**
   ```bash
   git clone https://github.com/YellowFireFighter/YxllowSDK.git
   cd YxllowSDK
   ```

2. **Setup vcpkg integration:**
   ```bash
   vcpkg integrate install
   ```

3. **⚠️ CRITICAL: Manual vcpkg Path Configuration**

   - Open `Vutrium/Vutrium.vcxproj` in a text editor
   - Set `<VcpkgTriplet>x64-windows-static</VcpkgTriplet>`
   - Update `<AdditionalLibraryDirectories>` to point to your vcpkg installation
   - Set Runtime Library to `Multi-threaded (/MT)` for Release builds

4. **Build the project:**
   ```bash
   # Open Vutrium.sln in Visual Studio
   # Select Release + x64
   # Build Solution (Ctrl+Shift+B)
   ```

5. **Output:** The compiled DLL will be in `x64/Release/`

6. **⚠️ Verify Static Linking:**
   ```bash
   dumpbin /dependents x64\Release\Vutrium.dll
   # Should only show system DLLs (KERNEL32.dll, USER32.dll, etc.)
   ```

### 🚨 Common Build Issues

**"Cannot find vcpkg libraries"** → Verify vcpkg paths in the `.vcxproj` file.

**"LNK2019: unresolved external symbol"** → Use the `x64-windows-static` triplet and verify `.lib` paths.

**"0xc000007b" error** → You're linking dynamically instead of statically. Set triplet to `x64-windows-static`.

**DLL injection fails with "module not found"** → Use Dependency Walker to check for missing DLLs and ensure all dependencies are statically linked.

## 🚀 Usage

### Quick Start

1. **Launch Rocket League** and enter a match
2. **Inject the DLL** using your preferred injector:
   ```bash
   injector.exe RocketLeague.exe Vutrium.dll
   ```
3. **Start your Python bot client** (connects to `localhost:13337`)
4. **Open the menu** by pressing `INSERT` in-game

**Troubleshooting:**
- Nothing happens after injection → Check console for hook init status
- Game crashes → Try injecting at a different time (main menu vs in-game)
- Overlays don't show → DirectX hook failed, try different injection timing
- TCP connection fails → Check Windows Firewall and antivirus

### Configuration

The in-game menu provides:
- **Status** — SDK and game state information
- **Visuals** — Toggle ESP features and adjust colors
- **Bot** — Configure bot behavior and key bindings
- **Connection** — Monitor TCP bridge health
- **Settings** — DLL management

### Python Client Integration

```python
import socket
import json

client = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
client.connect(('localhost', 13337))

while True:
    data = client.recv(4096).decode('utf-8')
    game_state = json.loads(data)

    # Your bot logic here
    bot_decision = analyze_game_state(game_state)

    response = json.dumps(bot_decision)
    client.send(response.encode('utf-8'))
```

## 📊 Performance

- **Memory footprint**: ~2 MB injected DLL
- **CPU overhead**: <1% additional load
- **Network latency**: <1 ms localhost communication

> Performance can be inconsistent due to current implementation issues. Expect potential stutters, crashes, or memory leaks during extended use.

## 🔍 Supported Game Versions

- ✅ **Steam version**
- ✅ **Epic Games version**
- ✅ **Automatic offset resolution** via pattern scanning
- ⚠️ **Major game updates** may require pattern updates

## 📋 API Reference

### Core Classes

```cpp
class RLSDK {
    SDK::AGameEvent GetCurrentGameEvent();
    MemoryManager& GetMemoryManager();
    void Subscribe(const std::string& event, Callback callback);
};

class AGameEvent {
    std::vector<ABall> GetBalls();
    std::vector<ACar> GetCars();
    std::vector<APRI> GetPRIs();
    bool IsRoundActive();
};

class ACar : public AVehicle {
    FVectorData GetLocation();
    FVectorData GetVelocity();
    APRI GetPRI();
    UBoostComponent GetBoostComponent();
};
```

### Event System

```cpp
sdk.Subscribe(EventType::OnRoundActiveStateChanged, [](const EventData& data) {
    // Handle round state changes
});

sdk.Subscribe(EventType::OnBoostPadStateChanged, [](const EventData& data) {
    // Handle boost pad pickups
});
```

## 🤝 Contributing

**Priority areas for improvement:**
1. 🔧 Fix function hook reliability
2. 🎯 Fix boost pad tracking system
3. 🧹 General code cleanup and documentation
4. 🛡️ TCP bridge stability
5. 🏗️ Architecture improvements

**How to contribute:**
1. Fork the repository
2. Create a feature branch: `git checkout -b fix/hook-reliability`
3. Test thoroughly — the code is fragile
4. Submit a pull request with a clear description of changes

## 📄 License

Licensed under the **GNU General Public License v3.0** — see the [LICENSE](LICENSE) file for details.

- ✅ Commercial use, modification, distribution, and private use are all permitted
- 📋 Any derivative work must also be licensed under GPL v3
- 📋 Source code must be provided for any distributed versions

## ⚖️ Legal & Ethical Use

- **Educational purposes** — Learn about game internals and memory management
- **Analysis tools** — Statistics and replay analysis
- **Research** — Academic study of game AI and human-computer interaction

> Always respect the game's Terms of Service. Use responsibly.

## 🙏 Acknowledgments

- **Epic Games / Psyonix** — For creating Rocket League
- **Kiero Team** — DirectX hooking framework
- **MinHook Project** — Function hooking library
- **Dear ImGui** — Immediate mode GUI

---

<div align="center">

**⚡ Yxllow.Tech ⚡**

</div>

