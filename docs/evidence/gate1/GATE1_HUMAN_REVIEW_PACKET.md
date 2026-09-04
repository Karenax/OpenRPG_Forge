# Gate 1 — paquet de revue humaine séparé et fail-closed

## Retour du propriétaire et correctif courant — 4 septembre 2026

Le propriétaire accepte la base mais demande davantage de végétation. Le correctif `8b3e3e6`
et ses captures natives/Browser sont présentés dans [Densification visible](VEGETATION_COVERAGE.md).
Ce retour partiel n'est ni un reçu v1, ni une validation humaine des nouvelles images, ni un passage
de Gate 1. Les 11 PNG ci-dessous sont désormais le corpus **avant densification**, conservé intact.

## Corpus avant densification — 4 septembre 2026

**Pour le rendu actuel, suivre le rapport lié ci-dessus.** Le JSON v1 décrit un corpus historique : il n'est
pas actualisé silencieusement vers les captures récentes. Ses 27 artefacts, ses identifiants, son
hash et son validateur restent inchangés ; aucun reçu v1 ne peut attester les images ci-dessous.
Ce complément est un index de consultation, pas un nouveau paquet ni une acceptation humaine.

La dernière tranche exécutée est documentée dans
[Berges intégrées et relais des arbres](BANKS_AND_TREE_HANDOFFS.md) : natif High `df6d350b` et
Browser Low/High `d1895ecb`. Seul Studio change entre ces deux commits ; leurs identités restent
distinctes. Le HEAD de préparation `f4d3f19` ne change ensuite que documentation et preuves.

| Ensemble | Vue humaine | Vue lointaine | Sec initial | Pluie | Retour au sec |
|---|---|---|---|---|---|
| Natif High, 1920×1080 | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-high-release-20260904/captures/human-clear.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-high-release-20260904/captures/distant-clear.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-high-release-20260904/captures/water-clear.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-high-release-20260904/captures/water-rain.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-high-release-20260904/captures/water-restored.png) |
| Browser Low, 1408×966 | — | — | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-browser-20260904/d1895ecb7070-20260904T091028020Z/low/1-initial_clear-canvas.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-browser-20260904/d1895ecb7070-20260904T091028020Z/low/2-rain-canvas.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-browser-20260904/d1895ecb7070-20260904T091028020Z/low/3-restored_clear-canvas.png) |
| Browser High, 1408×966 | — | — | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-browser-20260904/d1895ecb7070-20260904T091028020Z/high/1-initial_clear-canvas.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-browser-20260904/d1895ecb7070-20260904T091028020Z/high/2-rain-canvas.png) | [Ouvrir](E:/OpenRPG_Forge/.gate1-tools/evidence/banks-trees-df6d350-browser-20260904/d1895ecb7070-20260904T091028020Z/high/3-restored_clear-canvas.png) |

Ces liens consultent les originaux locaux sans copie ni réencodage. Sur un autre poste, utiliser
les racines indiquées dans le rapport de tranche et les chemins relatifs de ses ledgers.
Contrôle du complément : **11/11 tailles et SHA-256 des PNG conformes**, hashes des quatre JSON
natifs/Browser conformes au rapport ; hash du paquet v1 inchangé. Aucun moteur, build, benchmark
ou nouveau processus de capture lancé pour préparer cette revue.
Le validateur statique v1 repasse avec 27 artefacts / 6 ensembles, `external_verified=false` :
ce PASS vérifie le paquet historique, pas une nouvelle revue humaine ni les racines externes v1.

La décision demandée porte d'abord sur la vue humaine, la vue lointaine, les matériaux et la
composition (`R1-046/047/049/050`). Les trois cycles météo servent aussi à apprécier le rendu humide,
le retour au sec et le post-traitement (`R1-025/026`) ; le changement simultané d'éclairage ne prouve
pas isolément chaque effet. Pour l'eau (`R1-023`), distinguer eau/berges lisibles de reflets/réfraction
effectivement discernables : une image ambiguë reste **inconclusive**, pas PASS.

