#include <angelscript.h>
#include <scriptstdstring.h>

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <mutex>
#include <sstream>
#include <string>
#include <vector>

#if defined(_WIN32)
#define FORGE_EXPORT extern "C" __declspec(dllexport)
#else
#define FORGE_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace {

constexpr const char *ForgeApiVersion = "1.0.0";

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
    std::chrono::steady_clock::time_point deadline;
};

thread_local ExecutionState *CurrentExecution = nullptr;
std::mutex ExecutionMutex;

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

int ApiHealth(asQWORD entity) {
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
    Record("cast", std::to_string(entity) + ":" + ability);
}

asQWORD ApiSpawn(const std::string &blueprint) {
    if (CurrentExecution == nullptr) {
        return 0;
    }

    CurrentExecution->spawn_count++;
    if (CurrentExecution->spawn_count > CurrentExecution->spawn_cap) {
        CurrentExecution->diagnostic = "quota.spawn_cap_exceeded";
        if (auto *context = asGetActiveContext()) {
            context->Abort();
        }
        return 0;
    }

    const auto handle = static_cast<asQWORD>(1000 + CurrentExecution->spawn_count);
    Record("spawn", blueprint + ":" + std::to_string(handle));
    return handle;
}

void ApiQuest(const std::string &quest) {
    Record("quest", quest);
}

void ApiUnlock(asQWORD entity) {
    Record("unlock", std::to_string(entity));
}

void LineCallback(asIScriptContext *context, void *parameter) {
    auto *state = static_cast<ExecutionState *>(parameter);
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
    engine->RegisterTypedef("EntityRef", "uint64");
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
    char *events,
    int events_capacity,
    char *diagnostics,
    int diagnostics_capacity) {
    if (bytecode == nullptr || bytecode_size <= 0 || instruction_quota <= 0 ||
        timeout_milliseconds <= 0 || recursion_limit <= 0 || spawn_cap < 0) {
        CopyText("native:0:0:error:Invalid execute arguments\n", diagnostics, diagnostics_capacity);
        return -1;
    }

    std::lock_guard<std::mutex> lock(ExecutionMutex);
    std::ostringstream diagnostic_stream;
    auto *engine = CreateEngine(diagnostic_stream, recursion_limit);
    if (engine == nullptr) {
        CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
        return -1;
    }

    BytecodeStream stream;
    stream.bytes.assign(bytecode, bytecode + bytecode_size);
    auto *module = engine->GetModule("forge_module", asGM_ALWAYS_CREATE);
    if (module->LoadByteCode(&stream) < 0) {
        CopyText("native:0:0:error:Bytecode load failed\n", diagnostics, diagnostics_capacity);
        engine->ShutDownAndRelease();
        return -1;
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
    state.deadline = std::chrono::steady_clock::now() + std::chrono::milliseconds(timeout_milliseconds);
    CurrentExecution = &state;
    auto *context = engine->CreateContext();
    context->SetLineCallback(asFUNCTION(LineCallback), &state, asCALL_CDECL);
    context->Prepare(entry);
    const auto result = context->Execute();
    if (result == asEXECUTION_EXCEPTION) {
        state.diagnostic = std::string("script.exception:") + context->GetExceptionString();
    } else if (result == asEXECUTION_ABORTED && state.diagnostic.empty()) {
        state.diagnostic = "script.execution_aborted";
    }

    CurrentExecution = nullptr;
    CopyText(state.events.str(), events, events_capacity);
    if (!state.diagnostic.empty()) {
        diagnostic_stream << "runtime:0:0:error:" << state.diagnostic << '\n';
    }

    CopyText(diagnostic_stream.str(), diagnostics, diagnostics_capacity);
    context->Release();
    engine->ShutDownAndRelease();
    return result == asEXECUTION_FINISHED ? 0 : result == asEXECUTION_ABORTED ? 1 : 2;
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
