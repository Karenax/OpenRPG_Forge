# R079 — Creature interaction composition acceptance

## Scope

`R079 / FC-CREATURE-P1-03` closes exactly five P1 functions:

- `ORF-CRE-025` — creature faction;
- `ORF-CRE-026` — perception;
- `ORF-CRE-028` — associated conversation;
- `ORF-CRE-031` — plot, immortality and interruptibility;
- `ORF-CRE-034` — `OnConversation`.

Source criteria were read in full from
`Divers/OpenRPG_Forge_Aurora_Feature_Dossier/docs/requirements/05_CREATURE.md` at lines 1889–1963,
1964–2038, 2114–2188, 2339–2413 and 2567–2644. The clean-room adaptation requires a canonical,
versioned profile with stable UUIDs, provenance, engine-independent transform and asset identity;
one transactional authoring path; localized diagnostics; safe optional runtime fallback; typed
ForgeScript reads; filtered MCP reads plus dry-run semantic mutation; persistence and undo/redo.

## Dependency reduction

The declared AREA, DIALOGUE, DM, FACTION and SCRIPT dependencies are reduced to their already
available traversing contracts: canonical faction definitions, dialogue blueprint IDs, typed event
bindings, area-independent sense data and named GM permissions. R079 does not open the later P1
authoring lots for factions or GM tools and does not add P2 behavior.

## Audited gap matrix

| Source requirement | Present before R079 | Real limit to close | Required evidence |
|---|---|---|---|
| Weighted memberships, primary faction, rank, personal reputation and inheritance | Isolated RPG Kit records and faction catalog | No UUID-bearing canonical creature profile, cross-reference validation or authoring path | Domain tests, project validation, UI proof, round-trip |
| Vision, hearing, smell and magic with ranges, cones, occlusion, stimuli and memory | RPG Kit sense records and deterministic detection | Not attached to canonical creature foundation or surfaced to runtime/scripts/agents | Validator and detection tests, runtime node proof, typed handle |
| Dialogue bindings by context, priority, interlocutor, state and fallback | RPG Kit selection service | No canonical UUID-bearing bindings or dialogue blueprint resolution | Selection tests, project reference diagnostics, UI preview |
| Protection policy for invulnerability, narrative death, dialogue interruption and GM permissions | RPG Kit policy record | Not persisted or authorable on creature blueprints | Canonical validation, UI, command and round-trip |
| `InteractionRequested` / `DialogueStarting` with selection and refusal reasons | Typed RPG Kit events | No versioned canonical `OnConversation` binding connected to the profile | Event validation, request simulation, runtime and smoke markers |

## Acceptance matrix

| Acceptance criterion shared by the five source functions | Proof required for closure |
|---|---|
| Author without hand-editing generated files | Dedicated five-section creator window with empty/error states and visible simulation |
| Save, close and reload exactly preserves canonical data | JSON round-trip test and sample reload smoke |
| Invalid configuration is refused before build with a localized diagnostic | Domain, project and command-handler tests for UUIDs, ranges and references |
| UI mutation and domain command produce the same result | Window calls `SetCreatureFoundationHandler` for both dry-run and apply |
| Undo then redo restore old and new documents | Existing command history integration exercised by the R079 Toolset smoke |
| Runtime optional/required behavior is safe | Resolver tests plus runtime smoke marker |
| Runtime reads and agent access stay bounded | Typed ForgeScript handle, filtered MCP resource and dry-run mutation proof |

## Planned bounded changes

- Add one optional `CreatureInteractionCompositionProfile` tail to `CreatureFoundationProfile`.
- Validate component UUIDs, ranges, faction/dialogue references and typed event bindings.
- Reuse the existing foundation command, serializer and undo/redo path; do not create parallel storage.
- Adapt the canonical profile to existing RPG Kit interaction services.
- Add one Toolset window, one runtime representation and the corresponding script/MCP projections.
- Add a governed sample and reproducible tests/capture, then promote only these five tracker rows.

## Implemented outcome

- `CreatureFoundationProfile` owns one optional, versioned interaction-composition tail with stable
  component UUIDs, provenance, asset identity, preview transform and override deltas.
- The canonical validator, project validator and existing transactional foundation command reject
  invalid UUIDs, ranges, duplicates and unresolved faction/dialogue references before persistence.
- The creator window exposes the five audited sections, empty/error states, a deterministic dialogue
  request simulation, dry-run/apply and the shared undo/redo history.
- Typed ForgeScript and filtered MCP projections expose the bounded profile; the MCP mutation keeps
  the existing dry-run semantic-diff path.
- The RPG Kit adapter validates the mapped factions and senses, while the runtime materializes five
  explicit node groups for factions, perception, dialogue bindings, protection and `OnConversation`.
- The governed Crypt of Ashes sample carries two weighted factions, four senses, three contextual
  dialogue bindings, narrative protections and a server `OnConversation` event.

## Targeted validation

- Domain interaction-composition tests: 4/4 passed.
- RPG Kit interaction and rules filters: 6/6 passed.
- Application foundation command tests: 7/7 passed.
- Module-format sample and round-trip tests: 5/5 passed.
- MCP foundation projection test: 1/1 passed.
- Debug solution build and `dotnet format --verify-no-changes`: passed with zero warnings/errors.
- Toolset and runtime target smokes passed with the exact R079 markers in
  `artifacts/verification/r079-toolset-target.log` and
  `artifacts/verification/r079-runtime-target.log`.
- Visual proof `docs/aurora-parity/evidence/r079-creature-interaction-ui.png` was inspected at
  1500×930 with two memberships, four readable senses, dialogue selection, protection and event
  binding visible without overlap.
- Integrated portal `scripts/Verify.ps1`: passed with 1,558 .NET tests, 39 Python tests, exact R079
  Toolset/runtime markers and `OPENRPG_FORGE_VERIFICATION=PASS`; transcript:
  `.artifacts/r079-full-verify-transcript.log`.
- Roslyn graph: fresh and fully deterministic at 7,040 nodes / 44,899 relations before the final
  documentation refresh.

## Non-objectives

- `R080` creature event/AI functions;
- later FACTION or DM P1 lots;
- P2 functions and AV7–AV10;
- a new ruleset, networking protocol, engine fork or renderer;
- commit or push.
