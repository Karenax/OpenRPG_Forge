# Licences et provenance des assets du MVP

## Périmètre de la Crypte des Cendres

La couche artistique de la Crypte utilise neuf modèles issus de **KayKit Dungeon Remastered 1.0**
par Kay Lousberg, sous licence **CC0-1.0**. Le détail, la source et le hash de l'archive sont conservés
dans `apps/Forge.Runtime/art/kaykit-dungeon-remastered/CREDITS.md`. La composition est originale OpenRPG Forge.

La couche de gameplay utilise encore :

- des primitives géométriques intégrées au moteur 3D comme volumes physiques invisibles et effets ;
- des matériaux, couleurs, lumières et collisions créés par le code original OpenRPG Forge ;
- `openrpg_forge_splash.png`, visuel original généré spécifiquement pour ce dépôt et détenu par le
  projet OpenRPG Forge ;
- les scripts `ash_warden_phase_two.forge` et `ash_warden_death.forge`, code original du projet.

Ces éléments propres au projet sont distribués sous PolyForm Noncommercial 1.0.0 avec le code du
dépôt. Cette licence ne couvre pas les composants ou assets tiers, qui conservent chacun leur licence
et leurs obligations de notice.

## Composants tiers non artistiques

- AngelScript 2.38.0 est intégré au host natif sous licence zlib. Le texte complet et les hashes sont
  conservés dans `native/ForgeScript/THIRD_PARTY_NOTICES.md`.
- Le SDK C# officiel Model Context Protocol 1.4.1 est consommé comme paquet NuGet sous licence
  Apache-2.0 ; il n'est pas incorporé dans les archives créateur `.orpmod`.
- Zig 0.16.0 sert uniquement de toolchain locale au build natif et n'est pas redistribué dans les
  modules.
- Le moteur 3D .NET est une dépendance de développement/runtime séparée ; ses fichiers ne sont pas
  des assets du module de démonstration.

## Voix de référence de Solaren

Les quatre prises françaises de Maélis sont des sorties WAV synthétiques déterministes créées par les
auteurs du projet et dédiées à CC0-1.0. Leur texte, licence, provenance, paramètres et SHA-256 sont
enregistrés dans `apps/Forge.Runtime/content/solaren/audio/dialogues/voice-assets.json`. Elles signalent
explicitement leur nature synthétique et ne représentent aucune interprète humaine ni voix clonée.

eSpeak NG 1.52.0, voix `fr+f3`, sert uniquement d’outil de build reproductible sous
GPL-3.0-or-later ; son exécutable et ses données ne sont pas redistribués dans Solaren. Les anciens
octets Edge TTS ont été retirés faute de preuve de redistribution enregistrée. La procédure et la
frontière juridique/technique sont détaillées dans [`DIALOGUE_VOICE.md`](DIALOGUE_VOICE.md).

## Règle pour les futurs modules

Tout asset importé devra déclarer source, auteur, licence, URL ou preuve d'achat, modifications et
compatibilité de redistribution avant export public. Un asset sans provenance vérifiable reste un
brouillon local et ne doit pas entrer dans une release.

Depuis G7-13, cette règle est exécutable pour les métadonnées actuellement canoniques. Les profils
Client, DedicatedServer et Shipping refusent avant écriture un module, un blueprint ou une
dépendance requise dont la licence SPDX ou la provenance est inconnue. L’export inscrit les
dépendances approuvées dans le manifest et génère `THIRD-PARTY-NOTICES.txt`, revérifié au chargement.
Les futurs documents d’assets importés, y compris les prises vocales, devront rejoindre le même
validateur dès leur introduction.
