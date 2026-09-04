# R2-001 — Assistant de projet et snapshots source

Date : 4 septembre 2026. Gate 2 active sous ADR-0011 ; Gate 1 reste 43/55.
Code candidat : `c165a30a76117045b2f6b33fe067828fda7f7965`, branche `Rust`.
Statut : **R2-001 PASS** — contrôles automatisés et parcours natif observé, capturé et inspecté.
Les autres routes Gate 2 restent ouvertes ; ce PASS concerne uniquement l'assistant de projet.

## Périmètre livré

L'entrée **Projets · Gate 2** du Studio ouvre `?forge_editor=project` : un assistant crée un
projet, son module initial et une zone extérieure. Le document canonique `forge.workspace` v1
contient les identités typées, noms, seed, spawn et un vrai terrain `forge.terrain` v2.
Le service Rust natif détient la session ; le frontend n'en reconstruit pas une autre source.

La zone source mesure 512 × 512 m, avec un échantillonnage de 2 m et trois couches matériau.
Le point de départ est une métadonnée validée, pas un joueur effectivement placé dans une scène.
Aucun viewport n'est monté sur ce parcours. Les liens vers Gate 0 et Visual Valley sont des
scènes de preuve séparées, jamais présentées comme l'aperçu du nouveau projet.

Une création est annulable/rétablissable (révisions 1 → 2 → 3), avec reçus et hashes de
transition. Sauvegarder écrit un snapshot immuable `{project-id}-r{revision}.forge.json` dans un
dossier local existant choisi explicitement. Un fichier identique est accepté sans réécriture ;
un fichier différent n'est jamais écrasé. Le document est écrit dans un temporaire exclusif,
synchronisé puis publié par lien physique sans remplacement. Un système de fichiers ne prenant
pas en charge cette opération renvoie une erreur explicite.

L'ouverture valide le suffixe, la taille, le JSON, les identités et la recette avant de modifier
la session. Chemins relatifs, segments `..`, liens et points de réanalyse sont refusés. La borne
de révision IPC est 2^53−1 ; le domaine conserve son `u64`. La seed traverse l'IPC sous forme
de chaîne, sans perte de précision. Aucune dépendance ni permission Tauri n'est ajoutée.

## Contrôles exécutés

Depuis la racine du worktree, avec le toolchain et le lockfile du dépôt :

```text
cargo test --workspace --all-targets --locked --offline -- --quiet
cargo clippy --workspace --all-targets --locked --offline -- -D warnings
cargo clippy -p forge_studio --target wasm32-unknown-unknown --no-default-features --features web --locked --offline -- -D warnings
cargo fmt --all -- --check
git diff --check
```

416 tests PASS, zéro échec ; clippy natif/WASM et formatage PASS. Avertissement Cargo existant
de compatibilité future : `proc-macro-error2 2.0.1`, sans nouvelle dépendance ni warning projet.
Les logs sont conservés localement sous `artifacts/gate-2/project-sources-2026-09-04/` (ignoré).

Les tests couvrent notamment round-trip/hash canonique, entrées invalides, seed `u64::MAX`, IDs
distincts, conflit sans écrasement, sauvegarde idempotente, révisions périmées, réouverture exacte,
refus d'abandon d'un brouillon, JSON corrompu/surdimensionné, temporaires orphelins conservés,
révisions IPC extrêmes et garde native de fermeture (session dirty, occupée ou empoisonnée).

Un premier test de chemin Windows échouait : `PathBuf::join("..")` sur un chemin verbatim
normalisait le segment avant l'appel. Le test utilise désormais le chemin brut et vérifie la
présence de `ParentDir` avant l'opération ; la suite complète repasse. Son unique fichier de test
créé hors du sous-dossier temporaire a été identifié par nom, contenu et SHA-256 puis supprimé.

## Procédure d'acquisition native

1. Construire le frontend Release avec `FORGE_SOURCE_COMMIT` égal au candidat,
   `CARGO_NET_OFFLINE=true` et `NO_COLOR=true` :
   `trunk build --release --locked --config Trunk.toml` dans `apps/forge_studio`.
2. Depuis la racine :
   `cargo build --release -p forge_studio --features tauri/custom-protocol --locked --offline`.
   `tauri/custom-protocol` est nécessaire pour embarquer les fichiers : le premier lancement sans
   cette feature cherchait le serveur de développement localhost et a affiché
   `ERR_CONNECTION_REFUSED`. Il n'a pas été retenu comme preuve du parcours.
3. Lancer `target/release/forge-studio.exe`, puis **Projets · Gate 2**.
4. Saisir les noms de projet/module/zone, seed et point de départ. Créer le brouillon.
5. Essayer Alt-F4 : le brouillon reste présent et le titre indique comment enregistrer/annuler.
6. Annuler puis rétablir la création ; vérifier identité, contenu et révision 3.
7. Enregistrer dans un dossier de preuve existant ; relever le chemin et le hash du fichier.
8. Fermer et rouvrir le snapshot ; vérifier les métadonnées restituées et le fichier inchangé.
9. Capturer et inspecter la fenêtre ; conserver les captures et hashes localement.

## Résultat du parcours exécuté

Studio autonome embarqué, sans serveur Trunk ; captures de fenêtre à 1602 × 932 px. La scène
Gate 0 reste affichable avant **Projets · Gate 2**. Les champs ont été renseignés par les entrées
réelles de la fenêtre, sans commande injectée ni JSON édité à la main :

