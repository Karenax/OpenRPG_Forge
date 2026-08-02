# ADR 0003 — ForgeScript fondé sur AngelScript

- Statut : accepté
- Date : 1 août 2026

## Contexte

Les créateurs ont besoin d’un langage proche du C, compilable rapidement et chargé sans recompiler le moteur. Les modules communautaires ne doivent pas pouvoir exécuter de DLL, de code natif ou de script Godot arbitraire.

## Décision

**ForgeScript** est une couche d’API RPG contrôlée fondée sur AngelScript.

- AngelScript est intégré par une GDExtension C++ minimale.
- C# accède au moteur de scripts via `IForgeScriptRuntime`.
- Le MVP expose un service de compilation et d’invocation ; AngelScript n’est pas enregistré comme langage général de Godot.
- L’API ForgeScript utilise des handles opaques et une version explicite.
- Fichiers, réseau, processus, threads, chargement natif, mémoire brute et API Godot ne sont pas exposés.
- Le runtime impose des quotas d’instructions ou de temps, de pile, de mémoire, de créations et de récursion d’événements.
- Les scripts autoritaires s’exécutent côté serveur.

## Conséquences

- Source et bytecode sont conservés avec les versions de VM et d’API.
- La sécurité doit être prouvée par des tests de quotas et d’échec, pas supposée.
- Le spike AngelScript ne commence qu’après validation des fondations C# et du plugin Godot minimal.
