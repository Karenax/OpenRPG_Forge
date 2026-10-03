# Sélection des plantes importées dans le Studio — raccordement initial

Le canevas de terrain importé installe maintenant la même dérivation de shader
ID que le probe natif. Le panneau Nature affiche un bouton de sélection des
plantes de l'île lorsque la végétation possède une source importée. Le clic
normalisé est lu dans une cible RGBA8 hors écran de la taille physique de la
vue, avec copie des lots végétaux déjà sélectionnés par le renderer.

La passe ajoute les meshes visibles du terrain, du village opaque et des objets
d'auteur opaques en `StandardMaterial` comme masques de profondeur à ID zéro.
Les matériaux transparents ne sont pas des masques, faute de règle d'interaction
alpha. Une carte sans village reste sélectionnable si son terrain est prêt ; le
probe de l'île complète continue d'exiger et de compter le village.

La réponse n'est acceptée que si la révision canonique de zone, la génération
de bibliothèque, la caméra, la projection, la taille de vue et le geste armé
correspondent toujours à ceux de la demande. L'ID doit appartenir aux lots
végétaux réellement dessinés. Le résultat affiché est une identité stable ;
aucune source n'est modifiée par la sélection. Le passage de clic déjà utilisé
par les bâtiments et objets ignore la sélection d'objet manuel pendant que
l'outil plante est armé.

L'attente GPU regarde les pipelines des quatre shaders de cette passe seulement
(plantes, terrain, village éventuel, objets opaques éventuels). Un shader sans
rapport dans le Studio ne peut donc plus bloquer ce clic. Si les pipelines de
sélection ne se préparent pas en 120 secondes, la demande échoue explicitement
et ses entités temporaires sont retirées.

Vérifications obtenues : `cargo check` Studio WASM, Clippy ciblé Studio WASM
(avec l'exception `too_many_lines` déjà présente dans `atelier_ui.rs`), Clippy
strict du moteur/probe, `git diff --check` et bundle Trunk release de la version
finale (`forge_studio_bg.wasm` : 48 908 555 octets, SHA-256
`bfd2e47a329159a78e21e0405ce0ffc186060b0c1c26b2f2d997ff11b8d00b6c`).
Le probe natif complet a repassé le chemin terrain/village : 37 identités
canoniques distinctes, 1 draw terrain, 140 draws village et 5 108 pixels
occultés. Les preuves sont dans `native-picking-occluders-004.*` sous
`.tidewater-integration`. Cette scène ne contient aucun occlueur
`StandardMaterial` (`standard=0`). Un troisième passage natif avec un objet
Standard de diagnostic a ensuite prouvé 3 970 pixels de plantes masqués et
0 ID survivant modifié ; [preuve dédiée](2026-09-28-standard-plant-occlusion.md).

L'essai GUI dans Tauri n'a pas été exécuté : la revue automatique a refusé le
lancement du serveur local de test (`blocked by policy`). Aucun clic plante
observé dans le Studio ni exécution WebGPU de ce nouvel occlueur Standard n'est
donc revendiqué. Les autres matériaux opaques possibles, la sélection à travers
les surfaces transparentes et la pose libre restent à traiter. Le modèle canonique
porte maintenant un décalage persistant par clé de plante importée, avec
historique et contrôle des bornes ; voir
[la preuve de retouche](2026-09-28-imported-plant-offset.md).
Une pose par copie de plante source a ensuite été ajoutée ; voir
[sa preuve native](2026-09-28-authored-plant-copy.md).

Le panneau Nature dispose aussi de « Déplacer vers un clic sur le terrain »
pour la plante sélectionnée. Le clic terrain calcule le décalage horizontal par
rapport à sa source et conserve le dégagement vertical courant ; l'altitude
suit la projection canonique sur le relief. La révision de zone est revérifiée
avant de préparer la même commande Preview/Apply que la retouche manuelle, avec
son historique Undo/Redo. La compilation Studio WASM passe. Le clic GUI réel et
le rendu WebGPU du résultat dans le Studio ne sont pas encore observés ; cette
étape ne qualifie pas l'édition complète de l'île.

L'archive delta v6 conserve désormais également les décalages et les copies
de plantes importées lorsque l'historique compact dépasse sa limite ; le
rejeu, Undo et Redo sont validés côté Rust.
[Preuve de persistance](2026-09-29-imported-plant-delta-archive.md).
