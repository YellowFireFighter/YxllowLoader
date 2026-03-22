// YxllowSDKMain.cpp
// Connects the GigaLearn trained bot (AdvancedObs, obsSize=109) to the
// YxllowSDK2 bot server running on port 13338 inside Rocket League.
//
// Protocol (newline-delimited):
//   Client -> Server : "get_obs\n"               Server replies with game-state JSON
//   Client -> Server : "set_action:{...json}\n"  Server replies with "ok\n"

#include "RLGymCPP/ActionParsers/DefaultAction.h"
#include "RLGymCPP/ObsBuilders/AdvancedObs.h"
#include "GigaLearnCPP/Util/InferUnit.h"
#include "GigaLearnCPP/Util/ModelConfig.h"
#include <RLGymCPP/Gamestates/GameState.h>
#include <RLGymCPP/Framework.h>

#include <filesystem>
#include <iostream>
#include <memory>
#include <string>
#include <vector>
#include <map>
#include <cmath>
#include <algorithm>

#ifdef _WIN32
#  ifndef WIN32_LEAN_AND_MEAN
#    define WIN32_LEAN_AND_MEAN
#  endif
#  include <winsock2.h>
#  include <ws2tcpip.h>
#  pragma comment(lib, "Ws2_32.lib")
#else
#  include <sys/socket.h>
#  include <arpa/inet.h>
#  include <unistd.h>
#  define SOCKET         int
#  define INVALID_SOCKET -1
#  define SOCKET_ERROR   -1
#  define closesocket    close
   inline void Sleep(int ms) { usleep(ms * 1000); }
#endif

using namespace GGL;
using namespace RLGC;

// ── Constants matching ExampleMain.cpp training settings ────────────────────
static constexpr int   SDK_PORT          = 13338;
static constexpr int   TICK_SKIP         = 8;
static constexpr int   OBS_SIZE          = 109;
static constexpr bool  DETERMINISTIC     = true;
static constexpr bool  USE_GPU           = false;
static constexpr float UE_ROT_TO_RAD     = 3.14159265f / 32768.0f;
static constexpr float DOUBLEJUMP_MAX_DELAY = 1.25f;
static constexpr float TICK_DT           = TICK_SKIP / 120.0f; // ~0.0667 s per step

// ── Minimal JSON helpers ─────────────────────────────────────────────────────

static float parseFloat(const std::string& s, size_t& pos) {
    while (pos < s.size() && (s[pos] == ' ' || s[pos] == ',')) ++pos;
    size_t end = pos;
    while (end < s.size() && (std::isdigit((unsigned char)s[end]) ||
           s[end] == '-' || s[end] == '.' || s[end] == 'e' ||
           s[end] == 'E' || s[end] == '+')) ++end;
    float v = 0.f;
    try { v = std::stof(s.substr(pos, end - pos)); } catch (...) {}
    pos = end;
    return v;
}

static Vec parseVec3Array(const std::string& s, size_t& pos) {
    while (pos < s.size() && s[pos] != '[') ++pos;
    ++pos;
    float x = parseFloat(s, pos);
    while (pos < s.size() && s[pos] == ',') ++pos;
    float y = parseFloat(s, pos);
    while (pos < s.size() && s[pos] == ',') ++pos;
    float z = parseFloat(s, pos);
    while (pos < s.size() && s[pos] != ']') ++pos;
    if (pos < s.size()) ++pos;
    return Vec(x, y, z);
}

static bool parseBool(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\":";
    size_t pos = json.find(search);
    if (pos == std::string::npos) return false;
    pos += search.size();
    while (pos < json.size() && json[pos] == ' ') ++pos;
    return pos + 4 <= json.size() && json.substr(pos, 4) == "true";
}

static float getFloat(const std::string& json, const std::string& key, float def = 0.f) {
    std::string search = "\"" + key + "\":";
    size_t pos = json.find(search);
    if (pos == std::string::npos) return def;
    pos += search.size();
    while (pos < json.size() && json[pos] == ' ') ++pos;
    try { return std::stof(json.substr(pos)); } catch (...) { return def; }
}

static int getInt(const std::string& json, const std::string& key, int def = 0) {
    std::string search = "\"" + key + "\":";
    size_t pos = json.find(search);
    if (pos == std::string::npos) return def;
    pos += search.size();
    while (pos < json.size() && json[pos] == ' ') ++pos;
    try { return std::stoi(json.substr(pos)); } catch (...) { return def; }
}

// Extract the first {...} sub-object for the given key
static std::string getSubObject(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\":{";
    size_t start = json.find(search);
    if (start == std::string::npos) return "";
    start += search.size() - 1;
    int depth = 0; size_t end = start;
    while (end < json.size()) {
        if      (json[end] == '{') ++depth;
        else if (json[end] == '}') { --depth; if (depth == 0) { ++end; break; } }
        ++end;
    }
    return json.substr(start, end - start);
}

