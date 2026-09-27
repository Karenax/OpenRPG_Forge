# R1-040 — Raccord environnement en cours, non qualifié

2026-09-06, Rust, travail local non publié. **Gate 1 et R1-040 ouverts.**

Les sections initiales ci-dessous conservent le diagnostic historique. Le raccord de sonde et
les deux démarrages natifs décrits dans l'addendum final remplacent l'état « non exposé » initial.

## État implémenté

Ressource optionnelle `EnvironmentQualityRequest` : validation d'identité/révision et de politique,
hash immuable, tier demandé, observation CPU distincte. Le plugin environnement conserve les
paramètres de caméra et l'atmosphère préparés pour éviter de recréer les sources ou le medium.
Sans ressource, aucun override : le Runtime et le Studio ne l'insèrent pas encore.
L'adaptateur expérimental applique les tailles 64/128/256/512 de la politique aux composants CPU
avant cache, et comporte un chemin de suspension/restauration de l'atmosphère conservant HDR,
brouillard de distance, soleil et fallback ambiant. Ce dernier chemin n'est pas encore qualifié.
Le ledger optionnel conserve `full_preset_applied=false` pour tous les tiers.

## Limite concrète trouvée dans Bevy 0.19.1

Source locale `bevy_pbr-0.19.1/src/atmosphere/environment.rs`, fonction
`prepare_atmosphere_probe_components` : la texture n'est créée que pour les entités dépourvues
du composant privé `AtmosphereEnvironmentMap`. Modifier uniquement `AtmosphereEnvironmentMapLight.size`
ne redimensionne donc pas le cache. Le module `environment` est privé dans `atmosphere/mod.rs` :
la suppression directe de ce type n'est pas une API publique disponible pour Forge.

La tentative d'accès direct a été écartée. Le raccord actuel refuse une demande incompatible
avec un `GeneratedEnvironmentMapLight` déjà présent et émet `environment_quality_cache_blocked`.
Il garde la projection précédente plutôt que déclarer un budget appliqué sur un cache inchangé.
Le champ `sky_budget_projected` atteste uniquement les composants CPU, jamais la taille GPU.
Cette protection ne constitue pas une solution finale de qualité dynamique.

## Vérification réalisée

`cargo test -p forge_engine --locked -q` : **131 PASS**. Le nouveau test parcourt les quatre
tiers sans renderer GPU, conserve hash d'environnement, soleil/pose, entité atmosphère et handle
du medium, puis simule un cache généré et vérifie qu'une demande Low laisse 256 en place et
ne déclare pas le budget CPU Low appliqué. Politique de révision étrangère refusée.
Clippy moteur tous targets/features, check moteur WebAssembly, formatage et diff : PASS après
extraction mécanique des helpers de télémétrie. Aucun lint désactivé pour masquer les échecs rencontrés.
Pas de nouvelle capture ni benchmark : raccord non exposé et cycle GPU encore incomplet.
Les 366 tests workspace du lot eau restent une preuve historique, pas une exécution de cette révision.

SHA-256 `environment/quality.rs` : `28bbd06d13cc9f9ee7941b5ebfa6dbca61db78bb5060515ecdbe30b18d5554dc`.
SHA-256 `environment/quality/tests.rs` : `8299b0a54a6747b34d6c2381e8ae83922c00eabc5a4715378a908bf58da6ac40`.

## Prochain travail nécessaire

Gérer le cache via un objet de sonde dérivé possédé par l'adaptateur, renouvelable sans remplacer
la caméra ni les sources ; transférer son éclairage généré vers la caméra par les composants publics
et observer sa disponibilité réelle. Tester répétitions, désactivation/restauration et topologie
invalide, puis exposer l'option Runtime et inspecter le natif. Cette conception reste à implémenter
et vérifier ; elle n'est pas déclarée fonctionnelle ici. Ne pas forker Bevy ni accéder à ses types
privés via des identifiants de composants calculés. La volumétrie reste un fallback non qualifié.
CI **NOT RUN** ; aucun commit/push, aucune dépendance ou asset Cadle ajouté.

## Addendum — sonde dédiée et démarrages natifs Low/Ultra

