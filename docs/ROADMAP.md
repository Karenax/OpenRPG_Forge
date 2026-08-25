# Roadmap de consolidation

Dernière mise à jour : 25 août 2026.

La phase de création des capacités Aurora est terminée : 717 fonctions sont `Tested` dans 187 lots.
La roadmap ne réénumère plus les anciens gates. Elle conduit le prototype vers un éditeur de RPG
complet, installable et publiable, avec IA/MCP intégré mais facultatif.

## Étape 1 — Base reproductible

Objectif : un checkout propre produit les mêmes résultats localement et dans GitHub Actions.

Sortie : CI et graphe verts, artefacts locaux exclus, tests non dépendants de la cadence du runner,
documentation générée fraîche et portail intégré réussi.

## Étape 2 — Produit compréhensible

Objectif : une seule vision courante et une navigation Creator cohérente.

Sortie : `PROJECT_CONTEXT.md` unique, statuts sans historique contradictoire, neuf espaces principaux,
mode Débutant qualifié, zéro fenêtre parasite au démarrage, un seul outil secondaire actif et édition
manuelle structurée des collections métier.

## Étape 3 — Distribution autonome

Objectif : utiliser OpenRPG Forge sans dépôt source ni scripts de développement.

Sortie : version unique, Creator/runtime autonomes, archive portable, installateur/désinstallateur,
manifestes, hashes, licences, crédits, SBOM et qualification sur environnement Windows propre.

## Étape 4 — Création intégrale d’un module

Objectif : prouver le but produit plutôt que des fonctions isolées.

Sortie : un utilisateur crée, inspecte, modifie et supprime manuellement zones, contenu RPG, assets et
scripts, puis joue, diagnostique, sauvegarde, exporte, installe et recharge un module entier sans
modifier de JSON ni perdre les données non affichées par les formulaires rapides.

## Étape 5 — IA et MCP intégrés

Objectif : accélérer le même parcours sans créer de voie parallèle non gouvernée.

Sortie : contexte borné, proposition, dry-run, diff, approbation, application, test, audit et revert
depuis l’Assistant et le host MCP ; aucune écriture ni commande système arbitraire.

## Étape 6 — Qualité Aurora 2026

Objectif : une interface agréable et deux démonstrations visuellement convaincantes.

Sortie : revue responsive et accessible, Crypte habillée, outils de biome extérieur réutilisables,
éclairage et performances/collisions/navigation vérifiés sur les profils supportés.

## Étape 7 — Multijoueur Internet qualifié

Objectif : prolonger la base TCP directe désormais fonctionnelle en véritable expérience RPG
coopérative autoritaire, exploitable au-delà d'un réseau de confiance.

Base livrée : lanceur hôte/rejoint, listener runtime/headless, contrôle module/protocole, présence,
zone et déplacement des avatars. Sortie restante : intentions de gameplay raccordées, sauvegarde et
reconnexion, transport sécurisé et identité d'hôte, qualification WAN réelle, puis stratégie
explicite pour NAT/relais et découverte optionnelle. Voir `MULTIPLAYER_INTERNET.md`.

## Étape 8 — Candidat public

Objectif : transformer la distribution qualifiée en version traçable.

Sortie : CI verte répétée, changelog, limites connues, tests machine propre, signature ou décision
explicite, tag et release uniquement après autorisation de publication.

Les tâches exécutables et leurs cases d’acceptation sont exclusivement dans `../TASKS_MVP.md`.
