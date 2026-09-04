# R1-037 — acquisition RenderDoc et contrat de travail GPU

- Date de l'audit d'acquisition : **2 septembre 2026**
- Date du lot contrat/modèle : **3 septembre 2026**
- Date de l'alignement runtime/adaptateur/hôte : **3 septembre 2026**
- Base du lot : `1912806fc6e90277cd779a21a2557e6c820d5d44`
- Arbre de base : `4bfb863255dd865432093c5e7291287b8e78dc43`
- Intégration durcie : `347d78264e52363c769e2f2807303d84c65ea946`
- Arbre intégré : `4d6121f3dbc85fb090c1663c8257bee47c64300a`
- Statut R1-037 : **OUVERTE — COUCHE VULKAN ENREGISTRÉE, BUILD PASS, PREMIER LANCEMENT ARRÊTÉ AVANT INJECTION**
- Gate 1 : **OUVERTE**

## Actualisation du 4 septembre 2026

L'utilisateur a répondu « vas y » à la demande distincte d'enregistrement système de RenderDoc.
Le binaire signé épinglé `renderdoccmd.exe vulkanlayer --register --system`, lancé sous UAC,
retourne 0 ; `vulkanlayer --explain` confirme l'enregistrement. Deux valeurs DWORD 0 ont été ajoutées
dans les clés Vulkan ImplicitLayers HKLM x64 et WOW6432Node, pointant vers `renderdoc.json` et
`x86/renderdoc.json` du bundle externe. Les six valeurs OBS/Steam préexistantes sont conservées.
Preuve externe :
`E:\OpenRPG_Forge\.gate1-tools\evidence\r1-037-renderdoc-d6ec361-20260904T060000Z\host-registration.md`.

Le run exact source `d6ec36107565bf26412141c84bdd20fe8d82654e` termine son unique build Release
neuf, puis échoue avant injection dans `clear-01` :
`QRenderDoc disposable UI.config seed changed before injection`. La configuration finale archivée
montre les valeurs par défaut ajoutées par QRenderDoc 1.46 et `Analytics_TotalOptOut=true` conservé.
Cet échec n'est ni un défaut du renderer ni une capture GPU ; aucune métrique n'est publiée.

Le binaire externe `release/forge_runtime.exe` compte 82 668 032 octets ; son SHA-256 est
`e7968d1efb6281be30bdcc5ac36b97f4d378783b488f3a637db4cf1baa79eda5`.
Le garde contrôle désormais le chemin du profil chargé, sa version entière et l'opt-out booléen,
sans confondre seed et configuration étendue. Les **22/22 tests Python PASS** couvrent cette
réécriture, les mauvais types, l'opt-out absent/faux et le mauvais chemin. Le script PowerShell
parse sans erreur. ADR-0009 documente la reprise explicite de ce binaire sans nouveau build :
commit source et hash épinglés, diff des entrées compilées vide, provenance de l'outillage séparée.
Les quatre captures restent dues. Aucun code moteur, audit PresentMon ou asset Cadle n'est modifié.

La reprise outillage `2902353c1400376c872dd0a3c49648a38e87b5eb` franchit le contrôle du profil et
lance le runtime sans rebuild. Elle échoue ensuite avant readiness sur une contradiction du garde
Rust : `dynamically_applied_tier=Some(High)` était rejeté alors que le validateur moteur exige
précisément le tier courant appliqué. Ce champ décrit le dernier preset appliqué, pas une adaptation
de qualité. Le garde de capture exige maintenant `Some(High)`, tout en conservant sélection High,
adaptation désactivée et zéro transition. Un test exerce le vrai plugin qualité avant/après son premier
update et les rejets tier incorrect/adaptation/transition : **5/5 tests Rust ciblés PASS**,
formatage et `git diff --check` PASS. Les trois tests négatifs de reprise
(hash erroné, paramètres incomplets, sources compilées différentes) passent. Cette correction du
contrôle de capture exige un nouveau binaire, construit avec les dépendances déjà compilées.
Les erreurs d'acquisition sont conservées dans leurs dossiers externes distincts, sans compteur GPU
inventé ni modification de seuil. Le moteur de rendu lui-même reste inchangé.

