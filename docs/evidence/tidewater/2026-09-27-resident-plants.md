# Ressources vegetales conservees pendant la navigation

Date : 2026-09-27. Portee : projection ECS/ressources, pas benchmark GPU.

`scene::resident::Resident` prepare une fois les meshes, materiaux et buffers
de records/modeles d'une population. La selection reutilise les fonctions
near/far existantes, conserve les entites toujours selectionnees et ne cree
ou retire que les differences par groupe et niveau. L'ordre de selection est
conserve independamment de l'ordre d'allocation ECS. Une camera non finie est
refusee avant mutation. Le retrait consomme la projection et retire ses entites.

Studio utilise cette projection. Les changements de camera ne recreent plus
les ressources de geometrie ; un changement de source remplace la projection.
Les mises a jour de lumiere/ombres continuent de modifier les materiaux residents.

## Verification

Probe `tidewater_population_scene_probe`, paquet vegetal portable reel et
population canonique de 65 383 instances : PASS.

- Camera A, B, puis A : sequences d'IDs exactement identiques a l'ancienne
  projection recreee a chaque vue.
- Nombres d'assets constants sur les trois vues : 8 meshes, 8 materiaux,
  12 buffers. Handles des materiaux inchanges.
- 37 098 entites conservees cumulativement entre les deux changements de vue.
  Ce nombre est une somme de conservations, pas un compte d'instances uniques.
- Repetition de chaque vue : zero creation, zero suppression et memes entites.
- Camera invalide : refus sans changer les entites ni les nombres d'assets.
- Retrait final : aucune entite ImportedPlant restante dans le monde de test.
- Les controles precedents de suivi du relief et d'identite canonique passent.

Clippy moteur/probe strict PASS. Deux erreurs de preparation corrigees avant ce
verdict : import wildcard du nouveau module, puis chainage invalide de
World::init_resource dans le probe. Build natif PASS (5 min 48 s).
Studio WASM check PASS (1 min 03 s, attente de verrou comprise).
Clippy Studio strict conserve uniquement le diagnostic preexistant
atelier_ui.rs:122 (bind_main 103 lignes). Formatage et diff checks PASS.

Executable : 7497457970b6a8887a5b2e3285959574ade16ac88ab332ad1dc81e2426f770b4.
Logs locaux sous E:/OpenRPG_Forge/.tidewater-integration :
resident-plants-clippy-001/002/003.log, resident-plants-build-001.log,
resident-plants-run-001.log, resident-plants-wasm-001.log et
resident-plants-studio-clippy-001.log. Tous les processus sont termines.

## Limites et prochaine qualification

Le test porte sur l'ECS et les assets CPU ; il ne prouve pas la liberation GPU,
la fluidite, les FPS ou la stabilite visuelle de la navigation Studio. Aucune
nouvelle capture GPU n'est revendiquee pour ce chemin resident. Les selections
sont encore recalculees par parcours des records ; un index spatial et les
cadences de rafraichissement source restent a evaluer selon mesures.

Picking non implemente : le raycast standard lit la geometrie avant deformation
shader. La piste suivante est une passe GPU d'identifiants conservant deformation,
alpha/LOD et occlusion terrain/batiments. Cette piste ne vaut ni implementation
ni qualification. Retouches individuelles, persistance, herbe, ombres des plantes
et animation continue restent ouvertes. Aucun commit, push ou nouveau frontend.

## Complement GPU natif du meme jour

Le probe de scene accepte maintenant `--resident-plant-navigation` en derniere
option, apres la bibliotheque de batiments. Il prepare la projection residente,
effectue les selections A (42,14,-90), B (400,30,400), puis A, et conserve les
ressources dans le monde pendant le rendu. Les nombres d'assets sont verifies
constants et la conservation d'entites est exigee. La camera rendue reste A.
Ces selections ont lieu avant la premiere capture GPU : ce n'est pas une
navigation animee ni une preuve des images intermediaires.

Clippy du probe strict PASS ; build PASS (1 min 50 s). Une alerte sur le nombre
de booleens du probe a ete corrigee par un enum explicite de mode de projection.
Execution du mode resident PASS. Capture inspectee et trois SHA-256 identiques
a native-population-scene-001 :

- native-resident-plants-001.png :
  243ddf9659f95afea4bbd978b2b47006983d052fe3d841958c6f45182a3c4e78.
- native-resident-plants-001.shadow.bin :
  a8077ba841b789d6acfc7c7bc7da39496920a3b294068eab4b5ad25cb7ee8288.
- native-resident-plants-001.object-depth.bin :
  0c38d6ff937fbf5fe6248bbf5a272781804876bbe081222545f0334b611553a1.

Executable : 07fba25672d1cf4ab632a6a9722f026a3c430b9f1a79e21a5369fa600e014d07.
Preuve machine : resident-plants-gpu-proof-001.json. Logs :
resident-plants-gpu-clippy-002.log, resident-plants-gpu-build-001.log et
native-resident-plants-001.log, dans le meme dossier local d'integration.
Qualification limitee a la non-regression de cette vue finale native. GUI Studio,
performance, selection au clic et parite globale upstream restent non qualifies.
Les processus de ce complement sont termines.
