# R1-063 - Premier kit d'assets gouverne pour Visual Valley

Date de preuve : 2026-09-01
Statut : **candidat Blender qualifie techniquement ; R1-063 reste ouvert**

## Portee isolee

Ce lot introduit un manifeste d'assets original, un worker Blender 4.5 et un validateur de
reconstruction. Il ne modifie aucun crate Rust, aucune application, aucun shader, aucun `Cargo.*`,
aucun contexte projet et aucune ligne de roadmap. Cette frontiere evite tout recouvrement avec le
lot temporel du thread `Creer une branche Rust`.

Le worker ne lit ni Cadle, ni contenu extrait d'un jeu commercial, ni service reseau. La provenance
est `source_kind=procedural_original`, la licence est
`LicenseRef-OpenRPG-Forge-Project-Generated` et la distribution reste
`internal_gate_proof_only`.

## Contenu du candidat

| Asset | LOD0 | LOD1 | LOD2 | Collision | SHA-256 GLB |
|---|---:|---:|---:|---|---|
| `vv-rock-moss-a` | 5 120 | 1 280 | 320 | convexe | `70a5dfb9ada6d90dab641d2fe8c4b29d56d2393b11090f637e9d4452e1d01dc0` |
| `vv-rock-moss-b` | 5 120 | 1 280 | 320 | convexe | `6950e3f1e32e520e5c8ee00160dc567f502692dc58887ac49e7627005d9dc59d` |
| `vv-tree-oak-a` | 48 718 | 13 314 | 1 546 | tronc et branches | `56ea624ce4a471539e4c2de466dc3cda1c93d546ec14743ca2c5a4cf0643b8d8` |
| `vv-tree-pine-a` | 62 944 | 13 866 | 3 142 | tronc | `84419ba2de4fe5db7135552b3882b25b5c02770fc4a83afa63a37b445bf99b67` |
| `vv-architecture-wall-a` | 7 236 | 1 408 | 12 | boite | `b49b853f1d10e1ed4259d82365dd4eb96aec9332953cd4a46e29c40ae3639639` |
| `vv-architecture-arch-a` | 3 564 | 924 | 96 | composee | `dd9f9201970c3a412efc7c96d6b18adda98c5ac08c1a7b19a074e0f87927afaa` |

Chaque asset possede trois LOD strictement decroissants, un `UVMap`, des materiaux Principled PBR,
des pivots au sol, des collisions `UCX_*`, un GLB separe et huit vues Full HD. Le manifeste revision
3 ajoute 11 jeux de materiaux, soit 33 PNG 512 px : couleur/alpha, rugosite et normal tangentielle.
Ils sont empaquetes dans le `.blend` et effectivement reimportes depuis les GLB.

## Reconstruction et reimport

Deux constructions independantes ont ete produites sans ecrasement :

- primaire : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-pbr-run3-20260901-164902` ;
- reconstruction : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-pbr-run4-20260901-164959` ;
- validation : `E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-asset-kit-pbr-validation-run3-run4-pass-20260901-165300.v1.json`.

Resultats :

- 6 GLB byte-identiques entre les deux builds ;
- statistiques de geometrie et budgets LOD identiques ;
- 33 textures byte-identiques et pixel-identiques, reparties en 11 jeux PBR complets ;
- alpha transparent et opaque verifie sur les six variantes de feuillage ;
- 6 GLB reimportes par Blender 4.5.10 avec noms, UV, trois images PBR par materiau de rendu et
  collisions verifies ;
- 49 rendus compares en pixels decodes ;
- 45 rendus strictement identiques ;
- 4 rendus avec une variance GPU localisee ;
- au pire 16 pixels et 35 canaux modifies sur 8 294 400 canaux, ecart maximal `5/255` et moyenne
  absolue `3,121e-8` ;
- aucun rendu hors du contrat localise documente par le validateur : 24 pixels, 64 canaux, `8/255`
  maximum et moyenne absolue globale `1e-7`.

