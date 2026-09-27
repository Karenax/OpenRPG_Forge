# Proposition — Qualité effective et adaptation

2026-09-06. **Proposition acceptée par réponse explicite du propriétaire ; décision normative
dans ADR-0011. Implémentation en cours, adaptation non activée.** Ce texte ne valide pas Gate 1
et ne change pas les presets authorés.

## Constat vérifié dans le code

`crates/forge_world/src/quality.rs` exige un niveau installé et l'acquittement des
propositions. `crates/forge_engine/src/global_quality.rs` observe sept domaines mais
conserve `full_preset_applied=false` ; eau/environnement sont partiels.
`water/quality.rs` constate une distance SSR non représentée par le composant, une
réfraction écran absente et une réflexion environnement non qualifiée. Le budget SSR,
la teinte et la réflectance ont leurs observations séparées. `environment/quality.rs`
observe le ciel et la texture générée mais conserve le brouillard de distance en fallback.
Ces deux derniers chemins sont relatifs à `crates/forge_engine/src/`.

## Décision nécessaire

1. Conserver le contrat strict et implémenter/qualifier les capacités manquantes avant
   activation de l'adaptation.
2. Autoriser un contrat de **profil effectif dégradé explicite**, distinct du preset
   demandé. L'adaptation devient possible pour un ensemble effectivement installable,
   sans présenter les effets absents comme réalisés.

La seconde voie est désormais approuvée sous `../ADR/ADR-0011-explicit-effective-quality.md`.

## Contrat proposé

Le profil résolu moteur-neutre contient une version, le hash du preset demandé, son
niveau, une signature des capacités de l'adapter, les paramètres effectifs de chaque
domaine, les substitutions et leurs raisons typées, ainsi que son hash déterministe.
C'est un résultat dérivé, jamais une seconde carte canonique.

Substitutions à approuver individuellement :

| Demande | Proposition | Limite conservée |
| --- | --- | --- |
| Réfraction écran | Teinte/profondeur du matériau | Aucun décor réfracté à l'écran |
| Brouillard volumétrique | Brouillard de distance authoré | Aucun volume éclairé qualifié |
| Distance maximale SSR | Capacité absente explicitement signalée | Aucun faux plafond de distance |
| Réflexion environnement | Qualification exigée avant acquittement | Pas de suppression silencieuse des reflets |

Sans substitution approuvée ou capacité qualifiée, le profil reste non résolu : cette
table n'est pas à elle seule un ensemble installable. Les trois substitutions sont autorisées
par ADR-0011 ; leur mise en oeuvre et leur qualification restent nécessaires.

L'acquittement porte sur la séquence, le hash du profil résolu et la génération de
l'adapter. Les sept observations doivent correspondre aux paramètres effectifs et à
cette identité. Readiness GPU séparée : « CPU projeté » ne suffit pas. Caméra ambiguë,
pipeline rejeté ou preuve manquante interdisent l'acquittement. Timeout : adaptation
arrêtée et diagnostic, jamais restauration annoncée sans observation.

`full_preset_applied` conserve son sens strict. Ajouter des états distincts pour la
résolution effective, sa projection CPU et sa readiness GPU. Studio montre le niveau
demandé et les dégradations. Réglages manuels conservés, benchmark fixe sans adaptation.
Aucun changement des seeds, placements, retouches, sources terrain ou collisions.

## Tests et livraison à prévoir après approbation

- Résolution déterministe, sérialisation, capacités inconnues et substitutions refusées.
- Refus des hash/génération/séquence périmés et du simple rapprochement par nom « High ».
- Caméra ambiguë, capacité perdue, pipeline rejeté, timeout : aucun succès fictif.
- Hystérésis/cooldown conservés, priorité aux choix manuels et benchmark fixe.
- Cycles natif/Web avec le même contrat ; sources, retouches et collisions inchangées.
- Mesures et inspection du profil effectif précisément nommé, pas d'équivalence inventée.

Acceptation artistique, GPU cible, parité Tauri et passage de Gate 1 restent requis.
La proposition initiale était documentaire. Après acceptation sous ADR-0011, un résolveur
pur est implémenté dans `forge_project::effective_quality` ; le diagnostic moteur/Studio est
raccordé et le garde-fou pur d'acquittement est ajouté dans `forge_world::quality_application`.
Installation, observations GPU réelles et raccord de l'adaptation restent à réaliser.
Les tests synthétiques du plan et de l'acquittement ne qualifient aucun
profil sur le matériel réel. Voir `../evidence/gate1/R1_EFFECTIVE_QUALITY_PLAN.md`.

