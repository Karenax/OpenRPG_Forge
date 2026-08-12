#include <angelscript.h>
#include <scriptstdstring.h>

#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iomanip>
#include <locale>
#include <mutex>
#include <regex>
#include <sstream>
#include <string>
#include <unordered_map>
#include <vector>

#if defined(_WIN32)
#define FORGE_EXPORT extern "C" __declspec(dllexport)
#else
#define FORGE_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace {

constexpr const char *ForgeApiVersion = "1.0.0";
constexpr std::size_t MaximumStringBytes = 16384;
constexpr std::size_t MaximumRegexPatternBytes = 256;
constexpr std::size_t MaximumRegexInputBytes = 4096;

struct ScriptVec3 {
    float x;
    float y;
    float z;
};

struct ScriptQuat {
    float x;
    float y;
    float z;
    float w;
};

enum class ScriptHandleKind : std::uint8_t {
    Entity = 1,
    Area = 2,
    Asset = 3,
    Effect = 4,
    MovementTask = 5,
    ScriptTask = 6,
    InventoryResult = 7,
    QuestResult = 8,
    DialogueSession = 9,
    FactionResult = 10,
    PerceptionResult = 11,
    AudioEmitter = 12,
    AudioCaption = 13,
    WorldClock = 14,
    Timer = 15,
    CustomEvent = 16,
    HotReload = 17,
    DebugSession = 18,
};

struct HandleSlot {
    ScriptHandleKind kind;
    std::uint32_t generation;
    asQWORD subject;
    bool active;
};

struct MemoryBudget {
    std::size_t limit;
    std::size_t current = 0;
    std::size_t peak = 0;
    bool exceeded = false;
};

struct alignas(std::max_align_t) AllocationHeader {
    std::size_t size;
};

class BytecodeStream final : public asIBinaryStream {
public:
    std::vector<std::uint8_t> bytes;
    std::size_t cursor = 0;

    int Read(void *pointer, asUINT size) override {
        if (cursor + size > bytes.size()) {
            return -1;
        }

        std::memcpy(pointer, bytes.data() + cursor, size);
        cursor += size;
        return 0;
    }

    int Write(const void *pointer, asUINT size) override {
        const auto *source = static_cast<const std::uint8_t *>(pointer);
        bytes.insert(bytes.end(), source, source + size);
        return 0;
    }
};

struct ExecutionState {
    std::ostringstream events;
    std::string diagnostic;
    int instruction_count = 0;
    int instruction_quota = 0;
    int spawn_count = 0;
    int spawn_cap = 0;
    asQWORD movement_task_count = 0;
    asQWORD script_task_count = 0;
    asQWORD script_world_result_count = 0;
    asQWORD runtime_tool_handle_count = 0;
    asQWORD effect_count = 0;
    std::vector<HandleSlot> handle_slots;
    std::vector<std::uint32_t> available_handle_slots;
    std::unordered_map<std::string, asINT64> local_ints;
    std::unordered_map<std::string, double> local_floats;
    std::unordered_map<std::string, std::string> local_strings;
    std::unordered_map<std::string, asQWORD> local_entities;
    std::unordered_map<std::string, ScriptVec3> local_vectors;
    std::unordered_map<asQWORD, bool> audio_active;
    std::chrono::steady_clock::time_point deadline;
    std::uint64_t deterministic_state = 2026;
    asINT64 simulation_tick = 0;
    asINT64 simulation_time_milliseconds = 0;
};

thread_local ExecutionState *CurrentExecution = nullptr;
thread_local MemoryBudget *CurrentMemoryBudget = nullptr;
std::mutex ExecutionMutex;
std::once_flag MemoryFunctionsOnce;
std::atomic<bool> CancelRequested{false};

void *BudgetAllocate(std::size_t size) {
    const auto total = size + sizeof(AllocationHeader);
    if (CurrentMemoryBudget != nullptr && CurrentMemoryBudget->current + size > CurrentMemoryBudget->limit) {
        CurrentMemoryBudget->exceeded = true;
    }
    auto *raw = static_cast<std::uint8_t *>(std::malloc(total));
    if (raw == nullptr) return nullptr;
    reinterpret_cast<AllocationHeader *>(raw)->size = size;
    if (CurrentMemoryBudget != nullptr) {
        CurrentMemoryBudget->current += size;
        CurrentMemoryBudget->peak = std::max(CurrentMemoryBudget->peak, CurrentMemoryBudget->current);
    }
    return raw + sizeof(AllocationHeader);
}

void BudgetFree(void *pointer) {
    if (pointer == nullptr) return;
    auto *raw = static_cast<std::uint8_t *>(pointer) - sizeof(AllocationHeader);
    const auto size = reinterpret_cast<AllocationHeader *>(raw)->size;
    if (CurrentMemoryBudget != nullptr && CurrentMemoryBudget->current >= size) CurrentMemoryBudget->current -= size;
    std::free(raw);
}

void EnsureMemoryFunctions() {
    std::call_once(MemoryFunctionsOnce, [] { asSetGlobalMemoryFunctions(BudgetAllocate, BudgetFree); });
}

class MemoryBudgetScope final {
public:
    explicit MemoryBudgetScope(MemoryBudget *budget) { CurrentMemoryBudget = budget; }
    ~MemoryBudgetScope() { CurrentMemoryBudget = nullptr; }
};

void CopyText(const std::string &text, char *destination, int capacity) {
    if (destination == nullptr || capacity <= 0) {
        return;
    }

    const auto count = std::min<std::size_t>(text.size(), static_cast<std::size_t>(capacity - 1));
    std::memcpy(destination, text.data(), count);
    destination[count] = '\0';
}

void MessageCallback(const asSMessageInfo *message, void *parameter) {
    auto *stream = static_cast<std::ostringstream *>(parameter);
    const char *kind = message->type == asMSGTYPE_ERROR
        ? "error"
        : message->type == asMSGTYPE_WARNING ? "warning" : "info";
    *stream << message->section << ':' << message->row << ':' << message->col << ':' << kind << ':'
            << message->message << '\n';
}

void Record(const char *kind, const std::string &value) {
    if (CurrentExecution != nullptr) {
        CurrentExecution->events << kind << '|' << value << '\n';
    }
}

void AbortWithDiagnostic(const std::string &diagnostic) {
    if (CurrentExecution == nullptr) {
        return;
    }
    CurrentExecution->diagnostic = diagnostic;
    if (auto *context = asGetActiveContext()) {
        context->Abort();
    }
}

asQWORD EncodeHandle(ScriptHandleKind kind, std::uint32_t generation, std::uint32_t slot) {
    return (static_cast<asQWORD>(kind) << 56) |
           ((static_cast<asQWORD>(generation) & 0x00FFFFFFULL) << 32) | slot;
}

ScriptHandleKind HandleKind(asQWORD handle) {
    return static_cast<ScriptHandleKind>((handle >> 56) & 0xFF);
}

std::uint32_t HandleGeneration(asQWORD handle) {
    return static_cast<std::uint32_t>((handle >> 32) & 0x00FFFFFFULL);
}

std::uint32_t HandleSlotIndex(asQWORD handle) {
    return static_cast<std::uint32_t>(handle & 0xFFFFFFFFULL);
}

asQWORD RegisterHandle(ScriptHandleKind kind, asQWORD subject) {
    if (CurrentExecution == nullptr) return 0;
    std::uint32_t slot;
    std::uint32_t generation = 1;
    if (!CurrentExecution->available_handle_slots.empty()) {
        slot = CurrentExecution->available_handle_slots.back();
        CurrentExecution->available_handle_slots.pop_back();
        generation = CurrentExecution->handle_slots[slot - 1].generation + 1;
        if (generation == 0 || generation > 0x00FFFFFFU) {
            AbortWithDiagnostic("script.handle.generation_exhausted");
            return 0;
        }
        CurrentExecution->handle_slots[slot - 1] = {kind, generation, subject, true};
    } else {
        slot = static_cast<std::uint32_t>(CurrentExecution->handle_slots.size() + 1);
        CurrentExecution->handle_slots.push_back({kind, generation, subject, true});
    }
    return EncodeHandle(kind, generation, slot);
}

bool ResolveHandle(asQWORD handle, ScriptHandleKind expected, asQWORD *subject = nullptr) {
    if (CurrentExecution == nullptr || handle == 0 || HandleKind(handle) != expected) {
        AbortWithDiagnostic("script.handle.type_invalid");
        return false;
    }
    const auto slot = HandleSlotIndex(handle);
    if (slot == 0 || slot > CurrentExecution->handle_slots.size()) {
        AbortWithDiagnostic("script.handle.stale");
        return false;
    }
    const auto &entry = CurrentExecution->handle_slots[slot - 1];
    if (!entry.active || entry.kind != expected || entry.generation != HandleGeneration(handle)) {
        AbortWithDiagnostic("script.handle.stale");
        return false;
    }
    if (subject != nullptr) *subject = entry.subject;
    return true;
}

