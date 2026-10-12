# Conversion GLB des programmes source pour WebGPU

`normalize_source_graph_glb.py` corrige uniquement la garde NaN constante des deux
fonctions ABI de décodage des normales prises en charge. Les modes acceptés,
calculs du shader, géométrie, textures compressées, mipmaps, samplers et autres
métadonnées restent inchangés. Une garde inconnue ou une empreinte de shader
incorrecte provoque un refus. Les sorties et les sources existantes ne sont
jamais écrasées ; une seconde normalisation est identique octet pour octet.

```powershell
python -B scripts/hivemind/normalize_source_graph_glb.py `
  --input F:/OpenRPG_ImportWork/mon-lot/source.candidate.glb `
  --output F:/OpenRPG_ImportWork/mon-lot-web/converted.candidate.glb `
  --receipt F:/OpenRPG_ImportWork/mon-lot-web/CONVERSION.v1.json
```

Pour un assembleur, appeler `normalize_glb(pack_glb(document, binary))` avant
l'écriture du dérivé et conserver le reçu retourné. La recette versionnée
`assemble_firepit_glb_v3.py` utilise cette étape ; la recette v2 et ses preuves
restent conservées. Le foyer v3 produit actuellement les mêmes octets que v2,
car son programme ne nécessite pas cette correction.

Les preuves sur F: sont `source-graph-web-guard-converter-reopen-v215` et
`firepit-web-assembly-controller-v216`. Le premier reproduit exactement le GLB
Townsmith exécuté dans le vrai navigateur v183, puis contrôle l'idempotence et
cinq refus. Le foyer v3 est rouvert contre ses sources : neuf nœuds, dix-sept
blobs et quarante instances de flux géométriques, avec six contrôles négatifs.

Cette étape ne remplace pas le lecteur canonique, la validation glTF, la
comparaison Unity/Forge, la validation de placement, les LOD, les effets ou la
vérification Studio/runtime. Elle ne qualifie aucun nouvel asset à elle seule.
L'ancien dessin GPU est réutilisé seulement pour le dérivé Townsmith dont
l'identité intégrale est prouvée ; aucun nouveau rendu GPU du foyer n'est acquis.
