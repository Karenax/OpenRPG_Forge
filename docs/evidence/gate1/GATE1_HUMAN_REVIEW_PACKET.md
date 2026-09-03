# Gate 1 — paquet de revue humaine séparé et fail-closed

## Portée et frontière de décision

Le paquet [`gate1-human-review-package.v1.json`](gate1-human-review-package.v1.json) prépare une
**revue visuelle humaine partielle**. Il est séparé des ledgers automatisés et ne les modifie pas :
leurs champs `human_review_performed=false`, leurs verdicts techniques et leurs limites restent
intacts.

Le paquet est volontairement fail-closed : il lie une base technique exacte, vingt références
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
`cc6efae0289b10dcf12f97e6bb4d97b3ead29f90`. Le snapshot de préparation qui introduit les preuves
exactes est `aaed16c03965a1ec08a4f044194622f60137353b`. Les quatre ensembles conservent séparément leurs
commits et arbres exacts `4e476b31`, `5eadec7f`, `125ba0b` et Cadle `ea0da625`. Les deux PNG Browser
du premier ensemble correspondent exactement à la baseline logicielle qualifiée ; les autres preuves
restent historiques ou locales. Le paquet courant porte le SHA-256 `fa23ae11bf8e28ddcfb87f0e9955fbe38e7f4fd57c1613ce397105ef14289e27` ; toujours le recalculer avant une revue, car
tout changement d'octets invalide un reçu antérieur.

## Les cinq routes prêtes à la revue

Seules les routes suivantes ont `review_readiness=ready_for_human_review`. « Prête » ne vaut pas
« acceptée » et aucune case de la roadmap n'est fermée par la seule préparation du paquet.

| Route | Artefacts principaux | Points à juger à la résolution originale |
|---|---|---|
| `R1-014` | deux images temporelles Browser exactes et leur ledger | mouvement cohérent dans au moins deux ROIs végétation ; terrain et caméra stables ; stipple, crénelage et aplats acceptables |
| `R1-046` | `native_human_clear` | échelle humaine, matériaux et profondeur lisibles ; absence du défaut depth-only bleu-noir ; cadrage, stipple, silhouettes et aplats acceptables |
| `R1-047` | `native_distant_clear` | LOD et silhouettes lointaines lisibles ; vallée, cheminement et rivière convaincants ; crénelage, bruit et popping acceptables |
| `R1-049` | vues humaine, lointaine, eau clair/pluie et profils Low/High | palette cohérente entre terrain, végétation, roches, berges et eau ; matériaux ni plats, ni plastiques, ni surexposés ; cohérence météo et profils |
| `R1-050` | vues humaine, lointaine, eau claire et High | hiérarchie visuelle et foyer lisibles ; cheminement, profondeur et rivière compréhensibles ; overlay sans masquage des zones jugées |

Le relecteur doit ouvrir les fichiers enregistrés dans le paquet, vérifier leurs hashes avec le
validateur, les examiner sans réencodage ni remplacement, puis consigner des observations concrètes.

## Routes encore soumises à des prérequis

Les six autres routes humaines sont présentes pour rendre leurs blocages visibles, pas pour recevoir
prématurément un verdict favorable :

- `R1-023` : observation instrumentée du pass optique GPU et matrice native/Browser par profil ;
- `R1-025` : exécution GPU de la wetness et séquence Browser équivalente ;
- `R1-026` : exécution GPU du bloom/color grading et matrice Browser Low/High ;
- `R1-048` : replay PresentMon schema 12 qualifiant, acquisition réellement temps réel et revue de
  jouabilité humaine ; le MP4 actuel assemble des checkpoints pausés et ne qualifie pas le stutter ;
- `R1-051` : référence WebGPU reproductible manquante — URL, version ou commit, capture locale, hash
  et protocole ;
- `R1-052` : protocole A/B Forge/Cadle aligné sur la même machine, en 1920 × 1080 High, avec cadrages
  et versions identifiés, coûts séparés par système et verdict humain versionné.

Deux routes purement techniques restent hors du reçu humain :

- `R1-037` attend l'autorisation explicite de la mutation hôte nécessaire à la couche Vulkan
  RenderDoc, puis quatre captures indépendantes ;
- `R1-041` attend un benchmark sur le matériel cible RTX 3060 ou RX 6600 ; la RX 7800 XT du poste de
  développement ne remplace pas cette preuve.

Même si les cinq routes prêtes reçoivent un verdict humain `accepted`, ces prérequis et les autres
conditions de passage de Gate 1 restent dus.

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
FORGE_GATE1_HUMAN_REVIEW_PACKAGE_PASS artifacts=20 evidence_sets=4 exact_baseline_visual_artifacts=2 human_routes=11 review_ready=5 external_verified=false cadle_assets_embedded=0 routes_closed=0 sha256=fa23ae11bf8e28ddcfb87f0e9955fbe38e7f4fd57c1613ce397105ef14289e27
```

Rejouer aussi les seize cas synthétiques, dont quinze refus attendus. Ils testent le contrat du reçu et ne constituent
pas une revue humaine :

```powershell
pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1 -SelfTest
```

Le second marqueur attendu est :

```text
FORGE_GATE1_HUMAN_REVIEW_SELF_TEST_PASS cases=16 synthetic_only=true human_review_recorded=false
```

Sur le poste où les preuves locales actuelles sont disponibles, vérifier les vingt artefacts, y
compris les trois références Cadle in situ :

```powershell
$forgeEvidence = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-023-water-125ba0b-001'
$cadleReference = 'E:\OpenRPG_Forge\Cadle-GateC-Overlay-Probe-A-20260830'

pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1 `
    -VerifyExternal `
    -ForgeExternalRoot $forgeEvidence `
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
ajoutée pour chacune des cinq routes prêtes.

```json
{
  "schema": "forge.gate1-human-review-receipt",
  "schema_version": 1,
  "scope": "human_visual_review_only",
  "package_sha256": "fa23ae11bf8e28ddcfb87f0e9955fbe38e7f4fd57c1613ce397105ef14289e27",
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
les `evidence_set_ids` de la route. Un verdict `accepted` n'est autorisé que pour l'une des cinq
routes prêtes, cite **tous** ses `artifact_ids` enregistrés et conserve `blocking_issues` vide. Tout
autre verdict décisif cite au moins un artefact. Toute revue contient une observation concrète ;
`rejected` et `inconclusive` contiennent au moins un problème bloquant. Le validateur refuse tout reçu
si `-VerifyExternal` et les deux racines externes ne sont pas fournis. Ne jamais utiliser un nom
générique ou attester au nom d'une autre personne.

Une fois le reçu réellement complété et enregistré dans un emplacement local séparé, le vérifier avec
le paquet et les mêmes racines externes :

```powershell
$receiptPath = 'E:\OpenRPG_Forge\.gate1-human-review-local\gate1-human-review-receipt.v1.json'
$forgeEvidence = 'E:\OpenRPG_Forge\.gate1-tools\evidence\r1-023-water-125ba0b-001'
$cadleReference = 'E:\OpenRPG_Forge\Cadle-GateC-Overlay-Probe-A-20260830'

pwsh -NoProfile -File .\scripts\Test-Gate1HumanReview.ps1 `
    -ReviewPath $receiptPath `
    -VerifyExternal `
    -ForgeExternalRoot $forgeEvidence `
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