Le cycle de vie est désormais implémenté par `quality/probe.rs`, sans accès aux types privés
de Bevy. Dès le démarrage avec une demande de qualité, le cache est porté par une entité dérivée
`OwnedSkyProbe`, pas par la caméra. Un changement de taille renouvelle cette sonde et retire le
handle ancien de la caméra pendant la reconstruction. Une même demande ne recrée rien. La caméra,
le soleil, la source et le medium atmosphérique restent en place. L'activation tardive sur une caméra
historique portant son cache reste refusée explicitement : cette caméra n'est pas remplacée.

Après les systèmes Update de préparation Bevy, `publish` en PostUpdate attend une image existante
de largeur/hauteur demandées et six faces, puis copie son `GeneratedEnvironmentMapLight` sur la
caméra unique active. Le relevé suit dans une chaîne ordonnée. `generated_texture_bound` atteste
ce binding et ces dimensions en CPU, pas la fin d'une passe GPU. Aucune promesse de transition
sans variation temporaire de luminosité : le fallback ambiant subsiste pendant l'attente.

`--environment-quality low|medium|high|ultra` est maintenant exposé dans les deux modes vallée,
avec identité liée à la révision de zone. Sans option, comportement historique inchangé.
Le test CLI couvre quatre tiers, deux modes, omission, doublon, argument absent et invalide.
Le test du gestionnaire de sonde utilise des images synthétiques : mauvaise taille refusée,
bonne taille raccordée, sonde identique réutilisée, ancienne sonde supprimée au redimensionnement,
nouveau handle raccordé, topologie ambiguë refusée et suppression sur désactivation.
Ce test est CPU, pas un changement à chaud prouvé sur GPU.

Artifacts : `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-environment-quality-20260906`.
Binaire capturé : `007dc980e231804c422e73d65f666544b81ec3897911f0884dcb5194c9997b68`.
RX 7800 XT/Vulkan, 1920×1080, WorldSketch rev3, caméra human_bridge, météo clear, kit et preview
du protocole `R1_040_WATER_SSR_BUDGETS.md`. Deux lancements successifs sans compilation concurrente,
`--environment-quality low` puis `ultra`, avec `--capture-frame 1 --metrics-warmup-frames 600
--exit-after-frames 1020`, fichiers capture/metrics séparés. Aucun autre override de qualité.
Codes 0, zéro ERROR, captures inspectées : ciel, relief et pont présents, éclairage proche entre
les deux vues ; végétation tramée, géométrie stylisée et eau granuleuse toujours visibles.
Low observe 64², Ultra 512² et `generated_texture_bound=true`, même hash environnement
`845ae5b80b6cb1ef2390b0cc79efb121a5b8ffbd2e911f67f7a5b5ce38f00f0f`.
Capture Low émise/readback 65/68, Ultra 74/77 ; 90 pipelines prêts et huit observations stables.

| Tier | FPS moyens | p95 ms | 1 % low FPS | >16,7 ms |
| --- | ---: | ---: | ---: | ---: |
| Low | 104,48 | 10,3779 | 85,60 | 0 |
| Ultra | 78,13 | 13,5491 | 65,19 | 1 |

420 échantillons, 4,02/5,38 s : smoke court, pas benchmark de gate. Le coût Ultra reste visible,
pas présenté comme une optimisation universelle. Le binaire précède seulement les ajouts d'aide CLI
et de test CLI, pas une modification du rendu capturé.

SHA-256 :

- `quality.rs` : `e4e7d1cd15d54bd725522fec0df99c77e0b45f039e85f640e2383f7baa8d17be` ;
- `probe.rs` : `e154d1235a3fc8ba43335107ca094532df401791ec0876e01088deef892e37cd` ;
- test sonde : `2bde11eccb83d34305454f994e34d88448c745d3da288d0d64550b3b9c1c8069` ;
- Low PNG : `317de84c3aebfeab5176782b5c488e7af9eed1e479e3a95def60bef8e837972d` ;
- Low JSON : `544ca7c63eab4f75339bbc699334e22aa272403385d23546148b716d56e2436c` ;
- Ultra PNG : `e9a217e0d5970891250037da9361bb22dbbb033e806a50fa1aece9e14593e790` ;
- Ultra JSON : `68a9b9f568f6cd3abe0a58ebae4c9c5ac37bb9dc9c5b8dfc65550ca6a4db01c8`.