// Extract the first [...] array for the given key
static std::string getArray(const std::string& json, const std::string& key) {
    std::string search = "\"" + key + "\":[";
    size_t start = json.find(search);
    if (start == std::string::npos) return "";
    start += search.size() - 1;
    int depth = 0; size_t end = start;
    while (end < json.size()) {
        if      (json[end] == '[') ++depth;
        else if (json[end] == ']') { --depth; if (depth == 0) { ++end; break; } }
        ++end;
    }
    return json.substr(start, end - start);
}

// Split a JSON array literal into its top-level element strings
static std::vector<std::string> splitArray(const std::string& arr) {
    std::vector<std::string> items;
    if (arr.empty() || arr[0] != '[') return items;
    size_t pos = 1;
    while (pos < arr.size() && arr[pos] != ']') {
        while (pos < arr.size() && (arr[pos] == ' ' || arr[pos] == ',')) ++pos;
        if (pos >= arr.size() || arr[pos] == ']') break;
        if (arr[pos] == '{') {
            int depth = 0; size_t start = pos;
            while (pos < arr.size()) {
                if      (arr[pos] == '{') ++depth;
                else if (arr[pos] == '}') { --depth; if (depth == 0) { ++pos; break; } }
                ++pos;
            }
            items.push_back(arr.substr(start, pos - start));
        } else {
            size_t start = pos;
            while (pos < arr.size() && arr[pos] != ',' && arr[pos] != ']') ++pos;
            std::string val = arr.substr(start, pos - start);
            while (!val.empty() && val.back() == ' ') val.pop_back();
            if (!val.empty()) items.push_back(val);
        }
    }
    return items;
}

// ── Rotation conversion: UE units → RLGC RotMat ─────────────────────────────
// SDK sends rot as [Pitch, Yaw, Roll] in Unreal rotation units (32768 = 180°).
// We use the same axes decomposition as the SDK's GetAxes() helper.
static RotMat ueRotToRotMat(float pitch_ue, float yaw_ue, float roll_ue) {
    float SY = std::sinf(yaw_ue   * UE_ROT_TO_RAD);
    float CY = std::cosf(yaw_ue   * UE_ROT_TO_RAD);
    float SP = std::sinf(pitch_ue * UE_ROT_TO_RAD);
    float CP = std::cosf(pitch_ue * UE_ROT_TO_RAD);
    float SR = std::sinf(roll_ue  * UE_ROT_TO_RAD);
    float CR = std::cosf(roll_ue  * UE_ROT_TO_RAD);

    RotMat m;
    // Forward (X)
    m.forward = Vec(CP * CY, CP * SY, SP);
    // Right (Y)
    m.right   = Vec(SR * SP * CY - CR * SY, SR * SP * SY + CR * CY, -SR * CP);
    // Up (Z)
    m.up      = Vec(-(CR * SP * CY + SR * SY), -(CR * SP * SY - SR * CY), CR * CP);
    return m;
}

// ── Per-car tracking for fields not available directly from SDK ──────────────
struct CarTrack {
    bool  wasOnGround      = true;
    bool  hadJumped        = false;
    bool  hadDoubleJumped  = false;
    float airTimeSinceJump = 0.f;
    bool  hasFlipped       = false;
};

// ── TCP helpers ──────────────────────────────────────────────────────────────
static std::string recvLine(SOCKET sock) {
    std::string line;
    char ch;
    while (true) {
        int n = recv(sock, &ch, 1, 0);
        if (n <= 0) return "";
        if (ch == '\n') break;
        if (ch != '\r') line += ch;
    }
    return line;
}

static bool sendAll(SOCKET sock, const std::string& data) {
    size_t sent = 0;
    while (sent < data.size()) {
        int n = ::send(sock, data.c_str() + sent, (int)(data.size() - sent), 0);
        if (n <= 0) return false;
        sent += n;
    }
    return true;
}

// ── Checkpoint finding (same logic as rlbotmain.cpp) ────────────────────────
static std::filesystem::path findLatestCheckpoint(const std::filesystem::path& dir) {
    std::filesystem::path best;
    long long bestTs = -1;
    if (!std::filesystem::exists(dir) || !std::filesystem::is_directory(dir)) return {};
    for (const auto& entry : std::filesystem::directory_iterator(dir)) {
        if (!entry.is_directory()) continue;
        try {
            long long ts = std::stoll(entry.path().filename().string());
            if (ts > bestTs) { bestTs = ts; best = entry.path(); }
        } catch (...) {}
    }
    return best;
}

