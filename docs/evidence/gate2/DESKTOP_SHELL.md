# Gate 2 — Bureau Forge, coque commune

Date : 4 septembre 2026. Branche : `Rust`.

## Candidat et portée

Code initial : `d1e97c0c22e30e479891d8663a2ee43828ead83e`.
Code corrigé qualifié pour cette tranche : `0b3b9efe9f8c3b018611b57b224f5aff484649a3`.
**Coque fonctionnelle vérifiée localement**. Ce document ne ferme aucune route Gate 2.
La gate reste à **1/43**, Gate 1 à **43/55** sous ADR-0011. CI distante `NOT RUN — infrastructure billing`
sous ADR-0006 ; aucun audit PresentMon relancé.

Le bureau clair/doré reprend le langage graphique demandé par le propriétaire : verre translucide,
fenêtres arrondies, ombres douces, menus supérieurs et dock. Fond CSS et glyphes vectoriels originaux,
sans téléchargement d'assets du site de référence, sans marque Apple et sans asset Cadle.

- Projets, ses champs et ses projections restent montés dans une fenêtre unique.
- Déplacement, redimensionnement, réduction, agrandissement/restauration et masquage sont des états UI.
- Les menus utilisent les mêmes commandes Rust que les boutons ; aucune deuxième session canonique.
- Diagnostics monte une seule fois la scène de preuve Gate 0, explicitement indépendante du projet.
  `visibility:hidden` et `inert` conservent ses dimensions et son canvas lors du masquage.
- L'initialisation asynchrone Bevy embarquée ne réclame pas le focus clavier. Le contrôleur caméra
  interactif futur n'est pas simulé.
- Les cinq applications futures du dock sont marquées indisponibles.

## Vérifications automatisées acquises

Répertoire local ignoré : `artifacts/gate-2/desktop-shell-2026-09-04/`.

| Contrôle | Résultat |
|---|---|
| `cargo test --workspace --all-targets --locked --offline -- --quiet` | 422 tests PASS, dont six nouveaux tests de géométrie/transition |
| `cargo clippy --workspace --all-targets --locked --offline -- -D warnings` | PASS |
| clippy `forge_studio` WASM, `--no-default-features --features web` | PASS |
| `cargo fmt --all`, `git diff --check` | PASS |
| Paquet humain Gate 1 inchangé | SHA-256 `6d1219d19748f9e90d49e6431e17f160e65c90e7483b4d38a388e801ad754ac8` |

Le warning future-incompatibility de `proc-macro-error2 2.0.1` préexiste ; aucun warning projet ignoré.
Les tests couvrent l'état de présentation, pas le rendu navigateur ou la persistance native.

## Première acquisition réelle — code d1e97c0

Trunk Release et Studio autonome `tauri/custom-protocol` PASS. Exécutable initial de 33 437 184 octets,
SHA-256 `5119d10c0851de643d0b4ced6cf42bcb8856fca470eeda06089f6c5592ad38af`.

Dans le navigateur intégré en 1280 × 720 : formulaire « Brouillon conservé » intact après masquage,
réouverture, réduction/restauration et passage dans Diagnostics. Déplacement clavier +16 px et
redimensionnement -16 px observés dans le ledger ; agrandissement 1280 × 576 puis retour exact
1044 × 504 à (126,36). Masquage ajoute réellement `inert=""`. Une seule instance canvas ; son
`backendNodeId=315` est retrouvé par une nouvelle requête DOM après réouverture. Dimensions masquées
689 × 324, compteur de démarrage **1**. Rendu WebGPU capturé et inspecté, aucune erreur/warning console.
Sans Tauri, menus de fichiers et mutations désactivés, sans projet fictif.