Restent : cycle GPU à chaud, suspension/restauration complète d'atmosphère en natif, Studio,
volumétrie ou fallback accepté, profil global et qualification longue/humaine. Aucun passage de gate.

Validation finale de ce lot : `cargo test --workspace --locked -q` **370 PASS** ;
`cargo clippy --workspace --all-targets --all-features --locked -- -D warnings` PASS ;
`cargo check -p forge_engine --no-default-features --features web --target wasm32-unknown-unknown --locked` PASS.
Target isolé habituel, incremental=0, `RUSTFLAGS=-C debuginfo=0`. Formatage et diff vérifiés.

### Complément ciblé — suspension/restauration ECS

Un preset Low synthétique valide désactive l'atmosphère (résolution 0) ; High la réactive.
Trois cycles complets du plugin vérifient maintenant la suppression de chaque ancienne sonde
et du binding d'éclairage généré, le retrait/restauration des composants atmosphère, et la
conservation de HDR, lumière ambiante, brouillard de distance, exposition et pose du soleil.
L'entité de planète, son GlobalTransform et son handle de medium sont identiques après restauration ;
une seule ressource de medium et une seule sonde subsistent à chaque retour High. Hash source
inchangé et budget CPU observé cohérent. La mise en place de caméra/soleil est partagée entre tests.

`cargo test -p forge_engine environment:: --locked -q` : **9 PASS** ; Clippy moteur tous
targets/features, formatage et diff PASS. Aucune modification de production pour ce complément,
donc pas de nouvelle capture ni suite workspace répétée. Les 370 tests précédents restent datés
du lot précédent. SHA-256 tests qualité :
`50572ee76bd35f6c4f1f131c5918fba33189fedc504ddf24dddcb9c43591d9b3`.
Le cycle natif GPU reste à prouver ; ces contrôles ECS ne le remplacent pas.

Mise à jour ultérieure : le cycle natif a depuis révélé puis permis de corriger une seconde
allocation de filtrage sur la caméra. Le raccord actuel copie les textures déjà filtrées de la
sonde, pas son générateur. Cinq captures de cycle passent après correction ; voir
`R1_040_ENVIRONMENT_NATIVE_CYCLE.md`. Les sections ci-dessus conservent les étapes historiques.

## Caméra auxiliaire inactive — 2026-09-06

L'initialisation de l'environnement choisissait déjà l'unique caméra active, mais l'application
du tier, son observation et la publication de la texture de ciel exigeaient une caméra totale.
Ces trois chemins utilisent maintenant l'unicité active. Une caméra auxiliaire inactive ne
bloque plus le cycle ; deux caméras actives restent ambiguës et ne sont pas acquittées.
Les composants de l'auxiliaire ne reçoivent ni ciel ni texture par cette projection.

Deux tests ECS ajoutés : High/Low/Ultra avec une caméra inactive, source inchangée et
profil complet toujours faux ; activation de l'auxiliaire bloquant le changement, reprise
après désactivation ; texture synthétique conforme publiée seulement sur la caméra visée,
refus et restauration lors du passage à deux puis une caméra active.
17 tests environnement PASS, Clippy strict workspace/all-targets/all-features PASS.
Ces tests ne prouvent pas une texture réellement rendue ni une interface multi-vue Tauri.

La première suite workspace après copie du cache vers E: a échoué sur quatre tests CLI :
les anciens exécutables de tests incorporaient encore le chemin CARGO_BIN_EXE de C:.
Les assertions métier n'étaient pas atteintes (OS code 3). Reconstruction ciblée puis
invalidation des seuls artefacts forge_cli/forge_runtime dans le cache copié sur E:
269 fichiers compilés, 887,1 Mio reconstruisibles ; aucun source/asset/preuve supprimé.

Rejeu `cargo test --workspace --all-features --locked` : **453 PASS / 1 timing ignoré**,
dont les quatre tests CLI restaurés, sortie 0 ; moteur 188 PASS / 1 ignoré en 42,14 s.
Check Studio WASM/web PASS (18,26 s), avertissement connu `proc-macro-error2` inchangé.
Formatage/diff check PASS. Pas de nouvelle capture ni qualification visuelle multi-caméra.
Tous les processus terminés. Contrat de fallbacks toujours non approuvé, adaptation non
activée, Gate 1 ouverte et CI NOT RUN ; aucune publication Git.