void ReleaseHandle(asQWORD handle, ScriptHandleKind expected) {
    if (!ResolveHandle(handle, expected)) return;
    const auto slot = HandleSlotIndex(handle);
    CurrentExecution->handle_slots[slot - 1].active = false;
    CurrentExecution->available_handle_slots.push_back(slot);
}

bool IsFinite(const ScriptVec3 &value) {
    return std::isfinite(value.x) && std::isfinite(value.y) && std::isfinite(value.z);
}

bool IsFinite(const ScriptQuat &value) {
    return std::isfinite(value.x) && std::isfinite(value.y) && std::isfinite(value.z) && std::isfinite(value.w);
}

std::string VecToString(const ScriptVec3 &value) {
    std::ostringstream stream;
    stream.imbue(std::locale::classic());
    stream << std::fixed << std::setprecision(3) << value.x << ',' << value.y << ',' << value.z;
    return stream.str();
}

std::string LocalKey(asQWORD owner, const std::string &key) {
    if (!ResolveHandle(owner, ScriptHandleKind::Entity)) return {};
    if (key.empty() || key.size() > 128) {
        AbortWithDiagnostic("sandbox.local_key_invalid");
        return {};
    }
    return std::to_string(owner) + ':' + key;
}

asQWORD SpawnHandle(const std::string &blueprint) {
    if (CurrentExecution == nullptr) {
        return 0;
    }
    if (blueprint.empty() || blueprint.size() > 256) {
        AbortWithDiagnostic("sandbox.blueprint_invalid");
        return 0;
    }
    CurrentExecution->spawn_count++;
    if (CurrentExecution->spawn_count > CurrentExecution->spawn_cap) {
        AbortWithDiagnostic("quota.spawn_cap_exceeded");
        return 0;
    }
    return RegisterHandle(ScriptHandleKind::Entity, static_cast<asQWORD>(1000 + CurrentExecution->spawn_count));
}

int ApiHealth(asQWORD entity) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return 0;
    Record("health", std::to_string(entity));
    return 100;
}

void ApiVariable(const std::string &key, const std::string &value) {
    Record("variable", key + "=" + value);
}

void ApiSay(const std::string &text) {
    Record("say", text);
}

void ApiCast(asQWORD entity, const std::string &ability) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return;
    Record("cast", std::to_string(entity) + ":" + ability);
}

asQWORD ApiSpawn(const std::string &blueprint) {
    const auto handle = SpawnHandle(blueprint);
    if (handle == 0) {
        return 0;
    }
    Record("spawn", blueprint + ":" + std::to_string(handle));
    return handle;
}

void ApiQuest(const std::string &quest) {
    Record("quest", quest);
}

void ApiUnlock(asQWORD entity) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return;
    Record("unlock", std::to_string(entity));
}

ScriptVec3 ApiVec3(float x, float y, float z) {
    const ScriptVec3 value{x, y, z};
    if (!IsFinite(value)) {
        AbortWithDiagnostic("sandbox.vector_non_finite");
        return {0, 0, 0};
    }
    return value;
}

float ApiVecLength(const ScriptVec3 &value) {
    if (!IsFinite(value)) {
        AbortWithDiagnostic("sandbox.vector_non_finite");
        return 0;
    }
    return std::sqrt(value.x * value.x + value.y * value.y + value.z * value.z);
}

float ApiVecDot(const ScriptVec3 &left, const ScriptVec3 &right) {
    if (!IsFinite(left) || !IsFinite(right)) {
        AbortWithDiagnostic("sandbox.vector_non_finite");
        return 0;
    }
    return left.x * right.x + left.y * right.y + left.z * right.z;
}

ScriptVec3 ApiVecNormalize(const ScriptVec3 &value) {
    const auto length = ApiVecLength(value);
    if (length <= 0 || CurrentExecution != nullptr && !CurrentExecution->diagnostic.empty()) {
        return {0, 0, 0};
    }
    return {value.x / length, value.y / length, value.z / length};
}

ScriptQuat ApiQuat(float x, float y, float z, float w) {
    const ScriptQuat value{x, y, z, w};
    if (!IsFinite(value)) {
        AbortWithDiagnostic("sandbox.quaternion_non_finite");
        return {0, 0, 0, 1};
    }
    return value;
}

ScriptQuat ApiQuatNormalize(const ScriptQuat &value) {
    if (!IsFinite(value)) {
        AbortWithDiagnostic("sandbox.quaternion_non_finite");
        return {0, 0, 0, 1};
    }
    const auto length = std::sqrt(value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w);
    if (length <= 0) {
        AbortWithDiagnostic("sandbox.quaternion_zero_length");
        return {0, 0, 0, 1};
    }
    return {value.x / length, value.y / length, value.z / length, value.w / length};
}

std::string ApiFormatInt(asINT64 value) {
    return std::to_string(value);
}

std::string ApiFormatFloat(double value, asUINT precision) {
    if (!std::isfinite(value) || precision > 6) {
        AbortWithDiagnostic("sandbox.format_invalid");
        return {};
    }
    std::ostringstream stream;
    stream.imbue(std::locale::classic());
    stream << std::fixed << std::setprecision(precision) << value;
    return stream.str();
}

asUINT ApiUnicodeLength(const std::string &value) {
    if (value.size() > MaximumStringBytes) {
        AbortWithDiagnostic("quota.string_bytes_exceeded");
        return 0;
    }
    asUINT count = 0;
    for (const auto character : value) {
        if ((static_cast<unsigned char>(character) & 0xC0) != 0x80) {
            ++count;
        }
    }
    return count;
}

bool ApiRegexMatch(const std::string &value, const std::string &pattern) {
    if (value.size() > MaximumRegexInputBytes || pattern.size() > MaximumRegexPatternBytes) {
        AbortWithDiagnostic("quota.regex_bytes_exceeded");
        return false;
    }
    if (pattern.find_first_not_of("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-^$.*+?[]") !=
        std::string::npos) {
        AbortWithDiagnostic("sandbox.regex_feature_forbidden");
        return false;
    }
    try {
        return std::regex_match(value, std::regex(pattern, std::regex::ECMAScript | std::regex::optimize));
    } catch (const std::regex_error &) {
        AbortWithDiagnostic("sandbox.regex_invalid");
        return false;
    }
}

asQWORD ApiResolveAsset(const std::string &assetId) {
    if (assetId.empty() || assetId.size() > 256) {
        AbortWithDiagnostic("sandbox.asset_id_invalid");
        return 0;
    }
    asQWORD subject = 1469598103934665603ULL;
    for (const auto character : assetId) {
        subject ^= static_cast<unsigned char>(character);
        subject *= 1099511628211ULL;
    }
    const auto handle = RegisterHandle(ScriptHandleKind::Asset, subject);
    Record("asset.resolved", assetId + ':' + std::to_string(handle));
    return handle;
}

asQWORD ApiCreateEffect(const std::string &effectId) {
    if (CurrentExecution == nullptr || effectId.empty() || effectId.size() > 128) {
        AbortWithDiagnostic("sandbox.effect_id_invalid");
        return 0;
    }
    const auto handle = RegisterHandle(ScriptHandleKind::Effect, ++CurrentExecution->effect_count);
    Record("effect.created", effectId + ':' + std::to_string(handle));
    return handle;
}

void ApiReleaseEffect(asQWORD effect) {
    if (!ResolveHandle(effect, ScriptHandleKind::Effect)) return;
    Record("effect.released", std::to_string(effect));
    ReleaseHandle(effect, ScriptHandleKind::Effect);
}

int ApiRandomInt(int minimum, int maximum) {
    if (CurrentExecution == nullptr || minimum > maximum) {
        AbortWithDiagnostic("script.determinism.range_invalid");
        return minimum;
    }
    auto value = CurrentExecution->deterministic_state;
    value ^= value << 13;
    value ^= value >> 7;
    value ^= value << 17;
    CurrentExecution->deterministic_state = value;
    const auto span = static_cast<std::uint64_t>(static_cast<std::int64_t>(maximum) - minimum) + 1ULL;
    return minimum + static_cast<int>(value % span);
}

asINT64 ApiSimulationTick() {
    return CurrentExecution == nullptr ? 0 : CurrentExecution->simulation_tick;
}

asINT64 ApiSimulationTimeMilliseconds() {
    return CurrentExecution == nullptr ? 0 : CurrentExecution->simulation_time_milliseconds;
}

