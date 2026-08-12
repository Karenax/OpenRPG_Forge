# G8 DOOR-03 — Événements de porte et portail

`PortalUseRequested` expose acteur, destination typée, cause, conditions et veto contrôlé ; un
`PortalUseCompleted` corrélé n’est produit qu’après acceptation. `DoorOpened` et `DoorClosed` sont
émis uniquement après une transition vers un état terminal.

`DoorLocked` et `DoorUnlocked` conservent méthode, acteur, credential, états précédent/courant,
provenance et horodatage.

R081 ferme `PortalUseRequested`, sa complétion corrélée, `DoorOpened` et `DoorClosed` sur le document
canonique, l'interface, les agents et le runtime. Les preuves sont les tests ciblés, la capture
`evidence/r081-door-access-events-ui.png` et les marqueurs Toolset/runtime R081.

R082 ferme désormais `DoorLocked` et `DoorUnlocked` avec méthode, acteur, credential, états et
provenance sur le même document canonique. La capture `evidence/r082-door-lock-events-ui.png`, les
tests ciblés et les marqueurs Toolset/runtime R082 prouvent le parcours auteur → runtime.

La famille Door G8 est complète. La prochaine tranche séquentielle est
**R083 / G8 ENCOUNTER-01**.