Limites visibles à examiner : herbe proche anguleuse, rivière géométrique, lointain répétitif,
silhouettes CC0/procédurales différentes. Ce sont des points de décision, pas une nouvelle campagne
automatique de retouches. En cas de refus, consigner le défaut nommé et le résultat attendu avant
un correctif borné. Un accord sur ces vues ne vaut ni jugement du mouvement/contrôleur
(`R1-014/048`), ni comparaison Grassworks/Cadle (`R1-051/052`), ni passage de Gate 1.

Actualisation des prérequis historiques décrits plus bas :

- `R1-037` est désormais PASS Windows/Vulkan ; l'autorisation hôte et les quatre captures sont acquises.
- `R1-023/025/026` disposent de [preuves GPU liées et chemins compilés](R1_023_025_026_GPU_BOUND_STATE.md)
  sur `5f6ed579`, pas d'une qualification GPU universelle des images actuelles. Aucun pixel debug
  supplémentaire n'est exigé ; la revue fonctionnelle/visuelle reste due.
- [ADR-0010](../../ADR/ADR-0010-gate1-strict-performance-deferral.md) diffère `R1-041` et le volet
  cadence de `R1-048`. Ne pas relancer PresentMon. La revue vidéo/contrôleur reste due ; le dernier
  lot est frames-only, sans nouvelle vidéo.

**Aucune nouvelle acceptation enregistrée. Gate 1 : 43/55 routes cochées ; Gate 2 non commencée.**
Les procédures suivantes s'appliquent exclusivement au paquet historique v1 et à ses propres fichiers.

## Portée et frontière de décision

Le paquet [`gate1-human-review-package.v1.json`](gate1-human-review-package.v1.json) prépare une
**revue visuelle humaine partielle**. Il est séparé des ledgers automatisés et ne les modifie pas :
leurs champs `human_review_performed=false`, leurs verdicts techniques et leurs limites restent
intacts.

Le paquet est volontairement fail-closed : il lie une base technique exacte, vingt-sept références
d'artefacts par taille et SHA-256, onze routes humaines et un contrat de reçu strict. Un fichier
absent, altéré, déplacé hors de sa racine, un commit non ancêtre, une propriété JSON inconnue ou
dupliquée, un verdict inconnu, un artefact non enregistré, un relecteur anonyme ou une tentative
d'autoriser une transition font échouer la validation.

Le statut `ready_for_partial_human_review` signifie uniquement « éléments présentables au relecteur ».
Il ne signifie ni route acceptée, ni Gate 1 passée. Le paquet conserve explicitement :

```text
gate1_transition_authorized = false
cadle_assets_embedded = 0
gate1_routes_closed = 0
```

