# G7-27 — Séquences audio auteur

Le profil audio de zone contient maintenant une playlist de ressources, un ordre séquentiel ou
aléatoire déterministe, un intervalle, une variation bornée et un volume relatif. Le planificateur
produit des cues reproductibles à seed identique et borne les délais à zéro.

L’écran Environnement expose ces champs avec les musiques jour/nuit/combat, ambiance et reverb déjà
présentes. L’ensemble reste appliqué par la transaction d’environnement, sérialisé et annulable.

L’aperçu waveform et la lecture effective dépendent de l’adapter audio runtime ; le contrat auteur,
la recherche textuelle des chemins et la planification sont livrés.

Preuves : `AreaAudioSequencePlannerTests`, `SetAreaEnvironmentHandlerTests` et suite globale.

La prochaine tranche unique est **G7-28 — spatialisation et mixage audio**.
