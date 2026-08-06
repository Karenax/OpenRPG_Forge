# ADR-0035 — Installations Asset Vault adressées par contenu

Date : 5 août 2026
Statut : accepté

## Décision

Les packs installés dans l’Asset Vault utilisent un registre JSON versionné indépendant du projet et
des fichiers de catalogue. Chaque modèle ou aperçu est indexé par son SHA-256 dans
`packages/objects/sha256/<préfixe>/<hash>`. Le registre
`metadata/asset-vault-installations.v1.json` relie les packs, assets, rôles de fichiers, sources,
licences et objets partagés.

Une installation ou désinstallation passe par un plan déterministe lié à la révision courante du
registre. Le mode sec est utilisé par défaut. L’application refuse un état devenu obsolète, recalcule
les hashes avant toute écriture et produit un reçu. Une désinstallation sauvegarde le registre avant
mutation et ne supprime que les objets dont plus aucun pack ne dépend.

Les fichiers sous `originals/` et `normalized/` restent immuables. Ils servent uniquement de sources
à vérifier ; les objets installés sont des copies dédupliquées. Les doublons exacts partagent le même
objet. Les doublons proches ne sont jamais supprimés automatiquement.

Les crédits Markdown et texte sont reconstruits déterministement depuis les seuls assets installés.
Ils conservent auteur, source, URL, SPDX, texte d’attribution et caractère obligatoire ou de
courtoisie.

## Conséquences

- aucune base distante ni nouvelle dépendance de production n’est requise ;
- le catalogue expose l’état installé et les IDs de packs sans modifier ses manifests portables ;
- la désinstallation est récupérable par le backup et sûre vis-à-vis des blobs partagés ;
- la mise à jour d’un pack existant reste un workflow séparé, avec migration et remplacement
  explicites ;
- l’installation depuis une source encore `CatalogOnly` reste bloquée jusqu’à son acquisition
  gouvernée.
