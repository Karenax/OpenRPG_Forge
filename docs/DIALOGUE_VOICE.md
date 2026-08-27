# Voix de dialogue gouvernées

Dernière mise à jour : 26 août 2026.

Ce document décrit la capacité réellement livrée pour importer, authorer, distribuer et lire une
prise vocale de dialogue. Il ne faut pas la confondre avec le profil de voix/soundset d’une créature,
la voix localisée d’une zone ni le transport vocal multijoueur.

## Parcours auteur

Dans **Dialogues**, la section **Prise vocale gouvernée** permet de choisir un fichier local, une
réplique et son locuteur, puis de renseigner locale, prise, sous-titre, émotion, profil lipsync,
durée, licence SPDX et provenance. **Prévisualiser la voix** ne modifie rien : Forge calcule le
SHA-256, la destination, le diff et l’inverse. **Importer et lier** applique le plan approuvé ;
Annuler/Rétablir et la sauvegarde utilisent ensuite le même historique transactionnel que le reste
du projet.

Formats acceptés :

- MP3 avec MIME `audio/mpeg` ou `audio/mp3` ;
- WAV avec MIME `audio/wav`, `audio/x-wav` ou `audio/vnd.wave` ;
- taille de 1 octet à 64 Mio ;
- licence reconnue par le validateur SPDX, y compris une `LicenseRef-*` explicite, et provenance non
  vide.

Le fichier est publié sous une adresse immuable :

```text
content/audio/dialogues/blobs/sha256/<2 premiers caractères>/<sha256>.<mp3|wav>
```

Le cue canonique conserve ressource, hash, MIME, locale, prise, sous-titre, durée, licence,
provenance et politique de repli. Une annulation retire le cue par sa commande inverse mais conserve
le blob content-addressed : une ressource immuable non référencée est sûre et pourra être collectée
plus tard par un outil dédié.

## Contrôles de bout en bout

La planification et l’application vérifient toutes deux la racine source, les chemins relatifs, les
points de réanalyse Windows, l’extension, le MIME, la structure binaire, la taille, le SHA-256, la
licence, la provenance et une éventuelle collision. L’application recalcule le plan canonique et
refuse un objet de plan dont destination, hash ou taille ont été altérés.

La validation du workspace et de l’archive `.orpmod` répète les contrôles de chemin, hash et
structure MP3/WAV. Les profils Client, Serveur dédié et Publication refusent en plus toute voix
référencée dont la licence ou la provenance est inconnue. Le runtime relit au maximum 64 Mio,
recalcule le SHA-256 et revalide la structure avant décodage. Une ressource absente, traversante,
corrompue ou remplacée n’est jamais lue ; le sous-titre reste affiché lorsque le cue autorise le
repli accessible.

La lecture utilise le bus `Voice`; si ce bus n’existe pas dans un projet moteur ancien, le lecteur se
replie sur `Master`. Une nouvelle réplique interrompt proprement la prise précédente. Les événements
runtime `started`, `completed`, `interrupted` et `failed` restent séparés du fichier moteur décodé.

## MCP

Le host expose trois outils bornés, sans shell ni écriture libre :

- `plan_dialogue_voice_import` reçoit le contrat `DialogueVoiceImportPlanRequest` et retourne
  `planId`, destination, hash, taille, diff, inverse, diagnostics et audit sans mutation ;
- `apply_dialogue_voice_import_plan` exige le `planId`, la révision et les octets encore conformes ;
- `revert_dialogue_voice_import_plan` annule le cue le plus récemment appliqué et conserve le blob
  immuable.

Studio et MCP convergent sur `GovernedDialogueVoiceImportService`,
`SetDialoguePresentationHandler` et les mêmes diagnostics. Aucun fournisseur TTS, compte externe ou
accès réseau n’est nécessaire pour importer ou lire une prise.

## Prises de référence de Solaren

Les quatre répliques françaises de Maélis sont des WAV synthétiques déterministes produits avec
eSpeak NG 1.52.0, voix `fr+f3`. Elles ne représentent ni une interprète humaine ni une voix clonée.
Les anciens clips Edge TTS ont été retirés : aucune preuve suffisante de redistribution de leurs
octets n’était enregistrée dans le dépôt.

`apps/Forge.Runtime/content/solaren/audio/dialogues/voice-assets.json` enregistre la version du
générateur, son installateur et son SHA-256, ses paramètres, les textes, les quatre sorties, leurs
hashes et l’engagement CC0-1.0 des auteurs du projet. eSpeak NG et ses données restent un outil de
build sous GPL-3.0-or-later et ne sont pas redistribués dans le module. Les sorties sont reproductibles
avec :

```powershell
.\scripts\Generate-SolarenDialogueVoices.ps1 -EspeakRoot <dossier-espeak-ng-1.52.0>
```

Le manifeste de provenance est une preuve d’audit ; les cues et les octets hashés restent l’autorité
runtime.

## Frontière de sécurité

Le flux protège les mutations coopératives du Creator et du host MCP : confinement, rejet des
réanalyses, revalidation juste avant publication, écriture temporaire puis déplacement atomique,
collision sans écrasement et révision du projet. Il ne prétend pas résister à un processus local
hostile exécuté sous le même compte qui échangerait un répertoire parent dans la fenêtre résiduelle
entre vérification et ouverture. Une publication no-follow par handle et un verrou interprocessus
restent le durcissement prévu pour ce modèle de menace.

## Vérifications de maintenance

Après toute modification de ce flux :

```powershell
dotnet test tests/Forge.Domain.Tests -c Release --no-restore
dotnet test tests/Forge.ModuleFormat.Tests -c Release --no-restore
dotnet test tests/Forge.Mcp.Tests -c Release --no-restore
dotnet test tests/Forge.Studio.Core.Tests -c Release --no-restore
dotnet run --project tools/Forge.Cli -c Release -- validate apps/Forge.Runtime/content/solaren
```

Un export final doit aussi être validé avec `forge package validate`, puis son lanceur avec
`forge launcher verify`. L’archive Solaren doit contenir les quatre WAV et
`audio/dialogues/voice-assets.json`, sans ancien MP3 Edge TTS.