asQWORD ApiCurrentArea() {
    const auto area = RegisterHandle(ScriptHandleKind::Area, 1);
    Record("world.area", std::to_string(area));
    return area;
}

asQWORD ApiFindEntity(asQWORD area, const std::string &tag) {
    if (!ResolveHandle(area, ScriptHandleKind::Area)) return 0;
    if (tag.empty() || tag.size() > 128) {
        AbortWithDiagnostic("sandbox.entity_query_invalid");
        return 0;
    }
    asQWORD hash = 1469598103934665603ULL;
    for (const auto character : tag) {
        hash ^= static_cast<unsigned char>(character);
        hash *= 1099511628211ULL;
    }
    const auto subject = (hash % 900000ULL) + 100ULL;
    const auto entity = RegisterHandle(ScriptHandleKind::Entity, subject);
    Record("world.query", std::to_string(area) + ':' + tag + ':' + std::to_string(entity));
    return entity;
}

ScriptVec3 ApiEntityPosition(asQWORD entity) {
    asQWORD subject = 0;
    if (!ResolveHandle(entity, ScriptHandleKind::Entity, &subject)) return {0, 0, 0};
    const ScriptVec3 value{static_cast<float>(subject % 10), 0, static_cast<float>((subject / 10) % 10)};
    Record("world.position", std::to_string(entity) + ':' + VecToString(value));
    return value;
}

asQWORD ApiSpawnAt(asQWORD area, const std::string &blueprint, const ScriptVec3 &position) {
    if (!ResolveHandle(area, ScriptHandleKind::Area)) return 0;
    if (!IsFinite(position)) {
        AbortWithDiagnostic("sandbox.spawn_target_invalid");
        return 0;
    }
    const auto handle = SpawnHandle(blueprint);
    if (handle != 0) {
        Record("world.spawn", std::to_string(area) + ':' + blueprint + ':' + VecToString(position) + ':' +
              std::to_string(handle));
    }
    return handle;
}

void ApiDestroyEntity(asQWORD entity) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return;
    Record("world.destroy", std::to_string(entity));
    ReleaseHandle(entity, ScriptHandleKind::Entity);
}

asQWORD NextMovementTask(const std::string &description) {
    if (CurrentExecution == nullptr) {
        return 0;
    }
    const auto subject = ++CurrentExecution->movement_task_count;
    const auto task = RegisterHandle(ScriptHandleKind::MovementTask, subject);
    Record("movement.started", std::to_string(task) + ':' + description);
    return task;
}

asQWORD ApiMoveTo(asQWORD entity, const ScriptVec3 &destination, const std::string &mode) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return 0;
    if (!IsFinite(destination) || (mode != "walk" && mode != "run" && mode != "fly" && mode != "swim")) {
        AbortWithDiagnostic("sandbox.movement_request_invalid");
        return 0;
    }
    return NextMovementTask("move:" + std::to_string(entity) + ':' + mode + ':' + VecToString(destination));
}

asQWORD ApiFaceTowards(asQWORD entity, const ScriptVec3 &target) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return 0;
    if (!IsFinite(target)) {
        AbortWithDiagnostic("sandbox.movement_request_invalid");
        return 0;
    }
    return NextMovementTask("face:" + std::to_string(entity) + ':' + VecToString(target));
}

asQWORD ApiFollow(asQWORD entity, asQWORD target, float distance) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity) ||
        !ResolveHandle(target, ScriptHandleKind::Entity)) return 0;
    if (!std::isfinite(distance) || distance < 0 || distance > 1000) {
        AbortWithDiagnostic("sandbox.movement_request_invalid");
        return 0;
    }
    return NextMovementTask("follow:" + std::to_string(entity) + ':' + std::to_string(target) + ':' +
                            std::to_string(distance));
}

void ApiTeleport(asQWORD entity, asQWORD area, const ScriptVec3 &destination) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity) ||
        !ResolveHandle(area, ScriptHandleKind::Area)) return;
    if (!IsFinite(destination)) {
        AbortWithDiagnostic("sandbox.teleport_request_invalid");
        return;
    }
    Record("movement.teleported", std::to_string(entity) + ':' + std::to_string(area) + ':' +
          VecToString(destination));
}

void ApiSetVelocity(asQWORD entity, const ScriptVec3 &velocity) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return;
    if (!IsFinite(velocity) || ApiVecLength(velocity) > 1000) {
        AbortWithDiagnostic("sandbox.velocity_invalid");
        return;
    }
    Record("movement.velocity", std::to_string(entity) + ':' + VecToString(velocity));
}

void ApiCancelMovement(asQWORD task) {
    if (!ResolveHandle(task, ScriptHandleKind::MovementTask)) return;
    Record("movement.completed", std::to_string(task) + ":cancelled");
    ReleaseHandle(task, ScriptHandleKind::MovementTask);
}

asQWORD ApiQueueAction(asQWORD entity, const std::string &action, int priority) {
    if (!ResolveHandle(entity, ScriptHandleKind::Entity)) return 0;
    if (action.empty() || action.size() > 128 || priority < 0 || priority > 10000) {
        AbortWithDiagnostic("sandbox.action_request_invalid");
        return 0;
    }
    const auto task = RegisterHandle(ScriptHandleKind::ScriptTask, ++CurrentExecution->script_task_count);
    Record("action.queued", std::to_string(task) + ':' + std::to_string(entity) + ':' +
           std::to_string(priority) + ':' + action);
    return task;
}

void ApiCancelAction(asQWORD task) {
    if (!ResolveHandle(task, ScriptHandleKind::ScriptTask)) return;
    Record("action.cancelled", std::to_string(task));
    ReleaseHandle(task, ScriptHandleKind::ScriptTask);
}

void ApiCompleteAction(asQWORD task, const std::string &value) {
    if (!ResolveHandle(task, ScriptHandleKind::ScriptTask)) return;
    if (value.size() > 256) {
        AbortWithDiagnostic("sandbox.action_result_invalid");
        return;
    }
    Record("action.completed", std::to_string(task) + ':' + value);
    ReleaseHandle(task, ScriptHandleKind::ScriptTask);
}

bool ApiCombatAttack(asQWORD attacker, asQWORD target, const std::string &ability, float amount, int seed) {
    if (!ResolveHandle(attacker, ScriptHandleKind::Entity) ||
        !ResolveHandle(target, ScriptHandleKind::Entity)) return false;
    if (ability.empty() || ability.size() > 128 || !std::isfinite(amount) || amount < 0 || amount > 1000000) {
        AbortWithDiagnostic("sandbox.combat_request_invalid");
        return false;
    }
    Record("combat.attack", std::to_string(attacker) + ':' + std::to_string(target) + ':' + ability + ':' +
           ApiFormatFloat(amount, 3) + ':' + std::to_string(seed));
    return true;
}

asQWORD ApiApplyEffect(asQWORD source, asQWORD target, const std::string &effectId,
                       float magnitude, float durationSeconds) {
    if (!ResolveHandle(source, ScriptHandleKind::Entity) ||
        !ResolveHandle(target, ScriptHandleKind::Entity)) return 0;
    if (effectId.empty() || effectId.size() > 128 || !std::isfinite(magnitude) || magnitude < 0 ||
        !std::isfinite(durationSeconds) || durationSeconds <= 0 || durationSeconds > 86400) {
        AbortWithDiagnostic("sandbox.effect_request_invalid");
        return 0;
    }
    const auto effect = RegisterHandle(ScriptHandleKind::Effect, ++CurrentExecution->effect_count);
    Record("effect.applied", std::to_string(effect) + ':' + std::to_string(source) + ':' +
           std::to_string(target) + ':' + effectId + ':' + ApiFormatFloat(magnitude, 3) + ':' +
           ApiFormatFloat(durationSeconds, 3));
    return effect;
}

void ApiDispelEffect(asQWORD target, const std::string &tag) {
    if (!ResolveHandle(target, ScriptHandleKind::Entity)) return;
    if (tag.empty() || tag.size() > 128) {
        AbortWithDiagnostic("sandbox.dispel_request_invalid");
        return;
    }
    Record("effect.dispelled", std::to_string(target) + ':' + tag);
}

asQWORD ApiInventoryTransfer(asQWORD from, asQWORD to, const std::string &itemId, int quantity) {
    if (!ResolveHandle(from, ScriptHandleKind::Entity) ||
        !ResolveHandle(to, ScriptHandleKind::Entity)) return 0;
    if (itemId.empty() || itemId.size() > 128 || quantity <= 0 || quantity > 100000) {
        AbortWithDiagnostic("sandbox.inventory_request_invalid");
        return 0;
    }
    const auto result = RegisterHandle(ScriptHandleKind::InventoryResult,
        ++CurrentExecution->script_world_result_count);
    Record("inventory.transfer", std::to_string(result) + ':' + std::to_string(from) + ':' +
           std::to_string(to) + ':' + itemId + ':' + std::to_string(quantity));
    return result;
}

