# AV5 — livraison et extension contrôlée

Dernière mise à jour : 5 août 2026.

## Résultat livré

AV5 ferme le chantier Asset Vault avec une preuve unique, versionnée et bloquante. La commande
`assets delivery-report` mesure le catalogue chargé, les fichiers réellement présents dans le Vault,
le contenu effectivement référencé par Forgotten Forest, l’intégrité des chunks, le paquet `.orpmod`
et le benchmark terrain produit pendant le même gate.

Le rapport n’est pas un inventaire déclaratif : il recalcule les tailles et SHA-256 des 45 chunks du
workspace actuel, recherche les références `visual.asset` et `scatterResource` dans les documents de zone,
mesure le `.orpmod` exporté et consomme les 16 contrôles du benchmark runtime. Toute référence non
gouvernée, tout octet modifié ou toute licence manquante fait échouer la commande.

## Reproduire la preuve

Après un export et le smoke Forgotten Forest :

```powershell
dotnet run --project tools/Forge.Cli/Forge.Cli.csproj --configuration Release -- `
  assets delivery-report . apps/Forge.Runtime/content/forgotten-forest `
  --module artifacts/verification/forgotten-forest.orpmod `
  --benchmark artifacts/verification/forgotten-forest-terrain-benchmark.json `
  --output artifacts/verification/asset-delivery-report.json `
  --markdown artifacts/verification/asset-delivery-report.md `
  --vault E:\OpenRPG_AssetVault
```

`scripts/Verify.ps1` exécute cette commande automatiquement après avoir exporté, validé et joué le
module. Les fichiers sous `artifacts/verification/` sont des preuves locales régénérables et ne sont
pas des sources éditées à la main.

## Mesure de référence

Le premier passage local du 5 août 2026 a produit les valeurs suivantes :

| Domaine | Mesure |
| --- | ---: |
| Catalogue | 605 entrées, 605 licences vérifiées, 605 disponibles localement |
| Sources | 521 Poly Haven, 50 Kenney, 34 ambientCG |
| Extraits directement importables | 84 entrées |
| Taille déclarée des acquisitions | 4 471 668 545 octets |
| Vault mesuré, hors logs et quarantaine | 31 952 fichiers, 5 142 241 288 octets |
| Originaux | 3 158 fichiers, 4 474 798 756 octets |
| Normalisés | 28 189 fichiers, 663 937 944 octets |
| Métadonnées | 605 fichiers, 3 504 588 octets |
| Workspace Forgotten Forest | 75 fichiers, 1 351 930 octets |
| Contenu gouverné | 22 manifestes, 44 chunks, 22 références, 16 assets distincts utilisés |
| Octets de chunks | 1 073 258 octets |
| Licences du module | 22 manifestes CC0-1.0 |
| Export de référence | 435 667 octets |
| Benchmark | 16 contrôles réussis |

La taille et le hash du paquet ainsi que les temps de performance varient après un changement de
contenu ou selon la machine. Le JSON régénéré est donc la preuve autoritative ; ce tableau documente
le premier passage AV5.

## Journal d’implémentation

1. AV0–AV2 ont établi le catalogue CC0, le Vault externe immuable, les téléchargements reprenables,
   les profils de normalisation, les manifestes content-addressed et le worker Blender confiné.
2. AV3 a relié installation, palette, placement, dispersion de biome et mutations MCP au bus de
   commandes transactionnel, avec vérification de licence et de hash.
3. AV4 a livré le terrain générique multi-biome, la tranche Forgotten Forest 128 × 128 m, sa
   navigation, son eau animée, son scénario jouable, son export et ses budgets runtime.
4. AV5 consolide ces preuves, documente l’extension et interdit l’augmentation silencieuse du
   catalogue avant une nouvelle décision artistique et technique.

Les procédures détaillées d’ajout sont dans
[`ASSET_SOURCE_AND_PACK_EXTENSION.md`](ASSET_SOURCE_AND_PACK_EXTENSION.md). La politique juridique
reste dans [`ASSET_SOURCE_GOVERNANCE.md`](ASSET_SOURCE_GOVERNANCE.md).

## Politique d’extension

Le gate AV5 fixe actuellement le catalogue à exactement 605 entrées : plancher 605 pour détecter une
régression, plafond 605 pour empêcher un élargissement massif sans revue. Lever ce plafond exige :

1. un petit pack candidat gouverné et reproductible ;
2. une planche contact ou une scène de revue visuelle ;
3. le contrôle échelle, matériaux, collisions, LOD, budgets et licence ;
4. une décision documentée mettant à jour la politique, ses tests et le rapport ;
5. un gate complet réussi avant publication.

Une nouvelle source peut donc être développée et testée sans entrer immédiatement dans le catalogue
distribué. Le plafond ne change qu’après validation du pack candidat.

## Extension AV6-A sans élargissement du catalogue

Le personnage animé de Forgotten Forest réutilise le pack Kenney déjà présent et gouverné dans le
Vault. Le catalogue reste donc figé à 605 entrées. La politique de livraison attend désormais
23 manifestes CC0, 45 chunks et 22 références gouvernées ; le nouveau GLB embarque un rig de 58 os
et trois clips, tandis que l’ancien modèle statique reste disponible pour les modules existants.
La preuve détaillée est décrite dans
[`AV6_ANIMATED_CHARACTER_VERTICAL_SLICE.md`](AV6_ANIMATED_CHARACTER_VERTICAL_SLICE.md).
