# G7-06 — Clipboard sémantique et duplication

Date d’exécution : 2 août 2026.

## Capacités bornées

- `ORF-SHELL-014` — copier, coller et dupliquer avec identité sémantique ;
- `ORF-AREA-012` — copie complète ou gabarit d’une zone.

## Résultat livré

Le clipboard Creator est un contrat moteur-indépendant versionné. Il capture une ou plusieurs
entités avec leur blueprint, transform, tags et propriétés locales, puis recalcule de nouveaux UUID
au collage. La commande `paste_entities` valide la zone cible, les limites, l’occupation et la
révision avant de produire dry-run, patch, inverse, audit et entrée d’historique.

La commande `duplicate_area` sait créer une copie complète ou un gabarit vide. Une copie complète
remappe chaque entité et chaque transition. Les transitions internes pointent vers les nouvelles
ancres ; les transitions externes peuvent être conservées ou supprimées explicitement. Le mode
gabarit garde le type, l’environnement et les dimensions sans dupliquer le contenu.

La barre Monde expose « Copier », « Coller » et « Dupliquer » avec un résumé du contenu courant. La
fenêtre « Zones & transitions » ajoute le nom de la copie, le mode complet/gabarit, la politique de
références externes et une étape d’aperçu obligatoire avant création. L’interface ne possède aucune
logique de mutation distincte : elle orchestre les mêmes handlers que les tests et le smoke.

## Preuves

- `tests/Forge.Application.Tests/Creator/SemanticClipboardWorkflowTests.cs` couvre collage,
  dry-run, refus spatial, clone complet, clone gabarit, remapping interne, références externes et
  undo/redo ;
- les 56 tests `Forge.Application.Tests` sont verts ;
- `OPENRPG_FORGE_TOOLSET_G706_CLIPBOARD_PASS entities=semantic-copy area=full-clone uuids=remapped preview=available history=restored persistence=roundtrip`
  prouve le parcours intégré, la persistance et la restauration de l’historique ;
- le Toolset compile en Release sans avertissement ni erreur.
- `scripts/Verify.ps1` passe avec 163 tests .NET, 23 tests Python, graphe frais, smokes Toolset et
  forêt, exports des deux modules, runtime générique, MCP et sauvegarde/reprise.

## Limites conservées

- l’action couper et la copie multi-sélection depuis le viewport ne sont pas encore exposées par
  l’interface, même si le payload accepte plusieurs entités ;
- le collage conserve les références sémantiques connues mais ne remappe pas encore tous les futurs
  agrégats, scripts, terrains, navigations, éclairages ou données de gameplay d’une zone ;
- la résolution automatique d’un emplacement libre et la répétition de placement restent à livrer ;
- l’inspection Windows interactive a été bloquée par l’association locale du fichier de projet, qui
  tente une exécution sans scène principale ; le smoke officiel headless reste la preuve UI intégrée
  de ce lot.

Ces limites conservent les deux capacités au statut `Implemented` partiel.
