# agile-deob

A small, readable **Agile.NET** (SecureTeam, formerly *CliSecure*) deobfuscator
built on [dnlib](https://github.com/0xd4d/dnlib) — the metadata/IL library behind
[de4dot](https://github.com/de4dot/de4dot) and dnSpy.

Built as a **CTF / reverse-engineering learning tool**: the goal is code you can
read and extend to understand how managed-code obfuscation works and how it comes
apart, not a drop-in replacement for de4dot. Use it on binaries you have the right
to analyze (your own builds, CTF challenges, malware samples in a lab).

---

## What Agile.NET does, and how we undo it

| Protection | Obfuscated form | This tool's pass | Status |
|---|---|---|---|
| **String encryption** | `ldc.i4 key; call string Dec(int)` | `strings` | ✅ detects sites; ✅ inlines plaintext in `--dynamic` mode |
| **Proxy calls** | `call Forwarder` → real target | `proxies` | ✅ pure static forwarders inlined; ⏳ delegate-field variant detected only |
| **Control-flow flattening** | switch-dispatch over a state var | `cflow` | 🟡 peephole cleanup done; ⏳ full dispatch recovery stubbed |
| **Encrypted resources / native stub** | `<Module>::.cctor` installs hooks | (detector) | 🔎 detected, reported; not stripped |
| **Junk attributes** | `[SecureTeam…]`, `[Obfuscation]` | `cleanup` | ✅ removed |

Legend: ✅ implemented · 🟡 partial · 🔎 detect-only · ⏳ documented stub

Being honest about the stubs matters more than pretending they work — see
[Roadmap](#roadmap).

---

## Build

Requires the **.NET 8 SDK** (`dotnet --version` ≥ 8). dnlib restores from NuGet.

```bash
cd agile-deob
dotnet build -c Release
# binary at: src/AgileDeob/bin/Release/net8.0/agile-deob
```

Or run without a separate build step:

```bash
dotnet run --project src/AgileDeob -- <args>
```

> There was no .NET SDK in the environment where this was scaffolded, so it has
> not been compiled here. Build it locally and tell me about any errors — the
> code targets the dnlib 4.4.0 API.

---

## Usage

```
agile-deob <input.dll|exe> [options]

  -o, --output <path>   Output path (default: <input>-cleaned.<ext>)
      --detect          Only detect & report; do not rewrite
      --dynamic         Invoke the target's string decrypter in-process to
                        recover plaintext. RUNS UNTRUSTED CODE — use a VM.
      --force           Rewrite even if Agile.NET isn't detected
  -v, --verbose         Debug logging
  -h, --help            Help
```

```bash
# Just fingerprint it:
agile-deob Protected.exe --detect -v

# Full clean, decrypting strings (⚠ executes the target — sandbox it):
agile-deob Protected.exe --dynamic -o Clean.exe
```

### ⚠ About `--dynamic`

Recovering Agile.NET strings reliably means **running the target's own decrypter
method**. `--dynamic` loads the assembly into this process and reflection-invokes
it. That executes attacker-controlled code (module initializers, the decrypter
body, anything they chain off it). **Only use it inside a throwaway VM or
container.** Without `--dynamic`, the `strings` pass locates and reports the call
sites but leaves them untouched. de4dot makes the same trade-off.

---

## Architecture

```
src/AgileDeob/
  Program.cs                 CLI arg parsing → Deobfuscator
  Core/
    DeobOptions.cs           parsed flags
    DeobContext.cs           module + options + logger + change stats
    Deobfuscator.cs          load → detect → run passes → write
    DynamicInvoker.cs        reflection bridge for --dynamic string decryption
  Detection/
    AgileDetector.cs         heuristic Agile.NET fingerprinting
  Passes/
    IDeobfuscatorPass.cs     pass interface
    StringDecryptPass.cs     string literal recovery
    ProxyCallPass.cs         proxy/forwarder inlining
    ControlFlowPass.cs       peephole cleanup + dispatch-recovery skeleton
    CleanupPass.cs           strip obfuscator attributes
  Util/
    Logger.cs                leveled console logger
    InstructionExtensions.cs IL helpers (ldc.i4 decode, call resolution, …)
```

Passes run in order and each records what it changed via `DeobContext.Count`,
printed as a summary at the end. Adding a pass is: implement `IDeobfuscatorPass`,
add it to the array in `Deobfuscator._passes`.

The detector scores independent structural signals (encrypted resources, a
string-decrypter-shaped method, a `<Module>` cctor initializer, a flood of
delegate fields) and calls it Agile.NET at ≥ 2 markers. These are deliberately
readable heuristics, **not** de4dot's exact per-version fingerprints.

---

## Roadmap

The interesting unfinished work, in rough priority order:

1. **Control-flow recovery** (`ControlFlowPass.RecoverFlattenedDispatch`).
   Reverse switch-dispatch flattening properly:
   1. split each method into basic blocks;
   2. find the dispatcher (state-var load + switch routing to every block);
   3. per block, read the constant it stores to the state var → its successor;
   4. relink blocks into a normal CFG, drop the dispatcher + state var, re-emit
      in topological order, fixing up exception regions.
   This is the de4dot "blocks" framework; porting a minimal version of it is the
   single biggest win here.

2. **Delegate-field proxy calls.** Resolve the lazy
   `ldsfld delegate; … callvirt Invoke` pattern back to direct calls (track the
   field's initializer to find the real target).

3. **Static string decryption.** A tiny IL emulator for the decrypter body so
   `--dynamic` isn't required (safer, works on broken samples).

4. **Dead-code removal.** After inlining proxies, delete now-unreferenced
   forwarder methods and the obfuscator's helper types (behind a
   reference-count check so nothing live is removed).

5. **Resource / native-stub stripping.** Decrypt embedded resources and neutralize
   the `<Module>` cctor hook.

6. **Real version fingerprinting** in `AgileDetector.GuessVersion`.

---

## Credits & prior art

This stands on the shoulders of **de4dot** (its `AgileDotNet` module is the
reference implementation) and **dnlib/dnSpy** by 0xd4d. If you want a
battle-tested tool today, use de4dot; this repo is for learning how it works.