## Voie stricte indépendante de l'approbation — inspection du preset Low

Le preset Low actuel ne demande ni SSR, ni réfraction écran, ni brouillard volumétrique.
Il demande une réflexion environnement, une teinte de surface et un ciel avec cubemap 64².
Il est donc incorrect de supposer que tous les niveaux exigent les effets avancés absents.
La première étape stricte est de qualifier les capacités réellement demandées par ce profil,
sans modifier ses paramètres ni activer les substitutions proposées plus haut.

Depuis cette inspection initiale, l'observateur vérifie la disponibilité des images
d'environnement de l'unique caméra active. Le cycle natif de sept captures du 2026-09-06
isole aussi une contribution IBL visible par coupure/restauration de son intensité, sans
changer la réflectance. Voir `../evidence/gate1/R1_WATER_ENVIRONMENT_IMAGE_EVIDENCE.md`.
Cela ne qualifie pas toutes les vues/formats GPU ni le Web ; la teinte reste visuellement
non concluante. Un composant vide ou un test CPU ne suffit toujours pas.

Même cette preuve ne rendra pas Low intégralement qualifié : relever aussi les exceptions
de terrain sous les berges, les limites HLOD/forêt et les autres champs effectifs. Ne pas
remplacer `full_preset_applied=false` par une simple condition « tier == Low ».
L'adaptation exige ensuite chaque destination réellement installable et son acquittement ;
un seul niveau qualifié ne constitue pas un contrôleur adaptatif livré.

Cette inspection fixe le prochain travail strict sans valider les fallbacks, sans changer
les exigences High sur GPU cible et sans ouvrir Gate 2.

## Limite technique vérifiée — réfraction Bevy 0.19.1

Inspection locale du 2026-09-06, sans modification de dépendance ou de shader :

| Contrat Forge | API Bevy examinée | Conséquence |
| --- | --- | --- |
| Réfraction High : 24 pas de ray-march, profondeur maximale 3,2 m ; Ultra : 40 pas, 4 m | `ScreenSpaceTransmission::steps` compte des couches/passes avec copies de texture | Ne pas lui affecter 24/40 : ce n'est ni le même budget ni le même coût |
| Réfraction écran et SSR demandés ensemble en High/Ultra | Transmission en phase `Transmissive3d`, SSR documenté comme nécessitant le rendu différé | Basculer simplement le matériau eau vers la transmission n'établit pas les deux capacités simultanément |
| Distance SSR maximale 64/96/128 m en Medium/High/Ultra | `ScreenSpaceReflections` expose steps, épaisseur, raffinement et fondu écran, pas de plafond métrique | Aucun mapping direct ne satisfait le plafond ; l'épaisseur n'est pas une distance de réflexion |

Sources : `examples/visual_valley/quality-profile.v1.json`,
`crates/forge_engine/src/water.rs` (`bevy_water_optics_capabilities`, matériau opaque/différé),
et sources de la dépendance épinglée sous le registre Cargo local :
`bevy_pbr-0.19.1/src/transmission/mod.rs` (lignes 67–126) et
`bevy_pbr-0.19.1/src/ssr/mod.rs` (lignes 52–135).
SHA-256 des deux fichiers de dépendance :

```text
transmission/mod.rs 4394ad9ba8414da4783371908c72b86054d7e4df3b95ea4a476aca36b196e22a
ssr/mod.rs          4c4e358d62f8fe94b14674653816ad957c06ac1b3fcf0880bfc869e6eb5db9cb
```

Ce constat exclut le raccord naïf des composants comme solution complète ; il ne prouve
pas qu'une extension Bevy ciblée serait impossible. La voie stricte exige désormais une
conception de passe optique bornée et sa preuve native/Web, plutôt qu'un simple réglage.
Aucun fork de Bevy, deuxième renderer ou changement structurel n'est autorisé par ce constat.