Dans le Studio natif (client 1600 × 900) : rendu initial inspecté, fenêtre déplacée à la souris
sans perte des trois noms, création réelle, menu Fichier → Enregistrer sous, sauvegarde puis sortie
native propre observée. Captures `01-native-desktop.png`, `02-native-created.png`, `03-native-saved.png`.
Projet `Atelier doré de preuve`, module `Chapitre doré`, zone `Clairière du bureau`, seed 1,
spawn (0,2,0), révision 1. Snapshot 1 771 octets :

`snapshots/9d72c00d-02a6-50c2-87b3-411787233851-r1.forge.json`

SHA-256 `44a59f46476cfbf5d2a1523790512c6f35adcdabf2f6072f08324f9cf23e747b`.
Ce hash porte sur les octets du fichier, pas sur le reçu canonique.

**Défaut observé et conservé :** Ctrl S n'ouvrait pas le champ après création lorsque le bouton
devenu désactivé avait laissé le focus sur le document. Le menu fonctionnait. Le correctif 0b3b9ef
attache le raccourci à la fenêtre navigateur (toujours avec isolation des champs) et remplace le
libellé fixe « 1920 × 1080 » dans Diagnostics embarqué par « Dimensions adaptatives ».
Sa reconstruction et la vérification après redémarrage ont ensuite été réalisées ci-dessous.

## Réacquisition corrigée — code 0b3b9ef

- Trunk Release et Studio autonome reconstruits avec `FORGE_SOURCE_COMMIT` au commit exact ci-dessus,
  `--locked --offline`, puis lancement du nouvel exécutable.
- Ctrl S testé immédiatement après démarrage natif, sans champ préalablement choisi : focus réel et
  défilement vers la destination de sauvegarde observés (`04-native-shortcut-fixed.png`).
- Réouverture explicite du snapshot de la première session après arrêt complet et redémarrage :
  trois noms, identifiants, révision 1, seed 1 et spawn (0,2,0) identiques
  (`05-native-reopened.png`). Son hash avant/après est inchangé. Aucune restauration automatique
  n'est prétendue ; le fichier a été choisi dans le formulaire.
- Diagnostics WebGPU natif lancé dans le bureau avec le projet toujours ouvert. Cube et sol rendus,
  état WebGPU prêt, parité des sources et caméra 1/1 ; réduction/restauration conserve le compteur
  « Monde initialisé 1 fois » (`06-native-diagnostics.png`, `07-native-diagnostics-restored.png`).
- Navigateur : rendu inspecté en 1100 × 700 (taille minimale), puis passage en 1920 × 1080. Champs
  conservés après masquage ; Ctrl S depuis le menu place le focus sur `workspace-save-directory`.
  Diagnostics agrandi à 1920 × 936, réduit, restauré puis désagrandi : même identité DOM canvas
  (nouvelle acquisition, `backendNodeId=315`), un canvas et un démarrage, WebGPU prêt. Retour à une
  surface 689 × 304 après restauration ; libellé « Dimensions adaptatives » confirmé.
- Aucun warning/erreur console observé sur ce parcours navigateur. La persistance est prouvée par le
  parcours natif, pas par un faux backend navigateur. Aucun fichier de projet n'est supprimé.
  Le journal du serveur HTTP local signale toutefois un `favicon.ico` absent (404) lors de la première
  ouverture ; il ne s'agit pas d'un bundle manquant ni d'une preuve de réseau entièrement sans échec.
- Smoke des routes explicites sur le même bundle corrigé : `?forge_scene=gate0` conserve le mode
  `gate0_scene_proof`, sans bureau, avec un canvas et WebGPU prêt. `?forge_scene=visual-valley`
  conserve le mode `gate1_visual_valley`, sans bureau, avec un canvas, rendu inspecté en Full HD,
  hashes alignés, caméra 1/1, eau/météo/décor observés et aucune alerte ni erreur/warning console.
  Ce smoke ne remplace aucune qualification Gate 1.

