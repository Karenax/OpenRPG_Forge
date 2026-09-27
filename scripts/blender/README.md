# Blender workers

## Étude de groupes feuillus — Blender 5.2

Dernière variante : ajouter `--crop` à `Build-FoliageAtlasLod.py` pour recadrer
les cartes sur tout alpha non nul, marge d'un texel, UV/positions concordants.
Huit tests CPU couvrent le recadrage et les normales des cartes. L'encodage
explicite droite/haut/avant du bake est converti en droite/moins-haut/avant lors
de l'export : les UV retournés et tangent.w=-1 inversent la bitangente. Seul le
canal G de la normal map dérivée est réfléchi ; couleur, alpha et géométrie sont
conservés. Un aller-retour par la matrice TBN exportée teste deux orientations et
reproduit l'erreur de l'ancien signe ; encodage absent/inconnu refusé. Ce correctif
ne prouve pas la parité de densité ni la qualification temporelle des atlas.
ABBA natif historique du kit recadré (avant ce correctif de normales) :
~53 contre ~44,8 FPS forêt, gain répété ; mips/Studio/art toujours non qualifiés.
Verdict du premier kit atlas non recadré : régression ABBA, non retenu.
L'outillage reste une étude reproductible, pas une optimisation produit acquise.
`foliage_bake_pixels.py` normalise les EXR flottants par une référence d'émission
unitaire avant encodage PNG ; ne pas revenir au stockage direct de radiance en
PNG. `Test-FoliageBakePixels.py` et `Test-FoliageClusterOrm.py` couvrent cette
conversion. Les rapports v4/GLB v2 ont été utilisés pour la comparaison moteur.

`Build-FoliageAtlasLod.py --atlas atlas.json --sha256 HASH --output-dir SORTIE_NEUVE_E`
produit un GLB candidat en ajoutant les atlas et remplaçant uniquement le mesh LOD1
feuillu. Python tooling avec Pillow/NumPy, pas Blender ni dépendance moteur.
`Test-FoliageAtlasLod.py` contrôle padding/axes/UV ; `Verify-FoliageAtlasLod.cjs`
utilise le validateur Khronos local et `Inspect-FoliageAtlasLod.py` réouvre/rend
source et candidat. Aucun kit courant remplacé. Candidat sans mips, aspect et
fidélité PBR non qualifiés ; limites et preuves dans `R1_FOLIAGE_CLUSTER_ATLAS.md`.

`Bake-FoliageAtlas.py` reprend les mêmes arguments source/hash/objet/sortie et
rend désormais **tous** les groupes dans trois atlas couleur/alpha, normales et ORM.
Layout borné par `foliage_atlas_layout.py` : 2048² maximum, tuiles 64² dont 56²
de contenu. Trois rendus CPU/16 samples/deux threads par arbre, sans débruitage
ni échantillonnage adaptatif (cartes de données, normales contrôlées), watchdog externe
180 s ; sorties neuves sur E:, aucun remplacement de kit. `Test-FoliageAtlasLayout.py`
vérifie budget, mapping, absence de chevauchement et refus. Bases et centres en
monde Blender Z vertical, pixels des tuiles depuis le bas ; normales linéaires.
Les deux arbres complets ont été exécutés et les quatre atlas inspectés.
Restent dilatation/mips, autres canaux PBR, cartes 3D, export GLB et runtime.

`foliage_cluster_materials.py` partage l'extraction albedo/alpha et normales avec
l'étude initiale (`--normal`). `Test-FoliageClusterNormals.py` exerce cinq rendus
32² connus, sans modification des graphes source. Ces résultats ne qualifient
pas le matériau final du moteur. Détails : `R1_FOLIAGE_CLUSTER_ATLAS.md`.

`foliage_clusters.py` regroupe les faces par cellule en conservant chaque feuille
connectée entière, y compris ses sommets dupliqués à l'export. Connexions par arêtes
de positions exactes, pas de soudure par proximité. `Test-FoliageClusters.py` est
un test CPU autonome (quatre fixtures). Les indices retournés désignent les faces
d'origine ; aucun mesh source n'est muté.