asQWORD ApiQuestTransition(const std::string &questId, const std::string &scopeId,
                           const std::string &fromStatus, const std::string &toStatus,
                           const std::string &objectiveId, int delta, const std::string &cause) {
    if (questId.empty() || questId.size() > 128 || scopeId.empty() || scopeId.size() > 128 ||
        fromStatus.empty() || fromStatus.size() > 64 || toStatus.empty() || toStatus.size() > 64 ||
        objectiveId.size() > 128 || cause.empty() || cause.size() > 256 || delta < -100000 || delta > 100000) {
        AbortWithDiagnostic("sandbox.quest_request_invalid");
        return 0;
    }
    const auto result = RegisterHandle(ScriptHandleKind::QuestResult,
        ++CurrentExecution->script_world_result_count);
    Record("quest.transition", std::to_string(result) + ':' + questId + ':' + scopeId + ':' +
           fromStatus + ':' + toStatus + ':' + objectiveId + ':' + std::to_string(delta) + ':' + cause);
    return result;
}

asQWORD ApiDialogueStart(const std::string &graphId, asQWORD speaker, asQWORD listener) {
    if (!ResolveHandle(speaker, ScriptHandleKind::Entity) ||
        !ResolveHandle(listener, ScriptHandleKind::Entity)) return 0;
    if (graphId.empty() || graphId.size() > 128) {
        AbortWithDiagnostic("sandbox.dialogue_request_invalid");
        return 0;
    }
    const auto session = RegisterHandle(ScriptHandleKind::DialogueSession,
        ++CurrentExecution->script_world_result_count);
    Record("dialogue.started", std::to_string(session) + ':' + graphId + ':' +
           std::to_string(speaker) + ':' + std::to_string(listener));
    return session;
}

void ApiDialogueComplete(asQWORD session, const std::string &outcome) {
    if (!ResolveHandle(session, ScriptHandleKind::DialogueSession)) return;
    if (outcome.empty() || outcome.size() > 256) {
        AbortWithDiagnostic("sandbox.dialogue_outcome_invalid");
        return;
    }
    Record("dialogue.completed", std::to_string(session) + ':' + outcome);
    ReleaseHandle(session, ScriptHandleKind::DialogueSession);
}

asQWORD ApiFactionReputation(asQWORD actor, const std::string &factionId, int delta,
                             bool join, const std::string &scope, const std::string &cause) {
    if (!ResolveHandle(actor, ScriptHandleKind::Entity)) return 0;
    if (factionId.empty() || factionId.size() > 128 || delta < -200 || delta > 200 ||
        scope.empty() || scope.size() > 128 || cause.empty() || cause.size() > 256) {
        AbortWithDiagnostic("sandbox.faction_request_invalid");
        return 0;
    }
    const auto result = RegisterHandle(ScriptHandleKind::FactionResult,
        ++CurrentExecution->script_world_result_count);
    Record("faction.reputation", std::to_string(result) + ':' + std::to_string(actor) + ':' +
           factionId + ':' + std::to_string(delta) + ':' + (join ? "join" : "leave") + ':' +
           scope + ':' + cause);
    return result;
}

asQWORD ApiPerceptionQuery(asQWORD observer, asQWORD target, const std::string &sense,
                           float distance, float range, bool lineOfSight, float signal, int seed) {
    if (!ResolveHandle(observer, ScriptHandleKind::Entity) ||
        !ResolveHandle(target, ScriptHandleKind::Entity)) return 0;
    if ((sense != "sight" && sense != "hearing" && sense != "smell" && sense != "magic" &&
         sense != "tremor") || !std::isfinite(distance) || distance < 0 || !std::isfinite(range) ||
        range < 0 || !std::isfinite(signal) || signal < 0 || signal > 1) {
        AbortWithDiagnostic("sandbox.perception_request_invalid");
        return 0;
    }
    const auto result = RegisterHandle(ScriptHandleKind::PerceptionResult,
        ++CurrentExecution->script_world_result_count);
    Record("perception.query", std::to_string(result) + ':' + std::to_string(observer) + ':' +
           std::to_string(target) + ':' + sense + ':' + ApiFormatFloat(distance, 3) + ':' +
           ApiFormatFloat(range, 3) + ':' + (lineOfSight ? "los" : "blocked") + ':' +
           ApiFormatFloat(signal, 3) + ':' + std::to_string(seed));
    return result;
}

void ApiLocalSetInt(asQWORD owner, const std::string &key, asINT64 value) {
    const auto id = LocalKey(owner, key);
    if (id.empty()) return;
    CurrentExecution->local_ints[id] = value;
    Record("local.set", id + ":int:" + std::to_string(value));
}

asINT64 ApiLocalGetInt(asQWORD owner, const std::string &key, asINT64 fallback) {
    const auto id = LocalKey(owner, key);
    return id.empty() ? fallback : CurrentExecution->local_ints.count(id) ? CurrentExecution->local_ints[id] : fallback;
}

void ApiLocalSetFloat(asQWORD owner, const std::string &key, double value) {
    const auto id = LocalKey(owner, key);
    if (id.empty() || !std::isfinite(value)) {
        if (!std::isfinite(value)) AbortWithDiagnostic("sandbox.local_value_invalid");
        return;
    }
    CurrentExecution->local_floats[id] = value;
    Record("local.set", id + ":float:" + ApiFormatFloat(value, 6));
}

double ApiLocalGetFloat(asQWORD owner, const std::string &key, double fallback) {
    const auto id = LocalKey(owner, key);
    return id.empty() ? fallback : CurrentExecution->local_floats.count(id) ? CurrentExecution->local_floats[id] : fallback;
}

void ApiLocalSetString(asQWORD owner, const std::string &key, const std::string &value) {
    const auto id = LocalKey(owner, key);
    if (id.empty()) return;
    if (value.size() > MaximumStringBytes) {
        AbortWithDiagnostic("quota.string_bytes_exceeded");
        return;
    }
    CurrentExecution->local_strings[id] = value;
    Record("local.set", id + ":string:" + value);
}

std::string ApiLocalGetString(asQWORD owner, const std::string &key, const std::string &fallback) {
    const auto id = LocalKey(owner, key);
    return id.empty() ? fallback : CurrentExecution->local_strings.count(id) ? CurrentExecution->local_strings[id] : fallback;
}

void ApiLocalSetEntity(asQWORD owner, const std::string &key, asQWORD value) {
    const auto id = LocalKey(owner, key);
    if (id.empty() || (value != 0 && !ResolveHandle(value, ScriptHandleKind::Entity))) return;
    CurrentExecution->local_entities[id] = value;
    Record("local.set", id + ":entity:" + std::to_string(value));
}

asQWORD ApiLocalGetEntity(asQWORD owner, const std::string &key, asQWORD fallback) {
    const auto id = LocalKey(owner, key);
    return id.empty() ? fallback : CurrentExecution->local_entities.count(id) ? CurrentExecution->local_entities[id] : fallback;
}

void ApiLocalSetVector(asQWORD owner, const std::string &key, const ScriptVec3 &value) {
    const auto id = LocalKey(owner, key);
    if (id.empty() || !IsFinite(value)) {
        if (!IsFinite(value)) AbortWithDiagnostic("sandbox.local_value_invalid");
        return;
    }
    CurrentExecution->local_vectors[id] = value;
    Record("local.set", id + ":vector:" + VecToString(value));
}

ScriptVec3 ApiLocalGetVector(asQWORD owner, const std::string &key, const ScriptVec3 &fallback) {
    const auto id = LocalKey(owner, key);
    return id.empty() ? fallback : CurrentExecution->local_vectors.count(id) ? CurrentExecution->local_vectors[id] : fallback;
}

asQWORD ApiAudioEmitter(const std::string &id) {
    if (CurrentExecution == nullptr || id.empty() || id.size() > 256) {
        AbortWithDiagnostic("sandbox.audio_emitter_invalid");
        return 0;
    }
    const auto handle = RegisterHandle(ScriptHandleKind::AudioEmitter,
        ++CurrentExecution->runtime_tool_handle_count);
    CurrentExecution->audio_active[handle] = false;
    Record("audio.emitter", id + ':' + std::to_string(handle));
    return handle;
}

bool ApiAudioIsActive(asQWORD emitter) {
    if (!ResolveHandle(emitter, ScriptHandleKind::AudioEmitter)) return false;
    return CurrentExecution->audio_active[emitter];
}

