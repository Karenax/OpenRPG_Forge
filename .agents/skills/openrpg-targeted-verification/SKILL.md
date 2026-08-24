---
name: openrpg-targeted-verification
description: Planifier et exécuter les validations proportionnées d'une modification OpenRPG Forge. Utiliser pendant une implémentation, après un changement borné, pour choisir les tests d'un lot, ou lorsqu'un smoke Studio, runtime, asset, réseau ou distribution doit rester ciblé. Ne pas utiliser pour une simple lecture de statut sans validation demandée, ni comme preuve automatique de qualité visuelle.
---

# Valider OpenRPG de manière ciblée

Construire d'abord un plan explicable, exécuter ses étapes séquentiellement, puis élargir uniquement
quand le risque ou le jalon l'exige.

## Établir le périmètre

1. Confirmer la racine `E:\OpenRPG_Forge\OpenRPG_Forge`, lire `AGENTS.md` et les sources de vérité
   du lot, puis inspecter `git status --short`.
2. Identifier les fichiers modifiés, feature IDs, route Studio, scénario runtime et artefacts attendus.
3. Pour du C# transversal, vérifier puis interroger le graphe selon `AGENTS.md`. Le planificateur
   exploite les relations de test quand elles existent et retombe sur la suite du projet lorsqu'elles
   sont absentes ou peu fiables.

## Produire le plan avant les tests

Utiliser la logique déterministe du dépôt au lieu de recopier une matrice de commandes dans le skill :

```powershell
& .\scripts\Get-VerificationPlan.ps1 `
  -ChangedPath <fichiers> `
  -FeatureId <identifiants> `
  -Stage Development
```

Examiner `steps`, `reason`, `estimated_cost`, `side_effects` et `warnings`. Un plan vide est une
erreur. Ne pas inventer un filtre si le graphe ou les tests ne prouvent pas le lien : sélectionner la
suite du projet ou un profil plus large.

Choisir le palier correspondant au moment réel :

- `Development` : classes directement liées, build hôte si nécessaire, smoke `Route` ;
- `Lot` : suites complètes des projets affectés et preuve de surface du lot ;
- `Milestone` : profils de sous-système, smoke `Wave`, régénération puis contrôle complet du graphe ;
- `Publication` : portail `Full`, sans publier ni modifier une cible distante sans autorisation explicite.

## Exécuter séquentiellement

```powershell
& .\scripts\Invoke-TargetedVerification.ps1 `
  -ChangedPath <fichiers> `
  -FeatureId <identifiants> `
  -Stage Development
```

Utiliser `-PlanOnly` pour une analyse sans exécution et `-Restore` uniquement sur checkout neuf,
changement de verrou ou dépendances absentes. L'exécuteur impose :

- un seul build ou hôte de test à la fois ;
- un filtre propre à chaque projet ;
- un TRX et un nombre réel pour chaque étape .NET ;
- l'échec si zéro test est exécuté, même si VSTest retourne zéro ;
- l'arrêt au premier échec pertinent ;
- un plan et un résumé sous `artifacts/verification/targeted/`.

## Utiliser les profils au bon moment

Le portail accepte `Core`, `Studio`, `Runtime`, `Assets`, `Network`, `Distribution` et `Full` :

```powershell
& .\scripts\Verify.ps1 -Profile Studio -Page zones -Evidence ux2-nav-l -StudioSmokeScope Route
& .\scripts\Verify.ps1 -Profile Network
```

`Studio` utilise `Route`, `Wave` ou `Full`. `Distribution` vérifie des distributions existantes ; il
ne les reconstruit pas silencieusement. `Full` conserve le portail transversal historique et n'est
pas la boucle quotidienne.

Passer à `Full` après trois à cinq lots liés, dix à vingt routes, une fin de vague, un contrat partagé
important, ou avant publication. Un test ciblé vert ne doit jamais être présenté comme un gate global.

## Rendre compte sans sur-promettre

Rapporter le palier, les étapes réellement exécutées, les comptes `pass/fail/skip`, les smokes et
artefacts observés, ainsi que les validations omises. Distinguer toujours automatisation, capture,
inspection visuelle humaine, distribution locale et publication distante.
