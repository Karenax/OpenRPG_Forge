# AV6-F — retargeting inter-rigs gouverné

AV6-F ferme la dernière case du lot AV6 avec une preuve réelle entre deux squelettes incompatibles,
et non avec deux copies d’un même rig. Le chevalier KayKit de 41 os reçoit le clip de course Kenney
dont la source FBX contient 58 os.

## Contrat livré

La requête versionnée
[`kenney-run-to-kaykit-knight.json`](../../asset_manifests/retarget/kenney-run-to-kaykit-knight.json)
épingle les deux hashes SHA-256, la licence `CC0-1.0`, la provenance et le profil
`kenney58-to-kaykit41-v1`. Ses 19 paires cible/source sont univoques ; le worker refuse les champs
inconnus, les identifiants dangereux, les doublons, les comptes d’os incohérents, une couverture
insuffisante ou des seuils hors politique.

Le pont Blender échantillonne le clip source, transfère rotation locale et mouvement racine mis à
l’échelle, puis bake une action autonome sur le rig cible. L’action est exportée en piste NLA nommée
`run`. Les fichiers du Vault restent immuables.

## Audit réel du 5 août 2026

- source Kenney : 58 os ; cible KayKit : 41 os ; mapping : 19 os, soit 46,34 % de la cible ;
- clip exporté : `run`, images 1 à 17, durée 0,667 seconde à 24 images/s ;
- déformation : 9 poses et 63 216 sommets échantillonnés, zéro coordonnée non finie ;
- mouvement temporel normalisé maximal : `0.841093` ;
- ratio de bornes observé : `0.923082` à `1.088578`, sous le plafond gouverné `3.0` ;
- GLB : 413 616 octets, 7 032 triangles, 41 os, miniature 512 × 512 ;
- round-trip : réimport du GLB dans Blender, action `run` retrouvée et poses distinctes aux images
  1, 9 et 17.

Les artefacts locaux reproductibles sont écrits dans
`artifacts/verification/retarget/` et restent hors Git. Le gate intégré émet
`OPENRPG_FORGE_AV6F_RETARGET_PASS` seulement si le traitement, l’audit de peau, l’immuabilité et le
round-trip passent ensemble.

## Vérification

```powershell
pwsh -NoProfile -File scripts/Verify.ps1
```

Le test unitaire du contrat peut aussi être exécuté séparément :

```powershell
python -X utf8 -m unittest tests.asset_pipeline.test_blender_worker
```

## Limite explicite

Ce lot prouve un profil humanoïde Kenney vers KayKit reproductible. Il ne revendique ni détection
automatique universelle des os, ni correction artistique sans revue pour des anatomies non
humanoïdes. Ajouter un nouveau couple de rigs exige un nouveau profil gouverné et le même audit.
