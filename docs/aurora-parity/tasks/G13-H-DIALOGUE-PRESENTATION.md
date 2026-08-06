# G13-H — Présentation cinématique des dialogues

## Périmètre

- `ORF-DLG-014` — cues d’animation par participant, timing, fondu, geste, regard, interruption et fallback ;
- `ORF-DLG-015` — prises vocales localisées, sous-titres, lipsync, émotion, durée, licence et repli sûr ;
- `ORF-DLG-016` — plans automatiques ou ancrés, cadrage, FOV, priorité joueur et accessibilité ;
- `ORF-DLG-020` — bookmarks personnels/partagés, commentaires de revue et deep links stables ;
- `ORF-DLG-023` — interjections de compagnons selon personnalité, présence, relation, priorité,
  cooldown, conflits et autorité serveur.

## Contrat livré

`DialoguePresentationProfile` est un document canonique optionnel de `DialogueDefinition`.
`DialoguePresentationValidator` refuse UUID dupliqués, nœuds/blueprints/ancres absents, durées et
FOV hors bornes, ressources vocales non confinées ou sans licence/hash, deep links incohérents et
interjections non autoritaires. `DialoguePresentationRuntimeResolver` choisit locale, plan et
interjection de façon déterministe, respecte contrôle caméra/réduction de mouvement et produit un
fallback sous-titre ou le diagnostic requis.

`SetDialoguePresentationHandler` fournit dry-run, diff, inverse, audit et historique. La fenêtre
**Mise en scène des dialogues** propose cinq onglets structurés, un profil de départ, un résumé
diagnostiqué et l’accès depuis l’éditeur de dialogues, sans édition JSON. ForgeScript utilise un
handle typé ; MCP expose lecture filtrée/bornée et prévisualisation sans mutation. Les formats projet
`0.6.29`, workspace `1.23.0` et `.orpmod` conservent exactement le profil ; les versions précédentes
migrent vers un profil optionnel absent.

La Crypte des Cendres porte une mise en scène d’Elyra. L’interaction jouable applique l’animation
avec fallback de clip, la prise `fr-FR` avec sous-titre, le cadrage accessible, le bookmark et
l’interjection sélectionnée côté serveur avant de poursuivre la quête, le combat et le butin.

## Preuves de fermeture

- domaine : validation, locale, accessibilité, autorité/cooldown et fallback optionnel/requis ;
- application : dry-run/apply/undo/redo, conflits de révision, profil invalide et handle ForgeScript ;
- persistance : round-trip JSON exact, save/close/reopen, migration et `.orpmod` ;
- MCP : lecture filtrée/bornée et prévisualisation sans changement de révision ;
- Toolset : `OPENRPG_FORGE_TOOLSET_G13H_DIALOGUE_PRESENTATION_PASS` ;
- runtime : `OPENRPG_FORGE_RUNTIME_G13H_DIALOGUE_PRESENTATION_PASS`, puis smoke jouable complet ;
- porte intégrée : `OPENRPG_FORGE_VERIFICATION=PASS` avec 913 tests .NET, 24 tests Python généraux,
  30 tests Python du pipeline d’assets, exports/rechargements de modules, sauvegardes, réseau, MCP et
  worker Blender réel ;
- graphe d’architecture : 4 877 nœuds, 12 164 relations, génération et contrôle déterministes.

## Limites explicites

- le lot utilise un repli sous-titre pour l’exemple libre de la Crypte ; aucune voix propriétaire
  ou générée n’est ajoutée au dépôt ;
- l’UI est un formulaire structuré à cinq onglets. Un éditeur de timeline visuelle pourra consommer
  le même profil et la même commande sans migration ;
- le transport réseau des interjections reste hors de G13-H, mais le contrat autoritaire et
  déterministe est prêt à utiliser la couche G9.