`Bake-FoliageClusterStudy.py` exige un GLB autonome épinglé par `--sha256`, un
`--object-name` LOD0 à matériaux feuillus balisés et un `--output-dir` neuf sur E:.
Il importe sous Blender 5.2, partitionne à `--cell-size` 1,5 m par défaut, puis
rend le groupe le plus chargé selon trois axes. Cycles CPU, deux threads,
16 samples, PNG RGBA 512², albedo sans éclairage. Exécuter avec `--background
--factory-startup --disable-autoexec --python-exit-code 1` et watchdog externe.

Résultat de l'étude initiale : trois vues d'un seul groupe, pas un atlas complet ou un GLB
LOD. Les coordonnées du rapport sont celles du monde Blender après import glTF
(mètres, Z vertical), pas un document Forge Y vertical. Le complément décrit
ci-dessus fournit normales et packing ; roughness, dilatation/mips, cartes 3D et
raccordement runtime restent à construire.
Les deux études chêne/conifère ont été exécutées et inspectées :
`docs/evidence/gate1/R1_FOLIAGE_CLUSTER_ATLAS.md`. Ne remplace pas les workers 4.5.

## Toolchain de qualification isolée

Les workers épinglés 4.5 peuvent utiliser une copie portable officielle sans
modifier Blender 5.2, les associations de fichiers ou le PATH de la machine :

```powershell
./scripts/Prepare-Blender45.ps1 -Destination E:/OpenRPG_Forge/.gate1-tools/blender-4.5.10-portable
```

PowerShell 7.2+ ; Windows x64 seulement. L'archive 4.5.10 est épinglée par
SHA-256 officiel, les membres ZIP sont contrôlés avant extraction et la sortie
existante est refusée. `-VerifyExisting` ne télécharge ni n'extrait : il vérifie
l'archive et tous ses fichiers déployés avant le contrôle de version. En cas
d'échec, les fichiers sont conservés ; aucun nettoyage ou remplacement implicite.
Le JSON émis sur stdout décrit l'acquisition, pas une qualification du worker.

Exécutable local vérifié :
`E:/OpenRPG_Forge/.gate1-tools/blender-4.5.10-portable/blender-4.5.10-windows-x64/blender.exe`.
Utiliser `--background --factory-startup --disable-autoexec --python-exit-code 1`
pour les preuves. La copie est un outil externe, jamais un asset ou une dépendance
du package Forge. Le réimport rev.4 exécuté avec elle est documenté dans
`docs/evidence/gate1/R1_063_GOVERNED_ASSET_KIT.md`.

## Étude isolée du feuillage

`Test-FoliageAlpha.py --output-dir <sortie fraîche>` vérifie par de vrais exports
la politique d'alpha `mask` (Round, cutoff 0,5), `blend` et le contrôle opaque.
Exécuter en headless avec `--disable-autoexec --python-exit-code 1`. Test du helper
sous Blender 5.2.1, pas qualification du worker complet historiquement 4.5.
Le manifeste et le défaut restent `blend` ; aucun kit existant converti implicitement.
Voir `docs/evidence/gate1/R1_FOLIAGE_ALPHA_COST.md` : déploiement et gain non acquis.

`scripts/Derive-FoliageAlpha.cjs` (Node, hors Blender) dérive un kit runtime externe
en ne changeant que l'alpha des feuillages balisés ; les octets de géométrie et
textures restent intacts. Il exige une sortie fraîche et le validateur glTF local.
`Test-FoliageAlphaReimport.py` compare les géométries après import source/dérivé
sous Blender 5.2. Les mesures natives du candidat MASK n'établissent pas de gain
FPS ; il reste non promu, sans altérer le défaut historique.

`Build-TreeFoliageCandidate.py` prend `--base-build`, `--manifest` et `--output-dir`
(nouvelle sortie), sous Blender 4.5 avec `--disable-autoexec --python-exit-code 1`.
Il produit deux arbres candidats et leurs trois LOD sans modifier le générateur principal,
la recette ni les sources. Les arbres déclarés retouchés sont refusés. Ce script n'est pas
une commande de promotion ; voir `docs/evidence/gate1/R1_FOLIAGE_CANDIDATE.md`.