Décision désormais obtenue : adopter le contrat effectif sous ADR-0011, sans engager ici
l'extension optique stricte. Cette voie doit indiquer les effets absents et les budgets non garantis, sans
renommer un profil dégradé en High complet ; elle ne ferme pas la revue artistique,
les performances sur GPU cible, la qualification Studio ou Gate 1.
Aucun nouveau build, test GPU ou benchmark revendiqué pour cette inspection documentaire.

## Limite supplémentaire vérifiée — intensité des reflets (2026-09-06)

La preuve SSR indépendante et la preuve IBL établissent la présence des effets, pas le
respect de tous les paramètres authorés. Le diagnostic natif actuel
`r1-native-tint-capability-20260906/native-metrics.json` conserve
`requested_reflection_intensity=0.72` et
`reflection_intensity_status=requested_not_mapped_by_bevy_ssr_component`.
Le document `examples/visual_valley/water.v1.json` demande également 0,72.
`WaterReflectionProfile` accepte une intensité positive jusqu'à 4 ; l'adapter actuel
utilise `WATER_REFLECTANCE=0.36`, indépendamment de cette valeur.

Ne pas convertir ce constat en `Supported` complet au seul motif que des pixels de
reflet ont été observés. Au moment de ce constat, ADR-0011 ne permettait que trois
substitutions : l'intensité n'en faisait pas partie. L'extension acceptée ci-dessous
ne lève pas à elle seule les blocages des capacités de réflexion dans le code actuel.

Inspection des sources Bevy 0.19.1 épinglées :
`src/render/pbr_functions.wgsl:285` calcule F0 = 0,16 × reflectance² ;
`src/render/pbr_lighting.wgsl:544` calcule la réponse environnement par
F0 × F_ab.x + F_ab.y. Multiplier F0 par une intensité ne multiplie donc pas toute cette
réponse par cette intensité. Modifier l'IBL de la caméra affecterait aussi le reste de
la scène. Aucun de ces raccourcis ne constitue une implémentation exacte du gain demandé.

Deux voies ont été présentées pour ce paramètre :

- garder le contrat strict et concevoir/qualifier un gain de réflexion propre au matériau,
  sur les chemins IBL et SSR, sans modifier silencieusement la réflectance ou toute la scène ;
- approuver une quatrième limite explicite pour les profils dégradés : intensité authorée
  conservée dans le document mais non appliquée, réponse physique actuelle utilisée et
  divergence affichée. **Cette seconde voie est maintenant explicitement acceptée** par
  le « oui » du propriétaire, consigné dans l'extension d'ADR-0011 du 6 septembre 2026.
  Elle n'autorise ni `full_preset_applied=true` ni validation de Gate 1.

La clarification contractuelle est obtenue. Reprise suivante de l'objectif : la capacité
`reflection_intensity` et la substitution `reflection_intensity_not_applied` sont ajoutées
au résolveur ; l'adaptateur déclare ce gain indisponible et Studio prépare le texte explicite.
IBL/SSR restent non qualifiés ; aucune modification du shader ou des sources authorées.
La présentation en session réelle et l'installation du plan restent à vérifier, sans
benchmark ou preuve GPU supplémentaire revendiqué. Voir `../evidence/gate1/R1_EFFECTIVE_QUALITY_PLAN.md`.

## Choix distinct — protection des berges et budget terrain (2026-09-06)

Le runtime actuel expose et vérifie 20 chunks protégés sur 64 : les trois variantes LOD
conservent le stride 1, au lieu des strides 2/4 demandés en High pour LOD1/LOD2. Cette règle
existante protège les berges ; elle n'est pas retirée. L'écart est lié au plan par le hash
terrain et les IDs stables, et le diagnostic l'affiche. Les totaux de meshes en cache ne
constituent pas une mesure du coût GPU ; la qualification de performance reste distincte.

Proposition désormais acceptée par le « ok » du propriétaire : autoriser cet écart localisé dans le profil effectif,
conserver les distances et le reste du budget, garder `full_preset_applied=false` et les
critères Gate 1 inchangés. Cet accord distinct est consigné dans l'extension d'ADR-0011
« protection des berges ». Le raccord de cette exception est implémenté dans le résolveur
et l'adaptateur : inventaire courant cohérent requis, refus en mode strict, substitution
explicite sans promotion automatique de la readiness GPU. Validation et réserves dans
`../evidence/gate1/R1_EFFECTIVE_QUALITY_PLAN.md`.