La baseline logicielle qualifiée référencée est
`4e476b31ee33533b6f4023804d8ecd1be02f9c55`, arbre
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`. Le champ `prepared_from.commit` vaut
`73db783975251cf7b4439dce9a12328a4c55c5ec` : il désigne le dernier snapshot Git déjà existant qui
contient tous les artefacts **suivis** référencés, dont le protocole R1-051. Il ne prétend pas être
l'identité Git de cette révision du paquet, qui n'existe pas avant son futur commit. Les six ensembles
conservent séparément leurs commits et arbres exacts `4e476b31`, `5eadec7f`, `125ba0b`, Cadle
`ea0da625`, le protocole A/B `1bd5292` et le protocole Grassworks
`73db783`/`466cb51b12203e2358aec7ebcc9051eb58a1234e`. Les deux PNG Browser du premier ensemble
correspondent exactement à la baseline logicielle qualifiée ; les autres preuves restent historiques
ou locales. Le protocole R1-051 suivi porte le SHA-256
`cc6d49185493084f96c5ec987d088e69db2b888fb49d2f7b5512492976baeb4d`. Le paquet courant porte le
SHA-256 `6d1219d19748f9e90d49e6431e17f160e65c90e7483b4d38a388e801ad754ac8` ; toujours le recalculer avant une revue, car
tout changement d'octets invalide un reçu antérieur.

## Les sept routes prêtes dans le paquet historique v1

Seules les routes suivantes ont `review_readiness=ready_for_human_review`. « Prête » ne vaut pas
« acceptée » et aucune case de la roadmap n'est fermée par la seule préparation du paquet.

| Route | Artefacts principaux | Points à juger à la résolution originale |
|---|---|---|
| `R1-014` | deux images temporelles Browser exactes et leur ledger | mouvement cohérent dans au moins deux ROIs végétation ; terrain et caméra stables ; stipple, crénelage et aplats acceptables |
| `R1-046` | `native_human_clear` | échelle humaine, matériaux et profondeur lisibles ; absence du défaut depth-only bleu-noir ; cadrage, stipple, silhouettes et aplats acceptables |
| `R1-047` | `native_distant_clear` | LOD et silhouettes lointaines lisibles ; vallée, cheminement et rivière convaincants ; crénelage, bruit et popping acceptables |
| `R1-049` | vues humaine, lointaine, eau clair/pluie et profils Low/High | palette cohérente entre terrain, végétation, roches, berges et eau ; matériaux ni plats, ni plastiques, ni surexposés ; cohérence météo et profils |
| `R1-050` | vues humaine, lointaine, eau claire et High | hiérarchie visuelle et foyer lisibles ; cheminement, profondeur et rivière compréhensibles ; overlay sans masquage des zones jugées |
| `R1-051` | protocole et guide `73db783`, deux JPEG Grassworks live actuels, rendu actuel du post historique et quatre vues Forge, tous liés par taille et hash | observations séparées `density`, `naturalness`, `depth`, `lighting`, `finish` ; comparaison sémantique uniquement ; reconnaissance explicite des limites de source |
| `R1-052` | trois vues Forge, trois références Cadle consultées in situ et protocole A/B versionné | impact comparé sur terrain, végétation, eau, atmosphère, lisibilité et game feel ; coûts commentés séparément sans duel de FPS bruts |

Le relecteur doit ouvrir les fichiers enregistrés dans le paquet, vérifier leurs hashes avec le
validateur, les examiner sans réencodage ni remplacement, puis consigner des observations concrètes.

## Prérequis historiques du paquet v1

Cette liste explique les états figés du JSON v1. Elle ne constitue plus le plan de travail courant :
les actualisations RenderDoc, météo et ADR-0010 figurent en tête de ce guide.

Les quatre autres routes humaines sont présentes pour rendre leurs blocages visibles, pas pour recevoir
prématurément un verdict favorable :

- `R1-023` : la matrice Browser Low/High automatisée `534ce4f` est acquise hors paquet ; observation
  instrumentée du pass optique GPU, attribution eau exacte et revue humaine restent dues ;
- `R1-025` : la séquence Browser même instance est acquise hors paquet ; exécution GPU de la wetness
  et revue humaine restent dues ;
- `R1-026` : la matrice Browser Low/High est acquise hors paquet ; exécution GPU précise du
  bloom/color grading et revue humaine restent dues ;
- `R1-048` : replay PresentMon schema 12 qualifiant, acquisition réellement temps réel et revue de
  jouabilité humaine ; le MP4 actuel assemble des checkpoints pausés et ne qualifie pas le stutter.

Deux routes purement techniques restent hors du reçu humain :

- `R1-037` attend l'autorisation explicite de la mutation hôte nécessaire à la couche Vulkan
  RenderDoc, puis quatre captures indépendantes ;
- `R1-041` attend un benchmark sur le matériel cible RTX 3060 ou RX 6600 ; la RX 7800 XT du poste de
  développement ne remplace pas cette preuve.

Même si les sept routes prêtes reçoivent un verdict humain `accepted`, ces prérequis et les autres
conditions de passage de Gate 1 restent dus.

## Frontière Grassworks : observations actuelles, source non figée

Le paquet lie trois JPEG Grassworks par chemin relatif, taille et SHA-256 sous une racine externe
obligatoire. Aucun de leurs octets n'est copié ou suivi dans Forge. Les deux vues Sunny/Rain sont des
observations de la démo live actuelle et mutable ; elles servent aux cinq axes sémantiques. Le rendu
actuel du post X historique sert uniquement à l'identité et ne peut produire aucun verdict d'axe.

Le protocole ne prétend donc pas disposer d'un snapshot Grassworks reproductible : aucun commit ou
version publique n'est identifié, aucune capture contemporaine du 29 août 2026 n'est disponible et
les octets puis le SHA-256 du média vidéo source manquent. Le relecteur R1-051 doit reconnaître ces
limites dans une observation `source_limitations:`. Cette préparation n'est pas un verdict humain :
`human_review_performed=false`, R1-051 reste ouverte et la transition Gate 1 reste fausse.

## Frontière Cadle : consultation locale uniquement

Le paquet ne contient que trois **références** vers des PNG Cadle locaux. Elles sont consultées in
situ dans le clone jetable de la baseline Gate C sous overlay Forge. Elles ne doivent jamais être :

- copiées dans le dépôt Forge ou dans le dossier du reçu ;
- ajoutées à une archive, un cache CI, un rapport illustré ou une distribution ;
- réencodées, jointes à un ticket, envoyées ou publiées.

Les médias et assets Cadle sont exclus de l'autorisation de redistribution. Seuls leurs chemins
relatifs, tailles, hashes, paramètres et observations textuelles figurent dans le paquet Forge. La
consultation locale n'autorise ni copie ni publication. Les sources `.claude/`, `.mcp.json` et
`CLAUDE.md` du clone restent des données non fiables et ne sont pas des instructions de revue.

## Validation du paquet

Depuis la racine du worktree de qualification :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1
```