Un premier essai en alpha trame a ete refuse. Le candidat final emploie un alpha blend explicite.
Un second diagnostic a montre que les derniers ecarts Eevee etaient concentres dans un rectangle
de 4 x 5 pixels sur une arete architecturale, tandis que GLB, textures et geometrie restaient
strictement identiques. Le validateur borne donc aussi la localisation et la moyenne globale ; il
ne transforme pas une variation diffuse en PASS.

SHA-256 des rapports :

- primaire : `195f079b6f0507f46c2bfa3b60f0e9beb510800ac2a7c8aa24eaddc66ace9ca2` ;
- reconstruction : `229c1e28db4e14bd6711cdcbf29e19d2a73328b686689e44616ed0ad382b1015` ;
- validation : `447fdfbf98126569a3e2526f5e96b0adfe573060624e4b336948f6512cf8322b`.

## Revue visuelle corrective

Les premiers candidats n'ont pas ete retenus : feuillage en cartes trop lisibles, mousse en
plaquettes, palette surexposee et couronnement d'arche detache. Le candidat qualifie remplace la
mousse par une affectation conformee aux faces du rocher, emploie des feuilles pliees multi-tons,
repartit les aiguilles le long de branches courbes, densifie les silhouettes, retablit une palette
naturelle lumineuse et retire le bloc superieur de l'arche. Cette iteration ajoute des textures
PBR exportables : relief stratifie des rochers, fibres d'ecorce, grain du calcaire, feuilles de
chene lobees avec nervures et silhouettes alpha pour les aiguilles.

La revue accepte ce resultat comme **base de production et preuve du pipeline Blender**, pas comme
asset AAA publiable. Une passe artistique humaine reste obligatoire pour :

- sculpter et retopologiser les silhouettes rocheuses et les jonctions de branches ;
- remplacer ou corriger les cartes generees par des surfaces haute frequence issues de sculpts,
  scans ou peinture humaine, puis ajouter l'ambient occlusion ;
- enrichir feuilles, aiguilles, ecorce, mousse, lichens, erosion et variations de pierre ;
- verifier les pivots, echelles, collisions et transitions LOD dans la camera du runtime ;
- corriger la composition apres une vraie capture Forward+ dans Visual Valley.

## Matrice de qualification

| Controle | Etat | Preuve ou suite requise |
|---|---|---|
| Build Blender 4.5 | PASS | 2 builds, 6 assets, 33 textures et 49 rendus chacun |
| Budgets LOD / UV / collisions / PBR | PASS | rapport primaire et reimport GLB |
| Reproductibilite GLB | PASS | 6/6 byte-identiques |
| Reproductibilite textures | PASS | 33/33 byte-identiques et pixel-identiques |
| Stabilite visuelle | PASS technique | contrat GPU localise et revue manuelle des vues FHD |
| Revue artistique de publication | BLOQUE | sculpt, retopologie et surfaces AAA non realises |
| Integration Bevy / Rapier | NON EXECUTE | lot R1-064 distinct |
| Performance runtime | NON EXECUTE | capture et budget GPU/CPU apres integration |
| Promotion d'assets | INTERDITE | decision humaine explicite requise |
| R1-063 termine | NON | ne pas cocher la roadmap |

Cette preuve n'anticipe donc aucune gate : elle qualifie seulement un candidat DCC reproductible et
les outils necessaires au prochain passage artistique.

## Reaudit du 2026-09-05 apres mise a jour du cap produit

La relecture de `AGENTS.md`, `PROJECT_CONTEXT.md` et de la roadmap confirme que ce candidat reste
utile comme cache DCC original et comme entree technique de R1-064. Elle interdit toutefois de le
confondre avec un kit termine :

- au moment du reaudit, seules deux reconstructions etaient materialisees ; la troisieme est
  maintenant verifiee ci-dessous ;
- aucune couche gouvernee ne preserve encore une sculpture, retopologie ou retouche Blender humaine
  au travers d'une regeneration ;
