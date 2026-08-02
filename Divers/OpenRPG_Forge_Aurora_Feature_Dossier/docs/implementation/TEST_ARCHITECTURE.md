# Architecture de tests

## Niveaux

1. domaine C# sans Godot ;
2. persistance et migrations ;
3. commandes/undo ;
4. ForgeScript VM ;
5. adaptateurs Godot headless ;
6. UI ciblée ;
7. réseau multi-client ;
8. sécurité/fuzz ;
9. benchmarks ;
10. scénarios d’auteur et Live Director.

## Convention de preuve

Chaque feature ORF référence un test ou une procédure. Le tracker refuse le statut `Tested` sans evidence path.
