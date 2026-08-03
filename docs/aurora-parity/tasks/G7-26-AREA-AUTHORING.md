# G7-26 — Carte, événements et calques

Chaque zone possède maintenant un `AreaAuthoringProfile` schéma v1 regroupant : événements
utilisateur namespacés avec schéma de payload et script ForgeScript, carte orthographique
multi-étages avec repères et brouillard de guerre, et calques imbriqués avec visibilité,
verrouillage, propriétaire et sélection d’entités.

L’écran dédié permet d’ajouter ces objets puis applique le profil via `SetAreaAuthoringHandler`.
Révision, références d’entités, dry-run, patch inverse, audit, undo et sérialisation canonique sont
centralisés. Les anciens projets reçoivent un profil vide valide.

La révélation par joueur/parti, l’export partiel d’un calque et le dispatch runtime ForgeScript
seront branchés sur ces contrats dans RPG Kit/runtime ; le modèle auteur n’est plus bloquant.

Preuves : `SetAreaAuthoringHandlerTests`, tests de sérialisation et suite globale.

La prochaine tranche unique est **G7-27 — audio spatial**.
