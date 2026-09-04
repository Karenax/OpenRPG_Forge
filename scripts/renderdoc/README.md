# Modèle RenderDoc pur de R1-037

Ce dossier contient uniquement la politique de comptage testable de `R1-037`. Il ne contient pas
RenderDoc, ne charge aucune DLL, ne lance aucun runtime et ne touche pas au registre.

## Interface stable

L'API Python est :

```python
analyze_document(document: Mapping[str, object]) -> dict[str, object]
```

Une entrée acceptée retourne `forge-gate1-renderdoc-analysis/v2`. Toute ambiguïté lève
`ContractError(code, path, message)` ; aucune valeur partielle n'est qualifiante.

La racine `forge-gate1-renderdoc-actions/v2` contient exactement :

```text
schema
capture
actions
```

`capture` contient exactement :

```text
captured_frame_count
present_count
local_replay_support
pipeline_type
degraded
```

Chaque action normalisée contient exactement :

```text
event_id
name
flags
topology
num_indices
num_instances
instanced
indirect
indirect_resolved
active_stages
children
```

L'adaptateur exécuté par QRenderDoc convertit explicitement les enums RenderDoc vers ces noms
canoniques et émet ce schéma v2 exact, notamment le booléen `instanced`. Il ne doit jamais transmettre
une chaîne de repli pour une valeur inconnue. Les
actions indirectes ne sont admises que lorsque leur compte exact a déjà été résolu ; dans ce cas le
flag `Indirect`, `indirect=true` et `indirect_resolved=true` concordent.

Avant ce script, le host lance une copie nonce d'un miroir complet du bundle QRenderDoc 1.46. Son
profil RoamingAppData jetable contient le seed analytics opt-out ; l'adaptateur confirme par
`qrenderdoc.ConfigFilePath("UI.config")` le chemin exact et l'opt-out booléen avant `ExecuteAndInject`.
QRenderDoc 1.46 complète et sauvegarde les valeurs par défaut avant Python : la configuration chargée
ne doit donc pas être comparée octet par octet au seed. Le host conserve les hashes seed/final distincts.
Qt Core est épinglé à 5.15.2. Le profil nonce est supprimé par le host après conservation de la
configuration finale ; le profil réel `qrenderdoc` doit garder la même empreinte.
Le host recoupe aussi les 72 fichiers et 210 237 254 octets du bundle avec le manifeste d'extraction
dont le SHA-256 est épinglé dans Forge. Il refuse tout fichier absent, altéré ou ajouté, tout
chevauchement entre worktree, bundle, sortie et target Cargo, et ne supprime que les répertoires
jetables dont il a acquis la propriété après une création réussie.

Après un échec de l'outil externe, `Invoke-Gate1RenderDoc.ps1` peut reprendre le binaire existant avec
`-ReuseRuntimeSourceCommit <40 hex>` et `-ReuseRuntimeSha256 <64 hex minuscules>` ensemble. Aucun
build n'est lancé dans ce mode. Le commit doit être ancêtre du checkout propre et seuls les documents
et scripts de capture peuvent différer ; tous les fichiers compilés, assets et manifests de build
doivent être identiques. Le hash du binaire est vérifié avant et après les captures. Le reçu distingue
le commit runtime compilé du commit de l'outillage de capture ; les quatre captures restent neuves.

Le champ `instanced` concorde obligatoirement avec le flag `Instanced`. Pour une action réellement
non instanciée, `instanced=false` et `num_instances=0` forment le sentinel normalisé et contribuent
une instance effective. Pour une action instanciée, `instanced=true` conserve exactement
`num_instances`, y compris zéro : un draw instancié zéro reste un draw émis, mais ajoute zéro instance
et zéro primitive. Toute autre combinaison est rejetée.

Les actions conteneurs sont parcourues récursivement et ne sont pas comptées. Seules leurs feuilles
`Drawcall` contribuent aux métriques. Les topologies admises sont `TriangleList`, `LineList` et
`PointList`. Une feuille de dessin expose obligatoirement le stage `Vertex` et éventuellement
`Fragment`, sans autre stage actif.

Le modèle compte séparément chaque action portant le flag `Present`, exige exactement une occurrence
et la recoupe avec `capture.present_count`. Zéro, deux occurrences ou une valeur déclarée incohérente
échouent sans métriques partielles. Les résultats sont bornés à `uint64`. L'analyse ne produit
volontairement aucun champ FPS.

Le même test recoupe désormais les formes exactes du reçu Rust, du signal, du ledger, du manifeste,
de l'adaptateur et des assertions PowerShell. Le reçu fige explicitement les quatre seeds de source,
la requête `AutoNoVsync` et les horloges virtuelle/fixe remises au tick zéro avant les 30 frames de
settle. Il ne présente pas cette requête comme une observation du mode WSI négocié.

## Test pur

Depuis la racine du dépôt :

```powershell
python -S -B -m unittest discover -s scripts/renderdoc -p "test_*.py" -v
```

`-S` évite le chargement de paquets site et `-B` interdit les fichiers `__pycache__`. Les fixtures JSON
sont synthétiques ; elles ne contiennent ni capture `.rdc`, ni donnée ou asset Cadle.