Qualification complète du binaire de tests : **75/75 tests runtime PASS**, Clippy runtime strict
PASS. Pendant le build Release, une revue de l'API épinglée corrige aussi l'énumération des stages
de draw : le pipeline compute Vulkan reste lié séparément, et sa réflexion ne prouve pas son exécution
dans un draw. L'adaptateur interroge uniquement les stages graphiques ; les stages geometry,
tessellation et mesh/task restent détectés et rejetés par le modèle. Le test de cette distinction
passe avec la suite **23/23 Python**. Source primaire :
[RenderDoc 1.46, PipeState::GetShaderReflection](https://github.com/baldurk/renderdoc/blob/v1.46/renderdoc/api/replay/pipestate.inl).
Cette correction de l'analyseur ne modifie pas le binaire Release en cours de construction et ne
constitue pas encore une observation GPU.

Le build Release `0497bb4c0d061c91adf5dcb5a5472c831314c777` passe en 9 min 20 s avec cache,
SHA-256 `a81e2de92b689b26f79fcfe9bfcfb11f302e461332059b5c23b532bfdd46e327`.
La tentative outillage `b986e015493c3dc09ed594f5251b1d88f2832a90` franchit le contrôle High puis
échoue en `freeze_settling` avant toute capture : le hash de végétation incluait
`visibility.observation_frames`, compteur d'audit qui avance avec le main world même à temps de jeu
figé. Le hash neutralise désormais uniquement cet âge dans une copie ; les comptes de visibilité,
projections et autres invariants restent contrôlés. Un test prouve l'égalité du hash quand seul l'âge
change, l'intégrité de la ressource originale et le rejet des changements de comptes/projections.
Tout futur échec de stabilité conserve aussi les deux observations complètes dans son message.
La tentative échouée reste externe sous `r1-037-renderdoc-b986e01-20260904T064400Z` ; aucun `.rdc`
ou verdict GPU PASS n'y a été produit.

Validation de ce dernier correctif : **76/76 tests runtime PASS**, dont les deux régressions réelles
High appliqué et âge d'audit ; Clippy runtime strict, formatage et suite Python **23/23 PASS**.

Les sections qui suivent conservent les acquisitions et préflights antérieurs.

## Résultat borné

La distribution portable officielle RenderDoc 1.46 a été acquise et contrôlée hors de tout worktree
Git. L'acquisition passe ses contrôles HTTPS, tailles, SHA-256, signature OpenPGP détachée, sécurité du
ZIP, comparaison de l'extraction, signatures Authenticode, versions et licence.

La chaîne intégrée aligne désormais ADR-0009, manifeste épinglé, reçu/ledger Rust, adaptateur
QRenderDoc, analyseur Python pur et orchestrateur PowerShell. Les tests Rust et Python passent. Un
préflight réel du host a validé l'inventaire portable complet puis s'est arrêté, comme prévu, avant
build/runtime sur l'absence d'une couche Vulkan RenderDoc déjà correctement enregistrée. Le host n'a
modifié ni registre ni profil utilisateur et aucune capture n'a été lancée. Il ne ferme donc pas
`R1-037`.

PresentMon et RenderDoc appellent le même `validate_governed_gate1_render_state`. La tentative
PresentMon `9d19cde5` a révélé que ce chemin attendait encore la distribution qualité historique
`5/14/15`, alors que l'optique d'eau expose désormais le contrat canonique `5/16/13`. Le correctif
`4b283182` dérive les comptes attendus du contrat exact et son replay réel franchit la readiness avec
69/69 pipelines. Le prochain build RenderDoc devra inclure ce correctif. Le préflight `347d782` reste
valide dans sa frontière : il s'était arrêté avant build/runtime sur la couche Vulkan non enregistrée
et n'a donc jamais rencontré ce défaut pendant une capture.

## Identité de l'acquisition externe

Le dossier local ignoré observé est :

```text
E:\OpenRPG_Forge\.gate1-tools\renderdoc\v1.46\official-win64-acq-001
```

| Élément | Valeur observée |
|---|---|
| schéma du résultat | `renderdoc-portable-acquisition/v1` |
| statut | `PASS` |
| produit / plateforme | RenderDoc 1.46 / Windows x64 portable |
| tag / objet tag | `v1.46` / `102e5410cb596fddc140ef0fdcd4292c495b4b41` |
| commit release | `e4bd23b671d3d5a747ff5221dbe08a63eb6ca200` |
| résultat d'acquisition SHA-256 | `1353372A67D364AF2B33A05833FAE6F8A355378C4202CFD4DA281BC78E97C77F` |
| ZIP officiel SHA-256 | `9CA4D09ECABA2CC791168660D6FC2A7E3D70FE67146E87EC05C5F8DEA70772F7` |
| signature ZIP SHA-256 | `F2773031ED354F5E0C98FD87BDFFDB01CEBA1559776BF7E79377215F23B32077` |
| clé publique SHA-256 | `29062B19E84C72E2F2D3FFA4C0578CF9627E6E710EBFA78DA513B6C33471523A` |
| fingerprint OpenPGP | `1B039DB9A4718A2D699DE031AC612C3120C34695` |
| thumbprint Authenticode | `F91353AE1E90CBEFF15585165D1611C5CE72E346` |
| `qrenderdoc.exe` SHA-256 | `C5E9D3F254D80A7649FBD9EAE1C67618CB19278C603710370C220DE469397277` |
| `renderdoccmd.exe` SHA-256 | `400CB013EA52B9BAA818D69DC0A6341776D2A9560EDAD5BCC6E200FE8C03806D` |
| `renderdoc.dll` SHA-256 | `809DA38E3867D9FD09CC5C30DD5310500DEE75E999166D7A14AD1EF6E9ECA65D` |
| licence embarquée SHA-256 | `BA3B05B7F5BFDB4B4917BF8EA3AA9C4EA4C20FF6522DE9109A6CC982C5C35730` |
| licence | MIT, conservée avec le paquet externe |

L'audit ZIP observe 88 entrées, dont 72 fichiers, pour 210 237 254 octets décompressés. Il confirme
une racine attendue unique, aucune traversée de chemin, aucun lien, aucun doublon insensible à la casse
et la présence de tous les chemins cœur.

Un contrôle local en lecture seule a reparcouru les huit entrées SHA-256 du manifeste contre les
fichiers externes : archive, signature, clé publique, `qrenderdoc.exe`, `renderdoccmd.exe`,
`renderdoc.dll`, licence et résultat d'acquisition passent **8/8**. Le commit release, le fingerprint
OpenPGP et le thumbprint Authenticode concordent également avec le résultat d'acquisition.

## Garanties de non-mutation observées

Le résultat d'acquisition consigne :

- outil en dehors de tous les worktrees Git ;
- couche Vulkan inchangée et non enregistrée ;
- aucun lancement de capture ;
- aucun fichier `.rdc` créé ;
- aucun asset Cadle copié ;
- aucun commit ou push effectué pendant l'acquisition.

Les binaires et la licence restent externes. Le dépôt ne contient ni exécutable RenderDoc, ni DLL,
ni archive, ni asset Cadle.

## Contrat versionné ajouté

Le fichier `scripts/gate1-renderdoc.v1.json` fixe la version, les sources, les hashes, le signataire,
la matrice qualifiable, le workload et les rejets de l'analyse. ADR-0009 impose notamment :

- Windows x64/Vulkan seulement ;
- D3D12 `NOT QUALIFIED` ;
- Browser WebGPU `UNSUPPORTED BY RENDERDOC 1.46` ;
- Visual Valley High, 1920 × 1080, requête `AutoNoVsync`, caméra/seed/tick fixes ;
- pipelines stables 30 frames puis état figé et reçu atomique ;
- seeds de sources explicites, requête `AutoNoVsync` et horloges virtuelle/fixe remises au tick zéro
  avant les 30 frames de settle ; le reçu ne prétend pas observer le mode WSI négocié ;
- exactement une action `Present` observée par capture, recoupée avec `present_count=1` ;
- deux lancements indépendants pour chacune des météos `clear` et `rain` ;
- replay local `Supported`, pipeline Vulkan exact et mode non dégradé ;
- aucun FPS dérivé de RenderDoc.

Les formes exactes du reçu, du signal et du ledger sont versionnées dans le manifeste puis recoupées
par l'adaptateur et l'orchestrateur. Le signal lie les SHA-256 de `qrenderdoc.exe`,
`renderdoccmd.exe` et `renderdoc.dll` ; le host refuse un binaire différent avant le build. Le reçu
final de l'adaptateur et ses quatre objets imbriqués sont également décrits champ par champ dans le
manifeste et validés avant publication.

Le dialogue analytics de premier lancement est fermé par contrat, sans interaction humaine : le
bundle embarque Qt Core 5.15.2, est intégralement recopié dans une vue externe, puis sa copie
`forge-gate1-qrenderdoc-<nonce>.exe` utilise un profil RoamingAppData unique contenant le seed minimal
`rdocConfigData=1` et `Analytics_TotalOptOut=true`. L'adaptateur vérifie le chemin et le hash du seed
avant injection. Le host archive la configuration finale dans le dossier du run, supprime le profil
jetable en `finally` et recoupe l'empreinte du profil réel `qrenderdoc` avant/après sous mutex global.
Le manifeste d'extraction externe est lui-même épinglé dans Forge ; ses 72 fichiers, 210 237 254
octets et SHA-256 sont recoupés sur la source et le miroir. Les racines worktree, RenderDoc, sortie et
target Cargo sont disjointes, le build neuf a une borne de 45 minutes et les nettoyages exigent un
marqueur de propriété acquis seulement après création réussie.

Les deux revues correctives suivantes ferment également les chevauchements bidirectionnels entre les
quatre racines, bornent à dix secondes l'attente post-kill et lient les chemins réels du bundle et de
son inventaire aux chemins épinglés dans le manifeste. Elles sont intégrées respectivement à
`125ba0b8bb1fe04b2b34cae5abc41c26d1919b00` puis
`347d78264e52363c769e2f2807303d84c65ea946`.

## Analyseur pur

`scripts/renderdoc/gate1_renderdoc_model.py` consomme uniquement le schéma normalisé
`forge-gate1-renderdoc-actions/v2`. Il ne charge pas RenderDoc et ne touche ni au GPU, ni au registre,
ni au runtime. Ses tests couvrent le parcours des markers/`MultiAction`, l'absence de double compte,
les trois topologies admises, l'instanciation explicite, le comptage réel des `Present`, les comptes
divisibles et les rejets fermés.

Le format d'enveloppe du lock reste `forge-gate1-renderdoc-lock/v1`. Le payload normalisé, modifié de
façon incompatible par ces deux invariants, est explicitement monté à
`forge-gate1-renderdoc-actions/v2` et sa sortie à `forge-gate1-renderdoc-analysis/v2`.

L'adaptateur émet maintenant réellement `actions/v2`, dérive `instanced` du flag RenderDoc exact et
compte chaque action `Present` au lieu de faire confiance à une constante déclarée. L'orchestrateur
exige réellement `actions/v2` et `analysis/v2`, recalcule le nombre de `Present`, vérifie
`metrics.present_actions == 1` et inclut ce compteur dans la signature de répétabilité. Chaque
analyse doit aussi observer au moins un draw, une instance et un triangle ; les trois booléens du
verdict final sont dérivés de ces résultats au lieu d'être publiés en dur.

Commande exécutée depuis la racine du worktree :

```powershell
python -S -B -m unittest discover -s scripts/renderdoc -p "test_*.py" -v
```

Résultat : **22/22 tests PASS**. La matrice chargée couvre 25 rejets d'actions et 9 rejets
d'enveloppe de capture, en plus des tests programmatiques de cycle, identifiant dupliqué, overflow de
cumul, arbre sans draw, forme stricte et payload d'erreur sans métriques partielles. Trois tests de
chaîne recoupent statiquement les ensembles de champs Rust, manifeste et adaptateur, les schémas v2,
les gardes PowerShell `instanced`/`Present`, puis exercent les rejets de dérive du reçu et du ledger.
Un quatrième exécute les fonctions isolées de l'adaptateur et prouve le booléen d'instanciation ainsi
que le comptage des actions `Present` sans déduplication par identifiant d'événement ; un cinquième
exerce le garde fail-closed du profil QRenderDoc jetable.
`-B` n'a produit aucun `__pycache__`.

La validation Rust ciblée de la chaîne intégrée passe **67/67 tests unitaires** et **1/1 test
d'intégration** de `forge_runtime`. Les durcissements suivants `125ba0b` et `347d782` ne modifient pas
le code Rust ; ils resserrent l'orchestrateur et ses contrats, tests Python et documentation.

Le smoke CLI pur sur `nominal_nested_actions.json` retourne `PASS` avec 8 actions visitées, 4 draws,
7 instances, 5 triangles, 2 lignes et 15 points. Le parent `MultiAction` marqué `Drawcall` porte des
valeurs volontairement non qualifiantes mais n'est pas compté, ce qui prouve le parcours des seules
feuilles sans double compte. Une feuille indirecte résolue est comptée avec ses valeurs exactes ; les
indirects non résolus sont rejetés. Le smoke observe aussi exactement une action `Present`. Un draw
non instancié au sentinel zéro contribue une instance, tandis qu'un draw explicitement instancié zéro
conserve zéro instance et zéro primitive. Aucun champ FPS n'est émis.

| Artefact suivi | SHA-256 |
|---|---|
| ADR-0009 | `F05B89BDEF572A93ED1A0EF7DAAE0ABFDC4501270E5A7D56988AD6A05D2A9446` |
| `scripts/gate1-renderdoc.v1.json` | `C14BAD9189DB3656C70CEAAFC21DE3D5C8FB5C819BD0E473B5F63B64572CA447` |
| `gate1_renderdoc_model.py` | `6575D76F43BD9F3F39E95C7F23C50A1C364850529B37CB00629769C386C57668` |
| `test_gate1_renderdoc_model.py` | `A1AF6E2C803C74D9D7E11EBA5A9C874A89B528449C7DF70DC61E6FBB1474A281` |
| `nominal_nested_actions.json` | `E7373B924A521112FBBB99F22107D19AE5A69DB4F9EBA48EFBC8F91A27151048` |
| `action_rejections.json` | `705A3AC2B9F1FC6993F8B3ED9F791D672F1C0D5AD5CE5C7B6E46F0422E9D9123` |
| `capture_rejections.json` | `C5AF8AF8E92FB8A956A07A419F11C326912365AB5495CF7CDE185E5FC388D33C` |
| `scripts/renderdoc/README.md` | `EB796DE011908336EB3B41FA650F8F419FE256EA6699115B0C33702C9DC9E463` |
| `gate1_renderdoc_qrenderdoc.py` | `ED4FB4DA97852A76669D5451B68BCBE9920B1E3C116281F18AA69BDABDA846DE` |
| `scripts/Invoke-Gate1RenderDoc.ps1` | `403A28F6685F9D9C04954154444F78203CEE0D711360795D6D0355566680FCD7` |
| `apps/forge_runtime/src/gate1_renderdoc_capture.rs` | `78B22F77530DDEC4A46119496CFE4673D82B7EDF9739663A4DFC2DEC371314CA` |
| `test_gate1_renderdoc_contract_chain.py` | `51B6182BA1ABFAFC17DD11F12FA73C757822BAAFF9B802F3B711F0F2CB3DFF28` |
| `apps/forge_runtime/README.md` | `B8AC7459EF2816665238B839FDA7E12384635B18BAAB2AA7A4304C90BFE64EDD` |

## Préflight réel du host au commit intégré

Commande exécutée avec la racine officielle externe, un dossier de preuve neuf et un target Cargo
neuf dédié :

```powershell
pwsh -NoProfile -File scripts/Invoke-Gate1RenderDoc.ps1 `
  -RepoRoot E:\OpenRPG_Forge\.gate1-presentation-profile-fff9251 `
  -OutputRoot E:\OpenRPG_Forge\.gate1-tools\evidence\renderdoc-preflight-347d782-001 `
  -CargoTargetDirectory E:\OpenRPG_Forge\.gate1-tools\cargo-target\renderdoc-preflight-347d782-001 `
  -RenderDocRoot E:\OpenRPG_Forge\.gate1-tools\renderdoc\v1.46\official-win64-acq-001\extract\RenderDoc_1.46_64
```

Résultat borné :

- les 72 fichiers et 210 237 254 octets du bundle concordent avec
  `extracted-file-manifest.json`, SHA-256
  `C2F3D1C4F477F3DDB8B80C4AC2AB9BC72F881F0E95B612EAB40A59B3F3E300B1` ;
- les chemins réels du bundle, de l'inventaire, de la sortie, du target Cargo et du worktree passent
  les gardes de disjonction ;
- le host s'arrête avant build avec le message exact : `The RenderDoc Vulkan layer is not already
  correctly registered; this orchestrator will not alter host registration.` ;
- le reçu `gate1-renderdoc.failure.json` porte le commit `347d78264e52363c769e2f2807303d84c65ea946`
  et le SHA-256 `E99FEA9E92DE493D0C85A3BB1F57D066B29428333509A702B3FA658076EB3B26` ;
- le target Cargo existe mais contient zéro entrée ; aucun profil jetable `forge-gate1-qrenderdoc-*`,
  runtime, QRenderDoc ou fichier `.rdc` n'a été créé ;
- l'empreinte du profil réel `%APPDATA%\qrenderdoc` et l'état d'enregistrement Vulkan restent
  inchangés.

Ce FAIL est un arrêt de sécurité reproductible et non un résultat GPU. L'enregistrement de la couche
Vulkan est une mutation hôte distincte qui exige une autorisation explicite ; l'orchestrateur refuse
de l'effectuer implicitement.

## Limites et travail encore dû

| Élément | État |
|---|---|
| acquisition portable, signatures, licence et hashes | **PASS** |
| ADR, manifeste et modèle pur | **AJOUTÉS DANS CE LOT** |
| tests Python purs et chaîne statique cross-language | **PASS — 22/22, 34 sous-cas de rejet chargés** |
| tests Rust `forge_runtime` | **PASS — 67/67 unitaires + 1/1 intégration** |
| inventaire complet et gardes du préflight réel | **PASS — 72 fichiers, 210 237 254 octets, racines disjointes** |
| couche Vulkan RenderDoc enregistrée | **NON — BLOCAGE EXPLICITE** |
| workload runtime déterministe | **INTÉGRÉ ET TESTÉ — EXÉCUTION DE CAPTURE NOT RUN** |
| adaptateur et orchestrateur QRenderDoc | **INTÉGRÉS ET VALIDÉS HORS GPU — QRENDERDOC/CAPTURE NOT RUN** |
| captures Vulkan clear A/B et rain A/B | **NOT RUN** |
| replay/analyse de `.rdc` | **NOT RUN** |
| égalité inter-répétitions | **NOT RUN** |
| R1-037 | **OUVERTE** |

La prochaine étape exige une autorisation séparée pour enregistrer/corriger la couche Vulkan
RenderDoc sur l'hôte, puis un nouveau run exact-commit. Quatre captures indépendantes, leurs reçus,
hashes, replays et analyses restent indispensables. Ce document n'altère ni les verdicts PresentMon,
ni le verdict global Gate 1, ni la roadmap.