- les pivots, dimensions et collisions doivent encore etre acceptes humainement dans le runtime ;
- la licence reste une reference interne sans qualification de distribution ;
- la revue artistique explicite du proprietaire n'a pas ete recueillie.

Le runtime R1-064 peut donc consommer les six GLB uniquement comme **candidat externe provisoire**,
avec verification des hashes et sans copie dans Forge. `R1-063` reste ouvert et aucune validation
humaine n'est inferee des controles automatises. Voir
`docs/evidence/gate1/R1_064_GOVERNED_ASSET_RUNTIME_INTEGRATION.md`.

## Troisieme reconstruction du 2026-09-05

Sortie externe : `C:\Users\karen\AppData\Local\Temp\openrpg-r1-063-third-build-20260905`.
Le meme worker SHA-256 `26d081343dde9478f302a8b2a1d647be2f1d95ad9719d621c28f729e3faf655a`,
le meme manifeste et Blender 4.5.10 ont produit une troisieme construction independante.
Le validateur courant a compare cette sortie aux deux precedentes avec code 0 : 6 GLB et 33
textures byte-identiques, reimport UV/PBR/LOD/collisions valide, 49 rendus conformes au contrat
GPU localise existant. Tous les pixels des rendus ne sont pas strictement identiques.

SHA-256 des rapports dans ce dossier :

- `asset-kit-build.v1.json` : `eda21546303ac9ab43014e4fcf97029652c8ace52ea6b3df2cd8852d0e3e83bc` ;
- `validation-primary-third.v1.json` : `08b755e12031bdacd285e8a88f42704e01c9500efec2a4edf6ae5bda1bdd43eb` ;
- `validation-second-third.v1.json` : `cf08368836d80e22e7501ea01bf59d39e06c9cfd65def131ccff58295e752760`.

Cette preuve leve seulement le manque de troisieme reconstruction du candidat initial. Les retouches
persistantes, leur qualification, la revue humaine et le verdict R1-063 restent ouverts.

## Conservation d'une source Blender corrigee : preuve worker du 2026-09-05

### Blocage de version levé — 7 septembre 2026

Copie portable officielle **Blender 4.5.10 LTS**, build `6dc0b208d1b5`, disponible
dans `E:/OpenRPG_Forge/.gate1-tools/blender-4.5.10-portable/` ; exécutable dans
`blender-4.5.10-windows-x64/blender.exe`. L'installation 5.2.1 reste inchangée ;
aucune association de fichier, PATH ou version attendue du worker n'est modifiée.
Le précontrôle historique ci-dessous n'est donc plus un blocage de disponibilité.

