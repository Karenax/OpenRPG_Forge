# ForgeScript

ForgeScript fournit aux créateurs un langage statiquement typé, proche du C, compilable rapidement et chargeable sans recompiler le moteur. Il repose sur AngelScript et n’expose qu’une API RPG versionnée.

## Surface du runtime

Le pont natif minimal doit couvrir :

- compilation et diagnostics ;
- chargement de bytecode versionné ;
- création de contexte ;
- invocation d’événements ;
- suspension et interruption ;
- inspection de pile utile au diagnostic.

C# encapsule le pont derrière `IForgeScriptRuntime`. L’API Godot n’est jamais exposée directement aux modules.

## Sandbox

Sont interdits : fichiers, réseau, processus, DLL, threads, mémoire brute, réflexion native et horloge système non déterministe. Des quotas limitent instructions/temps, pile, mémoire, entités créées et récursion d’événements.

Les scripts de gameplay s’exécutent côté serveur autoritaire. Les effets purement visuels client restent séparés et ne peuvent changer l’état autoritaire.

## Gate

Le spike ForgeScript est le lot 6. Il ne commence qu’après la validation des fondations C#, du monde minimal, du runtime et du gameplay vertical nécessaire à un test réel du boss de la Crypte des Cendres.
