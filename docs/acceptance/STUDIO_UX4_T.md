# Acceptation Studio — UX4-T Marchands complets

État local au 20 août 2026 : **fonctionnel et qualifié** pour les 19 routes
`ORF-MER-001..019`. La page **Contenu social** expose une surface Marchands en cinq onglets et
remplace uniquement le profil ciblé via `SetWorldFeatureCatalogHandler`, dans une transaction
canonique annulable.

## Parcours canonique

La sélection d’un marchand existant ou sa création explicite précède toute édition. L’aperçu sec
résout vendeurs, objets, dialogues, monnaies, stock, prix, restrictions, scripts, transactions et
interface par les validateurs et adaptateurs marchands existants. L’approbation est liée à la
révision et aux empreintes binaires du projet, du brouillon et du candidat. Appliquer, annuler,
rétablir et enregistrer passent par l’historique et le round-trip du workspace.

### ORF-MER-001 — Objet magasin indépendant

Un marchand possède une identité stable, une localisation et un profil complet distinct des
créatures vendeuses et des objets vendus.

### ORF-MER-002 — Store Setup Wizard

La boutique, les vendeurs, le dialogue, le canal d’interaction et la fixture de validation sont
authorés sans JSON ni UUID visible.

### ORF-MER-003 — Inventaire du magasin

Le stock complet conserve ordre, variante, quantité, fournisseur, conditions masquées et prix.

### ORF-MER-004 — Markup et markdown

Multiplicateurs achat/vente, bornes, taux monétaires et courbes de prix sont prévisualisés par
`MerchantExperienceService`.

### ORF-MER-005 — Trésorerie

Les comptes par monnaie conservent solde, plafond et politique de dette.

### ORF-MER-006 — Prix maximal d’achat

La limite d’achat et les plafonds de prix sont validés avant application.

### ORF-MER-007 — Biens volés

Acceptation, refus ou condition de provenance des biens volés est représenté sans transaction live.

### ORF-MER-008 — Restrictions whitelist/blacklist

Types, tags, qualité, source et règle personnalisée composent une requête déterministe.

### ORF-MER-009 — Stock infini

La politique infinie est explicite et ne modifie aucune quantité runtime dans Studio.

### ORF-MER-010 — Réapprovisionnement

Quantité, intervalle, fournisseur et événement de réapprovisionnement sont persistés.

### ORF-MER-011 — OnOpen et OnClose

Bindings de session, résumé de transactions et API ForgeScript sont authorés sans exécuter de
script.

### ORF-MER-012 — Transactions atomiques

L’aperçu exécute une transaction copiée via `MerchantService` et vérifie qu’échec ou succès ne
laisse jamais un état partiellement muté.

### ORF-MER-013 — Monnaies multiples

Monnaies, taux, trésorerie et services partagent les mêmes identifiants canoniques.

### ORF-MER-014 — Négociation

Réputation, rareté, état et négociation alimentent un devis déterministe en mémoire.

### ORF-MER-015 — Marchés dynamiques

Région, volatilité, demande, offre et cadence du marché sont authorées comme politiques.

### ORF-MER-016 — Services

Les services conservent prix, monnaie, disponibilité, conditions et bindings.

### ORF-MER-017 — Interface de boutique

Colonnes, comparaison, quantité, clavier, manette, lecteur d’écran et messages localisés sont
éditables dans la même surface.

### ORF-MER-018 — Audit économique

Ledger, identifiants de transaction, rétention, provenance et résumé de session restent persistés ;
l’interface n’affiche jamais l’identifiant technique du ledger.

### ORF-MER-019 — Validation marchand

Le candidat complet passe `WorldFeatureCatalogValidator`, les adaptateurs Foundation/Experience et
le handler de catalogue avant toute mutation.

## Preuves

- portail incrémental réussi en **150,23 s**, résumé dans
  `artifacts/verification/studio-increment/ux4-t-merchant-authoring.summary.json` ;
- build Studio sans avertissement et **27/27 tests ciblés** : Domain 2, RpgKit Domain 20,
  Application 3 et ModuleFormat 2 ;
- smoke Studio global réussi avec sortie native 0 : sélection explicite, wizard, stock/prix,
  trésorerie/achats, biens volés/restrictions, restock/événements, transactions atomiques,
  monnaies/négociation/marché, services/interface/audit, validation, dry-run, gardes
  source/approbation, commande canonique, transaction UI, undo/redo, save/reopen, voisins
  préservés, surface restaurée et workspace temporaire libéré ;
- capture Forward+ FHD inspectée dans
  `artifacts/verification/studio-increment/ux4-t-merchant-authoring-fhd.png` : cinq onglets,
  sélection humaine, fallback canonique isolé, résumé et cinq actions transactionnelles visibles à
  1920×1080, sans UUID, JSON ni clipping bloquant ;
- la matrice atteint 332 routes `functional` et 385 `planned`. Le portail reste volontairement
  `full_gate=false` : il qualifie le lot Marchands, pas la totalité du produit.

## Préservation et limites honnêtes

- Forgotten Forest et la Crypte n’authorent actuellement aucun marchand dans leur document source ;
  la preuve riche ajoute donc un marchand uniquement dans une copie temporaire explicitement
  étiquetée, puis prouve projet, historique, dirty, sélection et approbation source inchangés ;
- la transaction remplace seulement le marchand ciblé et préserve l’ordre, les marchands voisins,
  toutes les autres familles WorldFeatures et toutes les racines projet ;
- Studio ne déplace aucun objet ou monnaie live, ne persiste aucune session client, ne charge aucune
  interface runtime, n’exécute aucun ForgeScript et n’effectue aucun commerce réseau. Les devis et
  transactions montrés sont des simulations déterministes en mémoire.

## Reproduction

```powershell
$projects = @(
  'tests/Forge.Domain.Tests/Forge.Domain.Tests.csproj'
  'tests/Forge.RpgKit.Domain.Tests/Forge.RpgKit.Domain.Tests.csproj'
  'tests/Forge.Application.Tests/Forge.Application.Tests.csproj'
  'tests/Forge.ModuleFormat.Tests/Forge.ModuleFormat.Tests.csproj'
)
$filter = 'FullyQualifiedName~Merchant|' +
  'FullyQualifiedName~Aggregate_and_workspace_round_trips_preserve_all_world_feature_families|' +
  'FullyQualifiedName~SampleProject_WorldFeatureFixtureIsCanonicalAndRuntimeResolvable'
& .\scripts\Verify-StudioIncrement.ps1 -Page social-content `
  -Evidence ux4-t-merchant-authoring -TestProject $projects -TestFilter $filter `
  -Capture artifacts/verification/studio-increment/ux4-t-merchant-authoring-fhd.png `
  -NoMatrixCheck
```
