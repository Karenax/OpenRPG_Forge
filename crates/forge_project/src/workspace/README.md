# Workspace authoring — ADR-0016

ReadHistory exposes accepted workspace operations, newest first, twenty per page.
Pages are tied to the current head hash and reject stale/out-of-range offsets.
Only command ID, resulting revision, typed action and recorded zone scope labels
are returned, never before/after workspace snapshots or zone archive payloads.
The read does not save, mutate, or load immutable objects. Managed zones and
playtest do not prevent this read-only request. Studio has explicit previous/next
page controls; other requests clear the displayed history instead of showing stale
entries. No canonical archive format change; rendering still needs qualification.

New module zones use ReviewNewZone/CreateNewZone with an exact initial terrain
recipe and the existing attachment receipt. Preview writes nothing; Apply validates
the recipe against reviewed archive/head hashes, publishes an immutable zone and
updates the workspace. Save remains explicit; Undo/Redo and managed tabs reuse
existing commands. A create-new lock-only area directory prevents concurrent legacy
creation before Save; no legacy authoring.json is written. Interrupted/abandoned
creation may leave a reserved key and orphan object; no implicit cleanup/reuse.
Studio has module/key/name/seed/relief fields and separate Review/Create buttons.
Graphical qualification is pending.

Managed compaction uses the existing zone delta codec, bound to a reviewed
workspace UpdateZone request. It preserves the complete zone head and receipts,
publishes a new immutable object and requires explicit Save. The host routes
existing compaction controls through this adapter; switching tabs clears review.

Managed tabs retain only identities/labels in the host (maximum 16); one zone
projection is active. Switching reads the accepted archive reference from the
workspace, so unsaved edits survive switching without extra 3D scenes. Dirty
state compares each reference with the last saved workspace checkpoint. Closing
an unsaved active zone is refused; Save saves the workspace and clears all zone
dirty flags. Structural workspace actions require all managed tabs to be closed.

Adoption UI now selects a saved zone and destination module. ReviewAdoption reads
and replays exact source bytes under its lock without publishing an object or
membership; Adopt reacquires the lock and revalidates the prepared request.
The source must be closed and belong to the destination project. UI rendering
and the full gesture path still require qualification.

The shared authoring::workspace protocol is exposed by AuthoringHost and the
Studio Tauri command using the same host mutex as legacy zone commands. Workspace
close rejects dirty state; mutation is blocked during playtest. Visible Studio
forms are wired for project/module commands (Web compiled, not visually qualified);
managed zones now open through OpenManaged and the existing authoring projection.
This wiring is Web-compiled, not visually qualified.

The host keeps a read-only projection of the accepted managed archive. Zone
Preview/Apply refresh that projection through the workspace service; Save commits
the workspace. History and playtest reuse the same accepted zone head. Workspace
structural commands are rejected while a managed zone is open, preventing a
stale editable projection. Undo/Redo keep the existing direct command gesture.

The engine-neutral head contains stable project/module IDs and zone hash references.
The command session owns preview, apply, retry, audit, undo and redo. Its versioned
archive stores the initial head, requests and final head; reopening replays and
checks the result instead of trusting a serialized undo stack.

Bounds: 4 MiB input/archive, 64 projects, 256 modules and 4096 unique areas globally,
256 UTF-8 bytes per trimmed name, 1024 commands and 32 MiB of serialized receipt
snapshots (not a measurement of total heap). Exact sibling names are unique
(case-sensitive; no Unicode normalization). Names are labels, never filesystem
paths. Module IDs are globally unique.

The native file session saves projects/modules under the reserved workspace-v1
directory using DocumentStore locking, expected hash and atomic replacement.
Zone references are verified on save/open, including retained history. Interrupted
saves block ordinary opening. ReviewRecovery reports saved/pending heads only
after replay and object verification. Recover requires exact saved/pending hashes,
archives both versions through DocumentStore, and promotes only a valid candidate.
Promotion holds existing legacy source locks through replacement. The host requires
all sessions closed. Studio exposes review and the two explicit choices; graphical
qualification is pending. No automatic opening, migration, or orphan cleanup.
A valid head alone is not proof that its referenced objects exist.

`objects` provides the native immutable archive store: exact SHA-256 review,
existing zone-codec replay, bounded reads, project/area/head verification and
create-new staging followed by no-replace hard-link publication. Final objects
are never overwritten. Orphans left by interruption are not garbage-collected.
This adapter does not activate workspace membership; publication of an object is
not adoption. A filesystem without hard-link support returns an explicit error.

FileWorkspaceSession::adopt requires an AttachZone request containing exact hashes,
locks the saved source, checks project ownership and retains that lock until Save
or session drop. Save publishes the head only after validating all unique historical
references (256 MiB actual-byte read budget, at most 4 MiB per unique archive;
each read is bounded by the remaining budget before allocation and replay).
The host checks ownership after acquiring a legacy lock; recovery does likewise.
Retained history protects adopted identities even after Undo. Dropping an unsaved
adoption leaves an unreferenced object and the old authority unchanged. UI wiring
must precede exposing this workflow to Studio users.

Managed editing reuses ZoneEditSession. preview_zone_edit returns workspace and
zone receipts without writing. apply_zone_edit requires the exact reviewed
workspace request plus the original zone command, recomputes the result and
publishes its immutable archive before updating the in-memory reference. Save
publishes the workspace head explicitly. Workspace Undo restores the previous
archive (and its zone history); ordinary zone Undo can also pass through this
same adapter. Direct UpdateZone execution is rejected by the file service.
