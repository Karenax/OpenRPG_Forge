# R1-037 — acquisition RenderDoc et contrat de travail GPU

- Date de l'audit d'acquisition : **2 septembre 2026**
- Date du lot contrat/modèle : **3 septembre 2026**
- Base du lot : `1912806fc6e90277cd779a21a2557e6c820d5d44`
- Arbre de base : `4bfb863255dd865432093c5e7291287b8e78dc43`
- Statut R1-037 : **OUVERTE — ACQUISITION OUTIL PASS, CAPTURES NOT RUN**
- Gate 1 : **OUVERTE**

## Résultat borné

La distribution portable officielle RenderDoc 1.46 a été acquise et contrôlée hors de tout worktree
Git. L'acquisition passe ses contrôles HTTPS, tailles, SHA-256, signature OpenPGP détachée, sécurité du
ZIP, comparaison de l'extraction, signatures Authenticode, versions et licence.

Ce lot versionne ADR-0009, le manifeste épinglé et un analyseur Python pur avec fixtures. Il ne lance
ni runtime, ni GPU, ni capture et ne modifie pas le registre. Il ne ferme donc pas `R1-037`.

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
- Visual Valley High, 1920 × 1080, VSync off, caméra/seed/tick fixes ;
- pipelines stables 30 frames puis état figé et reçu atomique ;
- exactement une action `Present` observée par capture, recoupée avec `present_count=1` ;
- deux lancements indépendants pour chacune des météos `clear` et `rain` ;
- replay local `Supported`, pipeline Vulkan exact et mode non dégradé ;
- aucun FPS dérivé de RenderDoc.

## Analyseur pur

`scripts/renderdoc/gate1_renderdoc_model.py` consomme uniquement le schéma normalisé
`forge-gate1-renderdoc-actions/v2`. Il ne charge pas RenderDoc et ne touche ni au GPU, ni au registre,
ni au runtime. Ses tests couvrent le parcours des markers/`MultiAction`, l'absence de double compte,
les trois topologies admises, l'instanciation explicite, le comptage réel des `Present`, les comptes
divisibles et les rejets fermés.

Le format d'enveloppe du lock reste `forge-gate1-renderdoc-lock/v1`. Le payload normalisé, modifié de
façon incompatible par ces deux invariants, est explicitement monté à
`forge-gate1-renderdoc-actions/v2` et sa sortie à `forge-gate1-renderdoc-analysis/v2`.

Commande exécutée depuis la racine du worktree :

```powershell
python -S -B -m unittest discover -s scripts/renderdoc -p "test_*.py" -v
```

Résultat : **17/17 tests PASS**. La matrice chargée couvre 25 rejets d'actions et 9 rejets
d'enveloppe de capture, en plus des tests programmatiques de cycle, identifiant dupliqué, overflow de
cumul, arbre sans draw, forme stricte et payload d'erreur sans métriques partielles. `-B` n'a produit
aucun `__pycache__`.

Le smoke CLI pur sur `nominal_nested_actions.json` retourne `PASS` avec 8 actions visitées, 4 draws,
7 instances, 5 triangles, 2 lignes et 15 points. Le parent `MultiAction` marqué `Drawcall` porte des
valeurs volontairement non qualifiantes mais n'est pas compté, ce qui prouve le parcours des seules
feuilles sans double compte. Une feuille indirecte résolue est comptée avec ses valeurs exactes ; les
indirects non résolus sont rejetés. Le smoke observe aussi exactement une action `Present`. Un draw
non instancié au sentinel zéro contribue une instance, tandis qu'un draw explicitement instancié zéro
conserve zéro instance et zéro primitive. Aucun champ FPS n'est émis.

| Artefact suivi | SHA-256 |
|---|---|
| ADR-0009 | `2D82EAD0B7713D9AEE1DC836E7C7E6159D45576BD5E0C2C976264036CC84BB40` |
| `scripts/gate1-renderdoc.v1.json` | `7C9C81A43154E7AFA7CAE7CEA7E18B915E87B032312D5D2A632601C34A8D2BB6` |
| `gate1_renderdoc_model.py` | `74348096DBC5B43D982270519130A42F675C2E3E47B0FE43027A71ECDF38E96B` |
| `test_gate1_renderdoc_model.py` | `A1AF6E2C803C74D9D7E11EBA5A9C874A89B528449C7DF70DC61E6FBB1474A281` |
| `nominal_nested_actions.json` | `E7373B924A521112FBBB99F22107D19AE5A69DB4F9EBA48EFBC8F91A27151048` |
| `action_rejections.json` | `705A3AC2B9F1FC6993F8B3ED9F791D672F1C0D5AD5CE5C7B6E46F0422E9D9123` |
| `capture_rejections.json` | `C5AF8AF8E92FB8A956A07A419F11C326912365AB5495CF7CDE185E5FC388D33C` |
| `scripts/renderdoc/README.md` | `E007E17C93474BC35E8830DCC6B9330A43CE0EF9CE04FC97997842CF0EEB2012` |

## Limites et travail encore dû

| Élément | État |
|---|---|
| acquisition portable, signatures, licence et hashes | **PASS** |
| ADR, manifeste et modèle pur | **AJOUTÉS DANS CE LOT** |
| tests Python purs | **PASS — 17/17, 34 sous-cas de rejet chargés** |
| couche Vulkan RenderDoc enregistrée | **NON — BLOCAGE EXPLICITE** |
| workload runtime déterministe | **NON LIVRÉ PAR CE LOT** |
| orchestrateur QRenderDoc | **NON LIVRÉ PAR CE LOT** |
| captures Vulkan clear A/B et rain A/B | **NOT RUN** |
| replay/analyse de `.rdc` | **NOT RUN** |
| égalité inter-répétitions | **NOT RUN** |
| R1-037 | **OUVERTE** |

La prochaine étape n'est autorisée qu'après intégration du workload et de l'orchestrateur, puis
autorisation séparée de la couche Vulkan. Quatre captures indépendantes, leurs reçus, hashes, replays
et analyses restent indispensables. Ce document n'altère ni les verdicts PresentMon, ni le verdict
global Gate 1, ni la roadmap.
