# ADR-0058 — Stockage partagé sans perte de la bibliothèque privée

Date : 27 octobre 2026. Décision autorisée par le propriétaire : corriger les
ressources répétées de toute la bibliothèque et adapter Forge pour en conserver
la compatibilité. Implémentation et migration à qualifier séparément.

## Décision

Le cycle de travail, la publication et la clôture sont définis dans
[ASSET_STORAGE_POLICY.md](../ASSET_STORAGE_POLICY.md). La mutualisation physique
des modèles et le retrait des copies de travail sont deux contrôles distincts :
la première ne justifie pas de conserver indéfiniment les sorties E:.

Le Vault conserve ses modèles logiques, IDs, variantes, licences, provenance,
retouches, chemins et empreintes de contenu. Les octets binaires identiques
peuvent être stockés une seule fois dans des fichiers de blocs immuables sur F:.
Les blocs sont regroupés dans des packs bornés, au lieu d'un fichier par bloc.
Le codage est sans perte : brut ou zlib ; aucune quantification ou recompression
avec perte des textures et aucune modification de la géométrie.

Un fichier de modèle compacté porte une enveloppe de stockage versionnée,
explicitement distincte d'un GLB. Le lecteur natif commun restitue le GLB exact
avant toute validation métier, transport Studio/Web, export ou chargement GPU.
Les SHA-256 existants restent ceux des octets logiques. Les paquets distribués
et les exports externes contiennent les vrais GLB autonomes ; ils ne dépendent
pas du Vault local. Une commande de matérialisation permet la réouverture dans
un outil externe qui ne connaît pas cette enveloppe.

Le lecteur s'appuie sur `forge_delivery`, adapter natif déjà partagé par le
Studio et la livraison. Le format ne crée ni moteur, ni crate, ni document métier
canonique supplémentaire. Les manifests de stockage sont des données techniques
fonctionnelles, pas des historiques détaillés de conversion.

## Sûreté et migration

- Confinement explicite au Vault fourni par l'hôte ; aucun chemin de pack libre,
  aucune traversée de jonction, aucune lecture réseau.
- Bornes de manifeste, taille logique, nombre et taille de blocs, offsets et
  allocations ; vérification SHA-256 de chaque bloc et du modèle reconstruit.
- Un pack est publié et vérifié avant de remplacer un original par son enveloppe.
  L'original doit toujours correspondre à la liste figée, taille/date/hash compris.
- Une seule opération de stockage F: à la fois ; index réutilisable et journal
  de reprise, pas de second parcours concurrent. Les packs non référencés ne
  sont jamais purgés aveuglément.
- Qualification sur copies, corruption, chemins sortants, interruptions et
  interopérabilité Python/Rust avant migration effective. Une étape n'est pas
  renommée « terminée » lorsque seuls les outils sont écrits.
- Pas de bascule d'un fichier encore utilisé par un ancien lecteur ou une
  conversion active. Les lecteurs livrés concernés doivent être adaptés avant
  le retrait des octets physiques d'origine.

## Motifs et alternatives

Le ZIP individuel ne gagne que 1,13 % sur un modèle de référence mesuré. La
mutualisation de dix références privées réduit 1,53 Go à 163 Mo avec restitution
exacte ; ce lot n'est pas représentatif de tous les assets qualifiés.
Le gain de la bibliothèque entière doit donc être mesuré, pas extrapolé.

Transformer massivement les GLB en glTF à ressources externes modifierait les
hashes et les contrats de transport. Un ZIP par modèle ne mutualise pas les
ressources. Une archive monolithique complique les lectures partielles et les
reprises. La déduplication du système de fichiers n'est pas disponible sur le
volume exFAT actuel et ne justifie pas de reformater le disque.

## Limites

Une ancienne version de Forge ou un outil externe qui ouvre directement le
fichier compacté ne connaît pas l'enveloppe. Le déploiement des lecteurs et les
exports matérialisés font partie du critère de compatibilité. Les variantes
gardent leurs droits et leur identité même si leurs octets sont partagés.
La qualification visuelle d'un asset ne découle jamais de sa seule compression.
