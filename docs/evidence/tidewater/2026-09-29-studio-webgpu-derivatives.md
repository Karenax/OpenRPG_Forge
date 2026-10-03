# Tidewater Studio — rendu WebGPU et pointage terrain

Le candidat Studio 002 ouvre la zone gérée `Tidewater island` et synchronise
ses collisions, mais son canvas devient noir avant tout placement. Le smoke
réel échoue donc avec `Tracé annulé : pointeur hors terrain` ; ce libellé
masque ici l'arrêt de l'application de rendu Bevy, pas un clic hors carte.

Un profil WebView2 isolé avec CDP a relevé deux erreurs de validation WGSL :
`'textureSample' must only be called from uniform control flow` dans les
shaders du terrain (`terDetail`) et des plantes (`vegImpSample`). La création
du pipeline `opaque_mesh_pipeline` échoue ensuite, puis Bevy annonce
`Quitting the application due to Validation RenderError`. Les journaux sont
dans `E:/OpenRPG_Forge/.tidewater-integration/studio-black-canvas-diagnostic-002/events.json`.

Le constructeur de shaders de la source figée Tidewater préfixe les modules
par `diagnostic( off, derivative_uniformity );` (`src/engine/gpu/Shader.js`).
Cette directive conserve ses échantillonnages et ses dérivées dans les
branches dépendant des pixels. Injectée uniquement dans les deux modules
terrain et plantes dans un profil de diagnostic temporaire, elle supprime les
erreurs WebGPU : le canvas affiche de nouveau la côte et le survol physique
du terrain produit `X 42.0 · Y 0.5 · Z -49.1 m`. Cette expérience n'a
modifié ni le candidat 002 ni les assets épinglés. Capture et état :
`E:/OpenRPG_Forge/.tidewater-integration/studio-black-canvas-diagnostic-004/screen.png`
et `E:/OpenRPG_Forge/.tidewater-integration/studio-black-canvas-diagnostic-005/state.json`.

Un premier correctif a placé la directive au staging Rust des shaders. Le
candidat 003, reconstruit avec ce code, conserve pourtant le canvas noir :
`naga_oil::Composer::make_naga_module` recompose le WGSL, puis la sortie
`naga::back::wgsl::write_string` de Bevy retire le diagnostic avant l'appel
WebGPU. Le profil CDP isolé du candidat 003 confirme `directiveCount=0` dans
les modules finaux terrain et plantes, avec les mêmes erreurs de validation :
`E:/OpenRPG_Forge/.tidewater-integration/studio-black-canvas-diagnostic-006/events.json`.
La correction devait réintroduire la directive dans le WGSL final ciblé. Elle
désactive une validation de contrôle uniforme ; comme dans la source, des
différences entre pixels voisins aux frontières d'une branche peuvent rester
visibles. Il fallait donc vérifier le résultat dans un nouveau binaire Studio,
sans injection CDP.

Un smoke conditionnel du candidat 002 avec cette seule injection a ensuite
franchi le clic physique, l'aperçu `23 → 24`, l'application, la sauvegarde
dans `workspace-v1` et la réouverture avec 24 placements. L'identifiant, le
pin d'asset et les composantes `f32` du transform sont identiques après
relecture ; la source initiale reste inchangée. La capture après application
montre l'échoppe et Joe dans le terrain rendu :
`E:/OpenRPG_Forge/.tidewater-integration/studio-fish-stand-conditional-smoke-003/fish-stand-applied.png`.
La capture prise juste après la réouverture montre encore la préparation des
ressources GPU et ne qualifie pas l'image finale. Ce test reste conditionnel
à l'injection CDP et ne pouvait valider seul le correctif dans un binaire.

Le candidat Studio 004 (SHA-256 de l'exécutable
`FD195DEED79CB338490EAC5ABFBE5D82B4CEC20094530799A09EE39B18ABA61A`)
embarque le correctif ciblé avant la compilation WebGPU, sans injection CDP.
Le smoke réel sur profil et copie de workspace isolés donne
`authoring_pass=true` : un clic physique valide prépare le passage de 23 à 24
placements, puis l'aperçu, l'application, la sauvegarde, la fermeture et la
réouverture aboutissent. Un premier point visé a été refusé comme hors terrain ;
le second a permis le parcours complet. La zone relue contient toujours
l'échoppe, avec le même identifiant, le même asset épinglé et les mêmes
composantes `f32` du transform. La fixture source est restée inchangée. Les
captures après application et après réouverture montrent toutes deux le
terrain rendu, l'échoppe et Joe :
`E:/OpenRPG_Forge/.tidewater-integration/studio-fish-stand-smoke-run-004/fish-stand-applied.png`
et `E:/OpenRPG_Forge/.tidewater-integration/studio-fish-stand-smoke-run-004/fish-stand-reopened.png`.
Le reçu est dans le même dossier, `fish-stand-studio-smoke-ledger.json`. Ces
captures automatisées prouvent que le canvas n'est plus noir dans ce parcours ;
elles ne constituent pas une revue humaine ni une preuve de parité visuelle
avec la source WebGPU Tidewater.

Le smoke global du candidat 004 reste `failed_closed` : au bouton `Jouer`, la
commande répond `Imported vegetation requires its content-addressed instance resolver`
et l'attente de l'entrée en Playtest expire après 120 secondes.
L'édition et la persistance ci-dessus ont réussi ; la simulation joueur n'a
pas démarré. Le même refus était déjà visible dans le smoke conditionnel du
candidat 002 (`ImportedSourceRequiresResolver`). Le jeu natif distribuable est
séparé de ce Playtest Studio.

Le raccord du Playtest demande une résolution des instances végétales par
leurs contenus épinglés, l'autorisation de la projection du terrain importé
en mode Jeu, puis le contrôleur joueur et sa caméra dans le viewport importé.
La mer et la nage demandent encore le système de requêtes d'eau du runtime ;
un simple contournement du refus de végétation ne rendrait pas l'île jouable
dans Studio.
