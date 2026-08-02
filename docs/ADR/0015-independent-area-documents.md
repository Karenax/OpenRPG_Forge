# ADR 0015 — Documents de zone indépendants et index workspace

## Statut

Accepté le 2 août 2026.

## Contexte

Le format agrégé `project.json` 0.6 contenait toutes les zones du module. Cette représentation était
simple pour le premier prototype, mais elle empêchait l’édition, la validation, le diff et le
chargement d’une zone sans réécrire l’ensemble du projet. Elle ne fournissait pas non plus de
frontière persistante stable pour les transitions entre zones.

Le domaine et les commandes ont cependant besoin d’une vue cohérente de tout le projet afin de
valider les UUID, les blueprints et les références croisées. Remplacer l’agrégat mémoire par des
accès fichiers dans le domaine violerait son indépendance vis-à-vis des adaptateurs.

## Décision

- `ProjectDefinition` reste l’agrégat canonique en mémoire dans `Forge.Domain`.
- Un workspace 0.7 sauvegarde un index `project.json` contenant les métadonnées globales et une liste
  de références de zones.
- Chaque `AreaDefinition` est sérialisée dans un document JSON 1.0 sous `areas/`, avec UUID, slug,
  nom, version de schéma, environnement, dimensions, instances et `PortalLink`.
- Le nom d’un document de zone contient son UUID et un préfixe du SHA-256 de son contenu. Une
  sauvegarde écrit les nouveaux documents avant de remplacer atomiquement l’index ; l’ancien index
  reste donc lisible si l’opération est interrompue.
- Les chemins lus depuis l’index doivent rester relatifs, normalisés et confinés à `areas/`.
- Les workspaces monolithiques 0.1 à 0.6 restent lisibles. Leur prochaine sauvegarde produit le
  format 0.7 sans demander de modification manuelle.
- Un `.orpmod` transporte l’index et les seuls documents de zone qu’il référence. Le validateur
  reconstruit ensuite l’agrégat avant les validations de domaine et le chargement runtime.
- Les snapshots de playtest peuvent conserver une représentation agrégée temporaire : ils sont
  immuables, jetables et ne constituent pas le format auteur.

## Conséquences

- Deux zones peuvent évoluer indépendamment et leurs changements produisent des fichiers et hashes
  distincts.
- Les transitions disposent d’identifiants opaques et de références validables sans dépendre du
  moteur 3D.
- Les écritures interrompues ne rendent pas l’index précédent incohérent.
- Les anciens documents de zone devenus orphelins ne sont pas supprimés pendant la sauvegarde. Une
  future commande explicite de nettoyage pourra les retirer après vérification ; cette prudence
  évite une suppression destructive implicite.
- Les futurs champs de terrain, navigation, audio, éclairage ou streaming devront versionner le
  document de zone et fournir une migration ainsi qu’une fixture historique.
