# Gate 1 — Préparation d'une vue fixe sans simulation

8 septembre 2026, branche Rust, sans publication. Gate 1 ouverte.

## Défaut réel et cause

Voir `R1_WATER_SHORE_OVERLAP.md`, export rapproché Web du 8 septembre : canvas
correctement rendu mais cover persistant, kit 20/20 et zéro collider enregistré.
La vue fixe suspend volontairement `Time<Virtual>` dès Startup. Le garde Studio
attendait `GovernedAssetKitReadiness.ready`, qui inclut les handles Rapier créés
par la simulation. Le PNG seul ne démontrait donc pas un chargement fixe réussi.

## Correctif candidat

Extraction de `prepared_for_static_view` dans la readiness du kit : mêmes contrôles
d'instances, dépendances, trois LODs, nœuds de collision et formes matérialisées
sans échec ; seule l'inscription des handles backend n'est pas demandée pour
une image figée. Le playtest réutilise ce contrat ET exige toujours ces handles.
Pas de suppression de la physique ni de mouvement implicite de l'horloge.

Le garde Studio utilise cette condition uniquement si la vue fixe est installée,
sans erreur, correspond à la caméra observée ET si le temps virtuel est en pause.
Sinon, readiness jouable inchangée. Huit observations stables du cache et le
timeout de 120 secondes restent obligatoires. Aucun verdict GPU ou humain ajouté.

Test de readiness existant étendu : zéro handle autorise la préparation statique
mais refuse le playtest ; forme manquante ou collision en échec refusée. La
qualification UI exige encore un nouveau WASM, lancement réel et capture de la
disparition du cover ; les anciennes preuves ne qualifient pas ce nouveau code.

Racine de validation locale :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-fixed-warmup-20260908`.
`Verify.ps1` journalise fmt, tests workspace, Clippy strict et check Studio WASM.
Test ciblé PASS ; suite workspace : 637 PASS, un timing préexistant ignoré.
Premier Clippy refusé pour annotation `must_use` manquante sur la méthode
publique. Annotation ajoutée sans changement de comportement ; `CheckFinal.ps1`
rejoue fmt PASS, Clippy strict PASS (30,38 s), check WASM PASS (31,93 s).
Warning futur préexistant `proc-macro-error2 2.0.1` conservé. Pas de dépendance
ajoutée, pas de nouveau verdict supply-chain ou CI distante (NOT RUN).

`BuildStudio.ps1` lancé ensuite, build et replay UI terminés (résultats ci-dessous).
Il réutilise exactement la caméra surélevée du contrôle de rive précédent et le
kit corrigé ; cette caméra ne devient pas une preuve de hauteur humaine malgré
son identifiant historique. Aucune condition de gate cochée.

## Rejeu Studio terminé — 18:35 UTC

Compilation Rust release 3 min 18 s, optimisation WASM et assemblage Tauri
terminés avec code 0. WASM SHA-256
`6AFDDF04C781DB5679CF2DFBF1460DF9FCD8FCD1719DCFC44DA92F484EC19A81` ;
EXE `4E85FD7CFE7EC18B3E097FA0E568DE80C2135FE17ED510188CC8B14B1DFA500E`.

Lancement réel PID 43348 : cover initial observé puis disparition automatique,
sans clic sur le pas d'animation. Vue du pont et de la rive colorée, carte et
caméra candidates inchangées, temps virtuel 0,000 s. Export JSON : warmup
`prepared` en 20 561,9 ms, 89 pipelines, 0 attente/erreur, 8 observations stables
sur 12, pic de 19 attentes shader transitoires. Kit 20/20, forêt 1 662 arbres.
Zéro collider backend dans cette vue figée reste signalé : aucun playtest qualifié.

Exports locaux et capture de fenêtre inspectés, hashes SHA-256 :

- `prepared-ui.jpg` : `621CA2EE186E8E9DD8107A1CD8DDABCA40E815DC809B5225D272CA3A7F0596B5` ;
- `studio-diagnostic.json` : `CE24B7E7A51EC982E4257BC8CC5FD02990ABDAA7B6ADD2AC1DB05081F3B32BD2` ;
- `studio-viewport.png` : `A49994079AE4D2E85D79538C30D020D8BA54954AE80EF22A9EACF3CCE4B1D506`.

Sources Downloads conservées : `forge-diagnostic-1788892538574.json` et
`forge-viewport-1788892548046.png`. PNG 1408×966 : raccord de rive conservé,
vagues régulières/crénelures restantes. Aucun changement artistique dans ce lot.
120 frames initiales : moyenne 48,48 ms, p95 53,90 ms ; temps figé et Studio
propriétaire concurrent, pas un benchmark. Couple PNG/JSON non atomique.

Session fermée normalement ; PIDs 43348 (Studio), 19008 (optimiseur) et 45216
(Trunk) absents après contrôle. Seul le Studio propriétaire 39624 est préservé.
Le défaut de cover est corrigé sur ce scénario ; ni qualité artistique globale,
ni cible matérielle, ni Gate 1 acceptées. Pas de publication Git.
