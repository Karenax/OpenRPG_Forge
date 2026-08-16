# ADR 0045 — Creator autonome Godot .NET

Date : 15 août 2026
Statut : accepté

## Contexte

Le Creator livré comme plugin de l’éditeur Godot possède toutes les familles fonctionnelles du
catalogue, mais son organisation reste dictée par le chrome de l’éditeur et par environ soixante-dix
`Window` spécialisées. L’ADR 0042 a supprimé leur ouverture simultanée, sans résoudre le problème
produit : les parcours de retouche restent fragmentés, les surfaces sont souvent coupées et la vue
3D perd sa place centrale.

L’utilisateur a validé une rupture explicite : conserver Godot 4.7.1 .NET et C#, mais livrer une
application de bureau indépendante de l’éditeur Godot, conçue d’abord pour ouvrir et retoucher un
module existant.

## Décision

- `apps/Forge.Studio` devient la nouvelle surface Creator. C’est un projet Godot .NET exportable dont
  la racine est un `Control`; il ne dépend pas de `EditorPlugin` ni d’`EditorInterface`.
- Le Studio présente une seule fenêtre produit, un Module Hub, huit rubriques stables et trente-six
  pages. Les pages spatiales conservent une vue 3D centrale ; les dialogues, quêtes, scripts,
  diagnostics et réglages utilisent une surface documentaire dédiée.
- Les onglets représentent des documents ouverts, jamais des familles de fonctions ni des fenêtres
  du système d’exploitation.
- La sélection, la zone active, la caméra, la navigation, l’historique, les diagnostics et les tâches
  d’arrière-plan appartiennent à une session Studio partagée par toutes les pages.
- Toute mutation continue de passer par les commandes Application transactionnelles. Le Studio ne
  modifie jamais directement les documents JSON, les scènes ou les assets gouvernés.
- `Forge.Runtime` reste un processus séparé. Un playtest utilise un snapshot temporaire immuable et
  un crash runtime ne ferme pas le Studio.
- Le plugin `Forge.Toolset` reste une compatibilité de migration. Aucune fenêtre historique n’est
  supprimée avant que sa destination possède la parité fonctionnelle, les tests et l’acceptation
  visuelle inscrits dans `tracking/studio/CREATOR_WINDOW_MIGRATION.csv`.
- Les `Window` Godot natives sont limitées aux sélecteurs de fichiers et confirmations système
  indispensables. Une fonction ordinaire ne peut pas introduire une nouvelle fenêtre.
- Windows et Linux sont les cibles de l’application Studio. Leur export et leur distribution sont
  qualifiés séparément avant le remplacement du lanceur Creator historique.

## Remplacement des décisions précédentes

Cette décision remplace uniquement :

- la clause de l’ADR 0001 imposant que le toolset reste un addon de l’éditeur Godot ;
- la solution durable de l’ADR 0042 fondée sur une fenêtre secondaire active.

Elle conserve Godot .NET, l’absence de fork, l’indépendance Domain/Application, les formats de
module, les commandes transactionnelles et le plugin historique comme filet de migration.

## Conséquences

- La première tranche est volontairement verticale : ouvrir Forgotten Forest, naviguer entre les
  rubriques, conserver la zone et la caméra, sélectionner dans la 3D et prouver l’absence de fenêtres
  parasites.
- Les fonctions sont migrées par page et non par copie individuelle des anciennes fenêtres.
- Les composants fortement liés à l’éditeur Godot sont remplacés ou extraits derrière des services
  de présentation testables ; le domaine n’est pas réécrit.
- Une page incomplète l’indique explicitement et ne présente jamais un bouton factice comme
  fonctionnel.
- L’ancien Creator n’est retiré qu’après la parité des 66 fenêtres, le parcours de retouche de bout
  en bout, les distributions Windows/Linux et une validation humaine explicite.
