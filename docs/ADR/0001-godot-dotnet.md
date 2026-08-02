# ADR 0001 — Godot .NET comme socle du MVP

- Statut : accepté
- Date : 1 août 2026

## Contexte

OpenRPG Forge doit livrer un moteur, un runtime et surtout un toolset 3D extensible sans reconstruire un éditeur complet. Le socle doit être libre, redistribuable et suffisamment simple à maintenir avec C# et des agents de développement.

## Décision

Le MVP utilise **Godot 4.7.1 .NET** avec C# comme langage principal.

- Le toolset démarre comme addon C# dans l’éditeur Godot standard.
- Le runtime joueur et le premier serveur autoritaire utilisent Godot, le serveur étant exécuté en headless.
- Le viewport, l’inspecteur, les gizmos, l’import glTF, la navigation et l’Undo/Redo natifs sont réutilisés.
- Aucun fork de Godot n’est autorisé avant qu’une tranche verticale complète démontre une limite précise et reproductible.
- La version du SDK .NET est épinglée seulement après vérification de la combinaison réellement installée et compatible.

## Conséquences

- La première livraison peut se concentrer sur le domaine RPG et les workflows de création.
- Le code Godot reste cantonné aux adapters, au toolset et au runtime.
- Stride et O3DE restent des solutions de repli uniquement en cas de blocage mesuré.
- Rust n’entre pas dans le cœur du MVP sans besoin isolé et démontré.