// ── main ─────────────────────────────────────────────────────────────────────
int main(int argc, char* argv[]) {
    // ── Model config matching ExampleMain.cpp ──────────────────────────────
    PartialModelConfig sharedHead = {};
    sharedHead.layerSizes    = {};
    sharedHead.activationType = ModelActivationType::RELU;
    sharedHead.addLayerNorm  = false;
    sharedHead.addOutputLayer = false;

    PartialModelConfig policy = {};
    policy.layerSizes     = { 1024, 1024, 1024, 1024, 1024, 512 };
    policy.activationType = ModelActivationType::RELU;
    policy.addLayerNorm   = true;
    policy.addOutputLayer = true;

    // ── Find latest checkpoint ─────────────────────────────────────────────
    std::filesystem::path checkpointPath;
    // Uncomment and set a specific path to override auto-detection:
    // checkpointPath = "C:/path/to/checkpoints/123456789/POLICY.lt";

    if (checkpointPath.empty()) {
        auto exeDir = std::filesystem::path(argv[0]).parent_path();
        checkpointPath = findLatestCheckpoint(exeDir / "checkpoints");
    }
    if (checkpointPath.empty() || !std::filesystem::exists(checkpointPath)) {
        std::cerr << "Error: No valid checkpoint found. Place checkpoints/ next to the exe.\n";
        return 1;
    }
    std::cout << "Loading checkpoint: " << checkpointPath << "\n";

    // ── Build inference objects ────────────────────────────────────────────
    auto obsBuilder   = std::make_unique<AdvancedObs>();
    auto actionParser = std::make_unique<DefaultAction>();
    auto inferUnit    = std::make_unique<InferUnit>(
        obsBuilder.get(), OBS_SIZE,
        actionParser.get(),
        sharedHead, policy,
        checkpointPath, USE_GPU
    );

    // ── Winsock init ───────────────────────────────────────────────────────
#ifdef _WIN32
    WSADATA wsa = {};
    if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
        std::cerr << "WSAStartup failed.\n";
        return 1;
    }
