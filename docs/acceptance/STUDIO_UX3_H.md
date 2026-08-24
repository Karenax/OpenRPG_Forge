# Studio UX3-H — données, import et export

Qualification Studio de `ORF-CONT-004`, `ORF-CONT-005`, `ORF-CONT-006` et
`ORF-CONT-018` sur la page `assets`. La surface « Données & transferts » sépare clairement trois
frontières : configuration projet canonique, copie locale réversible et export local déterministe.
Chaque collection est éditée directement, sans grille positionnelle brute ni UUID visible.

<a id="orf-cont-004"></a>
## ORF-CONT-004

L’import de masse expose filtre glob, extensions autorisées, politique de collision, provenance,
licence, dossier source et destination relative au workspace. Le dry-run `ContentBatchTransferService`
liste chaque fichier, sa taille et la décision de collision avant toute copie. L’application conserve
un reçu borné à 32 Mio, puis annule ou rétablit uniquement les fichiers dont le hash correspond
encore au résultat approuvé.

Le libellé historique « ERF » ne signifie pas que Studio décode ce format propriétaire : la surface
l’annonce explicitement. Elle filtre et copie des fichiers ouverts gouvernés ; aucune archive ERF
propriétaire n’est interprétée, convertie ou présentée comme supportée.

<a id="orf-cont-005"></a>
## ORF-CONT-005

L’export sélectionne un ou plusieurs blueprints par libellé humain. Le
`BlueprintExportPackageBuilder` ferme les dépendances transitives et produit deux fois le même hash
pour le même projet, la même sélection et la même option d’aperçu. Le pack affiche son contenu,
ses comptes de blueprints, dépendances et aperçus avant écriture. L’application, l’annulation et le
rétablissement refusent une sortie modifiée depuis le dry-run.

<a id="orf-cont-006"></a>
## ORF-CONT-006

Les packages et sources textuelles sont édités comme objets nommés : namespace, version,
priorité, licence, provenance, ressource logique, fichier relatif, format JSON/Texte/YAML/ForgeScript,
caractère requis, budget, alias et références. Le fichier réel est relu dans le workspace ; son
SHA-256 et la compilation `ContentPackageCompiler` doivent être cohérents avant application.
Les alias utilisent l’adresse canonique `package:type/path` et les tails package non édités
(`Dependencies`, `Assets`, ressources scellées) sont préservés.

<a id="orf-cont-018"></a>
## ORF-CONT-018

Les tables typées offrent une édition cellule par cellule. Une table porte slug, nom, licence,
provenance et éventuelle table de base ; ses colonnes ordonnées sont Texte, Entier, Décimal,
Booléen ou Référence, avec caractère requis et cible typée. Les lignes gardent une identité stable,
une provenance et leurs valeurs par clé de colonne. Ajout, retrait et modification recalculent le
hash canonique sans exposer une représentation 2DA positionnelle fragile.

## Transaction et préservation

La configuration sources/tables passe par `SetContentWorkspaceHandler` en dry-run, avec révision et
empreinte du projet complet, puis par `ProjectCommandHistory` pour apply/undo/redo et
`ProjectSession` pour save/reopen. Import et export gardent chacun leur propre approbation et leur
propre reçu local : aucune atomicité inter-agrégats fictive n’est annoncée.

La preuve utilise une copie temporaire du module réel. Elle ajoute uniquement dans cet isolat une
source JSON réelle, un package, une table de trois colonnes et deux lignes, deux fichiers d’import
avec un conflit, et une sélection de blueprint. Projet, historique, révision, journal, dirty state,
zone active, sélection, workspace et approbations de la source restent inchangés ; les workspaces
temporaires sont supprimés après le smoke.

## Preuves

- build `Forge.Studio` Debug : **0 avertissement, 0 erreur** ;
- tests ciblés : **18/18** — Domain 6, Application 3, ModuleFormat 9 ;
- smoke `OPENRPG_FORGE_STUDIO_UX3_H_PASS` : configuration batch, dry-run, import/export
  apply/undo/redo, sources, tables, commande canonique, gardes stale, round-trip, préservation,
  transaction UI, cleanup et restauration source à `true` ;
- portail incrémental : build, tests, smoke, capture et matrice en **173,10 s** ;
- cinq captures Forward+ **1920×1080** inspectées : import, export, sources, tables et aperçu sec
  restent lisibles ; les formulaires longs défilent et le footer transactionnel demeure visible ;
- résumé machine :
  `artifacts/verification/studio-increment/ux3-h-content-data.summary.json` ;
- captures :
  `artifacts/verification/studio-increment/ux3-h-content-data-current-forward-plus-fhd.png`,
  `ux3-h-batch-import-current-forward-plus-fhd.png`,
  `ux3-h-blueprint-export-current-forward-plus-fhd.png`,
  `ux3-h-text-sources-current-forward-plus-fhd.png` et
  `ux3-h-data-tables-current-forward-plus-fhd.png` ;
- `full_gate=false` et `graph_check=false` restent explicites pour ce portail incrémental ; le
  graphe Roslyn est régénéré et vérifié séparément après la promotion.

## Limites honnêtes

Studio ne décode aucun ERF, ne télécharge ni ne convertit de contenu, ne compile pas de modèle ou
texture, n’importe rien dans Godot, n’exécute aucun ForgeScript et ne publie aucun pack. L’aperçu
de package compile et vérifie seulement les fichiers de la copie locale. L’export blueprint est un
artefact déterministe local, sans installation dans un autre module ni signature de distribution.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~ContentWorkspaceTests|' +
  'FullyQualifiedName~ContentPackageCatalogTests|' +
  'FullyQualifiedName~SetContentWorkspaceHandlerTests|' +
  'FullyQualifiedName~ContentPackageScriptServiceTests|' +
  'FullyQualifiedName~ContentWorkflowTests|' +
  'FullyQualifiedName~ContentPackageCompilerTests'
& .\scripts\Verify-StudioIncrement.ps1 -Page assets `
  -Evidence ux3-h-content-data `
  -Workspace apps/Forge.Runtime/content/forgotten-forest `
  -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux3-h-content-data-current-forward-plus-fhd.png

foreach ($mode in 'ux3-h-batch-import', 'ux3-h-blueprint-export',
         'ux3-h-text-sources', 'ux3-h-data-tables') {
  & .\scripts\Open-Studio.ps1 -Page assets -Evidence $mode `
    -Workspace apps/Forge.Runtime/content/forgotten-forest `
    -Resolution 1920x1080 -SkipBuild -Wait `
    -Capture "artifacts/verification/studio-increment/$mode-current-forward-plus-fhd.png"
}
```
