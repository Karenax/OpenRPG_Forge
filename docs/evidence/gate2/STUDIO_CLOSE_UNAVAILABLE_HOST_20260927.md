# Studio : fermeture sans hôte d'édition disponible

Date : 27 septembre 2026. Correction demandée par le propriétaire ; aucune
publication Git ni qualification de gate. Changements préexistants conservés.

## Incident constaté

Le candidat `output/studio-gold-20260924/candidate/forge-studio-candidate.exe`
affichait le dialogue « Quitter le Studio ? » et l'erreur
`Set FORGE_AUTHORING_ROOT to an existing authorized directory before launching Studio`.
Le bouton de sauvegarde était désactivé. Le chemin natif `finish_window_close`
initialisait systématiquement `AuthoringHost`, y compris après le choix explicite
de quitter sans enregistrer ; l'erreur d'initialisation bloquait donc la fermeture.
Le dialogue assimilait en outre l'absence d'état à des retouches déjà enregistrées.

La fenêtre a été identifiée avec Computer Use (HWND retourné 199764), puis le
processus avec son chemin et sa date de création. L'essai d'action UI n'a pas
abouti : l'outil demandait une nouvelle observation malgré l'arbre accessible.
L'instance bloquée PID 26008, créée à 11:04:21 heure locale, a été arrêtée après
revalidation de ces identifiants. Absence du processus et de toute fenêtre Studio
confirmée ensuite. Aucun fichier supprimé ; aucune carte initialisée dans cette
session dont la racine d'édition manquait. Le lanceur web et les autres applications
n'ont pas été arrêtés.

L'exécutable précédent, son frontend et ses manifestes sont conservés dans
`output/studio-close-fix-20260927/before/` avant remplacement du candidat.
SHA-256 de l'exécutable précédent :
`F4140A39756568A1DC4D4DC8B193A6684A705462EB610C7A07031EB3E695749C`.

## Correction et contrôles

`finish_window_close` consulte l'hôte déjà initialisé sans le créer pour quitter.
Un état absent, en erreur ou empoisonné exige l'abandon explicite ; un état valide
conserve son verrou jusqu'à la destruction des fenêtres. Une carte modifiée ne
peut toujours pas être fermée avec `discard_unsaved=false`. Une erreur de fermeture
est rendue telle quelle, sans nouvelle tentative implicite. `WindowClosed` est une
réponse explicite ; aucun faux état auteur n'est construit. Le dialogue indique
l'état indisponible au lieu de prétendre que les retouches sont enregistrées et
refuse toute réponse inattendue.

Tests locaux, cache et fichiers temporaires sur E: :

- `cargo test -p forge_studio --bin forge-studio --lib --locked --offline` :
  bibliothèque 116 PASS et 2 IGNORÉS ; binaire 12 PASS et 1 FAIL.
  Les cinq tests de fermeture passent, dont quatre ajoutés par ce correctif.
- L'échec élargi est `gate0_capability_grants_no_privileged_tauri_command` : le
  test exige exactement deux permissions alors que la bibliothèque d'assets
  préexistante en déclare trois (`allow-read-asset-library` ajoutée). Ni les
  permissions ni ce test ne sont modifiés pour contourner l'échec. La suite
  entière n'est pas déclarée verte.
- `rustfmt --check --edition 2024 --config skip_children=true` sur les trois
  fichiers touchés et `git diff --check` : PASS. Les retouches assets existantes
  sont préservées ; seuls leurs retours à la ligne/ordre des modules ont aussi
  été normalisés par rustfmt dans ces fichiers.
- `cargo clippy -p forge_studio --bin forge-studio --lib --locked --offline -- -D warnings` :
  bloqué dans la dépendance `forge_project`, `zone_authoring/session.rs:349-350`,
  sur deux `needless_pass_by_value` hors du correctif. Aucun lint désactivé,
  aucun verdict Clippy vert attribué à ce lancement.

La reconstruction complète via `scripts/Refresh-StudioGoldCandidate.ps1` a été
essayée et a échoué avant toute copie du frontend : erreurs préexistantes E0716
et E0596 dans `crates/forge_engine/src/tidewater_plants/motion.rs:47-52`, chantier
Tidewater extérieur à cette correction. Ce fichier n'a pas été modifié.

Le frontend précédent reste intact, SHA-256 WASM :
`BB5EE51ED7336076133205F550E2FA6C5DC6F8AC2210F49520B2EF8E66C840AA`, identique à la
copie de récupération. Reconstruction native seule réussie avec ce frontend
identifié : le changement de message du dialogue n'est donc pas encore déployé.
La nouvelle réponse `WindowClosed` ne concerne que la destruction réussie de la
fenêtre ; les refus natifs gardent le contrat `Rejected` existant.

## Candidat installé et preuve de fermeture réelle

Compilation native réussie avec les mêmes paramètres de cache que le script de
rafraîchissement (`CARGO_INCREMENTAL=0`, `RUSTFLAGS=-C debuginfo=0`, hors ligne) :

```powershell
# Depuis apps/forge_studio
cargo tauri build --debug --no-bundle --config E:/OpenRPG_Forge/OpenRPG_Forge-Rust/output/studio-gold-20260924/candidate/tauri-candidate.conf.json -- --locked
```

`E:/OpenRPG_Forge/.build/cargo-target/debug/forge-studio.exe` a remplacé le seul
candidat préalablement fermé. SHA-256 installé :
`1B50A7A006882553D574437175BEFA67E0F1E01E46A1648244F0FB5BF2C50795`.
Le manifeste du candidat conserve les pins du frontend et déclare explicitement
`NATIVE_CLOSE_FIX_WITH_PRESERVED_FRONTEND`, pas un nouveau frontend apparié.

Parcours Computer Use exécuté sur cet exécutable :

1. Lancement PID 4676 à 11:35:26, sans `FORGE_AUTHORING_ROOT` dans l'environnement
   de ce seul processus ; aucune variable utilisateur persistante modifiée.
2. Observation de « Aucune zone ouverte » et de l'erreur de racine manquante.
   Fenêtre retournée 2755272, activée puis capturée avec son arbre accessible.
3. Clic sur la croix native : dialogue de confirmation affiché et inspecté.
4. Clic sur « Quitter sans enregistrer » : plus aucune fenêtre Studio dans
   l'inventaire ; PID 4676 absent au contrôle de processus. Aucun arrêt forcé
   utilisé pour ce candidat corrigé.

**Fermeture normale dans le cas bloquant : PASS.** Cinq tests unitaires de fermeture
PASS. Studio laissé fermé. Le serveur du lanceur web, PID 21712 sur le port 8848,
reste à l'écoute ; les autres applications sont préservées.

Les blocages de compilation complète, de test ACL et de Clippy ci-dessus restent
visibles. Aucune qualification globale Studio, graphique du monde ou de gate,
aucun commit/push. Le message honnête d'état indisponible reste un correctif source
en attente d'une reconstruction frontend après résolution du chantier Tidewater.
