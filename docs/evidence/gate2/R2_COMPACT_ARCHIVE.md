# Archive agrégée compacte — 13 septembre 2026

## Implémenté

Encodage v3 opt-in `ZoneEditSession::compact_archive_bytes`, sans mutation ni
écriture. Le checkpoint de migration est conservé inchangé ; toutes les commandes
sont enregistrées avec le hash du reçu complet (paramètres, inverse, bases avant/
après, révision et hash résultant). La tête terminale est vérifiée par son hash.
La relecture reconstruit les reçus et la timeline Undo/Redo, valide chaque digest,
refuse les doublons et les divergences. Aucun événement n'est supprimé.

Les lectures v1/v2 restent supportées et leur encodage reste inchangé. Une session
ouverte depuis v3 conserve cet encodage à la sauvegarde ; l'export préparé d'une
session ancienne ne la convertit pas implicitement. Bornes inchangées : 4 MiB,
1024 commandes, validation avant mutation. Ce format exige le replay déterministe
du schéma de commandes correspondant ; sa compatibilité doit être conservée lors
des évolutions futures. Pas de nouvelle dépendance, writer ou fichier canonique.

## Vérifié

`cargo test -p forge_project --test zone_session --test zone_regeneration
--test zone_file_session --offline --locked` : 8 PASS (4 session, 3 régénération,
1 persistance existante). Tests compacts : réduction, source inchangée, même
checkpoint/tête/reçus, roundtrip, Undo de régénération puis Redo après réouverture,
retry, digest falsifié, paramètres falsifiés et duplication refusés.
Clippy project/all-targets strict PASS après correction d'un import wildcard et
d'une déclaration placée après une instruction. Aucun lint désactivé.

Audit réel en lecture seule avec l'exemple `inspect_zone_archive` sur
`E:/OpenRPG_Forge/.gate2-evidence/authoring-vegetation-ui-projects/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json` :

- source : 3 888 891 octets ; représentation v3 : 2 026 913 octets ;
- 7 reçus identiques après reconstruction ;
- même tête : `0b6a75e2bd1d0e6c4b2980c58717fe24d4b2ef073edac45d00994216c11e5943` ;
- `lossless=true files_written=0`.

## Non livré / limites

### Raccord natif et panneau ajouté ensuite

`ReviewZoneCompaction` retourne tête/hash, version source, tailles et nombre de
commandes ; `CompactZoneArchive` revalide cette revue et reconstruit l'archive
avant de remplacer la session en mémoire. Pas d'écriture implicite. L'état dirty
tient compte du changement d'encodage même quand la révision et le nombre de
commandes n'ont pas changé. Sauvegarde atomique existante, fermeture refusée avant
save ; mode playtest protégé comme les autres mutations.

Panneau Studio « Taille du journal de zone » codé : examiner, lire les tailles,
convertir explicitement sans sauvegarder. La revue locale est invalidée à la
réponse suivante et le backend refuse un aperçu altéré/périmé. Une archive déjà
v3 est signalée comme telle par le refus explicite d'une conversion redondante.
Le changement d'encodage n'est pas présenté comme une commande Undo de la carte.

Vérifié : 5 tests ciblés host/session PASS avant extraction du helper terrain,
puis host rejoué PASS après correction ; Clippy project/all-targets et Studio
WASM/web strict PASS. Parcours disque du host : revue sans dirty, revue altérée
refusée sans mutation, conversion avec dirty/tête inchangée/Undo disponible,
disque original intact avant Save, Close refusé, Save v3 puis Open avec même tête
et Undo disponible. Corrections de compilation : méthode native exclue du WASM,
extraction des branches terrain et traitement de leurs types d'erreur distincts,
réduction de longueur du test sans retirer d'assertions.

Bouton pas encore lancé/capturé : le candidat graphique 366762df... est antérieur
à ce raccord. La copie utilisateur de test reste v2 ; seul le fixture temporaire
du test host a été sauvegardé en v3 et nettoyé. Aucun processus d'essai restant.

La représentation réduit la répétition des
reçus mais conserve les corrections complètes dans chaque commande Edit : elle
ne rend pas l'historique illimité et ne remplace pas les futurs deltas typés ou
une fermeture explicite de l'historique. Le checkpoint historique est encore un
tableau d'octets JSON. Pas de qualification de performances de replay ou de mémoire.
Gate 2 ouverte ; qualification workspace ADR-0006 due.

## Replay Studio regroupé — 13 septembre 2026

Candidat EXE SHA-256 `975b90334a61da63e1dc1f56eeba26d094ee2ea80e31f18dee4bcb0c21e3d14b`,
build Trunk et shell Tauri réussi. Copie isolée dans
`E:/OpenRPG_Forge/.gate2-evidence/authoring-density-ui-projects/81de8d42-89b9-58b5-b090-406cf4c63d06/authoring.json`.
Panneau ouvert, revue 3 888 891 → 2 026 913 octets / 7 commandes affichée ; conversion
explicite en mémoire marquée non sauvegardée, puis Save et état synchronisé.
Taille disque constatée : 2 026 913 octets. Scène inchangée par la conversion.
Le replay densité/verrou/reseed suivant est enregistré dans R2_VEGETATION_AUTHORING.md.
Archive finale v3 sauvegardée puis réouverte réellement dans le Studio.
La source précédente `authoring-vegetation-ui-projects` reste intacte (SHA-256
`b3d6aa1be6646f494434b59ea62863764414a8091756186b8bac4917d820f643`).
Captures inspectées dans la conversation, pas de PNG exporté. Pas de mesure de
performance du replay ni de qualification workspace supplémentaire.