void ApiAudioSetActive(asQWORD emitter, bool enabled) {
    if (!ResolveHandle(emitter, ScriptHandleKind::AudioEmitter)) return;
    CurrentExecution->audio_active[emitter] = enabled;
    Record(enabled ? "audio.started" : "audio.stopped", std::to_string(emitter));
}

void ApiAudioStart(asQWORD emitter) { ApiAudioSetActive(emitter, true); }

void ApiAudioStop(asQWORD emitter) { ApiAudioSetActive(emitter, false); }

void ApiAudioPause(asQWORD emitter) {
    if (!ResolveHandle(emitter, ScriptHandleKind::AudioEmitter)) return;
    CurrentExecution->audio_active[emitter] = false;
    Record("audio.paused", std::to_string(emitter));
}

void ApiAudioResume(asQWORD emitter) {
    if (!ResolveHandle(emitter, ScriptHandleKind::AudioEmitter)) return;
    CurrentExecution->audio_active[emitter] = true;
    Record("audio.resumed", std::to_string(emitter));
}

void ApiAudioFade(asQWORD emitter, float gain, float seconds) {
    if (!ResolveHandle(emitter, ScriptHandleKind::AudioEmitter)) return;
    if (!std::isfinite(gain) || gain < 0 || gain > 4 || !std::isfinite(seconds) ||
        seconds < 0 || seconds > 120) {
        AbortWithDiagnostic("sandbox.audio_fade_invalid");
        return;
    }
    CurrentExecution->audio_active[emitter] = gain > 0;
    Record("audio.faded", std::to_string(emitter) + ':' + ApiFormatFloat(gain, 3) + ':' +
           ApiFormatFloat(seconds, 3));
}

asQWORD ApiAudioCaption(asQWORD emitter, const std::string &locale) {
    if (!ResolveHandle(emitter, ScriptHandleKind::AudioEmitter)) return 0;
    if (locale.empty() || locale.size() > 32) {
        AbortWithDiagnostic("sandbox.audio_caption_locale_invalid");
        return 0;
    }
    const auto caption = RegisterHandle(ScriptHandleKind::AudioCaption,
        ++CurrentExecution->runtime_tool_handle_count);
    Record("audio.caption", std::to_string(caption) + ':' + std::to_string(emitter) + ':' + locale);
    return caption;
}

void ApiAudioSnapshot(const std::string &snapshot) {
    if (snapshot.empty() || snapshot.size() > 128) {
        AbortWithDiagnostic("sandbox.audio_snapshot_invalid");
        return;
    }
    Record("audio.snapshot", snapshot);
}

void ApiAudioListener(asQWORD listener, const std::string &policy) {
    if (!ResolveHandle(listener, ScriptHandleKind::Entity)) return;
    if (policy != "camera" && policy != "entity" && policy != "player" &&
        policy != "every-listener") {
        AbortWithDiagnostic("sandbox.audio_listener_policy_invalid");
        return;
    }
    Record("audio.listener", std::to_string(listener) + ':' + policy);
}

void ApiAudioComplete(asQWORD emitter, const std::string &outcome) {
    if (!ResolveHandle(emitter, ScriptHandleKind::AudioEmitter)) return;
    if (outcome.empty() || outcome.size() > 128) {
        AbortWithDiagnostic("sandbox.audio_completion_invalid");
        return;
    }
    CurrentExecution->audio_active[emitter] = false;
    Record("audio.completed", std::to_string(emitter) + ':' + outcome);
}

asQWORD ApiWorldClock() {
    const auto clock = RegisterHandle(ScriptHandleKind::WorldClock,
        ++CurrentExecution->runtime_tool_handle_count);
    Record("time.clock", std::to_string(clock) + ':' +
           std::to_string(CurrentExecution->simulation_time_milliseconds));
    return clock;
}

asINT64 ApiWorldCalendarMilliseconds(asQWORD clock) {
    if (!ResolveHandle(clock, ScriptHandleKind::WorldClock)) return 0;
    Record("time.calendar", std::to_string(clock) + ':' +
           std::to_string(CurrentExecution->simulation_time_milliseconds));
    return CurrentExecution->simulation_time_milliseconds;
}

asQWORD ApiScheduleTimer(asINT64 delayMilliseconds, asINT64 repeatMilliseconds,
                         const std::string &handler, const std::string &schedule) {
    if (delayMilliseconds < 0 || repeatMilliseconds < 0 || handler.empty() || handler.size() > 128 ||
        schedule.empty() || schedule.size() > 128) {
        AbortWithDiagnostic("sandbox.timer_invalid");
        return 0;
    }
    const auto timer = RegisterHandle(ScriptHandleKind::Timer,
        ++CurrentExecution->runtime_tool_handle_count);
    Record("time.timer.scheduled", std::to_string(timer) + ':' +
           std::to_string(CurrentExecution->simulation_time_milliseconds + delayMilliseconds) + ':' +
           std::to_string(repeatMilliseconds) + ':' + handler + ':' + schedule);
    return timer;
}

void ApiCancelTimer(asQWORD timer) {
    if (!ResolveHandle(timer, ScriptHandleKind::Timer)) return;
    Record("time.timer.cancelled", std::to_string(timer));
    ReleaseHandle(timer, ScriptHandleKind::Timer);
}

void ApiDeterministicDelay(asINT64 delayMilliseconds, const std::string &handler) {
    if (delayMilliseconds < 0 || handler.empty() || handler.size() > 128) {
        AbortWithDiagnostic("sandbox.delay_invalid");
        return;
    }
    Record("time.delay", std::to_string(CurrentExecution->simulation_time_milliseconds + delayMilliseconds) +
           ':' + handler);
}

bool ValidNamespacedEvent(const std::string &name) {
    return !name.empty() && name.size() <= 128 && name.find('.') != std::string::npos &&
           name.find_first_of(" \t\r\n") == std::string::npos;
}

void ApiSubscribeCustomEvent(const std::string &name, int version, const std::string &handler) {
    if (!ValidNamespacedEvent(name) || version <= 0 || handler.empty() || handler.size() > 128) {
        AbortWithDiagnostic("sandbox.event_subscription_invalid");
        return;
    }
    Record("event.subscribed", name + '@' + std::to_string(version) + ':' + handler);
}

asQWORD ApiCustomEvent(const std::string &name, int version, const std::string &payload) {
    if (!ValidNamespacedEvent(name) || version <= 0 || payload.empty() || payload.size() > 4096) {
        AbortWithDiagnostic("sandbox.event_payload_invalid");
        return 0;
    }
    const auto customEvent = RegisterHandle(ScriptHandleKind::CustomEvent,
        ++CurrentExecution->runtime_tool_handle_count);
    Record("event.created", std::to_string(customEvent) + ':' + name + '@' +
           std::to_string(version) + ':' + payload);
    return customEvent;
}

void ApiPublishCustomEvent(asQWORD customEvent) {
    if (!ResolveHandle(customEvent, ScriptHandleKind::CustomEvent)) return;
    Record("event.published", std::to_string(customEvent));
}

asQWORD ApiHotReloadRequest(const std::string &moduleId, int version, const std::string &sourceHash) {
    if (moduleId.empty() || moduleId.size() > 128 || version <= 0 || sourceHash.empty() ||
        sourceHash.size() > 128) {
        AbortWithDiagnostic("sandbox.hot_reload_invalid");
        return 0;
    }
    const auto request = RegisterHandle(ScriptHandleKind::HotReload,
        ++CurrentExecution->runtime_tool_handle_count);
    Record("hot-reload.requested", std::to_string(request) + ':' + moduleId + ':' +
           std::to_string(version) + ':' + sourceHash);
    return request;
}

asQWORD ApiDebugBreak(const std::string &sessionId, const std::string &authority) {
    if (sessionId.empty() || sessionId.size() > 128 ||
        (authority != "local-author" && authority != "server-admin")) {
        AbortWithDiagnostic("sandbox.debug_permission_denied");
        return 0;
    }
    const auto session = RegisterHandle(ScriptHandleKind::DebugSession,
        ++CurrentExecution->runtime_tool_handle_count);
    Record("debug.break", std::to_string(session) + ':' + sessionId + ':' + authority);
    return session;
}

void ApiDebugStep(asQWORD session, const std::string &mode) {
    if (!ResolveHandle(session, ScriptHandleKind::DebugSession)) return;
    if (mode != "into" && mode != "over" && mode != "out" && mode != "continue") {
        AbortWithDiagnostic("sandbox.debug_step_invalid");
        return;
    }
    Record("debug.step", std::to_string(session) + ':' + mode);
}

