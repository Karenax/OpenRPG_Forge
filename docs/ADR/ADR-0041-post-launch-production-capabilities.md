# ADR-0041 — Capacités de production post-lancement

## Statut

Accepté — 7 août 2026.

## Contexte

Les tranches G13-AC à G13-AL ferment 27 capacités de gameplay, Live Director, réseau, expérience
Creator, génération média et assurance qualité. Les répartir entre scènes moteur, préférences locales
et services serveur créerait des sources de vérité incompatibles avec le projet, le workspace et le
package `.orpmod`.

## Décision

`ModuleRuntimeExtensionProfile` possède un `PostLaunchCapabilityProfile` v1 composé de cinq profils
typés : gameplay, Live Director, réseau, expérience Creator et automatisation de production. Chaque
profil porte identité stable, schéma, bornes et provenance. Les décisions de mobilité, succès,
intervention, enregistrement, protocole, rechargement, plateforme, génération et soak test sont
pures ou passent par des services autoritaires explicites.

Le Studio post-lancement édite ce contrat sans JSON par une commande révisionnée avec dry-run,
diff, audit et historique. MCP n'expose que la lecture filtrée et les prévisualisations ; la mutation
continue de passer par le plan d'extensions runtime. Le format projet passe à `0.6.33` et le
workspace à `1.27.0`. Les versions précédentes migrent vers un profil désactivé à UUID stables.

## Conséquences

- Le runtime, l'éditeur, MCP et les services serveur consomment le même agrégat canonique.
- Voix, extensions serveur et fournisseurs média restent optionnels, allowlistés et désactivés par défaut.
- Le hot reload est prévisualisé, signé, atomique et réversible ; aucune DLL arbitraire n'entre dans un module.
- Un soak test est une évaluation déterministe de mesures ; le présent lot ne remplace pas une campagne matérielle longue.
