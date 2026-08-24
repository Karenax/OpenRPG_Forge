# Studio UX3-G — manifestes d’assets de production

Qualification Studio de `ORF-CONT-008`, `ORF-CONT-015`, `ORF-CONT-016` et
`ORF-CONT-017` sur la page `assets`. La surface « Paquets d’assets de production » édite un
`AssetPackageManifest` complet par sélection explicite, sans exposer d’UUID et sans réduire les
collections au premier élément.

<a id="orf-cont-008"></a>
## ORF-CONT-008

Le manifeste expose provenance, licence SPDX, hash source, budgets par plateforme et chunks
ordonnés avec rôle, caractère requis, chemin relatif, SHA-256, tailles, compression et plateformes.
Le Studio résout les fichiers réellement présents et leurs hashes avant l’approbation ; les tails
`ModelMetadata` et `Font`, hors périmètre de cette surface, restent inchangés.

<a id="orf-cont-015"></a>
## ORF-CONT-015

Collision et navigation conservent leur chunk gouverné, génération, tags ou surfaces, plateformes
de cuisson et caractère requis. Les références de chunks sont contrôlées contre le manifeste et
les budgets ; le Studio ne génère ni mesh de collision, ni walkmesh, ni bake de navigation.

<a id="orf-cont-016"></a>
## ORF-CONT-016

La bibliothèque d’animations conserve rig, retarget map, clips ordonnés, durée, root motion,
compression et événements ordonnés. L’édition couvre ajout, retrait et réordonnancement sans
lecture d’animation, chargement de modèle ou instanciation de squelette.

<a id="orf-cont-017"></a>
## ORF-CONT-017

Le profil VFX expose fournisseur, chunk, budgets particules/draw calls, distance de culling et
caractère requis. Les niveaux LOD restent ordonnés avec seuil écran, triangles et chunk. Le dry-run
construit uniquement des plans `AssetVfxRenderPlanBuilder` de disponibilité et de culling ; aucun
provider, système de particules ou benchmark GPU n’est exécuté.

## Données réelles et preuve isolée

Le store Crypte des Cendres contient **129** manifestes : **105** avec collision, **112** avec
animations et **129** avec LOD, mais aucun profil navigation ou VFX. Le parcours produit affiche cet
état réel sans synthèse. Pour exercer les quatre familles dans la transaction, la preuve copie un
manifeste réel et un voisin dans un workspace temporaire borné, ajoute seulement dans cette copie
un chunk navigation, un chunk VFX, un retarget map et un second clip avec événements, puis supprime
l’isolat. La source, son store, le projet, l’historique, la sélection et l’approbation restent
strictement inchangés.

## Transactions et préservation

- empreinte complète du document source et empreinte binaire typée du brouillon approuvé ;
- révision du store, dry-run sans mutation, recontrôle à l’application et refus des états périmés ;
- commande canonique `set_asset_package_manifest` et historique dédié ;
- application, annulation, rétablissement et save/reopen du document canonique ;
- manifeste voisin, projet, tails cachés, collections ordonnées et identités stables préservés ;
- nettoyage du workspace temporaire exigé par le smoke et par la transaction UI.

## Validation locale

- build Studio Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : Domain **4/4**, Application **6/6**, ModuleFormat **3/3** et EngineAdapter
  **2/2**, soit **15/15** ;
- smoke global : `OPENRPG_FORGE_STUDIO_UX3_G_PASS`, shell PASS et toutes les preuves de
  transaction/préservation à `true` ;
- portail incrémental : build, tests, smoke, capture FHD et matrice en **160,44 s** ;
- quatre captures Forward+ **1920×1080** inspectées : manifeste, collision/navigation,
  animations et VFX/LOD restent lisibles, avec actions et footer visibles, sans UUID ni clipping ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-g-asset-manifest.summary.json` ;
- captures :
  `artifacts/verification/studio-increment/ux3-g-asset-manifest-current-forward-plus-fhd.png`,
  `artifacts/verification/studio-increment/ux3-g-collision-navigation-current-forward-plus-fhd.png`,
  `artifacts/verification/studio-increment/ux3-g-animation-current-forward-plus-fhd.png` et
  `artifacts/verification/studio-increment/ux3-g-vfx-lod-current-forward-plus-fhd.png` ;
- `full_gate=false` et `graph_check=false` restent explicites pour ce portail incrémental ; le
  graphe Roslyn est régénéré et contrôlé séparément après la promotion.

## Limites honnêtes

Le Studio décrit, valide et persiste les manifestes. Il ne génère aucun mesh de collision ou de
navigation, ne charge ni modèle ni animation, ne rend aucun VFX, ne produit aucun LOD, ne mesure
aucun GPU, ne construit aucun package et n’exécute aucun script, acteur, moteur, serveur ou réseau
live. Les plans VFX et plateforme restent des décisions pures sur des fichiers et capacités copiés.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
  'tests/Forge.EngineAdapter.Tests/Forge.EngineAdapter.Tests.csproj'
)
$filter = 'FullyQualifiedName~AssetPackageManifest|' +
  'FullyQualifiedName~AssetVfxRenderPlanBuilder'
& .\scripts\Verify-StudioIncrement.ps1 -Page assets `
  -Evidence ux3-g-asset-manifest `
  -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-g-asset-manifest-current-forward-plus-fhd.png

foreach ($mode in 'ux3-g-collision-navigation', 'ux3-g-animation', 'ux3-g-vfx-lod') {
  & .\scripts\Open-Studio.ps1 -Page assets -Evidence $mode `
    -Workspace apps/Forge.Runtime/content/crypt-of-ashes `
    -Resolution 1920x1080 -SkipBuild -Wait `
    -Capture "artifacts/verification/studio-increment/$mode-current-forward-plus-fhd.png"
}
```
