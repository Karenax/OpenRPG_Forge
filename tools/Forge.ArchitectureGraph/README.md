# Forge.ArchitectureGraph

Official Roslyn implementation of the OpenRPG Forge architecture graph. Generated artifacts are
stored in `docs/architecture/graph.json` and `docs/architecture/overview.mmd`.

The tool uses the Roslyn assemblies shipped with the repository's pinned .NET SDK. No production
NuGet package is added. C# relations are resolved semantically in one pass over syntax identifiers,
avoiding the previous all-types-by-all-types regex scan.

## Commands

```powershell
dotnet run --project tools/Forge.ArchitectureGraph -- generate
dotnet run --project tools/Forge.ArchitectureGraph -- check
dotnet run --project tools/Forge.ArchitectureGraph -- check --full
dotnet run --project tools/Forge.ArchitectureGraph -- stats
dotnet run --project tools/Forge.ArchitectureGraph -- query "ProjectJsonSerializer" --format context
dotnet run --project tools/Forge.ArchitectureGraph -- query "AreaDefinition" --mode impact --format paths
dotnet run --project tools/Forge.ArchitectureGraph -- query "AreaDefinition" --mode tests --depth 2 --max-nodes 40
```

`check` compares the per-file manifest, the source digest and the derived overview without rebuilding
the Roslyn compilations. `check --full` additionally rebuilds and compares the canonical graph.

The Roslyn graph is the repository's authoritative architecture index. Generation is deterministic,
atomic and independent of checkout paths and LF/CRLF line endings.

## LLM context contract

`query --format context` returns a bounded, evidence-backed slice instead of the full graph:

- source files and symbol kinds to read;
- typed incoming and outgoing relations with evidence path and line;
- directly or transitively affected tests.

The `impact`, `dependencies`, `dependents`, and `tests` modes keep traversal intent explicit. Project
containment edges are retained in the stored graph but excluded from query traversal so one match
does not expand into an entire project. `--depth` and `--max-nodes` provide hard context bounds.

C# relations use Roslyn symbols across project references. Markdown, Godot, C++, and Python receive
conservative supplemental syntactic links. NuGet package assemblies are not loaded in this first
version; unresolved external APIs therefore do not create speculative edges.