void LineCallback(asIScriptContext *context, void *parameter) {
    auto *state = static_cast<ExecutionState *>(parameter);
    if (CurrentMemoryBudget != nullptr && CurrentMemoryBudget->exceeded) {
        state->diagnostic = "quota.memory_limit_exceeded";
        context->Abort();
        return;
    }
    if (CancelRequested.load(std::memory_order_relaxed)) {
        state->diagnostic = "script.cancelled";
        context->Abort();
        return;
    }
    state->instruction_count++;
    if (state->instruction_count > state->instruction_quota) {
        state->diagnostic = "quota.instruction_limit_exceeded";
        context->Abort();
        return;
    }

    if (std::chrono::steady_clock::now() > state->deadline) {
        state->diagnostic = "quota.timeout_exceeded";
        context->Abort();
    }
}

asIScriptEngine *CreateEngine(std::ostringstream &diagnostics, int recursion_limit) {
    auto *engine = asCreateScriptEngine(ANGELSCRIPT_VERSION);
    if (engine == nullptr) {
        diagnostics << "native:0:0:error:AngelScript engine creation failed\n";
        return nullptr;
    }

    engine->SetMessageCallback(asFUNCTION(MessageCallback), &diagnostics, asCALL_CDECL);
    engine->SetEngineProperty(asEP_MAX_NESTED_CALLS, static_cast<asPWORD>(recursion_limit));
    engine->SetEngineProperty(asEP_DISALLOW_GLOBAL_VARS, 1);
    RegisterStdString(engine);
    engine->RegisterObjectType("Vec3", sizeof(ScriptVec3),
        asOBJ_VALUE | asOBJ_POD | asGetTypeTraits<ScriptVec3>() | asOBJ_APP_CLASS_ALLFLOATS);
    engine->RegisterObjectProperty("Vec3", "float x", asOFFSET(ScriptVec3, x));
    engine->RegisterObjectProperty("Vec3", "float y", asOFFSET(ScriptVec3, y));
    engine->RegisterObjectProperty("Vec3", "float z", asOFFSET(ScriptVec3, z));
    engine->RegisterObjectType("Quat", sizeof(ScriptQuat),
        asOBJ_VALUE | asOBJ_POD | asGetTypeTraits<ScriptQuat>() | asOBJ_APP_CLASS_ALLFLOATS);
    engine->RegisterObjectProperty("Quat", "float x", asOFFSET(ScriptQuat, x));
    engine->RegisterObjectProperty("Quat", "float y", asOFFSET(ScriptQuat, y));
    engine->RegisterObjectProperty("Quat", "float z", asOFFSET(ScriptQuat, z));
    engine->RegisterObjectProperty("Quat", "float w", asOFFSET(ScriptQuat, w));
    engine->RegisterTypedef("EntityRef", "uint64");
    engine->RegisterTypedef("AreaRef", "uint64");
    engine->RegisterTypedef("AssetId", "uint64");
    engine->RegisterTypedef("EffectHandle", "uint64");
    engine->RegisterTypedef("MovementTaskRef", "uint64");
    engine->RegisterTypedef("ScriptTaskRef", "uint64");
    engine->RegisterTypedef("InventoryResultRef", "uint64");
    engine->RegisterTypedef("QuestResultRef", "uint64");
    engine->RegisterTypedef("DialogueSessionRef", "uint64");
    engine->RegisterTypedef("FactionResultRef", "uint64");
    engine->RegisterTypedef("PerceptionResultRef", "uint64");
    engine->RegisterTypedef("AudioEmitterRef", "uint64");
    engine->RegisterTypedef("AudioCaptionRef", "uint64");
    engine->RegisterTypedef("WorldClockRef", "uint64");
    engine->RegisterTypedef("WorldTimeSimulationRef", "uint64");
    engine->RegisterTypedef("TimerRef", "uint64");
    engine->RegisterTypedef("CustomEventRef", "uint64");
    engine->RegisterTypedef("HotReloadRef", "uint64");
    engine->RegisterTypedef("DebugSessionRef", "uint64");
    engine->RegisterGlobalFunction("int health(EntityRef)", asFUNCTION(ApiHealth), asCALL_CDECL);
    engine->RegisterGlobalFunction(
        "void variable(const string &in, const string &in)",
        asFUNCTION(ApiVariable),
        asCALL_CDECL);
    engine->RegisterGlobalFunction("void say(const string &in)", asFUNCTION(ApiSay), asCALL_CDECL);
    engine->RegisterGlobalFunction(
        "void cast_ability(EntityRef, const string &in)",
        asFUNCTION(ApiCast),
        asCALL_CDECL);
    engine->RegisterGlobalFunction(
        "EntityRef spawn(const string &in)",
        asFUNCTION(ApiSpawn),
        asCALL_CDECL);
    engine->RegisterGlobalFunction("void quest(const string &in)", asFUNCTION(ApiQuest), asCALL_CDECL);
    engine->RegisterGlobalFunction("void unlock(EntityRef)", asFUNCTION(ApiUnlock), asCALL_CDECL);
    engine->RegisterGlobalFunction("Vec3 vec3(float, float, float)", asFUNCTION(ApiVec3), asCALL_CDECL);
    engine->RegisterGlobalFunction("float vec_length(const Vec3 &in)", asFUNCTION(ApiVecLength), asCALL_CDECL);
    engine->RegisterGlobalFunction("float vec_dot(const Vec3 &in, const Vec3 &in)", asFUNCTION(ApiVecDot), asCALL_CDECL);
    engine->RegisterGlobalFunction("Vec3 vec_normalize(const Vec3 &in)", asFUNCTION(ApiVecNormalize), asCALL_CDECL);
    engine->RegisterGlobalFunction("Quat quat(float, float, float, float)", asFUNCTION(ApiQuat), asCALL_CDECL);
    engine->RegisterGlobalFunction("Quat quat_normalize(const Quat &in)", asFUNCTION(ApiQuatNormalize), asCALL_CDECL);
    engine->RegisterGlobalFunction("string format_int(int64)", asFUNCTION(ApiFormatInt), asCALL_CDECL);
    engine->RegisterGlobalFunction("string format_float(double, uint)", asFUNCTION(ApiFormatFloat), asCALL_CDECL);
    engine->RegisterGlobalFunction("uint unicode_length(const string &in)", asFUNCTION(ApiUnicodeLength), asCALL_CDECL);
    engine->RegisterGlobalFunction("bool regex_match(const string &in, const string &in)", asFUNCTION(ApiRegexMatch), asCALL_CDECL);
    engine->RegisterGlobalFunction("AssetId resolve_asset(const string &in)", asFUNCTION(ApiResolveAsset), asCALL_CDECL);
    engine->RegisterGlobalFunction("EffectHandle create_effect(const string &in)", asFUNCTION(ApiCreateEffect), asCALL_CDECL);
    engine->RegisterGlobalFunction("void release_effect(EffectHandle)", asFUNCTION(ApiReleaseEffect), asCALL_CDECL);
    engine->RegisterGlobalFunction("int random_int(int, int)", asFUNCTION(ApiRandomInt), asCALL_CDECL);
    engine->RegisterGlobalFunction("int64 simulation_tick()", asFUNCTION(ApiSimulationTick), asCALL_CDECL);
    engine->RegisterGlobalFunction("int64 simulation_time_ms()", asFUNCTION(ApiSimulationTimeMilliseconds), asCALL_CDECL);
    engine->RegisterGlobalFunction("AreaRef current_area()", asFUNCTION(ApiCurrentArea), asCALL_CDECL);
    engine->RegisterGlobalFunction("EntityRef find_entity(AreaRef, const string &in)", asFUNCTION(ApiFindEntity), asCALL_CDECL);
    engine->RegisterGlobalFunction("Vec3 entity_position(EntityRef)", asFUNCTION(ApiEntityPosition), asCALL_CDECL);
    engine->RegisterGlobalFunction("EntityRef spawn_at(AreaRef, const string &in, const Vec3 &in)", asFUNCTION(ApiSpawnAt), asCALL_CDECL);
    engine->RegisterGlobalFunction("void destroy_entity(EntityRef)", asFUNCTION(ApiDestroyEntity), asCALL_CDECL);
    engine->RegisterGlobalFunction("MovementTaskRef move_to(EntityRef, const Vec3 &in, const string &in)", asFUNCTION(ApiMoveTo), asCALL_CDECL);
    engine->RegisterGlobalFunction("MovementTaskRef face_towards(EntityRef, const Vec3 &in)", asFUNCTION(ApiFaceTowards), asCALL_CDECL);
    engine->RegisterGlobalFunction("MovementTaskRef follow(EntityRef, EntityRef, float)", asFUNCTION(ApiFollow), asCALL_CDECL);
    engine->RegisterGlobalFunction("void teleport(EntityRef, AreaRef, const Vec3 &in)", asFUNCTION(ApiTeleport), asCALL_CDECL);
    engine->RegisterGlobalFunction("void set_velocity(EntityRef, const Vec3 &in)", asFUNCTION(ApiSetVelocity), asCALL_CDECL);
    engine->RegisterGlobalFunction("void cancel_movement(MovementTaskRef)", asFUNCTION(ApiCancelMovement), asCALL_CDECL);
    engine->RegisterGlobalFunction("ScriptTaskRef action_enqueue(EntityRef, const string &in, int)", asFUNCTION(ApiQueueAction), asCALL_CDECL);
    engine->RegisterGlobalFunction("void action_cancel(ScriptTaskRef)", asFUNCTION(ApiCancelAction), asCALL_CDECL);
    engine->RegisterGlobalFunction("void action_complete(ScriptTaskRef, const string &in)", asFUNCTION(ApiCompleteAction), asCALL_CDECL);
    engine->RegisterGlobalFunction("bool combat_attack(EntityRef, EntityRef, const string &in, float, int)", asFUNCTION(ApiCombatAttack), asCALL_CDECL);
    engine->RegisterGlobalFunction("EffectHandle effect_apply(EntityRef, EntityRef, const string &in, float, float)", asFUNCTION(ApiApplyEffect), asCALL_CDECL);
    engine->RegisterGlobalFunction("void effect_dispel(EntityRef, const string &in)", asFUNCTION(ApiDispelEffect), asCALL_CDECL);
    engine->RegisterGlobalFunction("InventoryResultRef inventory_transfer(EntityRef, EntityRef, const string &in, int)", asFUNCTION(ApiInventoryTransfer), asCALL_CDECL);
    engine->RegisterGlobalFunction("QuestResultRef quest_transition(const string &in, const string &in, const string &in, const string &in, const string &in, int, const string &in)", asFUNCTION(ApiQuestTransition), asCALL_CDECL);
    engine->RegisterGlobalFunction("DialogueSessionRef dialogue_start(const string &in, EntityRef, EntityRef)", asFUNCTION(ApiDialogueStart), asCALL_CDECL);
    engine->RegisterGlobalFunction("void dialogue_complete(DialogueSessionRef, const string &in)", asFUNCTION(ApiDialogueComplete), asCALL_CDECL);
    engine->RegisterGlobalFunction("FactionResultRef faction_reputation(EntityRef, const string &in, int, bool, const string &in, const string &in)", asFUNCTION(ApiFactionReputation), asCALL_CDECL);
    engine->RegisterGlobalFunction("PerceptionResultRef perception_query(EntityRef, EntityRef, const string &in, float, float, bool, float, int)", asFUNCTION(ApiPerceptionQuery), asCALL_CDECL);
    engine->RegisterGlobalFunction("AudioEmitterRef audio_emitter(const string &in)", asFUNCTION(ApiAudioEmitter), asCALL_CDECL);
    engine->RegisterGlobalFunction("bool audio_is_active(AudioEmitterRef)", asFUNCTION(ApiAudioIsActive), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_set_active(AudioEmitterRef, bool)", asFUNCTION(ApiAudioSetActive), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_start(AudioEmitterRef)", asFUNCTION(ApiAudioStart), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_stop(AudioEmitterRef)", asFUNCTION(ApiAudioStop), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_pause(AudioEmitterRef)", asFUNCTION(ApiAudioPause), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_resume(AudioEmitterRef)", asFUNCTION(ApiAudioResume), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_fade(AudioEmitterRef, float, float)", asFUNCTION(ApiAudioFade), asCALL_CDECL);
    engine->RegisterGlobalFunction("AudioCaptionRef audio_caption(AudioEmitterRef, const string &in)", asFUNCTION(ApiAudioCaption), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_snapshot(const string &in)", asFUNCTION(ApiAudioSnapshot), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_listener(EntityRef, const string &in)", asFUNCTION(ApiAudioListener), asCALL_CDECL);
    engine->RegisterGlobalFunction("void audio_complete(AudioEmitterRef, const string &in)", asFUNCTION(ApiAudioComplete), asCALL_CDECL);
    engine->RegisterGlobalFunction("WorldClockRef world_clock()", asFUNCTION(ApiWorldClock), asCALL_CDECL);
    engine->RegisterGlobalFunction("WorldTimeSimulationRef world_time()", asFUNCTION(ApiWorldClock), asCALL_CDECL);
    engine->RegisterGlobalFunction("int64 world_calendar_ms(WorldClockRef)", asFUNCTION(ApiWorldCalendarMilliseconds), asCALL_CDECL);
    engine->RegisterGlobalFunction("TimerRef timer_schedule(int64, int64, const string &in, const string &in)", asFUNCTION(ApiScheduleTimer), asCALL_CDECL);
    engine->RegisterGlobalFunction("void timer_cancel(TimerRef)", asFUNCTION(ApiCancelTimer), asCALL_CDECL);
    engine->RegisterGlobalFunction("void deterministic_delay(int64, const string &in)", asFUNCTION(ApiDeterministicDelay), asCALL_CDECL);
    engine->RegisterGlobalFunction("void event_subscribe(const string &in, int, const string &in)", asFUNCTION(ApiSubscribeCustomEvent), asCALL_CDECL);
    engine->RegisterGlobalFunction("CustomEventRef custom_event(const string &in, int, const string &in)", asFUNCTION(ApiCustomEvent), asCALL_CDECL);
    engine->RegisterGlobalFunction("void event_publish(CustomEventRef)", asFUNCTION(ApiPublishCustomEvent), asCALL_CDECL);
    engine->RegisterGlobalFunction("HotReloadRef hot_reload_request(const string &in, int, const string &in)", asFUNCTION(ApiHotReloadRequest), asCALL_CDECL);
    engine->RegisterGlobalFunction("DebugSessionRef debug_break(const string &in, const string &in)", asFUNCTION(ApiDebugBreak), asCALL_CDECL);
    engine->RegisterGlobalFunction("void debug_step(DebugSessionRef, const string &in)", asFUNCTION(ApiDebugStep), asCALL_CDECL);
    engine->RegisterGlobalFunction("void local_set_int(EntityRef, const string &in, int64)", asFUNCTION(ApiLocalSetInt), asCALL_CDECL);
    engine->RegisterGlobalFunction("int64 local_get_int(EntityRef, const string &in, int64)", asFUNCTION(ApiLocalGetInt), asCALL_CDECL);
    engine->RegisterGlobalFunction("void local_set_float(EntityRef, const string &in, double)", asFUNCTION(ApiLocalSetFloat), asCALL_CDECL);
    engine->RegisterGlobalFunction("double local_get_float(EntityRef, const string &in, double)", asFUNCTION(ApiLocalGetFloat), asCALL_CDECL);
    engine->RegisterGlobalFunction("void local_set_string(EntityRef, const string &in, const string &in)", asFUNCTION(ApiLocalSetString), asCALL_CDECL);
    engine->RegisterGlobalFunction("string local_get_string(EntityRef, const string &in, const string &in)", asFUNCTION(ApiLocalGetString), asCALL_CDECL);
    engine->RegisterGlobalFunction("void local_set_entity(EntityRef, const string &in, EntityRef)", asFUNCTION(ApiLocalSetEntity), asCALL_CDECL);
    engine->RegisterGlobalFunction("EntityRef local_get_entity(EntityRef, const string &in, EntityRef)", asFUNCTION(ApiLocalGetEntity), asCALL_CDECL);
    engine->RegisterGlobalFunction("void local_set_vector(EntityRef, const string &in, const Vec3 &in)", asFUNCTION(ApiLocalSetVector), asCALL_CDECL);
    engine->RegisterGlobalFunction("Vec3 local_get_vector(EntityRef, const string &in, const Vec3 &in)", asFUNCTION(ApiLocalGetVector), asCALL_CDECL);
    return engine;
}

} // namespace

