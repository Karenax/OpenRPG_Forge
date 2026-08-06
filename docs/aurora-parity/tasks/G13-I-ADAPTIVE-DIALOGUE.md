# G13-I — Dialogues adaptatifs déterministes

## Périmètre borné

- `ORF-DLG-025` — timer optionnel, choix par défaut, pause accessible, autorité/révision serveur et
  hook ForgeScript de timeout ;
- `ORF-DLG-027` — variantes conditionnelles et pondérées, seed stable, anti-répétition et textes
  localisés cohérents.

Aucune autre exigence G13 n’appartient à ce lot.

## Contrat livré

`DialoguePresentationProfile` conserve deux collections supplémentaires : `TimedChoices` et
`VariantSets`. `DialoguePresentationValidator` refuse nœud ou choix absent, doublon par nœud, UUID
dupliqué, durée/poids hors bornes, script non confiné, révision serveur incohérente et clé de
localisation dupliquée. `DialogueAdaptiveRuntimeResolver` applique autorité, révision et pause, puis
résout timeout et variante par SHA-256 de façon indépendante du moteur.

La fenêtre **Mise en scène des dialogues** possède sept onglets, charge et écrit les deux familles,
affiche leurs diagnostics et simule timeout/seed sans éditer le JSON. Elle appelle toujours
`SetDialoguePresentationHandler`, donc dry-run, diff, audit, undo/redo et concurrence restent ceux du
document canonique. ForgeScript ajoute `ResolveAdaptive` sur le handle typé existant ; MCP expose les
comptages et filtre aussi par set ou texte de variante, tandis que sa mutation demeure un dry-run.

Les formats projet `0.6.30`, workspace `1.24.0` et `.orpmod` conservent les données. Une migration
depuis G13-H produit des collections vides. La Crypte des Cendres porte un timer de sept secondes et
deux répliques d’Elyra ; le runtime prouve timeout, choix par défaut, hook, pause accessible, seed,
locale et autorité serveur avant de poursuivre le smoke jouable historique.

## Preuves de fermeture attendues

- domaine : résolution nominale, refus client/révision périmée, pause accessible, timeout,
  déterminisme, conditions, pondération, anti-répétition et diagnostics localisés ;
- application : commande unique, dry-run/apply/undo/redo et handle ForgeScript adaptatif ;
- persistance : round-trip exact et migration depuis `0.6.29`/`1.23.0` ;
- MCP : ressource bornée filtrable par variante et prévisualisation sans mutation ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13I_ADAPTIVE_DIALOGUE_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13I_ADAPTIVE_DIALOGUE_PASS`, suivi du scénario complet ;
- portail intégré : `OPENRPG_FORGE_VERIFICATION=PASS` et graphe déterministe frais.

## Limites explicites

- le lot fournit le contrat runtime et une simulation auteur ; l’habillage final du compte à rebours
  dans un dialogue plein écran reste une évolution visuelle ultérieure ;
- aucune synchronisation réseau nouvelle n’est introduite : le resolver consomme l’autorité et la
  révision existantes de G9 ;
- aucune génération de texte IA n’est nécessaire à l’exécution des variantes.
