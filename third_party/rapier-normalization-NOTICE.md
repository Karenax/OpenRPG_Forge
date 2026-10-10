# Adoption du correctif de contact — 10 octobre 2026

Le fork est reconstruit depuis l archive du registre dont le SHA-256 est `d9961dfc6cabf508c2db7bc7a73b9d82800de898ccaed0bbaab58290c9620fc0` : 217 fichiers packages verifies, puis une seule correction fonctionnelle dans `compute_hit_info`. La version et les transitives restent inchangees. Les preuves locales et les limites de qualification sont consignees dans `docs/evidence/gate3/GATE3_CONSOLIDATION_20261010.md`. La note historique ci-dessous conserve son statut initial ; ses mentions de tests en attente et de primaire non modifie ne constituent pas le bilan actuel.

# Rapier character contact normalization — isolated candidate

Registry package: `rapier3d 0.35.0-glamx0.2`, Apache-2.0.
The exact registry archive checksum is checked against the baseline Cargo.lock;
217 packaged files are verified before copying. LICENSE is retained from the
upstream commit recorded by the package. The package VCS metadata says dirty;
this is not presented as an exact clean checkout of that commit.

One source change is proposed in `compute_hit_info`: normalize `normal1` with
`try_normalize`, preserving the original value for a degenerate normal. A nearly
unit vertical normal previously left a negative tangent residual from gravity;
the nonslip branch then removed all horizontal movement. This is reproduced by
an actual native KCC sweep over a scaled floor with 0.1 mm clearance, matching
the V11 public failure's normal and translation. No map or building identity is
used by the correction, and no movement threshold is relaxed.

Root evidence: `floor-contact-reproduction-preparation-001` in the Gate 3 evidence
directory. Native player regression tests are pending when this notice is added.
WASM, candidate packaging, public RPG replay, broader physics/navigation checks,
size/time effects and whole Gate 3 acceptance remain unqualified.

This private candidate is not applied to Primary. The project would maintain
this narrow patch until an equivalent upstream fix is adopted and requalified.
The existing Bevy/Rapier integration and transitive dependencies are retained.

Upstream: https://github.com/dimforge/rapier
Security page checked 2026-10-03: https://github.com/dimforge/rapier/security
No published advisory is listed there. This is not an audit of all transitives.

Integrated rehearsal hygiene: two trailing-space suffixes in upstream
rigid_body.rs documentation comments (lines 1724 and 1742) are removed.
The functional fork patch remains only the contact-normal normalization.
The original bytes and whitespace-check failure are archived in
integrated-primary-rehearsal-preparation-001; no license text is modified.
