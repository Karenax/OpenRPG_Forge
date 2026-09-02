# Modèle RenderDoc pur de R1-037

Ce dossier contient uniquement la politique de comptage testable de `R1-037`. Il ne contient pas
RenderDoc, ne charge aucune DLL, ne lance aucun runtime et ne touche pas au registre.

## Interface stable

L'API Python est :

```python
analyze_document(document: Mapping[str, object]) -> dict[str, object]
```

Une entrée acceptée retourne `forge-gate1-renderdoc-analysis/v1`. Toute ambiguïté lève
`ContractError(code, path, message)` ; aucune valeur partielle n'est qualifiante.

La racine `forge-gate1-renderdoc-actions/v1` contient exactement :

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
indirect
indirect_resolved
active_stages
children
```

Le futur adaptateur exécuté par QRenderDoc doit convertir explicitement les enums RenderDoc vers ces
noms canoniques. Il ne doit jamais transmettre une chaîne de repli pour une valeur inconnue. Les
actions indirectes ne sont admises que lorsque leur compte exact a déjà été résolu ; dans ce cas le
flag `Indirect`, `indirect=true` et `indirect_resolved=true` concordent.

Les actions conteneurs sont parcourues récursivement et ne sont pas comptées. Seules leurs feuilles
`Drawcall` contribuent aux métriques. Les topologies admises sont `TriangleList`, `LineList` et
`PointList`. Une feuille de dessin expose obligatoirement le stage `Vertex` et éventuellement
`Fragment`, sans autre stage actif.

`num_instances=0` représente le fallback RenderDoc d'une instance. Les résultats sont bornés à
`uint64`. L'analyse ne produit volontairement aucun champ FPS.

## Test pur

Depuis la racine du dépôt :

```powershell
python -S -B -m unittest discover -s scripts/renderdoc -p "test_*.py" -v
```

`-S` évite le chargement de paquets site et `-B` interdit les fichiers `__pycache__`. Les fixtures JSON
sont synthétiques ; elles ne contiennent ni capture `.rdc`, ni donnée ou asset Cadle.