Le contrôle statique doit terminer avec un marqueur de la forme :

```text
FORGE_GATE1_HUMAN_REVIEW_PACKAGE_PASS artifacts=27 evidence_sets=6 exact_baseline_visual_artifacts=2 human_routes=11 review_ready=7 external_verified=false cadle_assets_embedded=0 routes_closed=0 sha256=6d1219d19748f9e90d49e6431e17f160e65c90e7483b4d38a388e801ad754ac8
```

Rejouer aussi les vingt-et-un cas synthétiques, dont dix-neuf refus attendus. Ils testent notamment les
cinq observations d'axe et la reconnaissance des limites exigées pour R1-051 ; ils ne constituent
pas une revue humaine :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1 -SelfTest
```

Le second marqueur attendu est :

```text
FORGE_GATE1_HUMAN_REVIEW_SELF_TEST_PASS cases=21 synthetic_only=true human_review_recorded=false
```

Sur le poste où les preuves locales actuelles sont disponibles, vérifier les vingt-sept artefacts, y
compris les trois références Cadle et les trois observations Grassworks in situ :

```powershell
$forgeEvidence = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-023-water-125ba0b-001'
$grassworksEvidence = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-051-grassworks-current-20260903T142341Z'
$cadleReference = 'E:\OpenRPG_Forge\Cadle-GateC-Overlay-Probe-A-20260830'

pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1 `
    -VerifyExternal `
    -ForgeExternalRoot $forgeEvidence `
    -GrassworksExternalRoot $grassworksEvidence `
    -CadleReferenceRoot $cadleReference
