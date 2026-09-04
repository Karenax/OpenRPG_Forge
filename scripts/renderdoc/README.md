# Outillage RenderDoc de Gate 1

Les modèles purs vérifient les comptes `R1-037` et les états liés aux matériaux. Les adaptateurs
`*_qrenderdoc.py` / `gate1_material_replay.py` s'exécutent séparément dans QRenderDoc épinglé.
Le dossier n'embarque aucune DLL ni capture ; les modèles purs ne lancent aucun processus.

## Replay ciblé des matériaux, sans build ni nouvelle capture

`scripts/Invoke-Gate1MaterialReplay.ps1 -RepoRoot <Rust> -CaptureRoot <preuve R1-037>
-OutputRoot <nouveau dossier externe> -RenderDocRoot <bundle épinglé>` rejoue séquentiellement
les quatre RDC existants. Il exige les hashes du reçu R1-037 PASS, les 72 fichiers du bundle,
un profil QRenderDoc nonce avec opt-out, et l'intégrité finale des entrées/profil/Git. Il ne lance
pas Forge, ne compile pas et ne modifie pas le registre. Seuls ses répertoires temporaires sont
supprimés. Une sortie Python absente ou un `failure.txt` échoue même si QRenderDoc retourne zéro.

`python -S -B scripts/renderdoc/gate1_material_model.py <sortie> --output <nouveau reçu.json>`
vérifie les octets des buffers, bindings, chaîne Bloom/resolve MSAA et textures liées. Le statut
`PASS_BOUND_STATE_ONLY` ne qualifie **ni pixel exécuté, ni réversibilité dans une même instance,
ni Browser/Low, ni revue humaine, ni route**. Les disassemblages restent externes ; leur attribution
au WGSL est documentée dans `docs/evidence/gate1/R1_023_025_026_GPU_BOUND_STATE.md`.

Les noms des shaders Release sont supprimés et les bindings WGSL peuvent être remappés : les
identités proviennent de la réflexion de la capture, pas des noms source attendus. Le collecteur
interdit les bytecodes Python **avant** l'import QRenderDoc ; les valeurs infinies non concernées
sont représentées explicitement, avec leurs octets originaux conservés. Aucun seuil GPU n'est
assoupli et aucun résultat de performance n'est déduit.
QRenderDoc initialise aussi deux caches PySide2/shiboken2 avant le script : leurs copies générées
sont préservées et hashées hors du miroir puis retirées de celui-ci avant le contrôle final inchangé
des 72 fichiers épinglés. Aucun autre fichier inattendu n'est admis.

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