#endif

    std::cout << "Connecting to YxllowSDK2 bot server on 127.0.0.1:" << SDK_PORT << "...\n";

    while (true) {
        SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
        if (sock == INVALID_SOCKET) { std::cerr << "socket() failed.\n"; break; }

        sockaddr_in addr = {};
        addr.sin_family = AF_INET;
        inet_pton(AF_INET, "127.0.0.1", &addr.sin_addr);
        addr.sin_port = htons((u_short)SDK_PORT);

        if (connect(sock, reinterpret_cast<sockaddr*>(&addr), sizeof(addr)) != 0) {
            std::cerr << "Waiting for SDK server on port " << SDK_PORT << "...\n";
            closesocket(sock);
            Sleep(2000);
            continue;
        }
        std::cout << "Connected to SDK bot server.\n";

        // ── Per-connection state ───────────────────────────────────────────
        std::map<int, CarTrack> carTracks;
        Action prevAction = {};

        // Pre-allocate GameState with 34 boost pads
        GameState gs;
        gs.boostPads.assign(34, true);
        gs.boostPadsInv.assign(34, true);
        gs.boostPadTimers.assign(34, 0.f);
        gs.boostPadTimersInv.assign(34, 0.f);
        gs.deltaTime = TICK_DT;

        bool running = true;
        while (running) {
            // ── 1. Request game state ──────────────────────────────────────
            if (!sendAll(sock, "get_obs\n")) { running = false; break; }

            std::string jsonLine = recvLine(sock);
            if (jsonLine.empty()) { running = false; break; }
            if (jsonLine.find("\"error\"") != std::string::npos) {
                Sleep(50);
                continue;
            }

            // ── 2. Parse ball ──────────────────────────────────────────────
            {
                std::string bj = getSubObject(jsonLine, "ball");
                if (!bj.empty()) {
                    auto readVec = [&](const std::string& k) -> Vec {
                        std::string srch = "\"" + k + "\":[";
                        size_t p = bj.find(srch);
                        if (p == std::string::npos) return Vec();
                        p += srch.size() - 1;
                        return parseVec3Array(bj, p);
                    };
                    gs.ball.pos    = readVec("pos");
                    gs.ball.vel    = readVec("vel");
                    gs.ball.angVel = readVec("ang_vel");
                }
            }

            // ── 3. Parse boost pads ────────────────────────────────────────
            {
                auto padsArr   = splitArray(getArray(jsonLine, "boost_pads"));
                auto timersArr = splitArray(getArray(jsonLine, "boost_pad_timers"));
                for (int i = 0; i < 34 && i < (int)padsArr.size(); i++) {
                    bool active = (padsArr[i] == "true");
                    gs.boostPads[i]        = active;
                    gs.boostPadsInv[33-i]  = active;
                }
                for (int i = 0; i < 34 && i < (int)timersArr.size(); i++) {
                    float t = 0.f;
                    try { t = std::stof(timersArr[i]); } catch (...) {}
                    gs.boostPadTimers[i]       = t;
                    gs.boostPadTimersInv[33-i] = t;
                }
            }

            // ── 4. Parse cars ──────────────────────────────────────────────
            int localCarIndex = getInt(jsonLine, "local_car_index", 0);
            {
                auto carItems = splitArray(getArray(jsonLine, "cars"));
                gs.players.resize(carItems.size());

                for (int i = 0; i < (int)carItems.size(); i++) {
                    const std::string& cj = carItems[i];
                    Player& p = gs.players[i];
                    CarTrack& ct = carTracks[i];

                    auto readVec = [&](const std::string& k) -> Vec {
                        std::string srch = "\"" + k + "\":[";
                        size_t pos = cj.find(srch);
                        if (pos == std::string::npos) return Vec();
                        pos += srch.size() - 1;
                        return parseVec3Array(cj, pos);
                    };

                    p.index  = i;
                    p.carId  = (uint32_t)i;
                    p.team   = (Team)getInt(cj, "team", i % 2);

                    p.pos    = readVec("pos");
                    p.vel    = readVec("vel");
                    p.angVel = readVec("ang_vel");

                    // Rotation: SDK sends [Pitch, Yaw, Roll] in UE units
                    {
                        std::string rotSrch = "\"rot\":[";
                        size_t rp = cj.find(rotSrch);
                        if (rp != std::string::npos) {
                            rp += rotSrch.size() - 1;
                            Vec rv = parseVec3Array(cj, rp);
                            p.rotMat = ueRotToRotMat(rv.x, rv.y, rv.z);
                        }
                    }

                    p.boost        = getFloat(cj, "boost", 1.f) * 100.f;
                    p.isOnGround   = parseBool(cj, "on_ground");
                    p.isSupersonic = parseBool(cj, "supersonic");
                    p.hasJumped    = parseBool(cj, "jumped");
                    p.hasDoubleJumped = parseBool(cj, "double_jumped");
                    p.isDemoed     = parseBool(cj, "demoed");

                    // ── Track airTimeSinceJump and hasFlipped ──────────────
                    // These are not available directly from the SDK, so we
                    // derive them from state transitions (same logic as
                    // PlayerInternalState in RLBotClient.cpp, simplified).
                    if (p.isOnGround) {
                        ct.airTimeSinceJump = 0.f;
                        ct.hasFlipped       = false;
                        ct.hadJumped        = false;
                        ct.hadDoubleJumped  = false;
                    } else {
                        // First jump transition: reset air-time counter
                        if (p.hasJumped && !ct.hadJumped)
                            ct.airTimeSinceJump = 0.f;

                        // Second jump / flip transition
                        if (p.hasDoubleJumped && !ct.hadDoubleJumped)
                            ct.hasFlipped = true;

                        if (p.hasJumped)
                            ct.airTimeSinceJump += TICK_DT;
                    }

                    p.hasFlipped       = ct.hasFlipped;
                    p.airTimeSinceJump = ct.airTimeSinceJump;

                    ct.wasOnGround     = p.isOnGround;
                    ct.hadJumped       = p.hasJumped;
                    ct.hadDoubleJumped = p.hasDoubleJumped;

                    // Previous action for local player only
                    if (i == localCarIndex)
                        p.prevAction = prevAction;
                }
            }

            // ── 5. Infer action ────────────────────────────────────────────
            if (localCarIndex < 0 || localCarIndex >= (int)gs.players.size())
                continue;

            Player& localPlayer = gs.players[localCarIndex];

            Action action;
            try {
                action = inferUnit->InferAction(localPlayer, gs, DETERMINISTIC);
            } catch (const std::exception& e) {
                std::cerr << "InferAction error: " << e.what() << "\n";
                continue;
            }
            prevAction = action;

            // ── 6. Send action ─────────────────────────────────────────────
            char buf[320];
            std::snprintf(buf, sizeof(buf),
                "set_action:{\"throttle\":%.4f,\"steer\":%.4f,\"pitch\":%.4f,"
                "\"yaw\":%.4f,\"roll\":%.4f,"
                "\"jump\":%s,\"boost\":%s,\"handbrake\":%s}\n",
                (double)action.throttle,  (double)action.steer,   (double)action.pitch,
                (double)action.yaw,       (double)action.roll,
                action.jump      != 0.f ? "true" : "false",
                action.boost     != 0.f ? "true" : "false",
                action.handbrake != 0.f ? "true" : "false");

            if (!sendAll(sock, buf)) { running = false; break; }
            recvLine(sock); // consume "ok\n"
        }

        closesocket(sock);
        std::cerr << "Disconnected from SDK server. Reconnecting in 2s...\n";
        Sleep(2000);
    }

#ifdef _WIN32
    WSACleanup();
#endif
    return 0;
}
