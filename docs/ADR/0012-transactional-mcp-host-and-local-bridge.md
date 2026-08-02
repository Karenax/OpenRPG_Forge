# ADR 0012 — Hôte MCP transactionnel et pont local

## Statut

Accepté le 2 août 2026.

## Contexte

OpenRPG Forge doit permettre à un assistant de comprendre un module, préparer des dialogues et
tester des scripts sans lui ouvrir un accès générique aux fichiers, au shell ou au réseau. Les
écritures doivent conserver les garanties déjà utilisées par l'éditeur : révision de base,
validation, dry-run, diff, inverse et audit.

## Décision

- `Forge.McpHost` utilise le SDK C# officiel `ModelContextProtocol` 1.4.1 et son transport `stdio`.
- Les journaux du host sont envoyés sur stderr afin de ne pas corrompre le flux JSON-RPC stdout.
- Les lectures exposent seulement le résumé, la révision et les diagnostics du projet.
- Un dialogue est d'abord soumis sous forme de plan versionné. Le service exécute le handler métier
  en dry-run, conserve le diff et l'inverse, puis exige le `planId` pour appliquer ou réverter.
- ForgeScript expose une API allowlistée, la compilation native, les diagnostics, un test sandboxé
  sous quotas et un plan de liaison à une propriété d'entité sûre.
- Le pont éditeur emploie exclusivement un pipe nommé avec `CurrentUserOnly`. Un fichier de
  découverte local et atomique contient un jeton aléatoire de 256 bits à durée limitée. Chaque
  connexion est authentifiée par comparaison en temps constant et le serveur accepte les
  reconnexions successives. Aucun socket TCP n'est créé.
- L'agent de test lance un exécutable explicitement configuré par le processus hôte, impose un délai,
  vérifie des marqueurs, retourne des diagnostics structurés et conserve un journal d'artefact.
  Aucun outil MCP d'exécution arbitraire n'est exposé.

## Conséquences

- Un client MCP peut inspecter et planifier un module sans contourner le domaine.
- Une mutation échoue proprement si sa révision est devenue obsolète.
- Le pont peut être intégré au cycle de vie de l'éditeur sans ouvrir de port réseau.
- Les plans sont volontairement éphémères dans ce prototype ; leur persistance et les permissions
  interactives seront traitées avant une distribution multi-utilisateur.