Ces observations ne sont ni une acceptation visuelle humaine du propriétaire, ni un benchmark.
Le collecteur hérité relève dans la première acquisition navigateur 120 frames : moyenne 16,7217 ms,
p95 27,5 ms, 1 % low 32,6264 FPS. Ce sont des temps de boucle WASM, dans un contexte de validation,
pas des timestamps GPU, ni un rejeu gouverné ni une fermeture des réserves Gate 1. Le chargement
différé inclut le temps passé dans Projets dans les horodatages relatifs à la navigation.

### Artefacts locaux liés

Les sept PNG restent dans le répertoire ignoré mentionné plus haut. Les trois premiers correspondent
à d1e97c0, les quatre derniers à 0b3b9ef. Tous ont été inspectés pendant le parcours.

| Fichier | SHA-256 |
|---|---|
| `01-native-desktop.png` | `890cf247f8c2972b61c8affe6bb2f0cdc269e408179f653ad7e72e69d886a6dc` |
| `02-native-created.png` | `a9b3bae034391ae2185d9c0e092cbd56a5ad3cb35826a365725ad1ed3993570e` |
| `03-native-saved.png` | `28ef470c510aadb115c58c344e3227e9773ca8813490004422f31d5cc502d2f6` |
| `04-native-shortcut-fixed.png` | `91fbf45ac1e488993d9df6c69ca6b47f55520ad9f13e13d3bfd54c997a8f0290` |
| `05-native-reopened.png` | `7b1bc3a3940eecbec8bcfd72fcd3782686594549040428c5851d03d4864332bf` |
| `06-native-diagnostics.png` | `45bd4c9d0b4f0cc8b4a340e7b830123ec8a0faee70e87ccb2104aefb33aa6baa` |
| `07-native-diagnostics-restored.png` | `1673c8f1d1b4194d4d25fb4942eb5146ded460cadda58a39ee41840317bb82de` |
| `workspace-tests.txt` | `faf47189d2319b15565df7144bc100140605189a39d1ba50b945a08669606f27` |
| `clippy-native.txt` | `94c82f4d9f84741fd6dbb2d4c4a586965e48cc7f59da31ffbeef3ae8f093c2e4` |
| `clippy-wasm.txt` | `7ab23a1ad18909c2585779e99542c859be7e7495e8d3ff6bf4e613ecce57d61e` |
| `trunk-release-corrected.txt` | `b9808d6fc2e6d121ce0966e802487c395eacdc4d1cc7b69835380d53dc01151a` |
| `studio-release-corrected.txt` | `becf5be06a137597ffd72fbb0337db245e4689cdbace30eb4c6f118f0bcea286` |

| Binaire/bundle corrigé | Octets | SHA-256 |
|---|---:|---|
| `target/release/forge-studio.exe` | 33 437 696 | `aec4b0869221cd0a82bd74d97122388281abbecf0b6e1a9770e51763c7881ba5` |
| `apps/forge_studio/dist/forge_studio_bg.wasm` | 39 841 118 | `2ef5bc89befee05cafa6258dcebbb3b3c28381616629747ad505b97504f0ade9` |
| `apps/forge_studio/dist/forge_studio.js` | 187 769 | `4cd733dc530e64080e16008704c2507b53c1004eaa33c573f5826243ebca4394` |
| `apps/forge_studio/dist/desktop.css` | 21 911 | `10c38316245c120fe679e20bdd6de6d15e1ee96dc859b4f574c217c7ddcd4dad` |

## Limites conservées

Les zones créées ne sont pas encore projetées en 3D. La scène Diagnostics n'est pas leur aperçu.
Le bus général, les brosses, les onglets de zones et les applications de gates ultérieures restent dus.
Les fenêtres et formulaires sont conservés pendant la page courante, pas après rechargement/crash.
Le garde natif protège les documents acceptés non enregistrés, pas le texte de formulaire non soumis.
Masquer Diagnostics ne suspend pas son coût GPU. Aucune nouvelle qualification de performance,
de récupération après crash, d'accessibilité complète ou de système de fenêtrage OS n'est revendiquée.