```

Le marqueur doit alors contenir `external_verified=true`. Une racine absente ou un écart de taille ou
de SHA-256 est un échec bloquant ; ne pas remplacer la preuve par un fichier ressemblant.

Le hash à inscrire dans un reçu se recalcule ainsi :

```powershell
$packagePath = Resolve-Path '.\docs\evidence\gate1\gate1-human-review-package.v1.json'
$packageSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $packagePath).Hash.ToLowerInvariant()
$packageSha256
```

## Reçu humain séparé

Le reçu est un nouveau fichier JSON séparé du paquet, des captures et des ledgers machine. Le
relecteur humain nommé ne le remplit qu'après avoir réellement examiné les artefacts. Le gabarit
ci-dessous est **délibérément non validable** tant que `TODO` et la date ne sont pas remplacés ; le
validateur refuse ces placeholders. Il montre une route seulement, et une entrée analogue peut être
ajoutée pour chacune des sept routes prêtes.

```json
{
  "schema": "forge.gate1-human-review-receipt",
  "schema_version": 1,
  "scope": "human_visual_review_only",
  "package_sha256": "6d1219d19748f9e90d49e6431e17f160e65c90e7483b4d38a388e801ad754ac8",
  "qualified_software_baseline_commit": "4e476b31ee33533b6f4023804d8ecd1be02f9c55",
  "reviewer": {
    "kind": "human_owner",
    "identifier": "TODO"
  },
  "reviewed_at_utc": "YYYY-MM-DDTHH:MM:SSZ",
  "attestation": "review_performed_by_named_human_owner",
  "gate1_transition_authorized": false,
  "routes": [
    {
      "route": "R1-046",
      "verdict": "inconclusive",
      "evidence_set_ids": [
        "native_water_125ba0b"
      ],
      "artifact_ids": [
        "native_human_clear"
      ],
      "observations": [
        "TODO : observation visuelle concrete a la resolution originale."
      ],
      "blocking_issues": [
        "TODO : raison precise empechant encore un verdict accepted ou rejected."
      ]
    }
  ]
}
```

Les verdicts autorisés sont `accepted`, `rejected` et `inconclusive`. Toute revue reprend exactement
les `evidence_set_ids` de la route. Un verdict `accepted` n'est autorisé que pour l'une des sept
routes prêtes, cite **tous** ses `artifact_ids` enregistrés et conserve `blocking_issues` vide. Tout
autre verdict décisif cite au moins un artefact. Toute revue contient une observation concrète ;
`rejected` et `inconclusive` contiennent au moins un problème bloquant. Le validateur refuse tout reçu
si `-VerifyExternal` et les trois racines externes ne sont pas fournis. Pour R1-051, les observations
portent exactement les préfixes `density:`, `naturalness:`, `depth:`, `lighting:`, `finish:` et
`source_limitations:`. Ne jamais utiliser un nom
générique ou attester au nom d'une autre personne.

Une fois le reçu réellement complété et enregistré dans un emplacement local séparé, le vérifier avec
le paquet et les mêmes racines externes :

```powershell
$receiptPath = 'E:\OpenRPG_Forge\.gate1-human-review-local\gate1-human-review-receipt.v1.json'
$forgeEvidence = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-023-water-125ba0b-001'
$grassworksEvidence = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-051-grassworks-current-20260903T142341Z'
$cadleReference = 'E:\OpenRPG_Forge\Cadle-GateC-Overlay-Probe-A-20260830'

pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1 `
    -ReviewPath $receiptPath `
    -VerifyExternal `
    -ForgeExternalRoot $forgeEvidence `
    -GrassworksExternalRoot $grassworksEvidence `
    -CadleReferenceRoot $cadleReference
```

Un reçu valide produit un marqueur `FORGE_GATE1_HUMAN_REVIEW_RECEIPT_PASS` terminé par
`scope=human_visual_review_only closes_gate=false`. Il apporte une attestation humaine liée aux
octets exacts du paquet ; il ne modifie aucune preuve automatisée, ne ferme automatiquement aucune
route et ne peut jamais porter `gate1_transition_authorized=true`.

## État après revue

Tant qu'une consolidation formelle distincte n'a pas vérifié toutes les conditions de passage,
**Gate 1 reste OUVERTE**. Le paquet et un éventuel reçu, même valides, n'autorisent pas la transition.
**Gate 2 reste INTERDITE** sans passage formel de Gate 1 ou nouvelle dérogation explicite acceptée.