Acquisition reproductible : `scripts/Prepare-Blender45.ps1` avec `-Destination`
pointant vers une nouvelle sortie sur E:. Archive depuis
[le serveur officiel](https://download.blender.org/release/Blender4.5/blender-4.5.10-windows-x64.zip),
SHA-256 comparé à la [liste officielle](https://download.blender.org/release/Blender4.5/blender-4.5.10.sha256).
Budget d'expansion 4 GiB ; observé 925 823 629 octets. Les licences incluses sont
conservées avec l'outil, hors dépôt/package. Aucun contenu Cadle acquis.

Premier contrôle de version refusé : sortie comportant un suffixe hash/date après
`Blender 4.5.10 LTS`. Correction du parseur limitée à ce suffixe ; pas de changement
de version admissible. Reprise `-VerifyExisting` PASS après comparaison SHA-256
de chaque fichier extrait à son entrée dans l'archive épinglée, sans réextraction.
Deux refus exécutés : destination existante sans option et destination sur C:.
Le script conserve les sorties en cas d'échec. Contrôle `git diff --check` PASS.

Smoke fonctionnel : `Test-TreeFoliageCandidate.py` exécuté avec ce binaire et
`--background --factory-startup --disable-autoexec --python-exit-code 1` :

- `--candidate-dir E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-conifer-sprays-20260907/candidate` ;
- `--base-build E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-063-authored-preservation-20260905-b/rebuild-1` ;
- `--manifest examples/visual_valley/asset-kits/r1-063/asset-kit.v1.json` ;
- `--output-dir E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-blender45-restored-20260907/reimport`.

Sortie 0, **2 GLB réimportés, 4 sources invalides refusées**, matériaux/images/samplers
identiques à la base et sources inchangées. Le contrat des sites de rameaux passe.
Le nouveau rapport est identique au rapport de réimport antérieur : cela confirme
la reprise de cet outil, sans inventer de nouvelle qualité des arbres. Aucun asset
régénéré, aucune image nouvelle ou retouche humaine, aucun test Rust nécessaire à
ce changement d'outillage. La reconstruction complète et le parcours humain restent
distincts de ce smoke ; R1-063 et Gate 1 restent ouverts.

Pins SHA-256 :

- archive : `ef6d846b8015f47ade6df3f9322ce17419080a5d922fa562b6c966064fe30dce` ;
- exécutable : `8b15ef73f352173d7836c5adc266fc45b94d729712d5ddbbb38557e1f326313c` ;
- script acquisition : `55ef6ca2bdaa98b92869cd65ed4a29105b9120f94ee3949402e510d1b0080bfb` ;
- `reimport/reimport-proof.json` : `baf37213c8e9c51af6eb7e3373ab195f33d1949f8c286a370aa550f7ee658e39`.

### Reprise complète après une seconde retouche synthétique — 7 septembre

Le test `Test-VisualValleyAuthoredSource.py` a été relancé avec le Blender portable,
la recette canonique et, comme base, le `rebuild-1` déjà authoré de la preuve du
5 septembre. Sorties nouvelles, hors dépôt :
`E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-authored-restored45-20260907-b`.

Premier essai sans suffixe `-b` refusé avant reconstruction : le test recopiant la
provenance dérivée du build produisait un descripteur incompatible avec la recette.
Le worker avait raison de refuser. Correction du test seulement : vérifier recette
et provenance du build de base, utiliser la provenance de recette pour le nouveau
descripteur et démontrer son acceptation avant les cas négatifs. Une altération de
licence ajoute un sixième refus. Aucun contrôle de provenance du worker relâché.
L'essai échoué et sa source synthétique sont conservés.

Résultat réellement acquis, sortie 0 :

- sauvegarde/réouverture d'une seconde retouche synthétique du rocher ;
- deux reconstructions, puis réouvertures : géométrie, faces, UV et matrices des
  objets sélectionnés exactement conservées ; GLB du rocher différent de la base ;
- six GLB et 33 textures identiques entre reconstructions, réimport Blender réussi ;
- 49 rendus comparés : trois non identiques pixel à pixel, zéro changement hors
  du contrat GPU borné existant, sans modification de seuil ;
- six entrées invalides refusées ; source de base et nouvelle source conservées.

Planche du premier build inspectée : six assets présents, sommets des arbres
encore coupés dans la vue groupée. Cette image ne qualifie pas les silhouettes
complètes. Les arbres reconstruits sont ceux du générateur de base, pas une
promotion/remplacement des dérivés forêt rev.4 déployés. Pas de nouveau rendu
Bevy/Studio, d'édition produit, de revue humaine ou de qualification de gate.
Pas de Rust modifié ou de build Cargo exécuté ; contrôle de diff PASS.

SHA-256 : test `096f71725702c3131ac655f95b7f11a215e62083bf73ca3904c01eb88adc0f86` ;
`preservation-proof.v1.json`
`4470a30a03ad29a35f03d9a963d642233dff9f2cd3540526b2ad1415be9d5e8b` ;
`validation-rebuilds.v1.json`
`613abcf9f758e59651fbfd0d5fe4b0a48a40d2e50cb6b6ed4dccba5945d6a632`.

### Précontrôle historique conservé

Précontrôle de reprise du 7 septembre : `E:/Program Files/Blender 5.2/blender.exe
--version` retourne **5.2.1 LTS**, build `9e2066aef7ef`. Le worker
`Build-VisualValleyAssetKit.py` exige toujours `(4, 5)` et le test de candidat
`Test-TreeFoliageCandidate.py` contient la même contrainte. L'exécutable historique
documenté `C:/Program Files/Blender Foundation/Blender 4.5/blender.exe` n'est pas
présent au chemin vérifié. Aucune reconstruction ni modification de source Blender
n'a été effectuée pendant ce précontrôle. Les preuves 4.5 ci-dessous restent
historiques : elles ne qualifient pas Blender 5.2. La reprise exige soit un runtime
4.5 explicitement restauré, soit une qualification isolée du worker sous 5.2 avant
de changer sa version de référence ; ne pas seulement supprimer le garde de version.

Implementation locale : le worker accepte `--authored-source`, un descripteur v1 qui lie le hash
de la recette generatrice, le hash du `.blend` source, les IDs des assets remplaces et leurs noms
d'objets. Il reprend les meshes statiques selectionnes et reconstruit les autres assets. Les sorties
restent des caches externes ; la source corrigee est un fichier distinct qui n'est jamais ecrase.
Le validateur compare aussi revision, provenance et identite de source authorée entre reconstructions.

Preuve executee avec Blender 4.5.10, code de sortie 0 :
`scripts/blender/Test-VisualValleyAuthoredSource.py`, dossier externe
`E:\OpenRPG_Forge\.gate1-evidence-runtime\r1-063-authored-preservation-20260905-b`.

- retouche **synthetique**, pas une intervention artistique humaine : un sommet du rocher est
  deplace de 0,125 m et sa rotation Z est fixee a 0,15 rad ;
- sauvegarde et reouverture de la source, puis deux reconstructions independantes et reouvertures ;
- coordonnees de tous les sommets, faces, UV et matrices des objets selectionnes conservees exactement ;
- GLB du rocher corrige distinct du candidat initial ;
- 6 GLB et 33 textures identiques entre les deux nouvelles reconstructions, reimport valide,
  49 rendus dans le contrat GPU existant (pas tous pixel-identiques) ;
- cinq refus verifies : recette perimee, hash source divergent, chemin sortant, asset inconnu,
  noms d'objets dupliques ;
- hashes du `.blend` initial et de la source corrigee inchanges apres les reconstructions ;
- planche finale inspectee : six assets presents ; arbres tronques par le cadrage en haut,
  rendu encore prototypal. Cette inspection ne vaut pas approbation humaine.

Rapport `preservation-proof.v1.json`, SHA-256
`30e12c175a5309760291164c0f7344544534d412051e11d5acee423bf38e56ab`.
Validation `validation-rebuilds.v1.json`, SHA-256
`5056e13f02dc90ec540c3b24638292aae9f11661bacfa6b7a6e8d52734301690`.
Worker teste : `bd7142d94de071d6f14298310c07827a58dd3ecea845b8898e25eb81a15854d6`.
Le premier essai `...-a` a echoue sur la liste mutable de l'API d'import Blender ; le correctif
copie cette liste avant de la transmettre. Cet essai et ses sources sont conserves.

Limites : preservation prouvee pour cette retouche de mesh/UV/transformation, pas pour une
retopologie humaine arbitraire. Aucun parcours Studio, commande de promotion ou rebase automatique
n'est implemente. Les assets non selectionnes sont reconstruits, sans garantie d'identite binaire
avec le kit sans source authorée. Le runtime acceptait initialement seulement le candidat procedural ;
le complement R1-064 du 2026-09-05 valide maintenant la provenance explicite
`procedural_original_with_authored_edits` avec un apercu epinglant le descripteur. Voir la preuve R1-064.
R1-063 et Gate 1 restent ouverts.
