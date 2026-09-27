# ADR-0012 — Dérogation Gate 1 limitée aux mesures GPU du Studio

Date : 2026-09-10  
Statut : **ACCEPTED — autorisation explicite du propriétaire**  
Portée : Gate 1, Studio WebGPU, pile actuelle Bevy 0.19.1 / wgpu 29.0.4.

## Autorisation et décision

Après clarification entre adaptation du renderer et dérogation, le propriétaire a
choisi : « dérogation limitée aux mesures GPU du Studio ».

Les sous-exigences suivantes sont **WAIVED / ACCEPTED**, et ne bloquent plus
la clôture de Gate 1 à elles seules :

| Critère | Partie Studio WebGPU dérogée |
| --- | --- |
| R1-034 | Mesure du temps GPU dans l'overlay. |
| R1-037 | Mesures GPU des draw calls, triangles et instances, y compris leur capture externe exhaustive sur ce backend. |
| R1-038 | Mesure/estimation de mémoire GPU sur ce backend. |

Il s'agit d'une acceptation de mesures manquantes, **pas de tests réussis**, ni
d'instrumentation livrée. Les mesures natives restent exigées et distinctes.
Les critères parents ne sont pas cochés automatiquement : leurs parties non
dérogées et la qualification du candidat final restent à traiter sur leurs preuves.

Cette décision est une exception spécifique Gate 1 ; elle ne prolonge pas
implicitement [ADR-0004](ADR-0004-gate0-draw-call-telemetry-exception.md), dont la
portée Gate 0 demeure inchangée. Elle remplace seulement l'obligation de couverture
des mesures GPU du Studio pour Gate 1.

## Motif et preuves conservées

L'overlay du candidat Studio a effectivement affiché le temps CPU de passe opaque,
mais pas le temps GPU. Le chemin de diagnostic Bevy 0.19.1 exige des timestamps
dans l'encodeur, indisponibles en WebGPU. Aucun compteur global de draws Bevy ni
rapport allocateur WebGPU exploitable n'a été obtenu. Les causes sont distinctes :
l'absence de timestamps n'explique pas à elle seule tous les compteurs manquants.

Le [rapport de clôture](../evidence/gate1/R1_CLOSURE_PROGRESS_20260910.md) conserve
l'observation Studio, les pins du candidat et la capture/relecture GPU native.
Les fichiers de preuve historiques ne sont ni réécrits ni présentés comme PASS
Studio. L'index consolidé à 89 fichiers reste `partial_not_gate_pass`.

## Garanties et exclusions

- Garder `null` et les statuts d'indisponibilité dans les diagnostics ; jamais zéro
  inventé, valeur native substituée ou proxy CPU renommé mesure GPU.
- Conserver les mesures CPU, FPS, frame time, histogramme, p95 et 1 % low existants.
- Ne pas désactiver un contrôle fonctionnel, une erreur shader ou un échec de rendu.
- Aucune dérogation au préchauffage R1-039, à la fluidité/qualification temporelle,
  à la parité fonctionnelle native/Studio, aux profils graphiques ou au rendu.
- Aucun changement à la décision de performance RX 7800 XT, aux revues humaines
  du kit, à la provenance, aux comparaisons et au verdict sur révision exacte.
- Aucun fork Bevy, adaptation bas niveau, commit, push ou passage Gate 2 autorisé.
- **Gate 1 reste OUVERTE** ; aucune acceptation globale de la gate.

## Réexamen

Réexaminer cette exception lors d'un changement de renderer/backend/version
Bevy-wgpu affectant les diagnostics, de la disponibilité d'une solution publique
proportionnée, ou avant d'utiliser une de ces mesures dans une décision produit.
Les gates suivantes ne bénéficient pas automatiquement de cette dérogation.
