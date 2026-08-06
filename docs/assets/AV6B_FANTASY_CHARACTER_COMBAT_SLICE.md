# AV6-B — distribution fantasy et animation de combat

Dernière mise à jour : 5 août 2026.

## Résultat

Forgotten Forest ne duplique plus une seule peau de démonstration pour tous les rôles. Le joueur,
Maëlys, les deux rôdeurs et le Gardien emploient cinq silhouettes fantasy distinctes issues du pack
KayKit Adventurers : chevalier, mage, voleur, voleur encapuchonné et barbare. Le même contenu
gouverné est reconstruit dans Creator et dans le runtime.

Le contrôleur partagé couvre désormais six intentions sémantiques : repos, course, saut, attaque,
impact et mort. Les clips ponctuels ne bouclent pas et reprennent ensuite l’état locomoteur. Le smoke
jouable déclenche trois attaques, deux impacts et une mort sur le Gardien, en plus de terminer la
quête canonique.

## Source et immutabilité

- source officielle : `KayKit-Game-Assets/KayKit-Character-Pack-Adventures-1.0` ;
- commit épinglé : `672074b73ba276876a19e8816ecdc5241817ab47` ;
- licence : `CC0-1.0`, texte conservé dans l’archive officielle ;
- archive : 6 147 593 octets, SHA-256
  `00777a7b9811a7c7f8dd3fda4e56604f6a93b33673d64557153f354d677b47af` ;
- stockage : original immuable sous `originals/kaykit/`, extraction sous `normalized/kaykit/` ;
- catalogue borné : un pack, cinq modèles ; le catalogue distribué AV5 reste figé à 605 entrées.

Le téléchargement, la licence, les cinq chemins GLB et le hash de l’archive sont persistés dans
`asset_manifests/catalogs/kaykit_animated_characters.json`. Aucun chemin absolu du poste n’entre
dans le module exporté.

## Contrat des personnages

| Rôle Forgotten Forest | Modèle | Attaque principale |
|---|---|---|
| joueur | Knight | `1H_Melee_Attack_Slice_Horizontal` |
| Maëlys | Mage | `Spellcast_Shoot` |
| rôdeur corrompu | Rogue | `1H_Melee_Attack_Stab` |
| rôdeur encapuchonné | Rogue Hooded | `Dualwield_Melee_Attack_Slice` |
| Gardien des racines | Barbarian | `2H_Melee_Attack_Chop` |

Chaque GLB contient une armature de 41 os et 76 pistes Blender. Le manifeste runtime expose les 69
clips de durée positive ; sept poses statiques restent dans le GLB mais sont exclues du contrat de
lecture. Les propriétés d’entité `runtime.animationIdle`, `Run`, `Jump`, `Attack`, `Hit` et `Death`
permettent à chaque rôle de choisir ses clips sans branche spécifique au modèle.

## Pipeline et preuves

- Blender 4.5.10 réexporte réellement les cinq GLB en préservant skin, armature et pistes NLA ;
- cinq miniatures 512 × 512 et cinq audits qualité 100/100 ont été inspectés ;
- le second authoring réutilise les 28 assets et ne relance pas Blender ;
- le `.orpmod` contient 100 fichiers, 28 manifestes et 50 chunks, puis se valide sans diagnostic ;
- le runtime charge 18 modèles, cinq variantes animées et 76 pistes ;
- le benchmark couvre 28 assets, 72 niveaux LOD et seize budgets réussis ;
- la capture Forward+ rapprochée est
  `artifacts/verification/forgotten-forest-av6b-forward-plus.png`.

## Limites explicites

AV6-B ferme la diversité visuelle et le combat animé sur un rig commun. Il ne prétend pas encore
retargeter une animation entre deux squelettes incompatibles. Restent également le blend tree, le
root motion, l’IK avancée et la synchronisation réseau. La prochaine priorité artistique est la
composition sectorisée de la carte 512 × 512, avec budgets, navigation et revue visuelle renouvelés.