Option isolée `--wood-profile reduced` : simplification Blender des copies du bois
LOD1/LOD2 (ratios 0,35/0,25), sans changement du LOD0 ni du feuillage. Défaut `source`
inchangé. Les répétitions géométriques et exports sont contrôlés ; quatre rendus
comparatifs du bois lointain s'ajoutent aux quatre vues proches. Ce traitement hors
ligne n'ajoute aucun travail au renderer et ne vaut pas qualification visuelle/performance.
Le test accepte `--reference-candidate <ancien-candidat>` pour vérifier après réimport
l'identité géométrie/UV/transformation du LOD0, de tout le feuillage et des collisions.
Preuve : `docs/evidence/gate1/R1_WOOD_LOD_DERIVATIVE.md`.

`--conifer-area-scale` (0,25–1, défaut 1) est un paramètre d'étude isolée : il
réduit longueur/largeur des cartes de conifère par la racine du facteur, sans
modifier les points d'attache, textures, nombres de cartes, bois ou chênes.
Le test à 0,5 reste non promu : rendre la couronne plus ajourée ne suffit pas à
corriger sa silhouette. Voir `docs/evidence/gate1/R1_CONIFER_COVERAGE_STUDY.md`.

Profil isolé `--canopy-profile branch_sprays` : feuillage du conifère distribué
le long des demi-branches extérieures existantes et autour du leader, avec amas
aplatis orientés vers l'extérieur. Bois inchangé, chêne identique au profil coherent,
sites communs aux trois LOD. Le test vérifie interpolation, déterminisme et refus
des paires incomplètes. Aucun réglage canonique promu ; voir
`docs/evidence/gate1/R1_CONIFER_SPRAYS.md`.

`Test-TreeFoliageCandidate.py` prend les mêmes arguments, plus `--candidate-dir`. Il réimporte
les GLB avec le validateur existant, compare matériaux/images/samplers aux GLB sources et exerce
quatre conflits de source en mémoire. La sortie de preuve doit également être nouvelle.

Après ces contrôles, `scripts/Prepare-FoliageRuntimeCandidate.py` assemble hors dépôt une
fixture runtime avec `--base-build`, `--candidate-dir`, `--reimport-proof`, `--preview` et
`--output-dir`. Elle conserve les placements et les assets non remplacés, lie les hashes des
preuves et emploie une identité de kit distincte. Elle n'effectue aucune promotion Studio.

`Build-VisualValleyGreybox.py` consumes only the derived
`forge.world-sketch-blender-input` emitted by `forge_cli`. It refuses an existing output directory,
builds a deterministic Blender 4.5 scene, saves a `.blend`, exports a GLB, renders every WorldSketch
validation camera plus the hero image and writes a manifest with hashes.

Example after `compile-world-sketch` :

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background `
  --python scripts\blender\Build-VisualValleyGreybox.py -- `
  --input E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4\blender-input.v4.json `
  --output-dir E:\OpenRPG_Forge\.gate1-evidence-runtime\world-sketch-v4-blender
```

Les sorties sont des caches et preuves. Elles ne remplacent pas le WorldSketch, le terrain, les
splines, les placements ou les manifestes d'assets canoniques. Le worker version 7 conserve l'atlas
de praticabilité et consomme `forge.world-bridge-placements` v1 pour incliner le tablier, placer
pavés, parapets et piles, puis reporter les hashes et dimensions dans le manifeste. Blender ne crée
pas la qualification physique : le collider et le franchissement restent prouvés dans le runtime
Bevy/Rapier. L'atlas ne constitue ni un navmesh ni un pathfinder.

La reproductibilité visuelle se vérifie sur les pixels décodés : Blender peut inscrire des
métadonnées différentes dans les conteneurs `.blend` et PNG alors que le GLB et les pixels sont
identiques.

