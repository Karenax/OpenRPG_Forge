# G8 DOOR-03 — Événements de porte et portail

`PortalUseRequested` expose acteur, destination typée, cause, conditions et veto contrôlé ; un
`PortalUseCompleted` corrélé n’est produit qu’après acceptation. `DoorOpened` et `DoorClosed` sont
émis uniquement après une transition vers un état terminal.

`DoorLocked` et `DoorUnlocked` conservent méthode, acteur, credential, états précédent/courant,
provenance et horodatage.

Preuves : `DoorEventServiceTests`, suite RPG Kit et solution globale.

La famille Door G8 est complète. La prochaine tranche unique est **G8 TRIGGER-01**.
