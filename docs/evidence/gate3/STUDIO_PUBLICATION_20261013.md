# Publication du lot Studio — 13 octobre 2026

Le propriétaire autorise la mise à jour de documentation, puis commit et push
sur la branche `Rust` de `Karenax/OpenRPG_Forge`. Le lot rassemble les corrections
Studio des 12–13 octobre : retrait du test intégré, profil de lancement local,
chargement animé, recettes et matériaux réutilisés, cadence actif/repos,
export de carte vers le jeu natif et retour sur le viewport conservé.

Le README indique le parcours de lancement, les boutons auteur et les limites
du runtime. Les huit tests de callbacks, cadence et pause/reprise sont conservés
dans `apps/forge_studio/tests/editor-gpu-pacing.test.cjs`, avec des chemins
relatifs au dépôt. Le retour du jeu est accepté par le propriétaire ; les
mesures mémoire et de cadence antérieures restent datées et ne sont pas une
qualification de plusieurs heures d'édition ou des FPS présentés.

Les validations du Studio commun portent sur son manifeste de 2549 entrées
`a5ba7b3c2680c88ddb075fdfa0d5bc02702138dbd35affaf29702c651252ccd5` et son EXE
`4dd80264dbcfd11a590f831b14fe040fce6b67b2d310cc988b0ef38555aa242e`.
L'ajout du test et la mise à jour des documents ne reconstruisent pas cet EXE.
Le commit est un lot de sources explicite, pas un snapshot de tous les fichiers
modifiés du workspace. Les changements indépendants de
`apps/forge_studio/src/asset_library_native.rs`, les scripts `hivemind` et les
retraits de configuration Codex restent locaux. Leurs validations et leurs
changements ne sont pas absorbés par ce lot. Le manifeste complet commun ne
doit donc pas être présenté comme le hash exact de l'arbre publié.

Le lanceur propriétaire et son profil restent inchangés. Les 88 fichiers
auteur ont été conservés lors du test. Les EXE, le frontend généré, les données
de carte et le Vault privé ne sont pas publiés dans Git.

Le périmètre contient 52 fichiers. Avant commit, `cargo fmt --all -- --check`,
les huit tests JavaScript rejoués depuis leur emplacement dans le dépôt,
le contrôle de syntaxe du diagnostic GPU et `git diff --cached --check` passent.
Chaque source applicative du lot correspond par SHA-256 au manifeste du dernier
essai validé. Un index explicite garde les autres modifications et l'index
initial hors du commit.

Les rapports liés conservent les preuves et échecs initiaux :

- [Retrait du test intégré](STUDIO_PLAYTEST_RETIRED_20261012.md).
- [Lancement configuré](STUDIO_DIRECT_LAUNCH_20261012.md).
- [Chargement, mémoire et jeu séparé](STUDIO_LOADING_NATIVE_GAME_20261012.md).
- [Retour sur la carte conservée](STUDIO_RESIDENT_GAME_RETURN_20261013.md).

Les contrôles de publication sont enregistrés dans
`E:/OpenRPG_Forge/.gate3-evidence/20261013/studio-publication-001`.
La qualification CI distante du commit sera distincte de sa synchronisation Git.
Gate 3 reste ouverte ; la partie RPG native complète et P05 restent à qualifier.