## Source d'assets corrigée (worker R1-063)

`Build-VisualValleyAssetKit.py --authored-source <descriptor.json>` conserve les assets sélectionnés
depuis une source `.blend` indépendante des caches de reconstruction. Le descripteur strict
`forge.blender-authored-source` v1 contient `base_manifest_sha256`, `blend` (`path` relatif et
`sha256`), `assets` (ID vers liste complète des noms d'objets) et `provenance` identique à celle du
manifeste original. La recette génératrice reste l'autorité pour les assets non sélectionnés.

Les assets sélectionnés doivent fournir les trois LODs et collisions, des meshes statiques sans
parent, contrainte, modificateur non appliqué, animation ou shape key, les propriétés `forge_asset_id`,
`forge_role`, `forge_lod`, des UV et leurs images empaquetées. Leurs meshes, UV, matériaux et
transformations sont repris depuis la source authorée. Les budgets LOD et contrôles d'export restent
actifs. Un changement de manifeste ou d'octets du `.blend` provoque un refus explicite ; une nouvelle
révision de source exige de mettre à jour son descripteur. Le worker ne modifie jamais cette source
et refuse un dossier de sortie existant.

Le rapport lie les hashes du descripteur et du `.blend` et indique la provenance
`procedural_original_with_authored_edits`. Le runtime R1-064 accepte désormais ce candidat lorsque
l'aperçu épingle le hash de son descripteur et que le contrat de provenance et les GLB sont valides.
Ce chemin ne qualifie pas la promotion ni une commande de retouche Studio.
Les PNG listés par `textures` sont ceux de la génération procédurale ; les
matériaux authorés restent liés au `.blend` source et embarqués dans les GLB des assets remplacés.

Preuve reproductible avec une retouche synthétique, deux reconstructions et six refus d'entrée :

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' --background --disable-autoexec `
  --python-exit-code 1 --python scripts/blender/Test-VisualValleyAuthoredSource.py -- `
  --base-build <dossier-preuve-initiale> `
  --manifest examples/visual_valley/asset-kits/r1-063/asset-kit.v1.json `
  --output-dir <nouveau-dossier-preuve-externe>
```

Ce test déplace un sommet de 12,5 cm et applique une rotation à un rocher, sauvegarde une source
distincte, la rouvre, puis reconstruit deux caches. Il compare exactement géométrie, UV et matrices,
vérifie que le GLB diffère du candidat initial et valide la reproductibilité des deux nouveaux kits.
Une retopologie humaine et le parcours Studio restent à qualifier.

Le test accepte aussi un build déjà authoré : il vérifie son lien de recette et
sa provenance dérivée, puis lie le nouveau descripteur à la provenance originale
de cette recette. Un descripteur valide doit être accepté avant d'exercer les
refus, dont une altération de licence. Le worker garde ses contrôles inchangés ;
le test ne promeut aucune source ni aucun résultat dans le Studio.
# Contrôle du cadrage des aperçus

`Test-AssetKitFraming.py --base-build <kit-existant> --output-dir <nouvelle-sortie>` se lance
avec Blender 4.5 en arrière-plan (`--disable-autoexec --python-exit-code 1 --python ... --`).
Il contrôle les marges de projection de tous les assets, des formats paysage/portrait, des pixels
non carrés et une transformation auteur synthétique, puis rend les deux arbres. Il ne reconstruit
ni ne sauvegarde les assets sources. Le worker cadre les bounds réels de toutes les poses, avec
une caméra fixe et une marge de 8 %, plutôt que les dimensions nominales de la recette.
Voir `docs/evidence/gate1/R1_ASSET_KIT_FRAMING.md` pour la preuve et les limites.

La planche groupée utilise aussi les bounds des copies LOD0 et du repère humain,
au lieu d'une caméra fixe. Le test exerce le vrai `render_lineup` en paysage,
portrait et pixels non carrés, contrôle tous les coins projetés et le nettoyage
des copies. Les sources ne sont ni enregistrées ni régénérées par ce test.
