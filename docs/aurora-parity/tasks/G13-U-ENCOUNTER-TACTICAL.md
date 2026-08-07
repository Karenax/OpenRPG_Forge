# G13-U — Rencontres : renforts, visibilité et nettoyage

Features : `ORF-ENC-016`, `ORF-ENC-017`, `ORF-ENC-019`.

## Livré

Alertes, faction, route, délai et plafond gouvernent les renforts. Le mode de visibilité empêche un
spawn devant la caméra selon la règle choisie. Mort, fuite, abandon et unload utilisent une
politique de nettoyage persistante et bornée.

## Preuves

- test : `G13U_EncounterTactical_ReinforcesOutOfViewAndCleansAbandonedActors` ;
- Toolset/runtime : `G13U_ENCOUNTER_TACTICAL_PASS` et `G13U_ENCOUNTER_DIRECTOR_PASS`.

Limite : occlusion et ligne de vue concrètes sont fournies par l'adaptateur 3D.