FORGE_EXPORT const char *forge_script_vm_version() {
    return ANGELSCRIPT_VERSION_STRING;
}

FORGE_EXPORT const char *forge_script_api_version() {
    return ForgeApiVersion;
}

FORGE_EXPORT int forge_script_compile(
    const char *source,
    std::uint8_t *bytecode,
    int bytecode_capacity,
    char *diagnostics,
    int diagnostics_capacity) {
    if (source == nullptr || bytecode_capacity < 0) {
        CopyText("native:0:0:error:Invalid compile arguments\n", diagnostics, diagnostics_capacity);
        return -1;
    }

    EnsureMemoryFunctions();
    std::ostringstream diagnostic_stream;
    auto *engine = CreateEngine(diagnostic_stream, 32);
    if (engine == nullptr) {
        CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
        return -1;
    }

    auto *module = engine->GetModule("forge_module", asGM_ALWAYS_CREATE);
    module->AddScriptSection("module.forge", source);
    if (module->Build() < 0 || module->GetFunctionByDecl("void main()") == nullptr) {
        if (module->GetFunctionByDecl("void main()") == nullptr) {
            diagnostic_stream << "module.forge:0:0:error:Entry function 'void main()' is required\n";
        }

        CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        return -1;
    }

    BytecodeStream stream;
    if (module->SaveByteCode(&stream, false) < 0) {
        diagnostic_stream << "native:0:0:error:Bytecode serialization failed\n";
        CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        return -1;
    }

    if (static_cast<int>(stream.bytes.size()) > bytecode_capacity || bytecode == nullptr) {
        diagnostic_stream << "native:0:0:error:Bytecode buffer too small; required=" << stream.bytes.size() << '\n';
        CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        return -static_cast<int>(stream.bytes.size());
    }

    std::memcpy(bytecode, stream.bytes.data(), stream.bytes.size());
    CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
    engine->ShutDownAndRelease();
    return static_cast<int>(stream.bytes.size());
}