- projet `Vallée de preuve Gate 2` : `7f5ddc8d-7ff9-55df-ba8b-c2779184e52d` ;
- module `Chapitre de preuve` : `73ef63bc-e86b-5455-b035-7279609a1585` ;
- zone `Clairière des sources` : `ee31a883-697d-57aa-8526-3d0bdbf9e313` ;
- seed initiale `1`, spawn initial `(0, 2, 0)` m ; création révision `1`, rétablissement `3`.

Création visible, fermeture Alt-F4 empêchée avec consigne dans le titre, annulation vers une
session sans projet puis rétablissement du même ID de zone à la révision 3 : **PASS**.
Enregistrement par bouton, fermeture du projet puis ouverture du fichier : **PASS**. Hiérarchie,
nom, ID de zone, révision, seed et spawn sont restitués. Undo/redo sont désactivés après ouverture,
conformément au périmètre. Alt-F4 ferme ensuite normalement ; absence du processus vérifiée.

Le snapshot réel `snapshots/7f5ddc8d-7ff9-55df-ba8b-c2779184e52d-r3.forge.json` mesure 1780 octets.
Son contenu a été relu sur disque ; son SHA-256 byte-à-byte avant/après fermeture-réouverture est
identique : `45d21d3dcf6151a3c80108dfc30c3c6bc714c2608e2af4106bc23afaec880d7f`.
Ce hash du fichier n'est pas présenté comme le hash canonique interne des reçus, couvert par les
tests de service. La capture seule ne qualifie pas l'atomicité face à une interruption système.

Captures natives inspectées, conservées dans le dossier local de preuve :

| Capture | SHA-256 |
| --- | --- |
| `01-created.png` | `ca27bd8b2d7aa8fe91a89a18fbda0cece73d7b122544b069eb38329b1d0cf4dd` |
| `02-close-blocked.png` | `113dfc2da67c1bf4f620c0298f2532f2d39cb313d69c3ccfa1b36642b089c538` |
| `03-undone.png` | `f2130ebe0011ee635512689e247f05f9e78c8ed031c92e434c86a1f1bdc653fd` |
| `04-redone.png` | `e502e031f3358a8a10ac306d53110a09aa8b9bbff4cb31161dd3e7521d64f4dc` |
| `05-saved.png` | `317eb084c73def9ec1c595f4fd51a8426e9e0e7c8ec09ce384b60c2420fdcbcc` |
| `06-closed.png` | `129d07e4001631511b91cac24f97357ee0d2f6f5bf77eb01ea2b1a727094bbfa` |
| `07-reopened.png` | `93e48d7091291982ec0597421321d943a0ab041c9f67c394967da40c28352fd4` |

Inspection agent : formulaires et métadonnées lisibles, hiérarchie distincte, statut sauvegardé
visible ; le bloc de sauvegarde demande un défilement vertical. Ce n'est pas une acceptation
humaine de la finition UX ni une revue de terrain 3D. Le titre garde l'ancien avertissement jusqu'à
la fermeture autorisée, alors que le statut de page affiche correctement `Document enregistré`.

Artefacts construits depuis le code candidat, SHA-256 :

| Artefact | SHA-256 |
| --- | --- |
| `target/release/forge-studio.exe` (33373696 octets) | `f67a95352cd7f4b4aa06f2c215312b43fa942b199f3b7c2dde0775735423dc50` |
| `apps/forge_studio/dist/forge_studio_bg.wasm` | `a4d68b77f38551af55328ca45297855b8bd83b2c90650d514c41215efd24d011` |
| `apps/forge_studio/dist/forge_studio.js` | `ba685e3f2516d7344178b1d612d8cb6df76a675e704a53b88ea59a3912dac13b` |
| `apps/forge_studio/dist/styles.css` | `0a93f8447cf331a548a5bedffc21f59124116c53176c49672df6568afbd96e55` |
| `apps/forge_studio/dist/index.html` | `28b9d68de5ef7f6cb22cf379657644cdafb4802a17aac132acf29df5d23b32df` |

Lockfile inchangé. Le WASM a été exécuté dans WebView2 ; un essai séparé du mode navigateur
sans backend n'a pas été réalisé pour ce lot (ne pas le noter PASS runtime).

## Limites et routes suivantes

Pas d'ajout ultérieur de modules/zones, d'onglets de zones ou d'édition de métadonnées existantes.
La sélection présente les trois niveaux source ; ce n'est pas du picking 3D. En navigateur sans
Tauri, les opérations sont désactivées explicitement : aucune sauvegarde simulée.

Le reçu courant et undo/redo sont limités à la session de création. La réouverture n'importe pas
d'historique. Ce lot n'est ni un bus général avec dry-run, ni un audit durable, ni une preuve de
récupération après crash/coupure électrique ou de résistance à un attaquant concurrent local.
Les fichiers temporaires orphelins ne sont ni promus ni supprimés automatiquement.

La garde de fermeture normale ne protège pas d'un kill, crash ou arrêt forcé OS. Son retour est
affiché dans le titre natif, sans élargissement ACL. Le titre normal revient lors d'une fermeture
autorisée. Pas d'autosave. Les brosses, végétation par zones, sol texturé, placement, jeu et carte
complète restent à réaliser. Aucune preuve Gate 1 n'est remplacée ; aucun asset Cadle copié.
CI distante : **NOT RUN — infrastructure billing**, ADR-0006 ; PresentMon suspendu, ADR-0010.
