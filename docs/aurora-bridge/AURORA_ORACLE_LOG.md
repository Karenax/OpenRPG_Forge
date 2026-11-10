# Harnais oracle NWN externe

**Schéma :** `forge.nwn-oracle-log@1`.
**Commande :** `forge_cli aurora-oracle-log <log> <m4-report.json> [--output comparison.json]`.
**Portée :** comparer un log de combat NWN local autorisé au rapport M4 Forge. Aucun log NWN réel
n'est fourni ni versionné dans ce dépôt.

## Format

Le fichier est texte, une instruction par ligne, `clé=valeur` séparées par des espaces. Les lignes
vides et celles commençant par `#` sont ignorées. Les clés inconnues sont refusées.

```text
schema value=forge.nwn-oracle-log@1
seed value=2026
winner value=synthetic-fighter
rounds value=2
attack round=1 attacker=synthetic-fighter target=ab_guardian natural=17 total=20 ac=10 hit=true damage=5 hp=3 critical=false concealment=false
```

- `winner value=none` représente une rencontre non résolue ;
- seules les attaques sont comparées ; les déplacements restent un détail d'implémentation Forge ;
- `critical` correspond à la confirmation du critique côté Forge ;
- `concealment` indique un échec de concealment.

## Comparaison

La commande produit un `forge.nwn-oracle-comparison@1` avec `matches`, `divergences` et le détail
par attaque. Elle retourne le code 2 si une divergence existe. Les champs comparés sont `round`,
`attacker`, `target`, `natural`, `total`, `ac`, `hit`, `damage`, `hp`, `critical` et `concealment`.

## Limites

- aucun log NWN réel n'a été collecté : l'oracle reste `not-run` ;
- le format ne couvre ni les sorts, ni les attaques spéciales, ni les sauvegardes, ni les objets ;
- une divergence détectée doit être documentée, jamais masquée par une normalisation silencieuse.
