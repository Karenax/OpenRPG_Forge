# AV6-A — tranche verticale de personnage animé

Dernière mise à jour : 5 août 2026.

## Résultat

Forgotten Forest emploie maintenant un personnage humanoïde animé réellement reconstruit par le
Creator et le runtime. La tranche reste volontairement petite : une peau cohérente prouve toute la
chaîne avant d’introduire variantes, retargeting ou animation de combat.

Le pack source `kenney:animated-characters-protagonists` était déjà présent dans le Vault gouverné.
Ses originaux restent immuables. Le worker Blender confiné reçoit uniquement des chemins relatifs et
des SHA-256 attendus pour le modèle, la texture et les trois clips. Il vérifie les hashes, fusionne
les FBX compatibles, exporte un GLB autonome et écrit une réponse déterministe.

## Artefact livré

- asset stable : `kenney:animated-characters-protagonists:forest-adventurer` ;
- licence : `CC0-1.0`, preuve Kenney et catalogue local gouverné ;
- géométrie : 804 vertices, 1 604 triangles, un matériau et une texture ;
- rig : une armature, 58 os ;
- clips : `idle` 1,333 s, `jump` 0,5 s et `run` 0,667 s ;
- dimensions : 1,809 × 1,882 × 0,524 m ;
- miniature : PNG 512 × 512 ;
- audit Blender : 99/100, tous les contrôles requis réussis ;
- sortie : `assets/imported/kenney-animated-characters-protagonists-forest-adventurer/model.glb`.

Le modèle, la miniature, le rapport qualité, la requête et la réponse du worker sont inclus dans le
workspace. Ils ne contiennent aucun chemin absolu vers la machine de développement.

## Intégration auteur et runtime

Le projet canonique attribue `runtime.animationProfile=humanoid-basic` au joueur, à Maëlys, aux deux
rôdeurs et au Gardien. `ProjectPreview3D` charge le même chunk gouverné, vérifie son hash et joue
`idle`. Le runtime utilise le contrôleur partagé pour identifier les clips qualifiés ou non, boucler
idle/course, jouer le saut sans boucle et éviter de redémarrer une animation identique à chaque
frame.

Le contrôleur joueur choisit `run` pendant le déplacement, `jump` en l’air et `idle` au repos. Les
PNJ restent en idle dans cette tranche ; leur IA animée et les attaques constituent l’incrément
suivant.

## Preuves locales

- import réel Blender 4.5.10 puis réimport du GLB : 58 os et exactement trois actions ;
- second authoring idempotent : 23 assets réutilisés, aucun appel Blender ;
- build Release runtime et Toolset : zéro avertissement, zéro erreur ;
- smoke `.orpmod` : 23 manifests, 45 chunks, 18 modèles, cinq personnages animés, trois clips,
  navigation, quête et boss réussis ;
- rapport de livraison : 16 contrôles réussis, catalogue inchangé à 605 ;
- capture Vulkan Forward+ 1280 × 720 :
  `artifacts/verification/forgotten-forest-animation-forward-plus.png`.

## Limites explicites

Cette tranche ne prétend pas livrer un système complet d’animation. AV6-B ajoute désormais cinq
silhouettes et les états attaque/impact/mort sur le rig KayKit commun. Le retargeting entre rigs,
blend tree, root motion, IK avancée, animation réseau et la future carte 512 × 512 restent à livrer.
Voir [`AV6B_FANTASY_CHARACTER_COMBAT_SLICE.md`](AV6B_FANTASY_CHARACTER_COMBAT_SLICE.md).
