# G11-N — Audio spatial, patrouilles inter-zones et chargement

## Périmètre

- `ORF-AUD-010` — émetteurs dispersés par volume, surface, navigation, biome, exclusion et seed ;
- `ORF-AUD-016` — profils de voix localisés, phonèmes, sous-titres, fallback TTS et consentement ;
- `ORF-AUD-017` — API audio typée, états de musique et snapshots de mix ;
- `ORF-WPT-010` — routes de patrouille inter-zones, portail, streaming, ownership et reprise ;
- `ORF-AREA-028` — présentation de chargement image/vidéo/conseil/progression et fallback accessible.

## Résultat livré

- agrégat `AreaExperienceProfile` v1 avec UUID, provenance, validation de licence et hash ;
- scatter déterministe avec filtrage de biome, exclusion et navigation ;
- résolution de voix localisée et TTS refusé sans consentement explicite ;
- événements audio `AudioCueStarted`, `AudioCueCompleted`, `AudioCueInterrupted`, changement de musique
  et snapshots, exposés par un service ForgeScript à instantané copié ;
- curseur de patrouille qui attend le streaming, vérifie l'ownership et reprend au waypoint cible,
  plus décision Network par lease, révision et autorité serveur ;
- validation projet des aires et portails référencés par toutes les étapes d'une route ;
- commande auteur atomique avec dry-run, diff, inverse, undo et redo ;
- fenêtre **Audio, patrouilles & chargement** dans Zones ;
- API MCP en lecture, plan, application et réversion ;
- écran runtime sombre et accessible relié à la progression réelle d'`AreaStreamingCoordinator`,
  respect du temps minimal et du mode mouvement réduit ;
- lecture d'une ressource importée par `AudioStreamPlayer3D` et fallback diagnostiqué ;
- formats zone `1.8.0`, projet `0.6.16` et workspace `1.10.0`, avec migration des trois workspaces.

## Preuves

- `AreaExperienceProfileTests` couvre invariants, assets et consentement ;
- `AreaExperienceRuntimeServiceTests` couvre événements, scatter, voix et chargement ;
- `AreaExperienceScriptServiceTests` couvre lecture isolée et mutations runtime typées ;
- `CrossAreaPatrolRuntimeServiceTests` et `WorldAuthorityTests` couvrent attente, reprise et autorité ;
- `SetAreaExperienceProfileHandlerTests` couvre dry-run, application, undo et redo ;
- `AreaExperiencePersistenceTests` couvre migrations et round-trip ;
- `ForgeProjectAgentServiceTests` couvre le plan MCP réversible ;
- smoke Toolset `OPENRPG_FORGE_TOOLSET_G11N_EXPERIENCE_PASS` ;
- smoke runtime `OPENRPG_FORGE_RUNTIME_G11N_EXPERIENCE_PASS` sur la Crypte.

## Limites explicites

- les workspaces fournis migrent avec un profil audio vide : les fichiers audio artistiques doivent
  encore être sélectionnés, normalisés et licenciés depuis le Vault ;
- le lecteur runtime accepte les ressources importées par le projet ; un décodeur direct pour les
  médias placés hors de `res://` n'est pas ajouté ;
- le service de patrouille est exécutable et validé, mais la Crypte mono-zone ne contient pas de
  route inter-zone persistée ; une carte multi-zone artistique devra en fournir une ;
- la synchronisation distribuée complète attend une session Network Beta active ;
- ce lot ne constitue pas encore la composition artistique finale de la carte 3D.

## Critère de passage

G11-N est fermé lorsque tests ciblés, smokes Toolset/runtime, migrations, tracker, graphe et
`scripts/Verify.ps1` sont verts sur le même état Git.
