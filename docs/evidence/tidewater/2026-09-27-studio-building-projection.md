# Projection des batiments importes — 27 septembre 2026

Le module partage tidewater_assets::scene prepare les placements canoniques
avant toute modification ECS : ordre/identites, revisions d'assets, materiaux,
meshes et triangles sont verifies. La geometrie et les materiaux restent
partages ; chaque instance porte sa transformation canonique et ses identites
ManualPlacementProjection/ManualPlacementPart. Aucun document n'est modifie.

Chaque partie recoit une collision trimesh statique derivee des surfaces reelles
(pas une enveloppe convexe remplissant tout le batiment). Ce choix reste a
qualifier pour le gameplay, les ouvertures, les faces transparentes et les couts.
Le budget est borne a deux millions de triangles et 16 384 parties par projection.
Le remplacement retire les anciennes racines et leurs enfants ; la readiness
verifie aussi que les anciennes parties ont disparu du backend Rapier. Les
poses, formes et associations de la nouvelle generation doivent etre synchronisees.

Studio repartit les placements selon les identites de la bibliotheque verifiee.
Les autres assets gardent le chemin GLB existant. Une revision erronee d'un asset
importe ne bascule pas sur un GLB de remplacement. Un rechargement explicite de
bibliotheque relance la resolution courante. L'aperçu de proposition et le retour
aux sources acceptees utilisent le meme ensemble de placements desire.

Le nouveau rendu consomme les textures, le shader verifie et les sorties
lumineuses du terrain importe. Il reprend les marqueurs de selection, les racines
des gizmos et le cadrage. Les controles de readiness des placements combines
incluent les batiments importes. Le playtest du terrain importe demeure ferme.

## Limites

Branchement produit non encore observe dans le canvas Studio ; ni geste GUI,
ni pixels presentes, ni sauvegarde/reouverture apres geste ne sont qualifies.
La reconstruction actuelle remplace toutes les instances importees lors d'une
modification du lot ; optimisation incrementale a suivre. Les ombres d'objets du
viewport demeurent desactivees. Les batiments sources utilisent la geometrie
locale du paquet ; la comparaison visuelle avec les coordonnees de reference
reste a realiser. Les decors residuels du village ne sont pas inclus dans les
23 assets de batiments. Eau, vegetation, decors et equivalence complete restent
au programme initial.

## Verification

- Clippy moteur et probe --no-deps strict PASS (session 60617, 11,14 s).
- Build natif PASS, 5 min 30 s. Execution PASS sur le paquet reel : 23 batiments,
  111 colliders, revision d'asset incorrecte refusee sans remplacer la scene,
  translation/rotation/echelle non uniforme, duplication, suppression complete,
  retour aux sources initiales. Raycasts sur les triangles transformes passes.
  Toujours 111 meshes en memoire : les instances partagent la geometrie.
- La suppression refuse la readiness tant que les anciens colliders sont encore
  enregistres dans Rapier ; le probe verifie ensuite le backend vide.
- Compilation Studio WASM finale PASS (session 48604, 5 min 01 s avec attente
  native). Clippy strict Studio : seul echec restant, bind_main preexistant
  dans atelier_ui.rs:122 (103 lignes). Pas de diagnostic local restant.
- Le gate du gizmo distingue maintenant le debut d'un geste de sa continuation :
  une pose provisoire volontairement differente ne doit pas annuler le geste.
  Ce raccordement a ete inspecte et compile, sans verdict de geste GUI.

Log et empreintes : E:/OpenRPG_Forge/.tidewater-integration/building-scene-proof-001.json.
Executable du probe : f14bc49fe0edaf202a3b65088b6c369c066dc6e0af861525c5976b059e9d495f.
Archive canonique validee : 50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720.
Le test utilise des materiaux avec bindings factices uniquement pour la validation
CPU/physique : il ne constitue PAS une preuve du rendu. Aucun nouveau verdict
visuel Studio, aucun frontend distribue, aucun commit ou push. Processus termines.

Suite du meme jour : les ombres d'objets ont depuis ete raccordees dans le code
Studio et les casters reels verifies sur GPU natif ; voir
[Ombres editables](2026-09-27-studio-live-shadows.md). Le verdict GUI reste ouvert.