FORGE_EXPORT int forge_script_execute(
    const std::uint8_t *bytecode,
    int bytecode_size,
    int instruction_quota,
    int timeout_milliseconds,
    int recursion_limit,
    int spawn_cap,
    int memory_limit_bytes,
    std::uint64_t deterministic_seed,
    asINT64 simulation_tick,
    asINT64 simulation_time_milliseconds,
    char *events,
    int events_capacity,
    char *diagnostics,
    int diagnostics_capacity,
    asINT64 *instructions,
    asINT64 *peak_memory_bytes,
    asINT64 *elapsed_microseconds) {
    if (instructions != nullptr) *instructions = 0;
    if (peak_memory_bytes != nullptr) *peak_memory_bytes = 0;
    if (elapsed_microseconds != nullptr) *elapsed_microseconds = 0;
    if (bytecode == nullptr || bytecode_size <= 0 || instruction_quota <= 0 ||
        timeout_milliseconds <= 0 || recursion_limit <= 0 || spawn_cap < 0 ||
        memory_limit_bytes < 65536 || deterministic_seed == 0 || simulation_tick < 0 ||
        simulation_time_milliseconds < 0) {
        CopyText("native:0:0:error:Invalid execute arguments\n", diagnostics, diagnostics_capacity);
        return -1;
    }

    EnsureMemoryFunctions();
    std::lock_guard<std::mutex> lock(ExecutionMutex);
    const auto started = std::chrono::steady_clock::now();
    MemoryBudget memory_budget{static_cast<std::size_t>(memory_limit_bytes)};
    MemoryBudgetScope memory_scope(&memory_budget);
    CancelRequested.store(false, std::memory_order_relaxed);
    std::ostringstream diagnostic_stream;
    auto *engine = CreateEngine(diagnostic_stream, recursion_limit);
    if (engine == nullptr) {
        if (memory_budget.exceeded) diagnostic_stream << "runtime:0:0:error:quota.memory_limit_exceeded\n";
        CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
        if (peak_memory_bytes != nullptr) *peak_memory_bytes = static_cast<asINT64>(memory_budget.peak);
        return memory_budget.exceeded ? 1 : -1;
    }

    BytecodeStream stream;
    stream.bytes.assign(bytecode, bytecode + bytecode_size);
    auto *module = engine->GetModule("forge_module", asGM_ALWAYS_CREATE);
    if (module->LoadByteCode(&stream) < 0) {
        CopyText(memory_budget.exceeded
            ? "runtime:0:0:error:quota.memory_limit_exceeded\n"
            : "native:0:0:error:Bytecode load failed\n", diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        if (peak_memory_bytes != nullptr) *peak_memory_bytes = static_cast<asINT64>(memory_budget.peak);
        return memory_budget.exceeded ? 1 : -1;
    }
    if (memory_budget.exceeded) {
        CopyText("runtime:0:0:error:quota.memory_limit_exceeded\n", diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        if (peak_memory_bytes != nullptr) *peak_memory_bytes = static_cast<asINT64>(memory_budget.peak);
        if (elapsed_microseconds != nullptr) *elapsed_microseconds =
            std::chrono::duration_cast<std::chrono::microseconds>(std::chrono::steady_clock::now() - started).count();
        return 1;
    }

    auto *entry = module->GetFunctionByDecl("void main()");
    if (entry == nullptr) {
        CopyText("native:0:0:error:Bytecode entry function is missing\n", diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        return -1;
    }

    ExecutionState state;
    state.instruction_quota = instruction_quota;
    state.spawn_cap = spawn_cap;
    state.deterministic_state = deterministic_seed;
    state.simulation_tick = simulation_tick;
    state.simulation_time_milliseconds = simulation_time_milliseconds;
    state.deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeout_milliseconds);
    CurrentExecution = &state;
    auto *context = engine->CreateContext();
    if (context == nullptr) {
        CurrentExecution = nullptr;
        CopyText(memory_budget.exceeded
            ? "runtime:0:0:error:quota.memory_limit_exceeded\n"
            : "native:0:0:error:Execution context creation failed\n", diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        if (peak_memory_bytes != nullptr) *peak_memory_bytes = static_cast<asINT64>(memory_budget.peak);
        return memory_budget.exceeded ? 1 : -1;
    }
    context->SetLineCallback(asFUNCTION(LineCallback), &state, asCALL_CDECL);
    context->Prepare(entry);
    const auto result = context->Execute();
    if (result == asEXECUTION_EXCEPTION) {
        state.diagnostic = std::string("script.exception:") + context->GetExceptionString();
    } else if (result == asEXECUTION_ABORTED && state.diagnostic.empty()) {
        state.diagnostic = "script.execution_aborted";
    }

    if (memory_budget.exceeded) state.diagnostic = "quota.memory_limit_exceeded";

    CopyText(state.events.str(), events, events_capacity);
    if (!state.diagnostic.empty()) {
        diagnostic_stream << "runtime:0:0:error:" << state.diagnostic << '\n';
    }

    CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
    context->Release();
    engine->ShutDownAndRelease();
    CurrentExecution = nullptr;
    const auto elapsed = std::chrono::duration_cast<std::chrono::microseconds>(
        std::chrono::steady_clock::now() - started).count();
    if (instructions != nullptr) *instructions = state.instruction_count;
    if (peak_memory_bytes != nullptr) *peak_memory_bytes = static_cast<asINT64>(memory_budget.peak);
    if (elapsed_microseconds != nullptr) *elapsed_microseconds = elapsed;
    if (state.diagnostic == "script.cancelled") return 3;
    if (memory_budget.exceeded || state.diagnostic.rfind("quota.", 0) == 0) return 1;
    return result == asEXECUTION_FINISHED ? 0 : 2;
}

FORGE_EXPORT void forge_script_cancel_current() {
    CancelRequested.store(true, std::memory_order_relaxed);
}

// Minimal GDExtension entry point. The C# host owns the ForgeScript API; the native library is also
// loadable by the engine so deployment and ABI compatibility are exercised by runtime smoke tests.
using GDExtensionBool = std::uint8_t;
using GDExtensionInterfaceGetProcAddress = void *(*)(const char *);
using GDExtensionClassLibraryPtr = void *;
enum GDExtensionInitializationLevel {
    GDEXTENSION_INITIALIZATION_CORE = 0,
};
using GDExtensionInitializationFunction = void (*)(void *, GDExtensionInitializationLevel);
struct GDExtensionInitialization {
    GDExtensionInitializationLevel minimum_initialization_level;
    void *userdata;
    GDExtensionInitializationFunction initialize;
    GDExtensionInitializationFunction deinitialize;
};

void ForgeExtensionInitialize(void *, GDExtensionInitializationLevel) {}
void ForgeExtensionDeinitialize(void *, GDExtensionInitializationLevel) {}

FORGE_EXPORT GDExtensionBool openrpg_forge_library_init(
    GDExtensionInterfaceGetProcAddress,
    GDExtensionClassLibraryPtr,
    GDExtensionInitialization *initialization) {
    if (initialization == nullptr) {
        return 0;
    }

    initialization->minimum_initialization_level = GDEXTENSION_INITIALIZATION_CORE;
    initialization->userdata = nullptr;
    initialization->initialize = ForgeExtensionInitialize;
    initialization->deinitialize = ForgeExtensionDeinitialize;
    return 1;
}
