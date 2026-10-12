# Gate 3 — état consolidé au 11 octobre 2026

Reprise sur [une seule version commune](GATE3_SHARED_VERSION_20261011.md), dans
le checkout Rust principal, incluant les sources locales des autres conversations.
Les contrôles du 11 octobre sont distincts des résultats historiques ci-dessous.

[Consolidation locale du 10 octobre](GATE3_CONSOLIDATION_20261010.md) : corrections du candidat intégrées aux sources courantes ; format, Clippy, 2003 tests natifs, 13 replays privés et check WebGPU PASS sur le périmètre identifié. La CI distante sur cette correction et la clôture Gate 3 restent à obtenir.

**Gate 3 ouverte ; Gate 2 clôturée par ADR-0018.** Le test humain Gate 3 a été
[accepté le 8 octobre](GATE3_OWNER_HUMAN_VALIDATION_20261008.md) sur le correctif
005. Les mentions P05/humain en attente dans le journal suivant décrivent leurs
candidats et instants historiques ; elles ne révoquent pas cette acceptation.
Aucune acceptation humaine de Source075 ou des correctifs ultérieurs n'en est déduite.

Source075 (`7a0c770f1eac0afcc8fb67accaae539c4da10747`) conserve ses preuves
locales : 1951 tests natifs PASS, 52 ignorés, contrôles CPU/GPU/Studio appariés
et parcours publics frais. Les corrections retenues ont été intégrées et recontrôlées dans les sources
courantes selon le bilan du 10 octobre ; les preuves historiques restent attachées à Source075.

CI actuelle : [run 38123749165 du 11 octobre](https://github.com/Karenax/OpenRPG_Forge/actions/runs/38123749165)
sur `48a894d550c5770a5ea50bc54707113e85ab5218` : WASM et supply-chain PASS,
Windows/Linux FAIL à Clippy ; tests et builds natifs suivants non exécutés.
Les refus historiques de facturation ne couvrent pas ces erreurs de code.

Le [candidat installable initial](../../planning/INSTALLABLE_DELIVERY_20261010.md), rejeté par le propriétaire, est remplacé par [R3](../../planning/INSTALLABLE_DELIVERY_20261010_R3.md). La correction dispose de preuves locales de conservation des données et de deux relancements natifs réels. Ses binaires moteur restent ceux du 9 octobre ; elle ne qualifie pas ce lot moteur consolidé, l'aventure RPG complète ou la release.
L'intégration/CI Gate 3 est demandée le 10 octobre ; la reprise conserve le chantier
de généralisation moteur décrit dans `PROJECT_CONTEXT.md`, sans le qualifier.

## Journal historique des acquisitions — jusqu'au 8 octobre 2026

Les résultats et échecs suivants sont conservés sans modification. Leur statut
est celui de l'acquisition décrite, pas un nouveau verdict sur le checkout actuel.

8 octobre — Source075 : chaine publique005 terminale PASS, consolidation des21 criteres automatises sur22 exigences relue. Format/Clippy natifs stricts,1951 tests natifs PASS/52 ignores, CPU acquis21, GPU10, Studio/WASM apparie et parcours publics frais conserves. Feu/reaction IA : negatif du controle Save seul preserve ; complement temporel montre0,794802 m puis0,800025 m de progression, total1,594801 m, position finale sauvegardee/reprise et14 contre-exemples rejetes ; fuite complete non revendiquee. P05 : copie65 fichiers sans Save automate, lanceur prepare non execute. Fermeture en lecture ligne par ligne testee sur journal reel archive : neuf identites retrouvees, pic resident125800448 octets, aucun arret de processus ; aucun jugement humain deduit. Dossier P05 concret et dossier de publication prepares. Rust distant observe6b001dbe, aucun run CI sur7a0c770f ; push exact propose1244 fichiers modifies dont8 binaires de fixtures/fork Bevy, avance sans force possible, primaire/index preserves. Disponibilite humaine et autorisation de ce push demandees, aucune reponse presumee. Gate3 reste ouverte : P05, CI exacte et decision proprietaire, avec adoption locale distincte. Blocage de distribution GLib conserve sous la politique existante. Aucun seuil de RAM disponible ni publication.

8 octobre — Source075 : rive poussable/extinction/sauvegarde/reprise PASS, six controles causaux, 65 fichiers preserves et fermeture independante PASS. Pic prive mesure5892194304 octets, compteur GPU dedie par processus2032226304 octets ; aucune allocation exclusive globale inferree. Chaine004 arretee apres rive sur ancien nombre3216 du lecteur shader ; negatif conserve. Lecteur002 corrige ce seul nombre en3282 verifie contre le candidat et conserve tous controles WGSL/GPU. Shader reel79269 caracteres observe26 fois, diagnostics Dawn observes PASS ; aucune qualification de pixels globaux ou de periode avant instrumentation deduite. Chaine005 execute maintenant le feu ; etape observee fire-run. Generateur de dossier P05 prepare et syntaxe verifiee, dossier final non encore produit. Feu/reaction IA, P05 humain, adoption, CI exacte, Linux/GLib et cloture proprietaire restent ouverts. Aucun nouveau replay de rive ni remplacement du primaire ou publication.

8 octobre — Source075 : navigation publique patrouille/porte fermee NoPath puis ouverture/sortie/revision/sauvegarde PASS, 207 fichiers observations relus et fermeture independante PASS. Chaine002 arretee avant lancement rive : lecteur existant. Chaine003 arretee avant lancement rive : seul ecart CRLF/LF du lecteur. Negatifs conserves ; lecteur non modifie, equivalence apres normalisation CRLF seulement verifiee. Preparation partielle et 419 fichiers de harness verifies, chaine004 reprend uniquement rive puis feu. Etape observee shore-run. Aucun replay des parcours precedents, aucun nouveau candidat ou verdict humain. Rive/feu/danger, P05 humain, adoption, CI exacte, Linux/GLib et decision proprietaire restent ouverts. Primaire preserve, aucune publication.

8 octobre — Source075 : environnement lancer/impact/piege/destructible/butin/quete et reprise PASS, douze controles causaux ; impact neutralisant prouve avant combat. P02 PASS, treize controles, seules retouches reponse et porte X+0,05 m, refus de reprise ordinaire puis reprise compatible dans deux processus. P03 diagnostic de source de quantite manquante, cible editable et suppression refusee sans nouvelle revision PASS ; aucune correction commise revendiquee. P04 depart choisi, revision40, isolation de sauvegarde et nouveau depart distinct de Reprendre PASS ; physique bit-identique non revendiquee. Fermetures independantes PASS. Politique locale cargo-deny0.20.2 et cargo-audit0.22.2 PASS, 48 avertissements de versions multiples et dix avis RustSec conserves ; glib0.18.5/RUSTSEC-2024-0429 reste bloquant pour la distribution. Aucune qualification Linux distante deduite. Etape publique observee shore-prepare. Navigation, rive/feu/danger, P05 humain, adoption, CI exacte, Linux et cloture proprietaire restent ouverts. Aucun remplacement du primaire ni publication.

8 octobre — Source075 : parcours RPG frais PASS, douze controles causes/commandes/fermeture, cinq attaques de contact a 20 degats, butin, objet de quete consomme et objectif termine sauvegardes puis repris, interieur et routes publics verifies. Fermeture independante PASS. Etape observee compatible-retouch-prepare. Adoption : inventaire3282 chemins compare ; changements nouveaux du primaire preserves. Le dossier de differences s est refuse sur une nouvelle modification concurrente du rendu sous-marin ; resultat negatif et copies partielles conserves. Trois conflits des deux fichiers de transport scalaire resolus dans une proposition isolee, format par fichier PASS, dependances non assemblees et compilation non revendiquee. Aucun remplacement du primaire ni transfert de qualification vers ces propositions. Gate3 reste ouverte : parcours restants, P05 humain, adoption, CI exacte, Linux et decision proprietaire.

8 octobre — Source075 : authoring Studio frais base/physique/navigation PASS. Le pilote navigation conserve native_close_confirmed=false ; drain normal de 3,523 s puis fermeture independante des identites exactes PASS, sans arret force, et 16 contre-exemples de fermeture rejetes. Chaine negative001 preservee. Reprise002 sur le meme executable : reouverture publique PASS, 22 entites verifiees, revision40, 65 fichiers et tete sauvegardee preserves. Etape observee fresh-gameplay-run. Parcours RPG/environnement restants, P05 humain, adoption, CI exacte, Linux et decision proprietaire non qualifies. Aucun seuil minimum de RAM disponible ni publication.

8 octobre — Source075 : chaine native1951/CPU21/build Studio-WASM/GPU10 PASS revue et fermetures independantes PASS. Candidat prive 7a0c770f1eac0afcc8fb67accaae539c4da10747, exe 26f4534c5bbf3f3649b4d6a75c0126802aff5201aaab81481f716d769e133272, WASM ed6ac824f884e5bab1d5673e050bad86fe9457e0b94f422058efd264ab06ea1a; parite3282 sources/3000 imports/11 kit/259 acquis/4997 Debris/worker verifiee. Quatre regressions natives colorMask executees PASS; aucun readback RGB GPU inferre. Avertissement WASM import bevy_ecs inutilise conserve dans frontend.log. Parcours publics frais lances, etape observee physics-authoring-run; aucun PASS runtime Source072 herite. Exigences finales publiques, P05 humain, adoption, CI exacte, Linux et decision proprietaire restent ouvertes. Primaire preserve, aucun seuil minimum RAM ni publication.

8 octobre — Source075 : formatage, Clippy strict workspace/toutes cibles et tests natifs PASS revus; 1951 tests reussis,52 ignores,0 echec/filtre,165 executables archives,3282 sources verifiees et fermeture independante PASS. Les1939 tests Source072 sont tous conserves et12 tests supplementaires ont effectivement passe (projection finie, forward unlit, programmes et consommateur temporel). Les16 tests CPU acquisition Debris/pebble/scanned passent; inventaire52 ignores/6 harnais compile verifies, sans inference de replay. Checkpoint prive 7a0c770f1eac0afcc8fb67accaae539c4da10747 cree, index primaire preserve. Etape CPU/Studio observee : matched-build-run. Rendu GPU, parcours publics Source075, P05 humain, adoption, CI exacte, Linux et decision proprietaire restent ouverts. Aucun seuil minimum RAM ni publication.

8 octobre — Source074 : format PASS, compilation Clippy negative E0308 dans deux appels de tests encore passes par valeur; aucun test execute, fermeture independante PASS et aucun plafond memoire atteint. Source075 isolee : ces deux arguments sont empruntes, seul source_unlit_forward_program.rs change; 3282 fichiers et tous les noms de fonctions/tests conserves. Validation native complete en cours, etat observe RUNNING. Audit071 : les22 exigences finales restent ouvertes sans heritage des PASS Source072. Outils CPU21/Studio/GPU10 prepares pour Source075, non executes. Primaire preserve, aucun seuil minimum RAM ni publication.

Verdict Source075 — 8 octobre : [format, Clippy strict workspace/toutes cibles et tests natifs PASS revus](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-075-001/root-review.json). 1951 tests réussis, 52 ignorés, aucun échec ; 3282 fichiers source et 165 exécutables archivés vérifiés. Fermeture indépendante des identités PASS, aucun seuil de RAM disponible. Les tests ignorés d’acquisition, le rendu GPU, le candidat Studio apparié, les boucles RPG intégrées, adoption, P05, CI exacte et cible Linux restent à qualifier. Aucun remplacement primaire ni publication ; Gate 3 ouverte.

8 octobre — Source073 : fmt PASS, strict negatif dans nouveaux modules unlit (programme lu passe par valeur, defaults stencil/bias non types et points-virgules de tests). Aucun test natif execute ; fermeture independante PASS, aucun plafond memoire atteint. Source074 isolee3282 fichiers : seuls deux modules unlit corriges, programme emprunte en lecture seule avec deux appels natifs adaptes, defaults types et statements de tests termines. Contrat du parametre public passe de valeur a reference ; noms fonctions/tests conserves, aucun lint supprime. Validation native complete relancee, etat observe RUNNING. Audit070 conserve22 exigences sans transfert de PASS Source072/073. P05 final, integration/adoption, GPU/parcours finals, CI/Linux exactes et cloture proprietaire ouvertes. Primaire preserve, aucun seuil minimum RAM ni publication.

8 octobre — Source073 isolee composee :3282 fichiers,66 ajouts primaires versionnables preserves,13 changements primaires seuls repris,3 compositions explicites lib/source graph/probe. Conservation des helpers stricts Source072, cluster de qualification, batch uniformes et tampon vent ; ajout projection finie optionnelle, modules unlit et consommateurs post-effets. Intervalle near0,1m du probe statique primaire conserve et exige revalidation ;3 bases exactes anciennes indisponibles, aucune fusion3 voies authentifiee revendiquee. Format de preparation PASS ; validation native complete lancee, etat observe RUNNING. Lecteur exige tous1939 tests Source072 et tous52 ignores historiques, sans exclusion. Audit069 conserve22 exigences mais ne transfere aucun PASS Source072 a Source073. Copie et revue P05 Source072 restent preparatoires pour leur candidat exact ; P05 final, composition/adoption, CI/Linux exactes et cloture proprietaire ouvertes. Primaire et index preserves ; aucun seuil minimal RAM ni publication.

8 octobre — revue integration Source072 : comparaison des2813 chemins de bases primaires observees avec2876 fichiers versionnables actuels ;16 chemins derives et63 chemins sans observation precedente, dont5 nouveaux fichiers moteur absents de Source072. Fusion textuelle simulee au parent publie uniquement dans les preuves :28 changements candidat seuls,71 fichiers ajoutes divergents a relire,44 conflits a relire,3 differences de fins de ligne,7 merges egaux au candidat,2 merges propres differents exigeant revalidation,6 changements primaires seuls a preserver,2 documents vivants a preserver. Aucun resultat textuel promu en preservation semantique ou qualification runtime. Source072 conserve ses21 composants automatises qualifies dans leurs perimetres ; il ne qualifie pas les nouvelles modifications moteur du primaire. Audit068 conserve22 exigences ; composition/revalidation integration, P05, CI/Linux exactes et cloture proprietaire restent ouvertes. Aucun remplacement primaire, seuil minimum RAM ou publication.

8 octobre — Source072 : chaine publique003 terminee et21 composants automatises des22 exigences relus, sans transfert de resultats historiques. Feu et consequences hors ancien controle IA PASS ; supplement temporel IA mouvement KCC/eloignement/position finale Save/Resume PASS avec14 contre-exemples rejetes. Ancien controle IA sauvegarde seule negatif preserve ; aucune fuite complete deduite. Copie humaine65 fichiers sans sauvegarde et lanceur surveille prepares, syntaxes PASS, aucun lancement/P05. CI publiee6b relue : Windows/Linux/WASM FAIL, supply-chain PASS, aucun verdict CI du checkpoint prive. Audit067 consolide toutes exigences, dossier de revue concret prepare. P05, adoption preservant le checkout, CI/Linux finales et decision proprietaire restent ouverts. Gate2 validation ADR0018 reconfirmee ; Gate3 OUVERTE NON QUALIFIEE. Aucun seuil minimal de RAM disponible ni publication.

8 octobre — Source072 rive PASS6 controles causaux : poussee/extinction eau, recus nouveaux et Save/Resume verifies ; route 228.209904s et 51 arrives. Fermetures normale et independante PASS ; pic prive 5929709568.0 octets, GPU dedie 2215079936.0 octets sous plafonds, aucune cause exclusive allocation deduite. Shader reel genere 79269 caracteres capture avec diagnostics observes PASS apres installation seulement ; aucun verdict global pixels. Test feu en cours. Dossier de differences adoption prepare sans application, changements contemporains de documentation et focus hivemind preserves. Audit066 conserve22 exigences ; feu final, P05 humain, adoption, CI exacte, Linux et cloture proprietaire restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 navigation PASS : patrouille exterieure configuree et piece canonique fermee NoPath puis ouverte/sortie/revision/Reached, sauvegarde et252 fichiers observation verifies. Fermetures normale et independante PASS ; source preservee. Scope borne, aucun verdict sur tous les chemins ou comportements PNJ de Tidewater. Test de rive pousse/extinction eau/Save/Resume lance, resultat attendu. Audit065 conserve22 exigences ; shore/feu finals, P05 humain, adoption, CI exacte, Linux et cloture proprietaire restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 P03 PASS : suppression du Sceau refusee avec diagnostic nommant quete/objet/quantite, selection accessible pour correction, Appliquer desactive et aucune nouvelle revision/commande. Preuve et fermeture independante relues ; aucune correction effectivement commise deduite. Navigation finale en cours. Inventaire adoption en lecture seule sur3216 chemins candidat :2650 identiques,163 differents,403 absents du primaire ; HEAD et octets index preserves, aucun remplacement. Les fichiers primaires hors manifeste ne sont pas couverts par cet inventaire. Audit064 conserve22 exigences ; navigation/shore/feu finals, P05 humain, adoption, CI exacte, Linux et cloture proprietaire restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 P04 PASS : depart choisi, revision publique40, mouvement avant sauvegarde et second nouveau depart isole verifies ; magasin de sauvegarde source preserve, fermetures normales et independantes PASS. Aucune identite physique bit a bit deduite. Echec du coordinateur002 conserve : appel incorrect du lecteur historique069 ; lecteur072 relu apres fermeture, sans replay ni transfert de PASS historique. Reprise003 a P03 lancee avec lecteurs propres072. Audit063 conserve22 exigences ; P03/navigation/shore/feu finals, P05 humain, adoption, CI exacte, Linux et cloture proprietaire restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 P02 PASS13 controles : seules la reponse du dialogue et la position X de la porte (+0,05m) sont retouchees, liens et autres corrections preserves. Reprise ordinaire refusee, reprise compatible reussie dans deux processus natifs distincts, reponse retouchee visible, passage interieur et ancienne sauvegarde quete/objet consomme/porte verifies. Fermeture independante PASS ;67 fichiers source preserves. P04 puis P03 lances ou a suivre, aucun verdict anticipe. Audit062 conserve22 exigences ; navigation/shore/feu finals, P05 humain, adoption, CI exacte, Linux et cloture proprietaire restent ouverts. Outils du lanceur humain prepares et syntaxes Python/PowerShell PASS, aucun lancement humain. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 : boucle environnementale publique lancer/impact/piege/destruction/butin/quete/Save/Resume PASS12 controles causaux,117 arrives de route a tolerance0,75m verifies et fermeture independante PASS. Echec du lecteur001 conserve : une arrivee avec focus inactif apres mouvement actif reel. Lecteur002 verifie ce mouvement, sa progression et le recu public d arret dans la tolerance ;9 contre-exemples rejetes. Aucun runtime rejoue ni ancien resultat herite. Reprise sequentielle002 a P02 lancee, resultat attendu. Audit061 conserve22 exigences ; P02/P03/P04/navigation/shore/feu finals, P05 humain, adoption, CI exacte, Linux et cloture proprietaire restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 : parcours public interieur/combat au contact/butin/quete terminee/Save puis Resume dans un nouveau processus PASS, 12 controles verifies et fermeture independante PASS. Candidat SHA 7c542898d2c1da3e9cea85e63ade90c937c617f6b9d4f8211383cf2fb32bbfe9. Les 65 fichiers source sont preserves ; dommage reel 20 par attaque et objet de quete consomme sauvegarde/repris verifies. Ces preuves ne valident pas encore la boucle environnementale ni P02/P03/P04/navigation/shore/feu sur ce candidat. Audit060 conserve les 22 exigences et leurs perimetres precedents ; P05 humain, adoption finale, CI exacte, Linux et cloture proprietaire restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 : creation publique Studio base/physique/navigation PASS, parite de contenu relue et reouverture/sauvegarde des22 entites a revision40 PASS,65 fichiers preserves et fermetures indépendantes PASS. Archive de contenu 2f70d7200d6fe44c64d683143ac12b264372d29c08491bdc20b8f65e98063ed1 ; head gameplay 855eb32bae6794e97624bd44090f9707035d566b67b2f9ac702b8bb27d73a348. Parcours frais combat/interieur/dialogue/quete/Save/Resume lance ; aucun resultat runtime Source069 herite. Audit059 conserve22 exigences. Les scenarios RPG/environmentaux/P02/P03/P04/navigation/shore/feu du candidat final, P05 humain, adoption finale, CI exacte, Linux et decision de cloture restent incomplets. Les1939 tests natifs,CPU21,GPU10 et build apparies sont PASS dans leurs perimetres. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 : candidat apparié frontend/natif/source/assets PASS,3216 fichiers et3000 imports verifies, checkpoint563504278f63404d15e737cb622d4144a4d550f9. Natif SHA7c542898d2c1da3e9cea85e63ade90c937c617f6b9d4f8211383cf2fb32bbfe9 ; worker navigation SHAaa3072e628d69577af7edecd6cbdd02cba6a8e4e44177d65a30589e50afb3716. CPU assets21 PASS (16 debris/scanned/pebble+5 poissons), GPU10 PASS (8 moteur+2 PBR) et fermetures indépendantes PASS. Les quatre nouvelles regressions masque de couleur sont confirmees apres fermeture de la chaine native. Avertissement WASM import inutilise herite de Source069 consigne avec fichier identique, sans suppression ; PASS strict natif distinct de la cible WASM. Authoring public frais base14 entites PASS, physique/navigation/reouverture et parcours RPG/environmentaux en cours ou a suivre. Audit058 conserve22 exigences ; aucune qualification publique Source069 transferee au nouveau candidat. P05 humain, adoption finale, CI exacte, Linux et decision de cloture restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

8 octobre — Source072 : revue native complete PASS1939 tests,52 ignores,0 echec/filtrage,165 executables archives et3216 fichiers verifies. Les1935 tests Source069 sont conserves ; les quatre nouvelles regressions masque de couleur sont effectivement PASS dans le journal epingle. Fermeture independante PASS. CPU assets16 PASS et fermeture PASS ; checkpoint prive 563504278f63404d15e737cb622d4144a4d550f9 cree, index primaire preserve, aucun remote configure ni publication. Build Studio frontend/natif apparies lance, resultat attendu ; tests poissons5, GPU10 et parcours publics encore requis sur ce candidat. Audit057 conserve22 exigences et les preuves publiques Source069 distinctes, sans transfert de qualification. P05 humain, adoption finale, CI exacte, Linux et decision de cloture restent ouverts. Aucun seuil minimal de RAM disponible.

8 octobre — Source072 : fmt et Clippy strict complet workspace/all-targets/keep-going/-D warnings PASS (exit0). Tests natifs complets lances ; resultat encore manquant, conservation des1935 tests Source069 a verifier par le lecteur avec les nouveaux tests masque de couleur. Audit056 conserve les22 exigences et leurs preuves Source069 distinctes. Aucun PASS complet natif/GPU/runtime Source072 encore acquis ; P05 humain, adoption finale, CI exacte, Linux et cloture restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

Verdict Source072 — 8 octobre : [format, Clippy strict workspace/toutes cibles et tests natifs PASS revus](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-072-001/root-review.json). 1939 tests réussis, 52 ignorés, aucun échec ; 3216 fichiers source et 165 exécutables archivés vérifiés. Fermeture indépendante des identités PASS, aucun seuil de RAM disponible. Les tests ignorés d’acquisition, le rendu GPU, le candidat Studio apparié, les boucles RPG intégrées, adoption, P05, CI exacte et cible Linux restent à qualifier. Aucun remplacement primaire ni publication ; Gate 3 ouverte.

8 octobre — Source071 : fmt PASS, strict negatif sur documentation et initialisation de fixture du masque de couleur (doc_markdown, field_reassign_with_default, default_trait_access). Preuve preservee, fermeture independante PASS, aucun plafond memoire atteint. Source072 isolee : documentation corrigee et fixture construite directement avec types explicites ; code runtime et noms de tests inchanges, 3216 fichiers epingles, un seul fichier modifie depuis071. Outils futurs corriges pour appeler les lecteurs Source072, sans references mixtes Source070/071. Qualification native en cours, fmt PASS ; aucun verdict complet Source072 acquis. Factory P05 preparee pour exiger les preuves publiques finales et fermetures avant copie, non executee. Audit055 conserve22 exigences ; P05 humain, qualification/adoption finale, CI exacte, Linux et cloture restent ouverts ; aucune publication.

8 octobre — Source070 : fmt PASS, Clippy strict echoue sur manual_range_patterns dans source_graph_bundle.rs:284 (lib et lib-test). Tests natifs non executes, preuve negative preservee, fermeture independante PASS, aucun plafond memoire atteint. Source071 isolee : seule correction produit 2 | 3 | 4 vers 2..=4, versions acceptees identiques ; 3216 fichiers verifies, autres octets Source070 preserves. Nouveaux outils prepares, qualification native relancee, fmt PASS ; aucun PASS natif/GPU/runtime Source071 encore acquis. Audit054 conserve les 22 exigences et les preuves publiques Source069 sans les transferer au nouveau candidat. P05 humain, candidat final, adoption, CI exacte, Linux et decision proprietaire restent ouverts ; aucune publication.

8 octobre — Source069 : chaine publique P02/P03/P04/navigation/shore/feu terminee. Feu : controles causaux hors ancien controle IA PASS ; supplement temporel IA PASS avec 14 cas negatifs rejetes, mouvement reel KCC puis position finale Save/Resume preservee. Le controle historique IA fonde sur la seule sauvegarde reste negatif et archive ; aucune fuite complete hors danger ni acceptation humaine deduite. Fermeture independante PASS, aucun processus exact survivant. Source070 : qualification native/CPU/checkpoint/build lancee en isolation, aucun resultat Source069 herite. Coordinateur GPU10 prepare, non execute. Audit053 conserve les 22 exigences. P05 humain, qualification/adoption du candidat final, CI exacte, Linux et decision de cloture restent ouverts. Aucun seuil minimal de RAM disponible ; aucune publication.

# Gate 3 — progression Tidewater au 2 octobre 2026

8 octobre — Source069 : poussee/extinction eau Save/Resume PASS6 controles causaux, route shore222.021761s et reçus nouveaux/reprise verifies ; fermetures normale et indépendante PASS, aucun seuil de RAM disponible. Pic prive6145552384octets sous plafond6GiB, sans cause exclusive d’allocation deduite. Shader reel genere79269caracteres SHAceaa9fba capture dans26 observations ; diagnostics observes56echantillons apres installation,139 requetes shaders, aucun module erreur/erreur navigateur/GPU/fence observe, scope limite explicite. Feu/reaction IA en cours. Source070 outils natifs/CPU21/checkpoint/build prepares, motifs du lecteur testes sur vrais baselines1927/1935 ; aucun resultat070 encore execute. Audit052 conserve22 exigences ; P05 humain, feu, qualification/adoption070, CI exacte, Linux et cloture restent ouverts ; aucune publication.

8 octobre — Source069 : P03 diagnostic reference absente vers cible editable et Apply refuse PASS ; P04 depart choisi/isolation/revision dans deux nouveaux processus PASS, sans physique bit-identique revendiquee. Navigation publique patrouille, NoPath porte fermee, Open puis Reached, revisions/Saves distincts PASS. Poussee/extinction en cours. Source070 assemble isolément3216 fichiers,21 ajouts primaires,4 compositions semantiques couleur,2 fusions relues,2 changements primaires et3 documents/recus conserves ; format PASS, primaire/index preserves. Outils natifs070 prepares exigent les1935 tests069,1927 tests067,2 regressions RGB24,52 ignores et165 executables archives ; compilation non lancee avant fin des scenes GPU069. Aucun PASS069 transfere à070. Audit051 conserve22 exigences ; P05 humain, shore/feu, qualification070/adoption, CI exacte, Linux et cloture restent ouverts ; aucune publication.

8 octobre — Source069 : P02 PASS13 controles, retouches publiques replique/porte seules, refus Resume ordinaire puis deux Resume compatibles conservant quete/consommation/liens, texte retouche visible et traverse interieur. Coordinateur002 arrete apres ce PASS par lecture d’un champ candidate_sha256 absent dans ce schema de rapport ; correction003 verifie config et executable exacts, conserve la preuve negative, ne rejoue pas P02 et lance P03/P04. Quatre propositions semantiques couleur preparees/formatees isolément sur069 conservent toutes les fonctions/tests et les corrections de lint/API/helpers ; elles ne sont ni appliquees ni compilees. Pas de base primaire exacte revendiquee pour ces quatre compositions. Deux fusions connexes et21 ajouts restent à integrer/requalifier. Audit050 conserve22 exigences ; humain P05, autres runtimes, primaire, CI exacte, Linux et cloture restent ouverts ; aucune publication.

8 octobre — Source069 : seconde approche RPG environnementale PASS12 controles causaux : lancer/impact avant combat, piege/chute, destructible, loot, quete et consequences sauvegardees/reprises. P02 retouche compatible maintenant lance apres fermeture indépendante. Le coordinateur001 avait appele directement le lecteur structurel de parent gameplay, qui demandait un dossier non prepare ; erreur conservee, parcours réussis non rejoues, coordinateur002 repart du parent fresh effectivement eligible. Reconciliation primaire proposée sans application :21 ajouts preserves,2 changements primaires isoles,2 fusions à relire et4 bases exactes non retrouvées apres100 candidats de fichiers nommes et comparaison au commit primaire. Aucun remplacement approximatif ni verdict de compilation sur ces propositions. Audit049 conserve22 exigences ; P02-P05, navigation/poussee/eau/feu final, primaire, CI exacte, Linux et cloture restent ouverts ; aucune publication.

8 octobre — Source069 : boucle RPG publique village/foret/interieur, dialogue, combat par5 attaques de20 dommages, loot Sceau, quete terminee et objet consomme dans Save/nouveau processus Resume PASS ;12 controles vrais,65 sources preservees, portes et3 routes publiques sur sol relues. Approche environnementale par impact en cours. Audit048 rattache les composants A1/A3-A8 à ce candidat sans promouvoir toute la Gate. Inventaire actuel du primaire :21 fichiers versionnables hors candidat archives byte-exact, dont source_graph_color_write_mask et captures/horloges source_final_post ; autres entrees primaires recentes ont change depuis la reconciliation. Adoption primaire exige une nouvelle revue, aucune copie ni suppression effectuee. P05 humain, navigation/environnement final, CI exacte, Linux et decision de cloture restent ouverts ; aucune publication.

8 octobre — Source069 : authoring public depuis un seed sans gameplay PASS14 entites, ajouts physiques PASS24 puis navigation PASS28. Nouvelle ouverture publique verifie22 entites, Save revision40 et fermeture indépendante PASS ; contenu complet et sources65 fichiers preservés. Boucle RPG069 en cours, aucun PASS runtime067 transféré. Copie65 fichiers pour partie humaine P05 préparée et non lancée ; deux captures réelles depart/interieur inspectées, grande capsule bleue centrale au départ à juger pour identification/lisibilité, pas de jugement de rythme ou de partie entière. Audit047 rattache les22 statuts courants à069 et conserve explicitement les verdicts antérieurs. P05 humain, primaire, CI exacte, Linux et clôture restent ouverts ; aucune publication.

8 octobre — Source069 :21 tests CPU acquis (16 décor/scanned/pebble et5 poissons) et10 tests GPU réels (8 moteur,2 PBR) PASS revus sur3195 sources, avec fermetures indépendantes. Le lancement GPU001 a échoué avant toute exécution à cause du compte ancien3126 ; preuve négative conservée, aucun déclenchement mémoire, aucune scène lancée. Nouvelle tentative002 corrige uniquement ce compte vers3195, garde les dix tests exacts, parseur PBR, sampling100ms et plafonds de consommation ; aucun seuil de RAM disponible. Build apparié PASS et chaîne de récupération003 PASS. Authoring public069 démarré dans une fixture isolée ; ses résultats ne sont pas encore qualifiés. Audit046 conserve les22 exigences et les preuves067 sans transfert runtime. P05 humain, adoption primaire, CI exacte, Linux et décision de clôture restent ouverts ; aucune publication Git.

8 octobre — Source069 : frontend WASM, worker navigation et candidat natif appariés compilés et revus PASS. Checkpoint privé74795ddf,3195 sources,3000 imports,11 fichiers kit,259 entrées acquises et4997 entrées décor vérifiés ; exécutable SHA256 168d8a147148c19ff824d6bc2e0526cc60b8d357c37718c7cfa2bc109ccca16d. Fermeture indépendante du build PASS. Cette preuve reste build-only : aucune performance équivalente, partie publique ou acceptation humaine déduite. Chaîne publique authoring/RPG/P02/P03/P04/navigation/poussée/eau/feu préparée avec fermeture complète CPU/GPU préalable ; noms de lecteurs P03/P04 corrigés vers069 avant exécution. Les22 exigences restent dans l’audit045, sans transférer de PASS runtime067. P05 humain, adoption primaire, CI exacte, Linux et décision de clôture restent ouverts. Aucun seuil de RAM disponible ni publication Git.

8 octobre — Source069 : validation native complète PASS revue (fmt-check, Clippy strict workspace/all-targets/keep-going, tests1935PASS/52ignorés/0échec/0filtre,165 exécutables archivés,3195 sources vérifiées). Toutes les1927 occurrences de tests067 et les deux régressions RGB24 sont conservées ; les huit exécutions supplémentaires couvrent les deux tests de callbacks acquis dans quatre exemples. Fermeture indépendante et consommation protégée PASS sans seuil de RAM disponible. Les16 acquisitions CPU décor/scanned/pebble,4997 entrées et3 shaders générés passent ;52 noms ignorés vérifiés sans les promouvoir en tests exécutés. Checkpoint privé propre74795ddfb310d8556aa3f1d296b35c610524ce7e, parent6b001dbe, index/ref primaire préservés et aucun remote. Build frontend/native apparié lancé. Enchaînement séquentiel conserve le même test natif initial ; première observation WMI expirée avant toute étape, remplacée par PID/image/création/état de sortie Windows avec pins du lancement, contrôles de qualification inchangés. Les16 outils publics069 sont préparés mais non exécutés. Audit044 : `.gate3-evidence/20261002/gate3-integrated-source069-closure-audit-044/proof.json`. GPU/build/runtime final, P05 humain, adoption primaire, CI exacte, Linux et clôture restent ouverts ; aucun PASS067 transféré ni publication Git.

Verdict Source069 — 8 octobre : [format, Clippy strict workspace/toutes cibles et tests natifs PASS revus](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-069-001/root-review.json). 1935 tests réussis, 52 ignorés, aucun échec ; 3195 fichiers source et 165 exécutables archivés vérifiés. Fermeture indépendante des identités PASS, aucun seuil de RAM disponible. Les tests ignorés d’acquisition, le rendu GPU, le candidat Studio apparié, les boucles RPG intégrées, adoption, P05, CI exacte et cible Linux restent à qualifier. Aucun remplacement primaire ni publication ; Gate 3 ouverte.

8 octobre — Source069 : fmt-check et Clippy strict workspace/all-targets/keep-going PASS0, log final strict et preuve d’étape relus puis archivés ; tests workspace/all-targets en cours, aucun verdict natif complet anticipé. Suite des acquisitions CPU/checkpoint privé/build apparié/lecteurs préparée en exigeant les1927 tests067 conservés,52 noms ignorés,16 acquisitions décor et5 poissons. Graphes actuels Windows/Linux GLib verrouillés/offline PASS :0.18.5 absent sur Windows, présent sur Linux, blocage de distribution conservé. Archive GLib vérifiée contre checksum Cargo.lock ; fichier original et proposition à deux lignes identiques au correctif amont b5a4071e439bef2b5eea76c3aa25e5ae84839e34, licences et patch amont archivés. Aucun backport adopté, test natif GLib/Linux, waiver ni changement de politique. Audit043 : `.gate3-evidence/20261002/gate3-integrated-source069-closure-audit-043/proof.json`. La clôture complète attend natif complet/build/GPU/runtime final, P05 humain, adoption, CI exacte et résolution Linux ; aucun seuil de RAM disponible ni publication Git.

8 octobre — Source068 : fmt-check PASS ; Clippy strict workspace/all-targets/keep-going terminé négatif101. Seuls diagnostics : Option<Option<u32>> et champ feedback_frame inutilisé dans le reader partagé des callbacks spray acquis. Tests non exécutés ; sources inchangées, aucune protection mémoire déclenchée, fermeture indépendante PASS. Rapport négatif archivé. Source069 isolée : mêmes3195 fichiers et toutes les fonctionnalités primaires conservées,3 fichiers corrigés ; enum LegacyMetadata/Unavailable/Completed(frame), mêmes contrôles de provenance/chronologie et heads GPU natifs, diagnostic de callback publié par le probe océan, deux nouveaux tests définis sur états valides et huit altérations de chronologie. Aucune suppression de lint ; formatage PASS. Contrôles natifs complets Source069 lancés, aucun résultat anticipé. Audit042 : `.gate3-evidence/20261002/gate3-integrated-source069-closure-audit-042/proof.json`. Résultats publics067 conservés sans transfert vers069 ; build apparié/GPU/runtime final, P05 humain, adoption, CI/Linux et clôture propriétaire restent ouverts. Aucun seuil RAM disponible ni publication Git.

8 octobre — Préservation/adoption : inventaire primaire étendu aux69 fichiers versionnables absents du candidat067. La comparaison au parent publié produisait46 conflits textuels ; les bases primaires051/064 récupérées par SHA ramènent la réconciliation réelle à6 changements primaires seuls,2 fusions propres revues et3 documents/receipts vivants. Familles temporelles Scanned/Fish, rendu final de probe et contrôles de chronologie des callbacks spray acquis conservés, sans remplacer les données natives par les heads ou budgets source. Source068 isolée assemblée :3195 fichiers, formatage PASS sans delta ; correction shader067 intacte, primaire/index/ref inchangés. Contrôles natifs complets lancés (fmt-check, Clippy strict, tests workspace/all-targets, -j1, sans seuil RAM disponible). Les PASS runtime067 restent propres à067 ; aucun transfert vers068. Audit041 : `.gate3-evidence/20261002/gate3-integrated-source068-closure-audit-041/proof.json`. Build apparié/GPU/runtime068, P05 humain, adoption finale, CI/Linux et clôture restent requis ; aucune publication Git.

8 octobre — Source067 : rive002 PASS (33 points, 51 arrivées, 222,78 s), poussée effective puis ignition/extinction dans l’eau, Save/Resume concordants, fermeture normale et indépendante PASS. Shader généré capturé relu indépendamment sur rive002 : diagnostics observés sans erreur. Feu001 : dégâts joueur/ennemi, quête terminée et Save/Resume PASS ; lecteur monolithique IA négatif conservé. Supplément IA temporel PASS : exposition9426, HUD/navigation/feedback9456 concordants, déplacement KCC réel au sol opposé au feu0,461 m à2 m/s ; position finale déplacée1,595 m sauvegardée10326 et conservée à Resume14961. L’égalité initiale entre position intermédiaire et Save plus tardif était incorrecte (continuation1,133 m) ; lecteur original conservé, comparaison finale→reprise et14 contre-exemples PASS. Aucune sortie complète du danger ni acceptation humaine inférée. Audit complet040 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-040/proof.json`. P05 partie complète/lisibilité/rythme, adoption primaire préservant les modifications, CI/Linux du résultat final et clôture propriétaire restent ouverts. Aucun seuil minimum de RAM disponible ni publication Git.

8 octobre — Source067 : vrai WGSL WebGPU capturé (79 269 caractères, SHA ceaa9fba…) et relu PASS. Trois skBump et hookContactShadow sont hors branches ; leurs résultats alimentent les branches avec coefficients d’origine, diagnostics Dawn/browser/GPU observés sans erreur. Premier lecteur conservé : les noms source ont été abaissés par Naga en _e85/_e114/_e133 ; lecteur002 suit ces résultats générés. Rive001 non qualifiée malgré route complète vérifiée : Push refusé faute de contact confirmé, extinction non atteinte ; aucun plafond ni arrêt forcé, fermeture indépendante PASS, drain normal absent. Rejeu rive002 démarré avec approche à tangence nominale 0,55 m au lieu de 0,575 m, mêmes tolérance0,025 m/délai120 s, contrat Rapier0,05 m, route33/tolérance0,6 m/budget360 s et critères causaux. Le drain est tenté aussi sur échec, lequel reste refusé. Audit complet 039 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-039/proof.json`. Feu/IA, rive/extinction, P05 humain, adoption primaire, CI/Linux et clôture restent ouverts ; aucun seuil RAM disponible ni publication Git.

8 octobre — Source067 : P04 départ choisi 40/-115/révision40/isolation et P03 diagnostic de référence manquante vers Sceau éditable relus PASS, sources préservées et fermetures normales. Navigation relue PASS : patrouille/seam, NoPath porte fermée, sortie ouverte, Reached, révisions distinctes et Save. A2/P03/P04/P01 reliés aux preuves actuelles ; aucun jugement humain ni physique bit-identique revendiqué. Test de rive/poussée/extinction avec capture du vrai shader WebGPU démarré, sans PASS anticipé. Audit complet 038 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-038/proof.json`. Les 22 exigences restent conservées ; rive/feu/extinction/IA, shader généré, P05 humain, adoption primaire, CI/Linux et clôture propriétaire restent incomplets. Gate 3 OUVERTE — NON QUALIFIÉE ; aucun seuil RAM disponible ni publication Git.

8 octobre — Source067 : P02 relu PASS, 13 contrôles vrais. Seuls texte de réplique et X de porte (+0,05 m) retouchés ; liens et autres corrections préservés, reprise ordinaire refusée puis reprise compatible dans deux processus distincts, réplique visible et traversée intérieure réelle. P04 départ choisi/isolation démarré. Diagnostic GPU complémentaire du parcours impact non qualifié : observateur historique sans champs de compilation shader/browser requis ; ne prouve ni absence d’erreur de compilation ni échec shader. Capture complète dédiée rive encore requise. Audit complet 037 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-037/proof.json`. Les 22 critères, P03/P04/navigation/feu/rive/extinction/IA, P05 humain, adoption primaire, CI/Linux et clôture restent ouverts. Aucun seuil RAM disponible ni publication Git.

8 octobre — Source067 : seconde approche RPG par impact relue PASS, 12 contrôles causaux vrais : lancement réel et dégâts par impact neutralisent le gardien avant combat, piège/destructible, butin, quête achevée, sauvegarde cohérente et reprise dans un nouveau processus. Fermetures indépendantes acquises, sources préservées. B2/B3/B4/B7 et parcours C reliés à cette preuve actuelle ; P02 retouche compatible démarré. Audit complet 036 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-036/proof.json`. Tous les 22 critères restent conservés ; navigation, feu/rive/extinction/IA, shader généré, P03/P04/P05 humain, adoption primaire, CI/Linux et clôture propriétaire restent à qualifier. Gate 3 OUVERTE — NON QUALIFIÉE ; aucun seuil RAM disponible ni publication Git.

8 octobre — Source067 : boucle RPG fraîche combat/intérieur/dialogue/butin/quête/Save/nouveau processus Resume relue PASS, 12 contrôles vrais, cinq attaques par contact à 20 dégâts, état de quête et objets persistants, 65 fichiers source préservés et fermetures normales indépendantes. Les sept composants A1/A3–A8 sont reliés à cette preuve actuelle, sans clôture anticipée. Deuxième approche par impact démarrée ; P02/P03/P04/navigation préparés seulement. Audit complet 035 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-035/proof.json`. Les 22 exigences restent conservées ; feu/rive/extinction/IA, shader réellement généré, validation humaine P05, adoption primaire, CI/Linux et clôture propriétaire restent à qualifier. Gate 3 OUVERTE — NON QUALIFIÉE ; aucun seuil RAM disponible ni publication Git.

8 octobre — Source067 : chaîne d’authoring fraîche relue PASS (14 puis 24 puis 28 entités), conformité sémantique sans différence et réouverture publique relue PASS (22 entités spatiales, révision 40, 65 fichiers préservés). Fermetures normales et indépendantes acquises. La boucle RPG combat/butin/quête/Save/nouveau processus Resume est démarrée ; aucun résultat anticipé. Audit complet 034 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-034/proof.json`. Les 22 critères restent requis, ainsi que les preuves de scène et shader généré, la validation humaine P05, l’adoption préservant le primaire, CI/Linux et clôture propriétaire. Gate 3 OUVERTE — NON QUALIFIÉE ; aucun seuil RAM disponible ni publication Git.

8 octobre — Source067 : reconstruction et parité frontend/native relues PASS ; cinq tests poissons acquis, huit tests GPU moteur et deux tests PBR supervisés relus PASS, fermetures indépendantes acquises. Premier passage moteur non qualifié faute de processus observé, conservé ; rejeu à 100 ms, mêmes assertions et plafonds. Authoring Studio frais démarré, aucun PASS de scène anticipé. Audit complet 033 : `.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-033/proof.json`. Les 22 exigences, adoption primaire, validation humaine, CI/Linux et clôture restent à satisfaire. Gate 3 OUVERTE — NON QUALIFIÉE ; aucun seuil RAM disponible ni publication Git.

8 octobre — Source067 : validation native complète relue PASS (1 927 tests, 52 ignorés, 165 archives), puis 16 tests acquis débris/surfaces/galets relus PASS. Checkpoint privé propre 76104d8d, index primaire préservé ; reprise LF du fichier alternates documentée. Reconstruction frontend/native démarrée, aucune parité ou qualification runtime anticipée. [Audit complet 032](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-032/review.md). Les 22 exigences et la Gate 3 restent ouvertes au niveau final ; aucun push ni seuil de RAM disponible.

Verdict Source067 — 8 octobre : [format, Clippy strict workspace/toutes cibles et tests natifs PASS revus](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-067-001/root-review.json). 1927 tests réussis, 52 ignorés, aucun échec ; 3126 fichiers source et 165 exécutables archivés vérifiés. Fermeture indépendante des identités PASS, aucun seuil de RAM disponible. Les tests ignorés d’acquisition, le rendu GPU, le candidat Studio apparié, les boucles RPG intégrées, adoption, P05, CI exacte et cible Linux restent à qualifier. Aucun remplacement primaire ni publication ; Gate 3 ouverte.

8 octobre — Source067 : 16 scripts publics et lecteurs de parité préparés, contrôle causal P02 préservé. Capture bornée du shader matériel valide préparée : trois modes mock PASS, erreurs GPU toujours disqualifiantes. Tests natifs complets encore sans revue terminale ; aucun replay ou PASS runtime Source067 acquis. [Audit complet 031](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-031/review.md). Gate 3 ouverte ; aucun push.

8 octobre — Source067 : format et Clippy strict workspace/toutes cibles PASS ; tests complets en cours, verdict à relire. Capture IA cohérente préparée : 12 cas simulés PASS, même tick public requis et attente bornée sans retarder le premier recul. Aucun PASS IA ou rivage réel acquis. [Audit complet 030](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source067-closure-audit-030/review.md). Gate 3 ouverte ; aucune adoption ni publication.

8 octobre — WebGPU réel : original invalide reproduit ; hoist contact seul révèle skBump non uniforme ; contact et trois reliefs hoistés compilent sans erreur. Deux instances fermées normalement. Source067 isolée assemblée, seul full_library.wgsl modifié ; reconstruction et replay rive requis. Aucune parité visuelle ni clôture acquise. [Audit complet 029](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-029/review.md). 22 critères conservés ; aucun push ni seuil de RAM disponible.

8 octobre — Source066 : WGSL fautif capture et SHA-verifie ; hookContactShadow appele dans branche non uniforme de shadeSurface. Proposition isolee de hoist, non appliquee. Naga accepte aussi original invalide : regression negative CPU echoue, aucune correction qualifiee ; validation WebGPU navigateur requise. Tous nos replays/probe termines, Gate 3 ouverte. [Audit complet 028](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-028/review.md). 22 criteres conserves, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : feu/degats/Gardien/quete/Save/Resume passes et fermeture normale ; reaction IA NON QUALIFIEE, lecteur public refuse HUD 9469/navigation 9473. Egalite et assertions conservees. Replay rive capture WGSL en cours, aucun correctif produit anticipe. [Audit complet 027](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-027/review.md). Gate 3 ouverte, 22 criteres conserves, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : rive NON QUALIFIEE, un shader refuse dpdx dans flux non uniforme (ligne 1385), sept erreurs GPU, route incomplete et fermeture publique normale en echec ; identites finalement drainees. Capture WGSL bornee preparee et tests mock PASS ; aucune validation desactivee. Feu/reaction IA en cours. [Audit complet 026](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-026/review.md). Gate 3 ouverte, 22 criteres conserves, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : navigation PNJ bornee PASS apres verification/rejeu de 223 observations publiques (patrouille, porte fermee/ouverte, sortie, revision et sauvegarde). Rive poussee/extinction en cours avec diagnostics shader, sans verdict. Comparaison primaire en lecture seule consignée ; aucune adoption. [Audit complet 025](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-025/review.md). Les 22 criteres restent conserves ; Gate 3 ouverte, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : P03 reference absente/diagnostic/cible editable/refus Apply PASS ; P04 etat choisi/isolation/revision 40/deux nouveaux departs PASS. P02 deja verifie ; navigation PNJ en cours sans verdict. [Audit complet 024](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-024/review.md). Les 22 criteres restent conserves ; Gate 3 ouverte, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : P02 retouche publique dialogue/porte et reprise compatible PASS, treize controles independants et deux processus fermes normalement. P04 isolation en cours ; P03, navigation et feu/reaction IA prepares sans verdict. [Audit complet 023](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-023/review.md). Les 22 criteres restent conserves ; Gate 3 ouverte, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : parcours environnement/impact/loot/quete/Save/Resume PASS causal (12 controles) et deux tests PBR exacts PASS avec supervision GPU observee du lot. P02 retouche/reprise compatible en cours ; P03/P04 et navigation prepares. [Audit complet 022](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-022/review.md). Les 22 criteres restent conserves ; Gate 3 ouverte, revue humaine/adoption/CI exacte encore requises, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : parcours RPG interieur/dialogue/combat/loot/quete/Save/Resume PASS apres revue independante, cinq attaques de contact et deux processus fermes normalement. Parcours environnement/impact en cours sans verdict ; retry PBR prepare. [Audit complet 021](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-021/review.md). Les 22 criteres sont conserves ; Gate 3 ouverte, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : les trois creations publiques (28 entites) et la reouverture/sauvegarde (22 formulaires, 65 fichiers preserves, revision 40) passent leurs revues ; zero difference semantique de contenu. Premier parcours RPG combat/interieur/loot/quete/Save/Resume demarre sans verdict. [Audit complet 020](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-020/review.md). Aucun PASS runtime historique transfere ; Gate 3 ouverte, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : creation publique de base (14 entites) puis physique (24 entites) PASS revus ; supports, masses, feu/danger et preservation des contenus verifies. Navigation publique demarree sans verdict. [Audit complet 019](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-019/review.md). Les comportements RPG/physiques runtime restent a qualifier ; Gate 3 ouverte, aucun push ni seuil de RAM disponible.

8 octobre — Source066 : candidat frontend/worker/native/assets apparié PASS revu ; 5 tests Fish CPU et 8 tests Engine GPU supervises PASS. PBR : premier test fonctionnel PASS, parser nocapture et absence de supervision empechent la qualification ; second non lance. Authoring public de base demarre sans verdict. [Audit complet 018](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-018/review.md). Aucun transfert de PASS runtime, aucun push ni seuil de RAM disponible ; Gate 3 ouverte.

8 octobre — [Source066 frontend + worker + 3000 imports : revue de parité PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-source066-frontend-root-review-001/proof.json). 3126 fichiers sources verifies ; build natif en cours, candidat et runtime non qualifies. Import WASM inutilise fish_stand_scene et future-incompat proc-macro-error2 observes dans le log. [Audit complet 017](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-017/review.md). Aucun verdict historique transfere, aucun push ni seuil de RAM disponible ; Gate 3 ouverte.

8 octobre — Source066 : 1927 tests natifs PASS / 52 ignores, puis les 16 tests acquis CPU PASS revus. Checkpoint local isole e396ff912550973b37347c6a9693d055476c56fe ; construction WASM/worker/native appariée demarree sans verdict. [Audit complet des 22 criteres](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-016/review.md). Aucun PASS runtime historique transfere, aucun seuil de RAM disponible, aucune application primaire ni publication. Gate 3 ouverte et non qualifiee.

Verdict Source066 — 8 octobre : [format, Clippy strict workspace/toutes cibles et tests natifs PASS revus](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-066-001/root-review.json). 1927 tests réussis, 52 ignorés, aucun échec ; 3126 fichiers source et 165 exécutables archivés vérifiés. Fermeture indépendante des identités PASS, aucun seuil de RAM disponible. Les tests ignorés d’acquisition, le rendu GPU, le candidat Studio apparié, les boucles RPG intégrées, adoption, P05, CI exacte et cible Linux restent à qualifier. Aucun remplacement primaire ni publication ; Gate 3 ouverte.

Rive — diagnostic du 8 octobre : les captures Source063 montrent sept erreurs WebGPU (ShaderModule/opaque_mesh_pipeline/CommandBuffer invalides) au premier échantillon de ticks et soumissions figés. La compilation initiale WGSL reste à capturer ; observateur borné préparé, régressions simulées PASS, aucun GPU ou replay lancé. Source066 format/Clippy strict PASS, tests natifs en cours. Audit complet 015 : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-015/review.md. Gate 3 ouverte, aucune publication.

Source066 — jalon du 8 octobre : format et Clippy strict workspace/toutes cibles avec --keep-going PASS, 3 126 sources inchangées vérifiées. Tests complets lancés, verdict non encore acquis. Lecteur indépendant préparé : conservation des 1 920 tests précédents, deux régressions RGB24, 52 ignorés, 165 exécutables à vérifier après clôture. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-066-001/strict-pass-milestone.json. Aucun PASS rendu ou runtime transféré ; Gate 3 ouverte.

Source066 — 8 octobre : cinq diagnostics de probes corrigés dans trois fichiers isolés, 3 126 sources et toutes les assertions conservées, aucun nouvel allow. Contrôles format/Clippy strict workspace/toutes cibles/tests complets lancés, aucun verdict anticipé. Audit intégral 014 : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source066-closure-audit-014/review.md. Les preuves runtime antérieures restent liées à leur candidat ; Gate 3 ouverte, aucune adoption ou publication.

Source065 — résultat terminal du 8 octobre : format PASS, Clippy strict FAIL sur cinq diagnostics des probes Tidewater ; défaut RGB24 corrigé, tests complets non démarrés. Garde sans dépassement et fermeture indépendante PASS. Proposition de fermeture rive préparée avec délai inchangé de dix secondes, syntaxe PASS, runtime non rejoué. Audit intégral 013 : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source065-closure-audit-013/review.md. Gate 3 ouverte ; aucune adoption ou publication.

Source065 — 8 octobre : correction isolée de l’itération RGB24 signalée par Clippy Source064, 3 126 fichiers et toutes les assertions préservés. Source064 : format PASS, Clippy FAIL, tests non démarrés, aucune protection déclenchée, fermeture indépendante PASS. Source065 : contrôles complets lancés, aucun verdict anticipé. Audit intégral 012 : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source065-closure-audit-012/review.md. Aucun PASS runtime transféré ; Gate 3 ouverte, aucune adoption primaire ou publication.

Source064 — 8 octobre : assemblage isolé de 3 126 fichiers préservant les derniers changements primaires, 3 conflits résolus et 5 fusions revues, deux tests RGB24 ajoutés. Format PASS ; Clippy strict workspace/toutes cibles puis tests complets lancés. Source063 rive échouée avant interactions et arrêt forcé du test (garde sans dépassement, fermeture indépendante PASS), échec conservé. Audit intégral 011 : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source064-closure-audit-011/review.md. Aucun PASS Source063 transféré, aucune adoption primaire ou publication ; Gate 3 ouverte.

Source063 — 8 octobre : navigation 002 PASS après seul amorçage calibré du helper, mêmes trajet/cibles/tolérances/délais. Patrouille, porte fermée sans chemin, ouverture/sortie, révision et sauvegarde vérifiées par revue indépendante : E:/OpenRPG_Forge/.gate3-evidence/20261002/public-navigation-preparation-063-002/root-review.json. Fermeture PASS ; échec 001 conservé. Parcours rive prêt et conditionné à ce verdict réel ; feu/danger encore préparé. Dérive primaire à réconcilier, P05 humain, CI exacte, Linux et clôture propriétaire requis. Gate 3 ouverte, aucune publication.

Source063 — 8 octobre : navigation 001 non qualifiée, centrage joueur 0,324 m hors tolérance 0,1 m / 120 s ; garde sans dépassement, fermeture indépendante PASS. Rejeu 002 en cours avec seul amorçage de calibration testé du helper, mêmes cibles et seuils. Inventaire primaire : 17 dérives historiques, 45 nouveaux chemins ; sources produit concernées préservées avant réconciliation. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/source063-navigation-retry-and-primary-drift-001/proof.json. Aucune adoption ou publication ; Gate 3 ouverte.

Source063 — 8 octobre : P02 retouche/reprise compatible, P03 diagnostic référence absente menant à cible éditable et P04 départ choisi/révision/isolation passent les revues indépendantes. Fermetures PASS. Audit complet 010 des 22 critères : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source063-closure-audit-010/review.md. Navigation PNJ en cours sans verdict ; rive et feu/danger préparés. P05 humain, adoption primaire, CI exacte, Linux et clôture propriétaire encore requis. Aucune publication.

Source063 — 8 octobre : P02 retouche publique dialogue/porte et reprise compatible PASS, treize contrôles indépendants, deux processus fermés normalement. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/public-compatible-retouch-preparation-063-001/root-review.json. P04 en cours ; P03, navigation PNJ et rive poussée/feu/extinction préparés séparément, non qualifiés. Aucun ancien verdict transféré, adoption primaire ou publication ; Gate 3 ouverte.

Source063 — 8 octobre : les deux nouvelles boucles publiques combat et environnement passent les revues indépendantes, avec sauvegarde/reprise et fermeture normale. Impact 405,08 J / 100 dégâts neutralisant l’ennemi avant tout combat. Audit complet 009 des 22 critères : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source063-closure-audit-009/review.md. P02 en cours ; P03/P04 préparés, non qualifiés. Navigation PNJ et feu/poussée/eau/danger, revue P05, adoption primaire, CI exacte et Linux restent requis. Gate 3 ouverte, aucune publication.

Source063 — 8 octobre : nouveau parcours public combat / butin / quête / sauvegarde / reprise PASS, douze contrôles indépendants et deux processus fermés normalement, source 65 fichiers préservée. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/public-fresh-gameplay-preparation-063-001/root-review.json. Pics 5,13 GiB RAM privée / 1,84 GiB GPU dédié, aucun plafond atteint ni seuil minimum de RAM disponible. Parcours environnement en cours sans verdict ; P02 retouche dialogue et porte préparé sur cette sauvegarde actuelle, non lancé. Gate 3 ouverte et non qualifiée, aucune adoption primaire ou publication.

Source063 — 8 octobre : réouverture publique Studio de 22 éléments PASS, champs et sauvegarde complète concordants à la révision 40, 65 fichiers source préservés, fermeture normale et indépendante PASS. Comparaison de contenu : zéro différence sémantique gameplay, corrections hors gameplay identiques ; aucun résultat runtime historique transféré. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/public-fresh-reopen-preparation-063-001/root-review.json. Parcours frais combat/loot/quête/Save/Resume lancé ; approche environnementale préparée sans lancement parallèle. Revue humaine P05, adoption primaire, CI exacte et clôture restent requises. Gate 3 ouverte, aucune publication.

Source063 — 8 octobre : chaîne de création publique Studio fraîche PASS (base 14, physique 24, navigation 28 entrées, 22 éléments spatiaux), trois fermetures indépendantes PASS. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/source063-fresh-authoring-chain-review-001/proof.json. Deux PNJ et dialogues supplémentaires, points de patrouille et conservation des 24 entrées précédentes vérifiés. Réouverture publique complète, retouches/diagnostic et les deux approches RPG intégrées restent à qualifier sur ce candidat. Aucun comportement runtime ni revue humaine inféré ; Gate 3 ouverte, aucune publication.

Source063 — 8 octobre : création physique publique corrigée PASS, 24 entrées vérifiées avec supports, masse, poussée/lancer, piège, feu et danger ; source et non-gameplay préservés, fermeture indépendante PASS. Le lecteur initial a été recopié avec UTF-8 explicite (nom du Gardien), contrôle initial négatif préservé ; seules les retouches prévues passent. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/public-physics-authoring-preparation-063-002/root-review.json. Création publique navigation 063-001 lancée ; aucun comportement runtime ou PASS global inféré. Gate 3 ouverte, aucune publication.

Source063 — 8 octobre : huit tests GPU Forge Engine PASS avec échantillon GPU et fermeture indépendante PASS ; aucun rendu de scène qualifié. Création fraîche Studio de base PASS : 14 entrées, liens quête/dialogue/lieux/supports et sauvegarde vérifiés, source préservée et fermeture normale. Preuves : integrated-source063-native-gpu-engine-001/root-review.json et public-base-authoring-preparation-063-001/root-review.json sous E:/OpenRPG_Forge/.gate3-evidence/20261002. Étape physique 063-001 FAILED_CLOSED sur un argument Process omis dans le harness, source et fermeture préservées ; correction isolée et replay 063-002 lancé. Navigation, réouverture et deux approches RPG encore requises. Gate 3 ouverte ; aucune publication.

Source063 — 8 octobre : build frontend/worker et Studio natif apparié PASS, revue complète 3 074 sources + assets figés, candidat a219fb68bb292f9206b2977b40ca365b474b2eabe7d7106b60e2595cd89d8864. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-studio-candidate-root-review-063001/proof.json. Deux tests GPU PBR passent sur Radeon RX 7800 XT/Dx12, mais garde candidate_seen=false : supervision FAILED_CLOSED, fermeture indépendante PASS. Requalification de supervision, Studio et RPG intégrés encore requise. Gate 3 ouverte ; aucun produit primaire appliqué ni publication.

Source063 — 8 octobre : checkpoint privé propre ff7534f7aa4212ed1b55506df9ff8e34af646451, parent publié 6b001dbe, 3 074 fichiers validés inchangés. Preuve : E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-source063-private-checkpoint-001/proof.json. Build frontend/worker puis Studio natif apparié lancé : integrated-source063-matched-build-preparation-001. Garde CPU 6 Gio sans seuil de RAM libre, aucune scène rejouée ni PASS de build/runtime anticipé. Git et produit primaires préservés ; Gate 3 ouverte, aucune publication.

Source063 — 8 octobre : audit complet 008 conservant les 22 exigences : E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-integrated-source063-closure-audit-008/review.md. Format/Clippy strict/tests natifs PASS revus (1 920 réussis, 52 ignorés), puis 16 tests d’acquisition Debris/Scanned/Pebble PASS séparés et fermetures indépendantes PASS. La qualification GPU/Studio/deux approches RPG intégrée, l’adoption propriétaire, P05 humain, la CI exacte et la cible Linux restent à traiter. Le primaire produit reste préservé ; Gate 3 ouverte, aucune publication.

Verdict Source063 — 8 octobre : [format, Clippy strict workspace/toutes cibles et tests natifs PASS revus](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-063-001/root-review.json). 1920 tests réussis, 52 ignorés, aucun échec ; 3074 fichiers source et 165 exécutables archivés vérifiés. Fermeture indépendante des identités PASS, aucun seuil de RAM disponible. Les tests ignorés d’acquisition, le rendu GPU, le candidat Studio apparié, les boucles RPG intégrées, adoption, P05, CI exacte et cible Linux restent à qualifier. Aucun remplacement primaire ni publication ; Gate 3 ouverte.

Source063 — 8 octobre : format PASS et Clippy strict workspace/toutes cibles PASS effectivement terminés ; preuve locale : E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-063-001/strict-pass-milestone.json. La commande complète de tests a démarré ; aucun PASS de tests, GPU, Studio ou RPG intégré anticipé. Les entrées Debris/Nature sont figées dans integrated-debris-cpu-acquired-inputs-001 (4 906 fichiers, 17 832 444 octets), sans test complémentaire exécuté. Primaire produit préservé ; Gate 3 ouverte, aucune publication.

Intégration Source063 — 8 octobre : [corrections isolées de 2 fichiers, diff revu et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source063-current-integration-review-001/review.md). Source062 termine sur 2 diagnostics Clippy, aucun test exécuté ; échec préservé. Source063 conserve 3074 sources et lance les contrôles natifs complets sans minimum de RAM disponible. Aucun PASS intégré anticipé, aucune application produit primaire ni publication ; Gate 3 ouverte.

Verdict Source062 — 8 octobre : [format PASS, Clippy strict FAIL sur 2 diagnostics, sources figées vérifiées](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-062-001/root-review.json). Aucun test exécuté ; 3074 empreintes concordantes et garde terminée normalement sans dépassement, arrêt forcé ni descendant restant. Les états de lancement ci-dessous restent historiques. Aucun minimum de RAM disponible, aucune application au primaire ni publication ; Gate 3 ouverte.

Intégration Source062 — 8 octobre : [corrections isolées de 1 fichiers, diff revu et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source062-current-integration-review-001/review.md). Source061 termine sur 1 diagnostics Clippy, aucun test exécuté ; échec préservé. Source062 conserve 3074 sources et lance les contrôles natifs complets sans minimum de RAM disponible. Aucun PASS intégré anticipé, aucune application produit primaire ni publication ; Gate 3 ouverte.

Verdict Source061 — 8 octobre : [format PASS, Clippy strict FAIL sur 1 diagnostics, sources figées vérifiées](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-061-001/root-review.json). Aucun test exécuté ; 3074 empreintes concordantes et garde terminée normalement sans dépassement, arrêt forcé ni descendant restant. Les états de lancement ci-dessous restent historiques. Aucun minimum de RAM disponible, aucune application au primaire ni publication ; Gate 3 ouverte.

Intégration Source061 — 8 octobre : [corrections isolées de 19 fichiers, diff revu et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source061-current-integration-review-001/review.md). Source060 termine sur 42 diagnostics Clippy, aucun test exécuté ; échec préservé. Source061 conserve 3074 sources et lance les contrôles natifs complets sans minimum de RAM disponible. Aucun PASS intégré anticipé, aucune application produit primaire ni publication ; Gate 3 ouverte.

Verdict Source060 — 8 octobre : [format PASS, Clippy strict FAIL sur 42 diagnostics, sources figées vérifiées](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-060-001/root-review.json). Aucun test exécuté ; 3074 empreintes concordantes et garde terminée normalement sans dépassement, arrêt forcé ni descendant restant. Les états de lancement ci-dessous restent historiques. Aucun minimum de RAM disponible, aucune application au primaire ni publication ; Gate 3 ouverte.

Intégration Source060 — 8 octobre : [corrections isolées de 1 fichiers, diff revu et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source060-current-integration-review-001/review.md). Source059 termine sur 10 diagnostics Clippy, aucun test exécuté ; échec préservé. Source060 conserve 3074 sources et lance les contrôles natifs complets sans minimum de RAM disponible. Aucun PASS intégré anticipé, aucune application produit primaire ni publication ; Gate 3 ouverte.

Verdict Source059 — 8 octobre : [format PASS, Clippy strict FAIL sur 10 diagnostics, sources figées vérifiées](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-059-001/root-review.json). Aucun test exécuté ; 3074 empreintes concordantes et garde terminée normalement sans dépassement, arrêt forcé ni descendant restant. Les états de lancement ci-dessous restent historiques. Aucun minimum de RAM disponible, aucune application au primaire ni publication ; Gate 3 ouverte.

Intégration Source059 — 8 octobre : [corrections isolées de 1 fichiers, diff revu et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source059-current-integration-review-001/review.md). Source058 termine sur 6 diagnostics Clippy, aucun test exécuté ; échec préservé. Source059 conserve 3074 sources et lance les contrôles natifs complets sans minimum de RAM disponible. Aucun PASS intégré anticipé, aucune application produit primaire ni publication ; Gate 3 ouverte.

Verdict Source058 — 8 octobre : [format PASS, Clippy strict FAIL sur 6 diagnostics, sources figées vérifiées](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-058-001/root-review.json). Aucun test exécuté ; 3074 empreintes concordantes et garde terminée normalement sans dépassement, arrêt forcé ni descendant restant. Les états de lancement ci-dessous restent historiques. Aucun minimum de RAM disponible, aucune application au primaire ni publication ; Gate 3 ouverte.

Intégration Source058 — 8 octobre : [corrections isolées de 7 fichiers, diff revu et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source058-current-integration-review-001/review.md). Source057 termine sur 8 diagnostics Clippy, aucun test exécuté ; échec préservé. Source058 conserve 3074 sources et lance les contrôles natifs complets sans minimum de RAM disponible. Aucun PASS intégré anticipé, aucune application produit primaire ni publication ; Gate 3 ouverte.

Verdict Source057 — 8 octobre : [format PASS, Clippy strict FAIL sur 8 diagnostics, sources figées vérifiées](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-057-001/root-review.json). Aucun test exécuté ; 3074 empreintes concordantes et garde terminée normalement sans dépassement, arrêt forcé ni descendant restant. Les états de lancement ci-dessous restent historiques. Aucun minimum de RAM disponible, aucune application au primaire ni publication ; Gate 3 ouverte.

Intégration Source057 — 8 octobre : [détection des changements poissons extraite et imports tests explicités, diff revu et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source057-current-integration-review-001/review.md). Source056 termine sur un seul diagnostic de longueur de l’orchestrateur (109 lignes), aucun test exécuté et garde normale ; échec préservé. Source057 conserve 3 074 fichiers, modifie deux fichiers et lance les contrôles natifs complets sans nouvelle dérogation de lint ni minimum de RAM disponible. Aucun PASS intégré anticipé, aucune application produit primaire ni publication ; Gate 3 ouverte.

Intégration Source056 — 8 octobre : [sept extractions moteur revues, format PASS et contrôle natif complet lancé](E:/OpenRPG_Forge/.gate3-evidence/20261002/source056-current-integration-review-001/review.md). Source055 est terminé avec les sept seuls diagnostics de longueur, aucun test exécuté et garde normale. Source056 conserve 3 074 fichiers, modifie sept fichiers et garde les validations, comptes mémoire et cadence/publications GPU ; aucune nouvelle dérogation de lint. Préparations refusées sur ancres ambiguës et corrections de revue avant compilation archivées. Aucun minimum de RAM disponible, aucun PASS natif/runtime intégré anticipé, aucun remplacement produit primaire ni publication ; Gate 3 ouverte.

Intégration Source055 — 8 octobre : [contrats moteur revus, corrections isolées de 16 fichiers et format PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/source055-current-integration-review-001/review.md). Comparaisons source exactes et signatures système Bevy conservées, API de programme empruntée et neuf appels locaux adaptés, trois requêtes nommées sans changement de filtres. Première préparation refusée sur une ancre incorrecte avant build et conservée ; tentative 002 repartant des 3 074 sources Source054 vérifiées. Contrôles natifs complets lancés sans minimum de RAM disponible ; sept fonctions longues restent à revoir. Aucun PASS natif/runtime anticipé, aucun remplacement produit primaire ni publication ; Gate 3 ouverte.

Verdict Source054 — 8 octobre : [format PASS, Clippy strict FAIL sur 38 diagnostics, sources figées vérifiées](E:/OpenRPG_Forge/.gate3-evidence/20261002/full-workspace-native-validation-054-001/root-review.json). 29 cas différés, huit imports inutilisés issus des suggestions explicites et un point-virgule restent à corriger. Aucun test exécuté. Garde CPU terminée normalement sans dépassement, arrêt forcé ni descendant restant, sans minimum de RAM disponible. Les états de lancement ci-dessous restent historiques ; aucune qualification intégrée ni clôture de Gate 3 déduite.

Intégration Source054 — 8 octobre : [51 diagnostics moteur corrigés et revus dans la copie isolée](E:/OpenRPG_Forge/.gate3-evidence/20261002/source054-current-integration-review-001/review.md). Source053 est terminé : format PASS, Clippy FAIL sur 80 diagnostics moteur, aucun test exécuté ; sources et échec préservés. Source054 conserve les 3 074 fichiers, modifie 19 fichiers, passe format et lance les contrôles natifs complets. 29 diagnostics précédents restent à revoir. Aucun minimum de RAM disponible ; plafond de consommation privée conservé à 6 Gio. Aucun PASS intégré anticipé, aucune application au primaire ni publication ; Gate 3 ouverte.

Intégration complète isolée — 8 octobre : [44 conflits et 15 fusions revus, workspace de 3 074 fichiers assemblé](E:/OpenRPG_Forge/.gate3-evidence/20261002/source053-current-integration-review-001/review.md). 403 ajouts candidat et fichiers non suivis du primaire inclus, fonctions SourceGraph/Debris/étal complet et protections océaniques conservées. Source051 : format PASS, Clippy FAIL sur 13 diagnostics Assets ; Source052 : référence sampler hors portée détectée ; échecs conservés, gardes sans dépassement ni arrêt forcé. Source053 corrige la portée, passe format et lance Clippy/tests natifs complets sous plafond de consommation de 6 Gio, sans seuil de RAM disponible. Aucun PASS intégré anticipé, aucune application au primaire ni publication ; adoption, runtime, P05/CI et Gate 3 restent ouverts.

Intégration isolée source050 — 8 octobre : [29 conflits résolus et revérifiés, 15 encore ouverts](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-primary-reviewed-resolutions-004/review.md). Les lots conservent les ajouts SourceGraph/Debris et la correction du delta océanique du primaire ; trois résolutions combinent du code des deux côtés et ne sont pas encore compilées. Aucun fichier produit original remplacé par ces lots. Les 15 fusions sans marqueur restent à revoir, les 403 ajouts à assembler, puis le nouvel ensemble à qualifier. Vent partagé/Nature et capture cloud avant sortie du runner demandent encore une conciliation. Les inventaires ci-dessous restent datés. Aucune adoption ni publication ; Gate 3 ouverte/non qualifiée.

Deux approches fraîches source050 — 8 octobre : [création Studio, réouverture et parcours RPG relus PASS machine](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-fresh-two-approaches-review-001/review.md). Combat et environnement ont chacun mené au butin, à la quête terminée, au Save et à la reprise dans un nouveau processus ; intérieur visité, neutralisation environnementale prouvée avant combat,12 contrôles causaux PASS. Fermetures/drainages normaux et indépendants,65 fichiers sources conservés, aucun plafond dépassé ni seuil de RAM disponible. Même tête canonique complète855eb32b/révision40 vérifiée sur archive fraîchebc20c3a2. [Audit des22 critères](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-closure-audit-006/review.md) conserve P05/adoption/CI/Linux/clôture ouverts. Proposition d’intégration récente isolée :46 changements candidat seul,36 fichiers primaire/fins de ligne préservés,1 suivi primaire conservé intégralement,15 fusions textuelles à revue,44 conflits à résoudre ; aucune application ni qualification du workspace fusionné. Gate3 ouverte/non qualifiée.

Boucle RPG fraîche source050 — 8 octobre : [intérieur, combat, butin, quête, Save et nouveau processus Resume relus PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/public-fresh-gameplay-preparation-050-001/root-review.json),12 contrôles acquis, cinq contacts de combat réels20 dégâts chacun, gardien neutralisé, Sceau consommé et objectif Completed sauvegardé puis repris. Archive fraîche `bc20c3a2…`/révision40,65 fichiers sources conservés ; garde propre, drainage normal et fermeture indépendante PASS. Seconde approche environnementale fraîche lancée après cette fermeture : `public-fresh-environment-quest-preparation-050-001/started-proof.json`, verdict non anticipé. [Audit des22 critères actualisé](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-closure-audit-005/review.md). Baselines complémentaires :52 ajouts présents des deux côtés et absents de l’arbre/manifeste initiaux,2 baselines brutes authentifiées par SHA ; réconciliation toujours non appliquée. Aucun seuil de RAM disponible, aucune publication ; Gate3 ouverte/non qualifiée.

Intégration source050 — baseline snapshot relue le8 octobre : le commit initial privé `6c968efc…` existe encore. Parmi142 écarts examinés (139 divergences et3 fins de ligne dans l’inventaire antérieur), les SHA du manifeste initial authentifient88 baselines :6 changements candidat seul,11 changements primaire seul à préserver,70 modifications des deux côtés et1 écart de fins de ligne.54 baselines restent non authentifiées ; ne pas les traiter comme fusionnables. [Revue de baseline](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-primary-snapshot-baseline-review-001/review.md). Aucune ascendance Git commune ni fusion acquise revendiquée, octets et état Git primaires inchangés pendant cette lecture. Réconciliation isolée, adoption propriétaire et requalification restent requises ; Gate3 ouverte/non qualifiée.

Création fraîche source050 réouverte — 8 octobre : [lecture publique des22 fiches et Save relus PASS](E:/OpenRPG_Forge/.gate3-evidence/20261002/public-fresh-reopen-preparation-050-001/root-review.json), archive fraîche `bc20c3a2…`, révision40, contenu complet inchangé et65 fichiers sources conservés. Fermeture normale, drainage et revue indépendante PASS. L’option explicite de réouverture du même candidat conserve le refus par défaut et les mêmes contrôles de source/Save/head ; quatre cas du prédicat effectif et parse PowerShell PASS. La chaîne base/physique/navigation est créée puis réouverte publiquement ; aucun ancien verdict runtime hérité. Boucle RPG complète sur cette nouvelle archive lancée : `public-fresh-gameplay-preparation-050-001/started-proof.json`, verdict non anticipé. Aucun seuil de RAM disponible. Gate3 ouverte/non qualifiée.

Comparaison finale source050 fraîche — 8 octobre : zéro différence sémantique gameplay et toutes les corrections hors gameplay identiques à la référence qualifiée ; aucune retouche supplémentaire requise. [Revue de parité du contenu](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-fresh-authoring-chain-review-001/content-parity-review.json). Création publique des22 entités spatiales/28 entrées démontrée ; réouverture publique de l’ensemble et replays sur cette archive fraîche restent à exécuter, sans hériter des résultats runtime du checkpoint antérieur. Adoption primaire, P05 humain, CI exacte et clôture propriétaire restent requis. Gate3 ouverte/non qualifiée.

Création fraîche consolidée source050 — 8 octobre : trois étapes Studio publiques relues PASS sur candidat050002, base14 entrées puis physique24 puis navigation28, soit22 entités spatiales (6 acteurs,12 objets,4 lieux) plus1 item,4 dialogues,1 quête. Supports/feux/danger et patrouilles/dialogues vérifiés dans les nouvelles archives ; contenu parent conservé et trois fermetures normales/indépendantes PASS. [Chaîne de création fraîche](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-fresh-authoring-chain-review-001/review.md). La comparaison sémantique préparée reste en lecture seule : retouches finales, réouverture de l’ensemble et replays sur ce nouveau contenu restent requis. Les résultats runtime du checkpoint antérieur ne sont pas transférés à ces archives. Aucun seuil de RAM disponible, aucune publication ; Gate3 ouverte/non qualifiée.

Création fraîche source050 — 8 octobre : base créée dans le candidat050002 à partir d’une source Tidewater authentifiée sans gameplay. Revue indépendante PASS :14 entités (3 acteurs,1 item,5 objets,3 lieux,1 dialogue,1 quête), liens quête/dialogue/lieux/support vérifiés dans l’archive réellement sauvegardée,28 fichiers sources conservés, fermeture native normale et fermeture indépendante PASS. [Preuve de création de base](E:/OpenRPG_Forge/.gate3-evidence/20261002/public-base-authoring-preparation-050-001/root-review.json). Aucun verdict runtime ancien hérité et aucune création complète encore revendiquée. Extension physique lancée sur une copie de cette base : `public-physics-authoring-preparation-050-001/started-proof.json` ; verdict encore à relire. Navigation, retouches finales, sauvegarde/réouverture de l’ensemble et comportement en jeu sur ce nouveau contenu restent requis. Aucun seuil de RAM disponible, plafonds de consommation conservés. Gate3 ouverte/non qualifiée.

Feu009 terminé : commande0, drainage normal et fermeture indépendante PASS, premier acquittement clavier974.2608ms/1000ms. Allumage, dégâts thermiques réels au joueur vivant et au gardien, neutralisation environnementale avant combat, butin, quête terminée, huit Saves cohérents et reprise dans un nouveau processus vérifiés. Le lecteur initial reste **non qualifié** pour `danger_ai` : Save d’exposition avant mouvement, Saves suivants après fin du danger. Rapport conservé : `public-environment-fire-quest-preparation-050-009/root-review.json`. [Preuve complémentaire IA publique](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-fire009-public-ai-causal-review-001/review.md) PASS : exposition/fuite/LOS enregistrées au tick11635, diagnostic public post-writeback KCC au tick11683 de même source, déplacement1.5948m à2m/s opposé au feu, acteur sans patrouille ni commande ciblée, position persistante après danger puis Resume ; onze contre-exemples rejetés. Aucune sortie complète du danger ni acceptation humaine déduite. Ces preuves doivent être consolidées séparément dans l’audit ; le verdict initial n’est pas remplacé.

Provenance : seize ledgers d’authoring reliés par SHA sont restitués jusqu’à la création initiale de l’aventure ; vérification des commandes d’origine encore requise, aucune nouvelle création source050 revendiquée. Preuve `source050-authoring-provenance-chain-001/proof.json`. [Inventaire intégration actuel](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-primary-integration-inventory-001/review.md) :2175 fichiers identiques,403 absents du primaire,139 divergences existantes,3 écarts de fins de ligne ; octets et état Git primaires conservés, aucun fichier intégré. La baseline Git historique n’est pas accessible dans les snapshots inspectés : aucune fusion à trois branches ou ancien dossier Source9 réutilisé implicitement. Gate3 ouverte/non qualifiée ; restent authoring/consolidation, P05 propriétaire, adoption primaire, CI exacte, GLib Linux et clôture formelle.

Feu008 terminal négatif : première touche1263.6875ms pour1000ms, approche/recul/capture Save d’exposition passés, aucune boucle complète acquise. Garde terminal sans arrêt forcé ni plafond mémoire, fermeture indépendante PASS ; drainage normal absent, ne pas en déduire un drainage complet. Preuve : `public-environment-fire-quest-preparation-050-008/terminal-review.json`. L’attente asynchrone de capture n’a pas démontré de gain. Feu009 lancé après fermeture008 : l’acquittement Ignite utilise un sondage25ms et reporte ses diagnostics GPU additionnels après relâchement des premières touches, avant le trajet radial. Observateur d’allocation conservé, garde OS indépendante inchangée ; ce changement d’ordonnancement diagnostique ne prouve pas une amélioration produit. Fonctions effectives extraites : observation publique conservée, comportement par défaut, report opt-in, sondages25/200ms et refus à échéance testés PASS. Préparation `public-environment-fire-quest-preparation-050-009/preparation-proof.json`, lancement `started-proof.json` du même dossier ; aucun verdict natif anticipé. Gate3 ouverte/non qualifiée.

Feu008 lancé après fermeture indépendante007 : copie isolée du seul bloc d’observation de capture, avec une lecture CDP attendue et sondage navigateur25ms, mêmes focus/visibilité/pointer lock/exploration/entrée active et même échéance1000ms. Onze cas de JavaScript effectif passent, dont readiness retardée, refus immédiat/retardé, garde manquante et readiness après échéance ; SelfTest PowerShell PASS. Préfixe et suffixe du helper comparés, autres scripts inchangés. Aucune qualification native déduite de ces tests. Préparation : `public-environment-fire-quest-preparation-050-008/preparation-proof.json`, lancement : `started-proof.json` du même dossier. Aucun seuil de RAM disponible ; plafonds6GiB privés/4GiB GPU dédiée/2GiB GPU partagée conservés. Gate3 ouverte/non qualifiée.

Actualisation du 7 octobre après feu007 : rive002, P03 et P04 sont relus PASS sur source050/candidat050002, en complément de gameplay, navigation, impact et P02. Feu006 échoue sur le type `PSCustomObject` du plan ; la copie007 corrige ce défaut et les tests utilisent la véritable fabrique du plan. Feu007 atteint l’approche à0.02087m, allume réellement, atteint le recul radial et valide la capture Save d’exposition, mais le premier acquittement clavier arrive à1066.4121ms pour un budget1000ms : boucle complète non qualifiée. La garde a tenté une fermeture forcée après survie d’un descendant et signalé une erreur de nettoyage ; la revue indépendante ultérieure confirme la fermeture de toutes les identités. Aucun plafond mémoire déclenché, aucun seuil minimal de RAM disponible. Ne pas qualifier cette fermeture de normale. Preuves : `public-environment-fire-quest-preparation-050-006/terminal-review.json`, `public-environment-fire-quest-preparation-050-007/terminal-review.json` et [audit actualisé de tous les critères](E:/OpenRPG_Forge/.gate3-evidence/20261002/source050-closure-audit-002/review.md). Restent notamment la boucle feu/danger complète, la consolidation de provenance d’authoring, P05 propriétaire, l’intégration primaire, la CI exacte et GLib Linux. Gate3 ouverte/non qualifiée.

P03 et P04 source050 relus PASS après terminaux0, drainage normal et fermeture indépendante. P03 : diagnostic de référence invalidée mène au Sceau éditable et refuse la révision invalide, aucune suppression invalide enregistrée. P04 : départ choisi, révision conservée, simulation et Save isolés, second nouveau processus/fresh start ; aucune physique bit-identique revendiquée. Preuves : `public-invalid-quest-preparation-050-001/root-review.json` et `public-replay-isolation-preparation-050-001/root-review.json`. Feu006 lancé après P03 fermé : deux gestes trusted Save/canvas avec six acquittements, cibles fraîches exposées, focus/visibilité/capture relâchée explicitement vérifiés, cooldown1500ms acquis avant pairing ; chemin original complet conservé sinon. Après les gestes, pointer lock exact, exploration, entrée active et document focalisé/visible toujours exigés. Recul stabilisé relâché vers mêmes coordonnées,0.15m/12s, premier recul1000ms et Save original/heat vivant inchangés. Régressions protocolaires et gardes publiques PASS, aucun résultat natif006 acquis par préparation. Preuve : `public-environment-fire-quest-preparation-050-006/preparation-proof.json`. Gate3 reste ouverte/non qualifiée.

Rive050002 terminal PASS, revue causale racine et fermeture indépendante PASS : route vérifiée51 arrivées, poussée réelle, allumage/extinction et reçus nouveaux, Save cohérent puis reprise dans un second processus. Sources65 préservées, aucun arrêt forcé ni plafond mémoire, drainage normal acquis. Preuve : `shore-source050-preparation-002/root-review.json`. Ce PASS remplace l’échec rive001 au périmètre de la boucle rive, sans qualification anticipée du feu forêt ou de la gate. P04 source050 est ensuite lancé, départ de test choisi et isolation depuis le même candidat ; aucun PASS P04 hérité de049. Gate3 reste ouverte/non qualifiée.

Préparation du chemin critique feu006 : primitive distincte pour deux gestes souris trusted ordonnés (Save puis canvas) testée sur WebSocket réel, huit cas PASS. Tous six messages sont reçus avant tout acquittement dans la fixture ; six réponses exactes exigées, ordre inversé/événement/fragment acceptés, erreur ou résultat absent au second geste, acquittement final absent et délai expiré refusés ; coordonnées non finies rejetées avant transport. Preuve : `public-fire-two-click-protocol-regression-050-006/proof.json`. Cette primitive n’est pas encore intégrée au scénario natif et ne prouve ni capture publique ni délai de recul. Aucun contrôle de focus/visibilité/hit-test/cooldown ne sera remplacé par le seul succès transport. Rive050002 reste le seul replay natif en cours à cette actualisation ; Gate3 ouverte/non qualifiée.

Feu050005 terminal relu : approche finale stabilisée0.02998m pour0.04m, ignition réelle, puis recul1189.9844ms pour1000ms et trajet radial refusé en12s. Sources/candidat/helpers préservés ; aucun arrêt forcé ni plafond mémoire, identités fermées. Preuve : `public-environment-fire-quest-preparation-050-005/terminal-review.json`. Rive050002 démarre après cette fermeture avec helper calibré testé, mêmes coordonnées,4cm et12s au flanc, contact2.5cm inchangé. La syntaxe du scénario et le SelfTest physique passent ; le script EnvironmentQuest ne fournit pas de paramètre SelfTest, son appel erroné a été corrigé vers le lecteur syntaxique et le SelfTest physique disponible. Aucun PASS runtime002 acquis. Audit complet source050 : `source050-closure-audit-001/review.md` (snapshot antérieur au terminal005 ; ce paragraphe en actualise le feu). Revue humaine, adoption primaire, CI exacte, glib distribution et clôture propriétaire restent non acquises.

Rive050001 terminal relu non qualifié : route complète vérifiée,51 arrivées, puis approche de l’objet41.50060/-33.48904 refusée avant poussée. Aucun résultat allumage/extinction/Save/Resume promu. Sources/candidat/harness préservés, aucun plafond déclenché, fermeture indépendante PASS. Preuve : `shore-source050-preparation-001/root-review.json`. Feu050005 lancé après cette fermeture avec calibration20ms testée et mêmes critères. P03/P04 source050 sont préparés et leurs autotests passent, mais aucun parcours natif050 correspondant encore acquis ; préparations `public-invalid-quest-preparation-050-001` et `public-replay-isolation-preparation-050-001`. Gate3 reste ouverte/non qualifiée.

Préparation feu050005 : les16 impulsions de l’approche004 étaient toutes de1ms, donc aucune vitesse longue n’était mesurée et le contrôle proportionnel restait sans calibration. La copie005 ajoute une impulsion relâchée de20ms lorsque la vitesse mesurée manque, puis conserve l’estimation par déplacement public réel. Test de la fonction effective sous deux vitesses, trois dernières observations dans0.04m et refus d’un trajet bloqué à l’échéance PASS ; cela ne qualifie pas la physique native. Tolérance0.04m, budget12s, premier recul1000ms et cooldown1500ms inchangés. Preuve : `public-fire-calibrated-approach-helper-preparation-050-005/regression-proof.json`. Essai005 préparé mais non lancé ; fermeture indépendante de rive050 exigée avant tout lancement. Gate3 reste ouverte/non qualifiée.

Actualisation suivante du 7 octobre : feu050004 terminal négatif avant ignition. Attestation kernel et chargement passent ;35 observations de l’approche finale, minimum0.12937m pour0.04m dans12s. Aucun arrêt forcé du harness ou du garde-fou, aucun plafond mémoire déclenché, sources/candidat/helpers conservés, fermeture indépendante PASS. Aucun premier recul corrigé mesuré. Preuve : `public-environment-fire-quest-preparation-050-004/terminal-review.json`. Replay rive050 lancé ensuite après cette fermeture : poussée, allumage, eau/extinction, Save/Resume avec critères identiques, seul lancement attesté par handle kernel. Préparation : `shore-source050-preparation-001/preparation-proof.json`. Résultat rive et feu non qualifiés à ce stade ; Gate3 ouverte.

Actualisation du 7 octobre, feu050 : les helpers de chemin critique passent les régressions de focus/cooldown/activation et le JavaScript réel du ciblage avec DOM de test. L’essai003 échoue avant gameplay sur module/path Windows indisponible ; arrêt forcé individuel du candidat par le harness, puis fermeture indépendante PASS. La correction de latence n’a pas été exercée. Preuve négative : `public-environment-fire-quest-preparation-050-003/terminal-review.json`. Une nouvelle copie004 atteste l’image par le handle kernel retenu avec mêmes vérifications de PID/création/chemin/SHA ; worker réel positif et chemins/hash/création/identité manquante/processus sorti rejetés, sortie worker normale. Replay complet004 lancé après fermeture003, aucun seuil de RAM disponible, budgets1000ms/1500ms/12s et tolérances inchangés. Preuve de préparation : `public-environment-fire-quest-preparation-050-004/preparation-proof.json`. Aucun PASS feu ni clôture Gate déduit des tests du helper ou du lancement.

> État courant — 7 octobre : Source050 / candidat050002. Format, Clippy strict et tests workspace natifs PASS (1801 tests réussis,31 ignorés) ; checkpoint public, boucle RPG, R3-P02 compatible, navigation et boucle complète par impact relus PASS sur ce candidat. Feu050002 non qualifié : premier recul1451.6893ms pour1000ms et trajet radial refusé. Seuils de RAM disponible retirés, plafonds de consommation conservés. Autres preuves environnementales, revue humaine, adoption primaire, CI exacte et clôture propriétaire restent à établir. Les états datés précédents ci-dessous sont historiques ; Gate3 reste ouverte/non qualifiée.

**OUVERTE — NON QUALIFIÉE.** État actualisé le 4 octobre : qualification locale du candidat V13 `34791bb5…`, avec Source9 `d69e17db…` pour les contrôles machine. Gate 2 a été clôturée
par le propriétaire ; l'objectif actif est la clôture de Gate 3 sur Tidewater.
La [chronologie conservée](E:/OpenRPG_Forge/.gate3-evidence/20261002/progress-doc-historical-20261002-1422.md)
contient les essais antérieurs, y compris leurs échecs. Le présent document
décrit les résultats actuels et les travaux encore nécessaires.

## État courant V13 / Source9 — 4 octobre

| Périmètre | Preuve et portée actuelle |
|---|---|
| Machine Source9 | Workspace complet 1 514 PASS, zéro échec, dix ignorés ; cinq tests ignorés rejoués PASS, cinq non exécutés avec portée consignée. Strict natif fmt/Clippy PASS. WASM/supply Source8 réutilisés sur entrées production/dépendances/politique identiques ; aucun nouveau build/audit Source9 revendiqué. |
| R3-P01 et R3-C | [Deux approches](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v13-p01-two-approaches-root-review-001/proof.json) PASS locaux : combat et impact du décor avant combat, quête terminée et conséquences reprises ; lieux Village/Forêt/Intérieur liés et intérieur visité publiquement. |
| R3-P02 | [Retouche/reprise](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v13-p02-root-review-001/proof.json) PASS local : réplique, porte, liens et état conservés. |
| Quête par impact | [EnvironmentQuest](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v13-public-environmentquest-001/root-causal-proof.json) PASS public/causal local, cinq saves cohérents et nouvelle instance Resume. |
| Quête par feu | [EnvironmentFireQuest](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v13-public-environmentfirequest-001/root-causal-proof.json) PASS public/causal local, huit saves cohérents, retrait à654 ms sous1 000 ms. Début de fuite observé, sans revendication de sortie complète du danger. |
| Contrôles publics restants | ForestDanger PASS borné ; ShorePush et Navigation004 PASS Root. P03 et P04 PASS Root. La revue P05 et l’intégration qualifiée restent ouvertes. ForestThrow et ShoreWater sont couverts respectivement par EnvironmentQuest et ShorePush complets. Aucun résultat V8 hérité. |
| Intégration Primary | [Préparation bornée](E:/OpenRPG_Forge/.gate3-evidence/20261002/source9-primary-integration-preparation-001/proof.json) disponible, non appliquée. Les changements existants sont conservés. ADR0021 proposée, acceptation propriétaire absente ; requalification après intégration nécessaire. |
| P05 / Gate 3 | Éclairage temporaire de l’intérieur corrigé et comparé Off/On en V16 ; partie complète et lisibilité/rythme non acceptés par le propriétaire. Gate3 ouverte, CI distante NOT RUN, aucune publication implicite. |

Les sections ci-dessous conservent la chronologie des candidats précédents et des tentatives, y compris les échecs. Elles ne constituent pas le verdict courant V13.

## Résultats historiques du candidat V8 — 3 octobre

Le candidat `875f3731…` et le WASM `cf9ffab8…` sont construits sur le snapshot
propre `8530358…`. La [revue du candidat](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-candidate-root-review-001/review-proof.json)
vérifie les sources appariées et 1 340 fichiers. Campagne machine terminée :
1 507 tests PASS, zéro échec, dix ignorés ; les 50 cas critiques et les quatre
tests sur entrées acquises passent. Contrôles stricts natif/WASM et supply PASS.
La [revalidation Studio](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v8-001/gate3-revalidated-gameplay-authoring-ledger.json)
acquiert l'archive `19b4af30…` et la tête Gameplay `855eb32b…` sur ce candidat.
Les résultats runtime V7 ne sont pas hérités.

| Preuve actuelle | Résultat et limite |
|---|---|
| [P01 V8 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-p01-root-review-003/proof.json) | PASS public local : intérieur ouvert/traversé/quitté/refermé, quête reçue, cinq attaques distinctes de 20 dégâts avec contact, Sceau récupéré, quête terminée, Save et nouveau processus Resume. Harnais exécuté `61ada712…`. Hash d'état non recomputé par le lecteur ; reprise réellement acceptée par le moteur. |
| [EnvironmentQuest V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-environmentquest-001/causal-evidence.json) | PASS public local : gardien vaincu par impact du décor avant tout combat, quête terminée, Save/Resume et conséquences physiques durables. Avec P01, deux approches acquises sur le même contenu. |
| [ForestThrow V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-forestthrow-001/causal-evidence.json) | PASS : lancer, impact, support détruit, chute, champs durables après Save/Resume. |
| [ShorePush V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-shorepush-001/causal-evidence.json) | PASS : poussée, ignition et extinction par l'eau. Ne qualifie pas seul les dégâts de chaleur. |
| [Navigation V8 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-navigation-002/gate3-navigation-public-playtest-ledger.json) | PASS borné : patrouille extérieure, joint de tuiles, porte fermée sans chemin puis sortie et destination atteinte après ouverture, fermeture et Save avec reçus. |
| [P02 V8 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-p02-root-review-002/proof.json) | PASS : réplique et porte retouchées par Preview/Apply/Save Studio, refus ordinaire puis reprise compatible en nouveau processus ; quête Completed, liens, état consommé et intérieur conservés. Retouches uniquement dans la copie isolée. |
| [P03 V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-invalid-quest-p03-001/gate3-invalid-quest-smoke-ledger.json) | PASS : suppression du Sceau refusée, diagnostic lié à l'objet/quête/quantité, sélection pour retouche et Apply désactivé. Aucune correction effectivement commitée n'est revendiquée. |
| [P04 V8 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-replay-isolation-p04-001/gate3-replay-isolation-smoke-ledger.json) | PASS : deux départs identiques depuis l'état choisi, déplacement et Save du premier run, simulation isolée et même révision publique. |
| [ForestDanger V8 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-final-physics-forestdanger-003/causal-evidence.json) | ÉCHEC FERMÉ : Ignite-to-first-key 1 126,0985 ms dépasse le budget inchangé de 1 000 ms. Dégâts de chaleur observés sur joueur et gardien ; flux complet, reprise et réaction IA au danger non qualifiés. |
| P05 | Partie complète à observer par le propriétaire pour lisibilité et rythme ; aucune acceptation humaine acquise. |

Les échecs P01 001/002, P02 001, ForestDanger 001/002/003 et les refus de
préflight restent archivés. Les preuves PASS ne les réécrivent pas. Le
[recentrage à la porte](E:/OpenRPG_Forge/.gate3-evidence/20261002/doorway-centering-root-apply-001/proof.json)
`ea0e060f…` est appliqué et SelfTest PASS, mais n'était pas exécuté par P01 003 ;
sa qualification runtime spécifique reste absente. Le corridor P02 corrigé
est en revanche exécuté et validé par P02 002.

Les sources et sauvegardes d'entrée restent intactes ; les processus des preuves
terminées ferment sans force. Le rapport a été restauré après l'erreur d'encodage
022 depuis sa sauvegarde vérifiée ; les nouvelles écritures sont en UTF-8.

La [matrice des 22 critères](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-criteria-review-002/matrix.md)
est relue par le parent, avec seize fichiers de preuve vérifiés : **20 PASS
locaux bornés sur 22**. R3-C est acquis par le parcours EnvironmentQuest complet,
et non par une somme de fragments. Danger_ai et P05 restent en attente.

Le [polling de capture 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/fire-capture-poll-root-apply-005/proof.json)
est appliqué : lecture à 25 ms uniquement pour le dernier témoin de capture du
retrait après ignition ; défaut générique 200 ms et appel historique conservés.
Prédicat exact, geste souris, cooldown, retries et budget de 1 000 ms inchangés.
Deux SelfTests natifs et cinq cas sur la fonction réellement extraite du draft
passent ; capture tardive refusée. [ForestDanger V8 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v8-fire-root-review-004/proof.json)
termine le flux public et la reprise proprement : retrait 635,5515 ms sous
budget 1 000 ms, dégâts de chaleur sur joueur et gardien, cinq Save vérifiés
et cohérents. Le feu est acquis localement. La campagne physique couvre 7/8.

Danger_ai reste non qualifié : le Veilleur perçoit le danger (fleeing=true,
chaleur et dégâts présents), mais ne bouge pas. Le diagnostic public renvoie
no_path vers (22,181883 ; 56,530506 ; −265,817871), tuiles prêtes, aucun job en
attente, vitesse désirée nulle. Il reste exposé puis meurt. Il faut distinguer
la perception du danger de son évitement physique ; l'analyse du trajet est
en cours, sans attribuer encore le défaut au moteur ou au placement authoré.
Le dossier P05 est préparatoire : les captures ne valent pas observation humaine.

**Gate 3 reste ouverte.** Réaction IA au danger et revue humaine P05 restent
à qualifier. CI distante NOT RUN ; aucune publication ni qualification globale
de l'arbre Primary modifié. Les résultats sont locaux au candidat identifié.

## Historique de la reprise des contrôles du nouveau pin

Les contrôles stricts [natif 026](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-026/proof.json)
et [WASM 011](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-wasm-qualification-011/proof.json)
passent sur `8530358…`, avec sources stables et environnement restauré.
La [première campagne](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-candidate-validation-001/proof.json)
s'arrête pendant la compilation workspace 007 sur la limite de stockage ;
aucun test de cette tentative n'est déclaré réussi. Son arrêt et ses logs
restent conservés. Les deux contrôles stricts terminés gardent leur preuve.

Le [nettoyage borné et vérifié](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-storage-audit-001/cleanup-recovery-proof.json)
retire 304 anciennes bibliothèques Forge régénérables du seul cache Root.
Les neuf exécutables archivés contrôlés conservent leur hash. La suppression
a réussi mais l'écriture du premier rapport a échoué ; une vérification en
lecture seule scelle la récupération, sans répéter la suppression. L'espace
libre observé remonte à 7 952 912 384 octets. Cette variation du volume ne
permet pas d'attribuer toutes les consommations au build Root ; aucun réglage
Windows ni processus tiers n'est modifié.

La [campagne neuve 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-candidate-validation-002/proof.json)
reprend avec workspace 008, puis entrées acquises, dépendances et candidat
natif/WASM apparié. Elle réutilise seulement les deux contrôles stricts
vérifiés sur le même pin exact. Workspace 008 et entrées acquises 005 passent.
La [revue Root 007](E:/OpenRPG_Forge/.gate3-evidence/20261002/current-machine-qualification-root-review-007/review-proof.json)
vérifie 1 507 PASS, zéro échec, dix ignorés, 99 groupes, les 50 cas critiques
chacun une fois et la nouvelle régression float une fois. Les quatre tests sur
entrées acquises passent chacun une fois ; les neuf exécutables de bibliothèque
archivés sont contrôlés et les 967 sources restent identiques.
Supply 009 passe également avec audit/deny exit 0, politique source inchangée,
TLS vérifié et environnement restauré. Les deux bases RustSec propres utilisent
`83278802…`, avec 1 289 avis. À cet instant historique, le candidat était en construction. Les résultats
V8 actuels figurent au début du document ; Gate 3 reste ouverte.

## Nouveau snapshot de validation — précision du Save

Le snapshot isolé `8530358bee9a0d4bca7fe89e1bdb81c589ad6f74`, dans
`source-snapshot-002`, est propre et épingle 967 sources. Deux entrées diffèrent
du pin V7 `002b2e82…` : activation centrale de `serde_json/float_roundtrip` et
régression Save→Resume après impact. La ligne Cargo existait déjà dans Primary
et reste préservée ; seul le test y est ajouté après contrôle du préfixe ancien.
Le commit est privé, sans commit Primary ni publication. La [preuve de promotion](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-roundtrip-regression-001/promotion-recovery-proof.json)
conserve aussi le refus initial de l'assertion de propreté du clone : une
renormalisation de son index, sans modification des fichiers ni second commit,
aboutit à un status vide et aucun diff staged. Les 965 autres pins restent exacts.

Le [ForestThrow V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-forestthrow-001/gate3-physics-environment-quest-ledger.json)
acquitte réellement Lancer, puis refuse Save avec « save incompatible: state
integrity mismatch ». Le message générique du harnais sur la stabilisation ne
décrit pas cette cause. launch-before est acquis ; launch-after et Resume ne le
sont pas. La fermeture est propre sans force, sources préservées. Le harnais ENV
est désormais `25234b3d…` après le [delta de fenêtre de contact](E:/OpenRPG_Forge/.gate3-evidence/20261002/forestthrow-contact-window-root-apply-001/apply-proof.json),
qui garde tolérance et délais, et place le décalage X à 0,575 m.

Le [diagnostic du candidat exact](E:/OpenRPG_Forge/.gate3-evidence/20261002/forestthrow-save-integrity-analysis-001/diagnostic-proof.json)
lie son EXE `acb91235…` au serde_json natif sans float_roundtrip. Le [probe offline](E:/OpenRPG_Forge/.gate3-evidence/20261002/forestthrow-save-integrity-probe-001/execution-proof.json)
utilise les API réelles Save/Resume avec contacts synthétiques : 251 divergences
sur 2 048 sans l'option, zéro avec ; le contrôle sans contact passe dans les deux.
Premier témoin : énergie 0,09339775299865849→0,09339775299865848, bits
`3fb7e8ea46a62d00`→`3fb7e8ea46a62cff`, qui reproduit le refus d'intégrité.
Le champ exact du Save réel après lancer n'a pas été acquis : ce témoin établit
le défaut générique du codec, pas une mesure de l'énergie de ForestThrow.
La [régression négative puis positive](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-float-roundtrip-regression-001/execution-proof.json)
échoue avant correction sur ce même refus et passe après, avec énergie, hash
et état identiques. Suite Core/Gameplay : 49 PASS, zéro échec, sept groupes ;
Clippy strict ciblé PASS. Les contrôles machine globaux passent ; les résultats du build et des replays
V8 acquis ensuite figurent au début du document. Tous les résultats V7
ci-dessous gardent leur ancien candidat et ne sont pas hérités par `8530358…`.

Les probes natifs [003](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-003/execution-proof.json),
[004](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-004/execution-proof.json)
et [005](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-005/execution-proof.json)
passent chacun leur test réel et conservent leur EXE. 003 emploie un sol plat
valide ; 004 et 005 le raster canonique exact et les 256 tuiles de production.
005 ne change que le seuil d'entrée public et journalise l'arrêt obtenu :
(45,854263 ; 6,649617 ; −147,21243), à 0,013481972 m de la pose P01, puis
sortie réelle à Z −149,29642. Aucun collider importé ni PNJ n'est inclus, aucune
pose postspawn n'est imposée. Le blocage public reste non reproduit ; aucun
skinfix ni qualification de P01 n'est déduit. Ces probes sont natifs, alors
que le viewport public utilise le moteur WASM. Ils omettent aussi la transition
Gameplay SetDoor→ColliderDisabled ; leur PASS ne valide pas ce chemin public.

Le [lecteur causal renforcé](E:/OpenRPG_Forge/.gate3-evidence/20261002/physics-causal-persistence-root-apply-001/apply-proof.json)
exige, pour les completions applicables, released/destroyed/initiator présents,
typés et conservés entre after et Resume, avec leur cause attendue. Les huit
lignes officielles et leurs seuils restent identiques, les observations des
flows partiels restent visibles. Dix-sept contrôles synthétiques passent ;
aucune qualification runtime n'en est déduite.

## Qualification du candidat V7 précédent

- Strict natif 025 : **LOCAL_CHECK_PASS** sur `002b2e82…`.
- Strict WASM 010 : **LOCAL_CHECK_PASS** sur `002b2e82…`.
- Workspace 006 : **LOCAL_TEST_PASS** sur `002b2e82…`.
- Entrées acquises 004 : **LOCAL_TEST_PASS** sur `002b2e82…`.
- Dépendances 008 : **LOCAL_CHECK_PASS** sur `002b2e82…`, audit et deny exit 0.

Sur le pin historique `002b2e82…`, le [contrôle Root indépendant](E:/OpenRPG_Forge/.gate3-evidence/20261002/current-machine-qualification-root-review-006/review-proof.json)
confirme 1 506 tests réussis, zéro échec, dix ignorés et 99 groupes, ainsi que
50 cas critiques chacun exécuté une fois et réussi. Les quatre tests acquis
Tidewater passent également, avec inputs inchangés et environnement restauré.
La campagne 005 conserve son arrêt sur les verrous des bases en lecture
seule. La reprise [Supply 008](E:/OpenRPG_Forge/.gate3-evidence/20261002/supply-qualification-008/proof.json)
passe sur le même pin : 1 288 avis RustSec, zéro vulnérabilité détectée,
aucune erreur advisories/bans/licenses/sources. La politique existante reste
identique, avec 48 avertissements de doublons et 18 notes advisories.
Les deux bases officielles propres attestent la révision `f8dee89e…`.
Supply 007 conserve son échec Schannel ; la réparation utilise OpenSSL avec
vérification des certificats active, limitée au processus du contrôle et
restaurée ensuite. Aucun ignore ni contrôle désactivé n'est ajouté.
La [revue Root](E:/OpenRPG_Forge/.gate3-evidence/20261002/supply008-root-review-001/review-proof.json)
confirme les résultats et les 967 sources identiques. La construction V7
et la revalidation publique authoring 004 passent dans leurs périmètres
respectifs ; le COMMON est figé, NAV passe dans le périmètre observé et
P03/P04 passent localement. Les replays finaux restent incomplets. Gate 3 reste ouverte.

La campagne 004 sur `e4d8edf3…` a passé les contrôles stricts natif 024 et
WASM 009, puis [workspace 005 a échoué](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-005/proof.json) :
1 505 tests réussis, un échoué, dix ignorés, 99 groupes. L'échec porte sur
`danger_motion_uses_real_los_kcc_obstacle_and_saves_actor_pose`. Les exécutables
effectivement mesurés, les 967 sources et les logs sont archivés ; aucune
qualification globale n'est déduite des 49 tests critiques réussis.

Le [diagnostic natif réel](E:/OpenRPG_Forge/.gate3-evidence/20261002/danger-motion-native-diagnostic-001/result.json)
a reproduit l'assertion de contact imposé avec le mur. Les 180 observations
montrent un déplacement latéral d'environ deux mètres, avec appui réel,
sans contact avec le mur, puis l'arrêt de la fuite hors de portée du feu
(3,0165 m pour une portée de 3 m). Le PNJ reste devant le mur ; aucun détour
complet autour du mur n'est revendiqué. L'instrumentation peut modifier
l'ordonnancement. Ses sources originales ont été restaurées, sans erreur.

La [correction revue du test](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-009/apply-proof.json)
vérifie chaque pose par rapport au volume réel du mur, le déplacement latéral
physique, l'arrêt après sortie de portée et la position XYZ sauvegardée puis
reprise dans un nouveau runtime. Elle ne modifie pas la production et ne
supprime aucune garde produit. La campagne 005 requiert le workspace complet
PASS et [50 tests critiques réellement exécutés](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-candidate-critical-coverage-004/apply-proof.json),
chacun une fois et réussi, avant build Studio et replays sur la base finale
commune. Aucun nouveau critère officiel de Gate 3 n'est ajouté.


La [reprise effective](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-product-candidate-resume-001/proof.json)
est **CHECKS_PASS_BUILD_ONLY**. Frontend release et worker de navigation release
concordent avec le candidat natif debug `acb91235…`, sur `002b2e82…` propre.
Le premier build natif conserve son échec de restauration d'environnement ;
une seconde sortie utilise le wrapper revu, restaure exactement onze variables
et conserve les mêmes 967 sources. Aucun nouveau résultat runtime n'en est déduit.

La [revalidation publique V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-001/gate3-revalidated-gameplay-authoring-ledger.json)
conserve son échec avant tout input authoring ou Save sous `CodexSandboxOnline`.
Les [annotations des dumps conservés](E:/OpenRPG_Forge/.gate3-evidence/20261002/startup-breakpoint-annotation-review-001/findings.json)
identifient le breakpoint `80000003` : GPU inutilisable, dernier code
`STATUS_ACCESS_DENIED`. Le dump simulé `0517a7ed` précédent porte un échec
de création de token restreint pour un service, erreur 87. Le lien causal
service→GPU et l'objet Windows refusé restent indéterminés. Le [même essai V6](E:/OpenRPG_Forge/.gate3-evidence/20261002/startup-cdp-diagnostic-v6-001/proof.json)
échoue dans ce contexte ; cela ne démontre pas une régression de code V7.

En contexte Windows normal `FIXEFRED\karen`, le [run V7 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-002/gate3-revalidated-gameplay-authoring-ledger.json)
observe CDP, Shell et UI, puis échoue après quatre acteurs relus, avant Save,
sur le calcul des paires de dimensions du harnais. Le [run 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-003/gate3-revalidated-gameplay-authoring-ledger.json)
relit les 20 éléments publics et acquitte Save sur archive `39c8f9d8…` et
head `80e87c31…` inchangés, puis refuse à tort la fermeture en édition.
Ces deux échecs restent conservés. Les corrections de harnais revues
(parenthèses des dimensions `5d09464b…`, distinction édition/playtest
`ab1b0672…`) conservent les autres gardes et passent leurs [tests offline de dimensions](E:/OpenRPG_Forge/.gate3-evidence/20261002/checkpoint-physical-dimensions-preparation-001/test-result.json)
et [de fermeture](E:/OpenRPG_Forge/.gate3-evidence/20261002/checkpoint-authoring-zone-close-preparation-001/test-result-v2.json).

Le [run V7 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-revalidation-v7-004/gate3-revalidated-gameplay-authoring-ledger.json)
est **PUBLIC_AUTHORING_PASS** : 20 éléments relus (4 acteurs, 12 objets,
4 lieux), Save public acquitté, archive/head complets inchangés, original
`50bfd655…` préservé et fermeture propre sans force. Ledger SHA-256
`b6509a01a291fbe7fafae2396735c040a8e2c7d311e2b59605388cec2b0c6969`.
Le candidat `acb91235…`, le pin `002b2e82…` et ses 967 sources restent
inchangés ; aucun résultat runtime antérieur n'est hérité.

Les [conditions machine 004 réellement acquises](E:/OpenRPG_Forge/.gate3-evidence/20261002/final-machine-conditions-004/machine-conditions.json)
passent les cinq requêtes CIM en contexte normal : Windows 11 Professionnel
build 26300, Ryzen 5 7600 (6 cœurs/12 logiques), 64 GiB de capacité des
modules déclarés, Radeon RX 7800 XT et Radeon Graphics déclarés, WebView2
154.0.4258.37 installé. L'adaptateur actif, la résolution/réglages du playtest
et tous les timings/FPS restent `null`. Les refus machine 001/002 et l'échec
d'affectation PowerShell 003 sont conservés séparément.

L'[extension authoring NAV 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-navigation-authoring-v7-001/gate3-final-gameplay-authoring-ledger.json)
passe, avec [revue indépendante authoring](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-public-authoring-independent-review-001/review-proof.json)
`c72f8550…`. Le [gel Root COMMON](E:/OpenRPG_Forge/.gate3-evidence/20261002/final-common-freeze-v7-001/proof.json)
atteste **PUBLIC_AUTHORING_COMMON_PINNED** : ledger `ca2ad216…`, archive
`19b4af30…`, head `855eb32b…`, 65 fichiers et inventaire `1e8a77aa…`.
Les 20 éléments parent sont préservés, avec deux PNJ dédiés supplémentaires
(6 acteurs, 12 objets, 4 lieux), révision 34→40 et reçus 33→39 après six
commandes publiques. L'assertion initiale de gel qui incluait la révision
a refusé avant toute écriture ; la comparaison corrigée conserve identité,
base, schéma/version, contenu hors gameplay et préfixe des reçus parent.
Aucune source ni fixture n'est modifiée par ce gel. La fermeture authoring
est propre ; la restauration exacte null/chaîne vide de son environnement
enfant n'est pas attestée. Aucun résultat runtime n'est hérité.

Le [parcours NAV V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-navigation-001/gate3-navigation-public-playtest-ledger.json)
porte **observed_complete**, ledger SHA-256
`1d46f968f1bfc88d4f521aa1478a992e2c269c9cb36ee5828ad4efc62c22ffc8`.
Save `21872d42…` est acquis, source et clone authoring sont préservés, et les
processus propres sont fermés sans force. La [revue indépendante runtime](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-public-runtime-independent-review-001/review-proof.json)
`fc767d74…` passe 70 contrôles sur 36 échantillons réels : patrouille extérieure,
raccord de tuiles, NoPath porte fermée, sortie/Reached après ouverture,
révisions de porte et Save public. Ce **PASS_RECORDED_BOUNDED_RUNTIME** utilise
une politique explicitement vide ; il ne qualifie ni toutes les routes,
ni une politique eau/coûts, ni un nouveau processus Resume, ni Gate 3.

Le [rejeu P03 V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p03-001/gate3-invalid-quest-smoke-ledger.json)
est **PASS local Studio**, ledger SHA-256
`1935ca86d9025d2938bf9720f69436856ff4bafa1e821d651bb1f59190d5bda1`.
La suppression du Sceau est refusée avec diagnostic quête, objet et quantité ;
Apply reste désactivé. Archives, révisions et séquences sont inchangées,
sans nouveau reçu, avec source préservée et fermeture propre sans force.
Le [rejeu P04 V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p04-001/gate3-replay-isolation-smoke-ledger.json)
est **PASS local reset/isolation**, ledger SHA-256
`af7948f63acd29ea00b3498f5c519bc6c2884bdf23c99acb09367304473a7be2`.
Source, clone et Save source sont préservés ; isolation, même départ choisi
et même révision publique sont vérifiés. Les deux processus sont terminés
et fermés sans force. Ce reset ne qualifie pas une reprise de partie.
Le [rejeu P01 V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p01-001/gate3-gameplay-playtest-smoke-ledger.json) conserve son échec avant l'entrée : le messager ne satisfait pas l'arrêt hors du passage dans le délai prévu, ledger `b5170694…`.
Le [rejeu P01 V7 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-p01-002/gate3-gameplay-playtest-smoke-ledger.json) reste **FAILED_CLOSED**. Après le recul public du joueur revu dans le harnais `95b91a97…`, le messager atteint une pose extérieure au sol et stationnaire, avec retours du même pas aux ticks 1681, 1698 et 1715. Le joueur entre ensuite dans la Room canonique au tick 1843. Sa sortie KCC reste bloquée douze secondes : position HUD (45,86 ; 7,52 ; −147,20), commande Z −0,05 m, déplacement X/Z résolu nul et un contact, alors que simulation, appui et entrée sont actifs. Les sources sont préservées, fermeture propre sans force ; aucun parcours intérieur complet, combat, Save final ou Resume n'est qualifié par ce préfixe.

Le [rejeu ShorePush V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-shorepush-001/gate3-physics-environment-quest-ledger.json) conserve son échec d'approche avant Pousser, avec HP 67 et 33 dégâts de contact réellement enregistrés. Le [rejeu ShorePush V7 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-shorepush-002/gate3-physics-environment-quest-ledger.json), ledger `d90439b4…`, échoue plus tôt : le hash de la transaction Save push-before est verrouillé après le clic public. Pré-hash réussi, destination Copy absente, Saves before et failure-state acquis ; aucun pulse de contact ni Push exécuté. Les sources et la fermeture propre sans force sont conservées.

Le [rejeu ShorePush V7 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/gate3-v7-final-physics-shorepush-003/gate3-physics-environment-quest-ledger.json), ledger `1ab5534a…`, reste **FAILED_CLOSED** avant le contact de rive et Pousser. Le segment public `shore-approach-waypoint-4-step-4` reste sans progrès horizontal pendant douze secondes ; au tick 2164, simulation, appui et entrée sont actifs, Z demandé +0,05 m, X/Z résolu nul et un contact. L'anchor public des pieds est (29,658571 ; 3,4564757 ; −110,921844) ; le Save d'échec porte le centre du joueur à Y 4,3314757 au tick 2190. Les Saves before `978a7fa3…` (tick 843) et failure-state `b23ff1b1…` sont acquis, acquittés et cohérents, avec vitalité 100 dans les deux. Aucun Push, pulse de contact ou Resume n'est qualifié. Sources préservées et fermeture détenue sans force. Aucune cause commune avec P01 n'est présumée ; la fermeture WebSocket du cleanup après le Save d'échec n'est pas attribuée au blocage KCC. Le compteur sharing32 n'est pas exposé dans ces captures : charger le helper corrigé ne démontre pas que cette branche a été rencontrée.

Les correctifs externes revus concernent le harnais, sans changer le candidat `acb91235…` ni le COMMON. La [fenêtre de contact et l'anchor public précis](E:/OpenRPG_Forge/.gate3-evidence/20261002/shorepush-contact-window-root-apply-001/apply-proof.json) gardent douze secondes et les gestes existants ; séparation 0,575 m et tolérance 0,025 m décrivent seulement une fenêtre nominale [0,55 ; 0,60]. Un anchor présent malformé est refusé. L'acquittement et les reçus du vrai Push restent nécessaires, sans promesse d'approche sans dégâts ni PASS hérité du V6.
La [proposition de lecture sous sharing32](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-commit-sharing-read-preparation-001/proposal-proof.json) ne relit que le commit déjà demandé sous IOException Windows 32, dans le même délai. Si la dernière lecture est toujours verrouillée à l'échéance, son ErrorRecord est relancé pour empêcher un second Save dans l'appelant. Les dix tests offline passent, dont un unique clic Save simulé dans les deux fonctions réelles sous verrou persistant ; aucune réussite Save/Push runtime n'en est déduite. Copy, les gardes publiques et FIRE restent inchangés : premier recul avant attente et capture brute. La [preuve d'application Root](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-commit-sharing-read-root-apply-001/apply-proof.json) épingle PHYS `1785fb0a…` et ENV `ff5f65e4…`, sans changement produit, candidat ou COMMON. Root a répété les tests dans la préparation, remplaçant le premier rapport temporel `770e892c…` ; ses octets ne sont pas récupérables localement. La proposition originale reste intacte. La [nouvelle preuve Root conservée](E:/OpenRPG_Forge/.gate3-evidence/20261002/save-commit-sharing-read-root-apply-001/root-rerun-proof.json), SHA `d3ad1af5…`, référence le vrai rapport `8cdb19b4…` : dix tests PASS, trois lectures sous verrou et un clic public Save simulé. L'ancien pin n'est pas présenté comme un rapport encore disponible ; aucune qualification runtime n'est déduite de ce rerun offline.

Le [probe natif isolé 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-001/execution-proof.json) échoue en compilation, exit 101, sans exécutable ni résultat physique. La copie des 967 entrées omettait dix JSON embarqués à la compilation par include_str, soit 39 913 octets. Le probe 002 reprend la même copie et le même overlay de test, avec ces [entrées supplémentaires épinglées](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-002/additional-compile-inputs.json) depuis le baseline `002b2e82…` propre. Le [probe 002 terminé](E:/OpenRPG_Forge/.gate3-evidence/20261002/p01-room-exit-native-probe-002/execution-proof.json) finit à 05:41:10 UTC, exit 101. Son exécutable réellement compilé est archivé, SHA `0841d92582554d19d93b052c303162d18b6892215cc01820902143d897785efc`. Le test refuse son SampledTerrainPlan avec « raster domain must match the centred area and authored spacing », avant construction de l'App, démarrage ou simulation. Il s'agit d'un défaut du montage de test ; aucun résultat physique, correctif produit ou PASS n'est annoncé. L'observation précédente de compilation en cours reste conservée comme historique. Les montages corrigés suivants sont acquis séparément dans le bilan du nouveau snapshot ci-dessus.

P02, les trois modes physiques et leurs lecteurs causaux/campaign restent à acquérir sur le COMMON pour prouver les huit capacités physiques, les deux approches de quête et Save/nouveau processus Resume. P05 attend une partie complète concrète observée par le propriétaire. **Gate 3 reste ouverte, non qualifiée.**

## Sources et candidats

- Pin courant propre : `002b2e82a2f34a61f9ec1ed0d4757e2afa66169c`, dans
  [source-snapshot-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/source-snapshot-001).
  [Pins et résultats](E:/OpenRPG_Forge/.gate3-evidence/20261002/source-snapshot-001-current.json).
  Navigation ADR0009, impact, appui et synchronisation des contacts sont intégrés.
  La [campagne V7 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-product-candidate-validation-005)
  a passé les tests puis s’est arrêtée aux verrous des bases de dépendances ; les résultats sont ci-dessus.
  NAV V7 001 passe dans son périmètre observé, P03/P04 passent localement ;
  les autres replays restent ouverts, sans qualification globale de ce pin.
  Le dernier pin qualifié antérieur reste `1d002107e54a5b4fb6b5f41df6fd49951aa72058`.
- Candidat joué V4 : source `776e392f82ef0faf1b5403463026d3b355a620d9`,
  SHA-256 `bd86bb3c175b0f077c46be2613ac8ecff506fa2b62d43e55639f19de21ed9926`.
  Il précède la source figée actuelle. Le candidat V5 est désormais construit
  sur `07a5fb19…`, avec frontend/natif concordants et source stable ; SHA-256
  `7ced17b5f177f6361063377bf7ac2ed6a5fe301e078a9b46da12478b338560cc`.
  La source actuelle corrige le setup de test puis la résolution tardive des
  objets importés du playtest ; le candidat V5 ne contient pas ce correctif.
  Le démarrage runtime V5 échoue dans les runs 001 et 003 : la simulation
  reste au Tick 0, avec projection d'objets/collisions non prête. Ce blocage
  est conservé comme échec ; les tests réussis ne qualifient pas ce candidat.
- Candidat V6 : frontend et natif concordants sur `1d002107…`, source propre
  et stable. SHA-256 natif `3c0ee5d3270edf7e53730e829a1d2eb427b42f9f86bacd4fe5ef0364edadbdc9`,
  WASM `8cad9a60eff38f128e08d3ac369a78ef26424534924edae7552dbb9a800de8c9`.
  La [campagne de construction](E:/OpenRPG_Forge/.gate3-evidence/20261002/readiness-fix-validation-001/proof.json)
  reste BUILD_ONLY ; une capture publique distincte confirme le démarrage
  sur la fixture V5 inchangée. Le natif est un build debug ; aucun résultat
  de performance release ou verdict Gate 3 n'en est déduit.
- Primaire : HEAD `53d9ef794f69ba4b5d76d370b256997735fea7c9` inchangé,
  travail concurrent conservé, sans commit ni publication de ce chantier.
  Après la réconciliation 004, les [57 fichiers intégrés en 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-005/apply-proof.json)
  et les [cinq corrections revues en 006](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-006/apply-proof.json)
  préservent les travaux concurrents ; leurs diffs ciblés passent.
  Les évolutions concurrentes du Vault et du bateau restent dans le primaire
  et hors du candidat privé. Aucun PASS global du primaire n'est revendiqué.
- Tidewater original reste épinglé à
  `50bfd655a7fb4f5a61e4593240ea86dc8d13136d82d558d34737a41b4c6c8720`.
  L'authoring et les parties utilisent des copies isolées, avec hashes avant/après.
  Les lieux, acteurs, objets et règles sont des données génériques du module.

Sur `13568d53…`, le strict natif 021 a refusé neuf diagnostics distincts
(commentaires, style de tests et longueur de `resolve`). Cet échec reste
[archivé](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-021/proof.json).
Les cinq fichiers corrigés ne changent pas la logique : le calcul de support
est extrait en fonction privée et les autres corrections sont de forme.
Le [checkpoint](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-physical-strict-lint-apply-001/checkpoint.json)
est propre et passe formatage/diff ; la qualification complète reste en cours.
Les 54 tests ciblés communs 003 passent sur les sources archivées avant ces
corrections ; ils ne qualifient pas automatiquement la nouvelle source.

Le strict natif 022 a ensuite révélé deux diagnostics dans le test de budget
navigation (longueur et comparaison flottante). Ils restent
[archivés](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-022/proof.json).
L'extraction des trois assertions de cancellation et la classification exacte
du zéro préservent le contrat, y compris les deux signes du zéro ; aucune
tolérance n'est ajoutée. Cette [correction du test](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-007/apply-proof.json)
est intégrée au primaire avec ses travaux concurrents conservés.
Sur le checkpoint propre `e2bc4679…`, [formatage et Clippy natif 023 passent](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-023/proof.json),
workspace, toutes cibles et features, avertissements refusés. Tests complets,
WASM, dépendances, builds et replays restent à réunir sur cette source.

## Contrôles et correctif vérifiés

Le correctif fonctionnel `53f595d4…` passe workspace 003 : 1 469 tests,
zéro échec, dix ignorés et quatre nouvelles régressions exécutées.
WASM 006 refuse seulement le nom local `resolved` trop proche de `resolver`.
Le [renommage sans changement de logique](E:/OpenRPG_Forge/.gate3-evidence/20261002/playtest-source-resolution-style-fix-001/apply-proof.json)
donne le pin final `1d002107…`, sur lequel les contrôles stricts natif 020 et
WASM 007 passent. La [campagne V6 terminée](E:/OpenRPG_Forge/.gate3-evidence/20261002/readiness-fix-validation-001/proof.json)
confirme workspace 004, les quatre tests acquis 003 et les builds concordants.
Supply 005 passe également sur le pin final ; les résultats antérieurs
restent attachés à leurs sources et ne sont pas réétiquetés.

| Contrôle | Résultat et limite | Preuve |
|---|---|---|
| Formatage et Clippy natif | PASS 020 sur `1d002107…`, workspace, toutes cibles et tous features, avertissements refusés ; ne lance pas les tests | [Strict natif 020](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-native-qualification-020/proof.json) |
| Clippy WASM | PASS 007 sur `1d002107…`, bibliothèques Web pertinentes ; ne lance pas le viewport | [Strict WASM 007](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-wasm-qualification-007/proof.json), [refus de style 006](E:/OpenRPG_Forge/.gate3-evidence/20261002/strict-wasm-qualification-006/proof.json) |
| Tests workspace | LOCAL_TEST_PASS 004 sur `1d002107…` : 1 469 réussis, zéro échec, dix ignorés, 95 groupes, quatre nouvelles régressions exécutées ; source stable et environnement restauré | [Proof 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-004/proof.json), [totaux et hashes](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-004/summary.json), [échec 001 conservé](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-tests-001/proof.json) |
| Dépendances | PASS 005 sur `1d002107…`, audit et deny exit 0, aucune nouvelle exception, inputs et source stables ; ne couvre pas le laboratoire navigation | [Supply 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/supply-qualification-005/proof.json) |
| Entrées acquises Tidewater | LOCAL_TEST_PASS 003 sur `1d002107…` : quatre tests explicites (océan, ressac, surface avec rejet d'un octet falsifié, vrais shaders Bevy), avec 74 payloads, exécutables et source inchangés, environnement restauré | [Proof acquis 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/acquired-input-tests-003/proof.json), [préflight](E:/OpenRPG_Forge/.gate3-evidence/20261002/acquired-input-preflight-001.json) |
| Frontend et candidat V5 | Build concordant sur source `07a5fb19…` stable, WASM `1f9812c8…` ; runtime FAILED_CLOSED au Tick 0, gardes de projection/collisions non satisfaites | [Source V5](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-authoring-005-candidate/source-proof.json), [runtime 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-playtest-v5-runtime-003/gate3-gameplay-playtest-smoke-ledger.json) |
| Frontend et candidat V6 | BUILD_ONLY sur `1d002107…`, source stable et imports complets épinglés ; capture publique séparée Ready/Tick 30 sur fixture V5 inchangée, aucun parcours complet encore déduit | [Source V6](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-authoring-006-candidate/source-proof.json), [capture readiness](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-v6-public-readiness-001/proof.json) |

Le [correctif d'impact KCC](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-velocity-fix-001/apply-proof.json)
est vérifié séparément : le [négatif causal 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-regression-negative-002/proof.json)
fait passer la vitalité de 76 à 0 en maintenant une commande bloquée contre un
objet stationnaire. Le calcul utilisait la vitesse demandée au lieu du mouvement
résolu. Le correctif prend `effective_translation / fixed_timestep` pour le
joueur et conserve la vitesse backend pré-solver des objets. Le [positif 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-regression-positive-001/proof.json)
passe les six tests physiques : commande bloquée, objet en chute réellement
dommageable, rupture/support, rotation, gardes Save et nouvelle reprise.
Inputs compilés et exécutables archivés stables, environnement restauré.
L'essai négatif 001 échouait sur une assertion de mouvement de la fixture,
avant les dégâts ; sa [limite causale](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-kcc-regression-negative-001/causal-assessment.json)
reste conservée. Aucun rejeu Tidewater corrigé ni PASS du futur pin complet
n'est encore déduit de ces tests. Les deux fichiers ont été réintégrés au
primaire seulement après égalité exacte avec leur baseline sauvegardée.
Le [prédicat de stabilisation extrait](E:/OpenRPG_Forge/.gate3-evidence/20261002/impact-adapter-lint-refactor-001/apply-proof.json)
conserve les mêmes bornes et évite de dépasser la limite Clippy du système
fixe ; sa validation reste à réunir avec le prochain pin complet.

Run workspace 001 a exécuté 604 tests moteur réussis, un échoué et trois
ignorés, puis s'est arrêté avant les autres packages. Le test de reçu gouverné
attendait un refus de batches absents sans activer leur option ; le contrat
et un autre test vérifient explicitement que l'option inactive ne bloque pas
les arbres individuels. La [correction de setup](E:/OpenRPG_Forge/.gate3-evidence/20261002/workspace-fixture-fix-001/apply-proof.json)
ajoute ce contrôle positif, puis active l'option avant de vérifier le refus
des batches demandés mais non projetés. Les gardes produit sont inchangées.

Les échecs stricts antérieurs restent conservés. Les corrections WASM 004
portent notamment sur les paramètres de projection, requêtes et gardes.
Huit callbacks longs ont une exception locale de style motivée ; aucun refus
fonctionnel n'a été supprimé. Les prérequis temporels compilent mais leur rendu
n'est pas qualifié par ces checks. Les deux régressions de freinage 002 ont
passé sur le pin antérieur `e9b6f97`, avec arrêt borné sur pentes ±40 degrés.
Les deux régressions passent également dans workspace 004 sur le pin final.

Quatre des dix tests ignorés du workspace sont exécutés explicitement dans
acquired-input-tests 003. Six restent optionnels et non exécutés : trois probes
CPU sans seuil de qualification, une reprise d'archive Studio locale et deux
lectures du vrai Vault. Run acquis 001 est interrompu avant les tests parce que
la sélection par crate recompilait une combinaison de features différente ;
le [motif et les seuls processus possédés arrêtés](E:/OpenRPG_Forge/.gate3-evidence/20261002/acquired-input-tests-001/interruption.json)
restent conservés. Run 003 appelle les binaires exacts du workspace 004, avec
hashes et logs de provenance ; il ne reconstruit rien. Aucun succès CI distant,
renderer ou primaire global n'en est déduit.

Le [négatif commun exécuté](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-contact-corner-negative-001/result.json)
a donné deux résultats distincts. L’interaction périmée est causale : un vrai
contact KCC autorise encore une poussée après morphing de la capsule, séquence
1 au lieu de 0. Le coin oblique passe sous le correctif d’appui existant : deux
faces confirmées, 120 poses consécutives, étendue 3D nulle, appui conservé et
vitesse verticale nulle. Les sources 671, les exécutables archivés, les logs et
l’environnement sont vérifiés ; le [dossier scellé](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-contact-corner-negative-001/final-inventory.json)
ne sera pas réécrit. Le cône proposé reste non appliqué, faute de défaut runtime
reproduit par ce cas borné.

La [correction du contact périmé appliquée dans la copie privée](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-contact-resync-apply-001/apply-proof.json)
exige la capsule installée courante et une sortie KCC fraîche ; elle diffère
les actions et Save pendant la transition tout en conservant les frames et
les contacts des autres acteurs/objets. La [visibilité de l’indicateur existant](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-sync-visibility-apply-001/apply-result.json)
est le seul delta supplémentaire du contrôleur. Le contrôle positif restaure
un vrai contact après une étape d’installation sans sweep et exige que la
frame monde continue pendant le report. Formatage/diff ciblés passent. La
[compilation commune positive](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-navigation-common-positive-001/command.json)
a compilé et exécuté 54 tests depuis quatre exécutables archivés : 52 PASS,
2 FAIL. Les sept contrôles physiques, les 22 régressions Tidewater, les trois
contrôles courbes/heightfield et les sept contrats fournisseur passent ; la
navigation passe 13/15. Les échecs conservés portent sur la vitesse KCC réelle
bornée à 2 m/s et le remplacement de la tuile après un changement de source
pendant un job. Les 843 sources et le gel NAV002 sont inchangés, l’environnement
est restauré. Le [résultat commun](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-navigation-common-positive-001/result.json)
reste FAIL ; aucune qualification du candidat ni aucun rejeu Tidewater corrigé
n’est déduit. Les brouillons 002/003 avaient
un encodage altéré ; ils restent archivés, non appliqués, et le littéral UTF8
final a été vérifié avant application.

Le [positif commun 003](E:/OpenRPG_Forge/.gate3-evidence/20261002/physical-navigation-common-positive-003/result.json)
passe les 54 tests nommés : 25 moteur, sept physiques, 15 navigation et sept
fournisseur. Les quatre exécutables et les 843 sources sont archivés et stables ;
l’environnement est restauré. La patrouille fournit 1 530 retours KCC mobiles
courants et une demande initiale sans retour, avec déplacement déclaré nul.
Les maxima mesurés sont 2,0000005 m/s demandés en XZ, 2,0066721 m/s demandés en
3D avec gravité et 2,0034387 m/s résolus en XZ. Le contrôle compare le retour à
sa demande physique courante ; il ne revendique pas une borne absolue XZ de
2 m/s après projection de gravité. Le test d’invalidation observe le retrait
avant synchronisation puis le nouveau reçu Ready de la tuile concernée.

Le [checkpoint privé propre](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-physical-clean-checkpoint-001/proof.json)
`13568d5367d2f932443c35c740c65ab4359102ee` réunit 59 fichiers ; un seul fichier
cache est normalisé par rustfmt après le positif 003. La [réconciliation 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/private-primary-reconciliation-005/apply-proof.json)
intègre 57 fichiers dans Primary et préserve les options de précision JSON,
exemples et exports des travaux concurrents. Aucun commit Primary ni push
n’est effectué ; la qualification globale de Primary reste distincte.

La [pipeline V7 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-product-candidate-validation-001/proof.json)
reste FAILED_CLOSED sur ce checkpoint : fmt passe, Clippy strict refuse neuf
diagnostics distincts (cinq commentaires NAV, longueur de resolve et trois
styles du test fournisseur). Les sources et l’environnement sont stables.
WASM, workspace final, supply et construction du candidat n’ont pas été
atteints. Les corrections préparées hors checkout exigent un nouveau pin et
un nouveau contrôle ; aucun PASS actuel ni replay corrigé n’est déduit.

Le [Clippy séparé 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-005/clippy-cargo-result.json)
a échoué sur le commentaire de `TileInput::areas` dans le nouveau fournisseur,
avant de valider le moteur. Ce commentaire est corrigé dans le gel NAV002 ;
la validation stricte sur le prochain pin reste à exécuter. La couverture finale
exigera [49 tests critiques nommés](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-candidate-critical-coverage-003/apply-proof.json),
exécutés une seule fois chacun dans le vrai log workspace, dont les deux
nouvelles régressions ; aucun build seul ne suffit.

Les [quatre scripts publics de navigation intégrés et auto-testés](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-public-replay-promotion-003/review-result.json)
préparent le raccord réel entre tuiles, la porte, les reçus et la fraîcheur
post-writeback. Les scripts de feu et de parcours déjà corrigés sont conservés.
Ces auto-tests synthétiques n’ont lancé ni Studio ni le playtest et n’héritent
d’aucune qualification runtime.

## Parcours réellement joués

L'[authoring V5-002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-authoring-v5-runtime-002/gate3-gameplay-authoring-smoke-ledger.json)
est PASS sur une copie isolée, mais le playtest V5 ne démarre pas. Les
[runs 001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-playtest-v5-runtime-001/gate3-gameplay-playtest-smoke-ledger.json)
et 003 conservent après 180 secondes « Simulation suspendue · objets/collisions
en reconstruction ou incohérents · Tick 0 ». L'eau, le ressac et les nuages
continuent de calculer ; cela ne prouve ni readiness des objets ni gameplay.
Les sources sont préservées et les instances fermées proprement. La
[capture publique 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-v5-public-readiness-002/public-readiness.json)
observe ce refus avec 24 placements et 116 primitives enregistrées, zéro
primitive en attente. Le diagnostic du projecteur GLB générique reste stale
(epoch 5/0, zéro objet), car ces imports sont projetés par leurs adapters.
Ce compteur seul ne qualifie pas l'égalité avec les objets du playtest.

La [correction locale](E:/OpenRPG_Forge/.gate3-evidence/20261002/playtest-source-resolution-fix-001/apply-proof.json)
remplace la liste figée avant chargement par une résolution sur le même head
canonique, renouvelée à l'arrivée des imports vérifiés et de sa population.
Un échec de résolution refuse la simulation et est réessayé sans renvoyer
l'ancienne liste. Les contrôles exacts de source, transform et Rapier sont
conservés. Quatre régressions couvrent l'arrivée tardive, le refus/retry,
l'owner étranger et le snapshot sans head.

La [capture causale V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-v6-public-readiness-001/proof.json)
reprend exactement la fixture V5-002, archive `f5a89455…` et head `584a38d5…`.
Le HUD public indique « Simulation active », Tick 30, au sol true et simulation
prête true, à `(40 ; 4,57 ; -115)`, sans geste ni mouvement. Le diagnostic
GLB générique reste stale 5/0 : cette observation confirme qu'il ne remplace
pas la garde effective du playtest. Les quatre archives source et clone
restent inchangées ; fermeture propre, sans arrêt forcé. Cette capture
confirme le démarrage, sans qualifier P01, le GPU, la fluidité ou P05.

L'authoring V6-001 puis l'extension physique V6-002 passent par les commandes
publiques Studio. Le [ledger final v2](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-complete-authoring-v6-002/gate3-final-gameplay-authoring-ledger-v2.json)
épingle l'archive `39c8f9d8…`, head `80e87c31…`, et conserve les attestations
de la salle à X 46 avec provenance vers le parent. Les douze commandes de
l'extension préservent les entrées du parent, sauf le flag de fuite du Gardien
explicitement authoré false ; le Veilleur reste configuré pour fuir le danger.
La base commune comprend les objets des deux approches, le piège, le feu et
le bloc de rive. Source préservée, fermeture propre et aucun résultat runtime
hérité. Les essais V6 sont en cours ; un lancer puis Save sont observés dans
EnvQuest 001, mais le projectile manque sa cible et la frappe reste hors
contact : ce run est FAILED_CLOSED, sans impact ni piège qualifié.

Le [combat V4-005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-room-retouch-v4-runtime-005/gate3-gameplay-playtest-smoke-ledger.json)
est PASS : dialogue, quête acceptée, cinq attaques, gardien vaincu, Sceau
récupéré puis consommé au retour, quête Completed, Save publique et reprise
dans un nouveau processus. Gardien et pickup restent absents à la revisite.
La source et le checkpoint authorés restent inchangés, les fermetures sont
propres. Le cap caméra initial et celui de la reprise sont observés à 0 degré ;
aucune persistance du cap n'en est déduite. Le pickup reste stable, avec support
Rapier confirmé et sans glissement après relâchement.

La [retouche compatible V4-010](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-room-retouch-v4-compatible-runtime-010/gate3-compatible-retouch-smoke-ledger.json)
est PASS : réplique modifiée et porte décalée de 0,05 m par Preview/Apply/Save,
reprise ordinaire refusée pour changement de source, puis reprise compatible
explicite. Deux processus conservent le texte, la quête Completed et le Sceau
consommé. L'archive retouchée vaut `abc59e9765ec0a709356b8bd1ae59fcf44f04389b1f91b37e0e33e2238dba285`.
Un franchissement KCC du seuil ouvert est observé au sol. Il prouve un segment
intérieur de salle générique ; il ne découpe pas le collider plein de House H.
La correction finale du harnais compare la réplique dans le paragraphe du HUD
préfixé par le nom du PNJ. Aucun correctif produit n'en est déduit.

La [campagne physique V4-012](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-playtest-retouched-v4-012/gate3-physics-playtest-smoke-ledger.json)
reste **FAILED_CLOSED**, mais deux résultats bornés sont observés : lancer
public avec objet déplacé et `released=true`, puis Ignite public avec
`burning=true`. Les deux actions, leurs conséquences et la pose du joueur
survivent à une Save réelle puis une reprise dans un nouveau processus.
Les hashes de partie avant/après/reprise sont distincts et deux receipts
sont conservés. Poussée, dégâts d'impact, piège/destruction, extinction et
réaction IA au danger ne sont pas établis. Le déplacement borné du PNJ ne
prouve pas une fuite ; `fleeing` reste false dans les sauvegardes observées.

Le [scénario eau/feu de rive V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-shore-water-v6-001/gate3-physics-environment-quest-ledger.json)
acquiert un PASS ciblé sur le candidat et la base finale communs : Ignite/Save,
quatre queries GPU fraîches `4413 < 4428 < 4484 < 4516`, puis Extinguish/Save et
Resume/Save dans un nouveau processus. Les sauvegardes restent concordantes,
la source est préservée et les deux fermetures sont propres, sans arrêt forcé.
Le [contrôle causal](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-shore-water-v6-001/causal-assessment.json)
valide feu et eau/extinction ; les autres capacités R3-B et la quête
environnementale restent explicitement false. Ces parcours devront être
rejoués sur le candidat issu du correctif d'impact en préparation.

Le [scénario eau/feu de rive V4-009](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-playtest-water-shore-009/gate3-physics-playtest-smoke-ledger.json)
est PASS ciblé, sur V4 et WebView2 `154.0.4258.37`. Quatre queries GPU de l'eau
progressent de 3167 à 3306 avec position concordante et surface au-dessus du
pied, autour de `(39,469 ; -32,381)`. Les actions publiques Ignite puis
Extinguish sont acceptées ; une Save conserve le foyer allumé entre les deux,
puis une autre Save et un nouveau processus conservent son état éteint.
L'authoring isolé règle seulement les dégâts du feu de rive à zéro pour ce
contrôle ; le feu forestier reste à un dégât par tick. Source originale,
fermetures propres et vitalité 100 sont vérifiées. Les échecs 007 (activation
native) et 008 (mort avant extinction) restent conservés. Le PASS ne couvre
aucune autre action R3-B, aucun parcours narratif ni le candidat final V5.

L'[essai feu/quête V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-physics-envfire-quest-v6-001/gate3-physics-environment-quest-ledger.json)
reste FAILED_CLOSED : le Gardien subit des dégâts de chaleur attestés par
exposition/LOS et événements `Damage(contact=false)`, puis est vaincu ; le
Veilleur passe en fuite et se déplace de 0,905 m dans la direction opposée.
Le joueur meurt également par chaleur avant la collecte. Les sauvegardes
causales et la fermeture propre sont conservées ; la quête environnementale
n'est pas qualifiée. Le préflight retrouve un glissement avant Ignite et
une attente de focus doublée dans le harnais. Les replays préparés conservent
100 de vitalité, le contrat de chaleur et les gardes produit.

Le [parcours V6-005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-gameplay-playtest-v6-runtime-005/gate3-gameplay-playtest-smoke-ledger.json)
observe l'entrée et la sortie de la salle, l'appui sur son sol puis sa porte
refermée. Il reste FAILED_CLOSED : la frappe est refusée faute de contact
physique confirmé avec le Gardien. Le nombre de collisions KCC inclut le sol
et les objets voisins ; il ne prouve pas ce contact. Les états de pente
montrent aussi un appui Rapier avec perte de l'état au sol dans l'adaptation
Tidewater et accumulation de gravité. Le [négatif causal d'appui](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-support-regression-001/negative-cargo-result.json)
reproduit le défaut sur de vrais colliders à 35 et 40 degrés : le KCC confirme
le sol tandis que la source reste en chute sous commande bloquée. L'exécutable
négatif est archivé. Le [premier positif](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-support-regression-001/positive-cargo-result.json)
passe 14 tests sur 15 ; l'appui et la vitesse verticale sont corrigés, mais la
garde de stabilité à 40 degrés échoue. Le mouvement vertical de capsule est
encore étudié, sans qualification du correctif. Saut, vraie pente trop raide,
nage/marche et synchronisation backend passent dans cette suite ciblée.
Le [négatif causal de stabilité](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-001/negative-cargo-result.json)
échoue sur la fenêtre complète de 120 pas consécutifs : oscillation verticale
22,76 mm à 35 degrés et 23,70 mm à 40 degrés. Le premier lancement positif a
réutilisé l'exécutable beta à cause du mtime conservé lors de la copie des
sources ; sa [qualification est invalidée](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-001/positive-cached-binary-invalid.json).
Après recompilation attestée, le [positif réel 002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-002/positive-cargo-result.json)
passe 20 tests sur 21 : les deux arrêts sur pente ne varient plus en Y,
mais longer le mur conserve 19,36 mm de variation verticale. Retrait de
l'obstacle, déplacement de l'obstacle, saut, autostep et objets dynamiques
passent dans cette suite. Le défaut tangent reste à corriger ; aucune preuve
runtime Tidewater ni qualification globale n'en est déduite.
Le [positif 004](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-004/positive-cargo-result.json)
reste un échec archivé, 19 tests sur 21 : tangent et autostep refusés.
Le [positif réel 005](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-walking-wall-regression-005/positive-cargo-result.json)
passe ensuite les 21 mêmes tests du négatif beta, sans relever leurs seuils.
Les fenêtres de 120 pas à 35 et 40 degrés restent immobiles ; longer le mur
sur 0,714 m produit au plus 0,719 mm de variation X et 0,713 mm en Y ; la marche
de 20 cm est réellement franchie. L'exécutable est archivé, SHA `2049b412…`,
sur la source `53faf50d…` et les tests `774fa7d7…`, stables avant/après.
La validation Clippy, le pin combiné et le rejeu Tidewater restent à établir.
L'[index V6 v2](E:/OpenRPG_Forge/.gate3-evidence/20261002/map-replay-preparation-001/map-v6-replay-index-v2.json)
conserve les cinq échecs P01, sans promouvoir ce préfixe de parcours en PASS.

Le [harnais de preuve du décor avant combat](E:/OpenRPG_Forge/.gate3-evidence/20261002/environment-before-combat-promotion-001/review-and-apply-proof.json)
est revu et appliqué dans les scripts publics : snapshots nommés avant/après,
source/frame/tick/séquence exacts, contact objet vers gardien initié par le joueur,
Impact et Damage contact positif, ou chaleur/LOS/Damage sans contact. Le gardien
doit passer de vivant à zéro avant toute frappe ; un décor seulement blessant
suivi de combat ne qualifie plus cette branche. Les 42 cas synthétiques Python
et PowerShell passent ; les helpers de retraite feu et de combat direct sont
byte-identiques. La lecture des anciens formats V6 conserve les échecs de quête
et de reprise, même lorsqu’une neutralisation thermique bornée est observée.
Aucun nouveau replay ni verdict runtime n’est acquis par cette préparation.

## Couverture des critères

| Critère officiel | Résultat acquis | Reste à établir |
|---|---|---|
| R3-A joueur, stats, inventaire | Déplacement, endurance, pickup et consommation repris V4-005 | Rejeu sur candidat final et qualification transversale |
| R3-A PNJ/navigation | NAV V7 001 revu : patrouille, raccord, NoPath, sortie/Reached, révisions et Save ; 70 contrôles PASS sur 36 échantillons | Périmètre observé avec politique vide ; aucune qualification de toutes les routes ou eau/coûts |
| R3-A interaction ciblée | Interactions publiques et pickup dans la boucle V4-005 | Rejeu final avec cible physique confirmée |
| R3-A dialogue | Dialogue joué V4-005 ; texte retouché conservé V4-010 | Rejeu final et reprise du texte retouché |
| R3-A quête | Quête Completed V4-005 et progression reprise | Approche environnementale complète sur candidat final |
| R3-A ennemi | Ennemi vaincu et disparition persistante V4-005 | Neutralisation par décor sur candidat final |
| R3-A combat | Combat direct joué V4-005 | Contact causal et approche environnementale sur candidat final |
| R3-A sauvegarde/reprise | Nouveau processus V4-005 et retouche compatible V4-010 | Rejeu final et conséquences physiques persistantes |
| R3-B objet poussable | Primitives de domaine disponibles | Poussée publique et mouvement observé sur Tidewater |
| R3-B objet lançable | Lancer public V4-012, objet déplacé et libéré, conséquence sauvegardée/reprise | Campagne R3-B complète et rejeu final |
| R3-B dégâts d'impact | Adapter Rapier et tests ciblés | Contact, dégâts et causalité observés dans la partie |
| R3-B chute ou piège | Support/charge authorés en essais | Conséquence jouée et persistante |
| R3-B feu | PASS ciblé V6 ShoreWater001 : Ignite/Save/Extinguish/Save/Resume sur base finale commune | Dégâts de chaleur, campagne R3-B complète et rejeu après correctifs |
| R3-B eau/extinction | PASS ciblé V6 ShoreWater001, quatre queries GPU fraîches concordantes, extinction réelle sauvegardée et reprise | Rejeu après correctifs et qualification transversale |
| R3-B objet destructible | Primitives et support authoré | Destruction publique et état repris |
| R3-B réaction IA au danger | V6 EnvFireQuest001 : exposition avec LOS, fleeing=true et déplacement réel de 0,905 m du Veilleur ; le parcours échoue ensuite par chaleur | Rejeu du PNJ avec navigation intégrée sur le candidat corrigé |
| R3-C parcours narratif | Village, forêt, combat, retour, Save/Resume V4-005 ; seuil intérieur V4-010 | Approche environnementale complète, candidat final |
| R3-P01 | Trois lieux authorés, approche combat et conséquence persistante, franchissement intérieur | Deuxième approche par le décor et rejeu final |
| R3-P02 | PASS local V4-010, texte/porte retouchés et deux reprises compatibles | Rejeu sur candidat final |
| R3-P03 | PASS local Studio V7-001 sur COMMON 19b4/855e : suppression du Sceau refusée, diagnostic quête/item/quantité, Apply désactivé ; aucun nouveau reçu ni mutation | Rejeu affecté si le candidat ou les liens de la fixture changent |
| R3-P04 | PASS local V7-001 reset/isolation sur COMMON : même départ choisi et révision, source/clone/Save source préservés, deux fermetures sans force | Rejeu affecté si candidat ou fixture changent ; ce reset ne qualifie pas une reprise de partie |
| R3-P05 | Aucun jugement humain consigné | Partie complète observée, lisibilité, rythme, erreurs et reprise |

La preuve historique [P03 V6-001](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-invalid-quest-v6-runtime-001/gate3-invalid-quest-smoke-ledger.json),
SHA-256 `7e45a41d6413312767d460389db832beda5f40323ae3a73a927946b4ec32cd83`,
épingle le candidat V6 et la base finale `39c8f9d8…` / `80e87c31…`.
La fermeture est propre, sans arrêt forcé. La preuve historique P03 V2-002
reste attachée à son candidat. P04 V6-001 attendait après un clic Save trop
précoce : le refus de stabilisation physique est conservé ; le run ne prouve
pas un mouvement persistant au moment du timeout. Le [rejeu P04 V6-002](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-replay-isolation-v6-runtime-002/gate3-replay-isolation-smoke-ledger.json)
passe : trois transactions Save publiques, deux refus transitoires puis
acquittement au Tick 637. Le nouveau processus retrouve `(40 ; 4,57 ; -115)`
et la même révision 34 ; sources, clone et partie source restent inchangés.
Les deux fermetures sont propres, sans arrêt forcé. La garde produit reste
inchangée ; la qualification porte sur reset et isolation.
Le ledger P04 historique V1 a été retrouvé
directement sur F: exFAT et [copié avec provenance](E:/OpenRPG_Forge/.gate3-evidence/20261002/tidewater-replay-isolation-runtime-ntfs-002-f-provenance-001/source-provenance.json)
sur E: sans modifier l'original ; les 67 617 octets conservent le SHA-256
`0af28abac668bb2317c555e6d449d3fce6c184cb4680003a28bdd580c8f760ca`.
Les tests de domaine ou Rapier synthétiques ne remplacent aucune ligne de
parcours Tidewater. Aucun flag `gate_qualified` n'est promu par un build.

## Navigation et mesures

Les échecs navigation 011 sont conservés : index de détail hors buffer puis
63 erreurs MissingStartSpan. Le correctif utilise les sommets source complets
et refuse les indices invalides. Les campagnes suivantes remplacent ces
preuves pour leur périmètre, sans effacer les échecs.

La [campagne KCC 015](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-015/runtime-execution.json)
utilise la capsule 1,80 m et la vraie géométrie canonique, avec gravité intégrée
et collisions Rapier ; elle ne projette pas directement le joueur sur le Y
du navmesh. Le parcours atteint sa cible avec 1 800 ticks au sol, et l'obstacle
inséré bloque la capsule avant retrait. La
[campagne 016](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-016/runtime-execution.json)
reprend une capsule de 1,90 m correspondant au profil PNJ inspecté. Les deux
sweeps natifs font 63/63 cas bruts chacun ; Node fait 18/18 générations.
Le lancement Edge 016 échoue et n'est pas cité comme preuve navigateur.

Le [natif 017](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/runtime-execution.json)
est rattaché à l'archive source `ce628764…` et à ses entrées inchangées. Le
[rapport Edge réel 017](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/browser-report.json)
passe 18 générations brutes et recalcule le parcours Landmass/Rapier KCC avec
insertion puis retrait d'obstacle dans un worker WASM ;
[exécution](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/edge-execution.json)
27,31 secondes, sans injection d'état. Node recalcule aussi ce parcours.
Ces résultats sont ceux du laboratoire, pas des PNJ intégrés au playtest.
La vitesse 4 m/s du probe est déclarée ; elle ne devient pas une constante PNJ.

Le [benchmark global 512 m 017](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-017/navigation-provider-benchmark.v1.json)
ne panique plus mais reste **NO-GO** : le triangle/polygone 606 possède un
winding invalide et Landmass refuse la sortie brute. Le chemin obtenu après
normalisation est explicitement non accepté. Une correction causale de la
triangulation et son rejeu sont nécessaires. Les preuves de reconstruction
locale et de dépendances doivent être réunies avec ce benchmark dans le
dossier actuel, avec les limites de couverture intérieur/eau.

Le [benchmark brut 018](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-018/navigation-provider-benchmark.v1.json)
sur l'archive source `7207044b…` corrige ce défaut : height mesh accepté,
chemin trouvé, aucun winding invalide et trois hashes identiques ; aucune
normalisation ni fallback accepté. La correction vérifie les diagonales à
leurs deux extrémités et conserve des sommets exacts du contour brut lorsque
la simplification crée des cordes croisées. Le [dossier NAV019 prêt](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-kcc-019/decision-packet.md)
réunit désormais les générations globales brutes et la reconstruction du seul
tile 1 sur les mêmes 74 337 triangles VisualValley, les 126 cas natifs et
18 cas Node/Edge, le KCC physique Tidewater et les audits actuels.
L'archive source vaut `1e69a943…`, l'inventaire final `ca65d0f9…` et les sources
restent stables. Les exécutables natifs mesurés ne sont pas archivés : les
tests ont remplacé les binaires du cache ; leurs hashes et rapports restent
conservés et tout nouveau replay exige une reconstruction/revalidation.

ADR0009 est **accepté par le propriétaire le 2 octobre**. Les cinq conditions
machine passent dans leur périmètre LAB ; la [réponse explicite](E:/OpenRPG_Forge/.gate3-evidence/20261002/navigation-owner-acceptance-001/proof.json)
sélectionne Rerecast 0.4.0 direct avec un fork testé entretenu par le projet,
Landmass 0.9.2 direct et adapter Forge vers Rapier. Le raccord produit et les
vrais PNJ sont en cours d'implémentation puis doivent être qualifiés. Aucun
PASS runtime produit ni verdict Gate 3 n'est déduit de cette sélection.

Les mesures de partie G3-10 se préparent sur le vrai playtest Tidewater V6.
Le précédent probe WorldSketch n'est pas un résultat Tidewater. Le canvas
de la capture readiness vaut 1 314 × 797, avec réglages individuels ; aucun
profil High ni rendu 1 920 × 1 080 n'est observé. La cadence du collecteur CDP,
un compteur de simulation ou d'étapes océan ne devient pas un FPS de présentation.
L'overlay donne une cadence d'images et des passes CPU/GPU bornées, pas un
benchmark complet. La cible du contexte reste 1080p High après préchauffage :
moyenne 60 FPS, p95 ≤16,7 ms, 1 % low ≥45 FPS. Matériel, workload, dimensions,
préparation et limites doivent accompagner chaque mesure ; les lots G3-01
à G3-10 du plan sont proposés, sans création de nouveaux critères officiels.

## Exception CI distante ADR0006

La [lecture distante du 2 octobre](E:/OpenRPG_Forge/.gate3-evidence/20261002/remote-ci-observation-001.json)
confirme que le dernier run Rust
[36300940284](https://github.com/Karenax/OpenRPG_Forge/actions/runs/36300940284)
porte sur `53d9ef79…` : quatre jobs failure, `runner_id=0`, `steps=[]`.
Il n'a testé aucun code et ne porte pas sur le snapshot Gate 3 actuel.
Le run [33335683606](https://github.com/Karenax/OpenRPG_Forge/actions/runs/33335683606)
reste l'origine de l'exception acceptée pour facturation. Ses annotations
récentes n'ont pas été accessibles par le connecteur ; leur cause n'est pas
revendiquée comme vérifiée à nouveau. CI distante Windows/Linux/WASM/supply-chain :
**NOT RUN avant runner**. Aucun succès distant n'est annoncé.

## Suite nécessaire

Les contrôles machine, Supply 008 et le build concordant du pin `002b2e82…`
passent. La revalidation publique V7 004 et l'extension NAV 001 conduisent au
COMMON figé `19b4af30…` / `855eb32b…`, ledger `ca2ad216…`, sans résultat hérité.
NAV V7 passe dans son périmètre observé, P03 passe localement en Studio et
P04 passe localement pour reset/isolation. Diagnostiquer et corriger le parcours public P01, le rejouer, puis P02 et les modes
ShorePush, ForestThrow et EnvironmentFireQuest sur cette même base finale,
avec Save et Resume dans un nouveau processus. Leur couverture des huit
capacités environnementales et des deux approches de quête dépend des reçus
réels et des lecteurs causaux/campaign complets. Aucun résultat préparatoire,
V6 ou statut observed_complete n'est promu en qualification globale.
Conserver tout échec et réexécuter uniquement les contrôles affectés si une
correction devient nécessaire. Après ces preuves concrètes, présenter la
partie complète au propriétaire pour P05, puis consigner son verdict de
clôture. Aucun nouveau critère Gate 1/2 ou FPS n'est ajouté.

Sources d'autorité : [roadmap Gate 3](../../planning/MIGRATION_ROADMAP.md#gate-3--première-boucle-rpg-environnementale),
[plan détaillé](../../planning/GATE3_PREPARATION_PLAN.md),
[ADR0006](../../ADR/ADR-0006-remote-ci-exception-gates-0-4.md),
[ADR0009](../../ADR/ADR-0009-navigation-provider-benchmark.md),
[ADR0018](../../ADR/ADR-0018-gate2-owner-validation-gate3-tidewater.md),
[ADR0019](../../ADR/ADR-0019-gate3-authored-gameplay-simulation.md).


### Diagnostic du mouvement de fuite — copie Source3 isolée

La campagne V8 reste à 20/22 critères locaux bornés, avec danger IA non qualifié et P05 en attente. Fire004 mesure la perception du feu puis `no_path`, vitesse horizontale nulle et mort du Veilleur. Ses douze tuiles sont prêtes ; la sous-cause Landmass n’est pas exposée par V8.

La copie `source-snapshot-003-navigation-escape` part des 2009 fichiers byte-identiques du HEAD privé `8530358bee9a0d4bca7fe89e1bdb81c589ad6f74` (preuve `navigation-escape-preparation-001/proof.json`). Un diagnostic de route y est en préparation : état fournisseur exact, échantillonnage départ/cible et requête de chemin dans le même Archipelago après update ; aucune modification de destination ni d’actuation. Ce diff isolé n’est pas appliqué au Primary, pas committé et pas qualifié.

Le premier test natif s’est arrêté avant compilation sur `os error 112` (disque E: plein). Le cache Cargo inactif de cette campagne a ensuite été audité et 662 fichiers PDB régénérables retirés (3 890 135 040 octets ; inventaire et preuve dans `navigation-escape-preparation-001`). Sources, rapports et exécutables archivés sont conservés. La relance du test ciblé est en cours ; aucun PASS nouveau n’est déduit. Une session Studio extérieure à cette campagne est ouverte : aucune nouvelle UI de qualification n’est lancée à ce stade.


### Diagnostic Source3 — contrôles natifs ciblés PASS

Après l’échec disque, une deuxième tentative de compilation s’est arrêtée sur `rustc-LLVM ERROR: out of memory`, avant toute exécution du test. La tentative suivante passe après retour de la marge mémoire. Clippy avec avertissements interdits, le test de destination déconnectée et les 15 tests de `gameplay_navigation` passent. L’exécutable exact est archivé, SHA-256 `19914d144040407d3d1a6202a889246fd85bd4d7c0803d69e99f5cbe4d1f2bdc`.

La copie privée est propre au HEAD `9411091277b85ec618b62a9a9c8577f0634b4976`, avec seulement trois fichiers modifiés par rapport à Source2 ; les 2009 fichiers de Source2 sont recontrôlés inchangés. Preuve : `navigation-escape-preparation-001/validation-proof.json`. Le cache diagnostic est rafraîchi sur changement de génération ou déplacement départ/cible supérieur au demi-rayon (.175 m) ; le stamp intègre aussi la révision de policy. Il enregistre ses points effectivement interrogés. Aucun mouvement ni choix de destination n’est changé.

Le frontend correspondant `tidewater-navigation-diagnostic-009-frontend` est en construction avec les imports figés de V8. Cette copie n’est pas appliquée au Primary ; aucun commit Primary ni publication. Ces tests natifs ciblés ne constituent ni une qualification du nouveau candidat complet ni une preuve publique sur Tidewater. Le score V8 demeure 20/22, danger IA et P05 non qualifiés.


### Reprise du 3 octobre — diagnostic public achevé, correctif Source4 en cours

Pause demandée puis reprise explicitement par le propriétaire. Note de reprise conservée dans `gate3-pause-20261003-001/RESUME.md`.

Le frontend Source3, son candidat natif et la revalidation publique des 22 formulaires sont terminés. Le replay ForestDanger sur ce candidat (`51eb0a1098740b3c3d7c235849dbaaa410249c23066e743f8728667d44decf2e`) a mesuré `NoPath` : départ échantillonné IslandId(9v1), objectif IslandId(5v1), recherche sans chemin ; aucune commande horizontale. Les quatre conteneurs de capture du journal correspondent à une seule observation distincte. Cinq sauvegardes sont recontrôlées byte pour byte, les sources sont préservées et les processus possédés sont fermés sans arrêt forcé. Preuve : `gate3-navigation-diagnostic-root-review-001/proof.json`. Cela établit une route indisponible ; la cause précise de la topologie n'est pas démontrée.

Source4 (`source-snapshot-004-reachable-flee`) est maintenant préparée depuis les 2009 fichiers Source3, byte-identiques au départ. Le correctif runtime isolé recherche au plus huit destinations de fuite, uniquement via le Landmass courant après son tick partagé ; la destination choisie devient l'intent du tick suivant, puis le même agent et Rapier KCC déterminent le mouvement. Aucun branchement sur un nom de module, PNJ ou asset, aucune translation de secours. Cache borné invalidé par génération/reconstruction et pause ; départ invalide explicitement exclu. Deux tests réels de chaleur couvrent sortie latérale et enclave sans mouvement, avec invalidation du cache négatif. Compilation/tests en cours ; aucun PASS Source4 ou Tidewater nouveau n'est annoncé.

La campagne formelle V8 demeure à 20/22 : réaction physique IA et revue humaine P05 ouvertes. Aucun code Source3/4 appliqué au checkout principal, aucun commit Primary/push, aucune nouvelle CI distante.


### Source4 — 18 tests natifs PASS, candidat Studio en construction

Les 18 tests de navigation passent, Clippy ciblé avec avertissements interdits passe, formatage et diff check passent. Trois cas de chaleur couvrent objectif direct déconnecté avec fuite KCC latérale réelle, enclave sans translation horizontale et cache négatif invalidé par une retouche de mur, puis destination maintenue pendant une reconstruction de policy avec aucune actuation de route périmée. L'ancienne fixture enclavée échouait avant toute recherche ; sa salle a été élargie pour garder le joueur d'allumage à l'intérieur. L'échec et les logs précédents sont conservés, aucun seuil produit n'a été modifié.

Source4 est propre au commit privé `8baa5027136426949ab05baaad04bb8cbfca4bea`, cinq fichiers de code/test changés. Source3 et ses 2009 fichiers sont recontrôlés inchangés. L'exécutable des tests réellement exécutés est archivé (`6a79ceedd65ed0afc979fd95020923b6a1d29903db092606ca27b3845cbe0ddd`). Preuve : `reachable-flee-preparation-001/validation-proof.json`.

Le frontend `tidewater-reachable-flee-010-frontend` est en construction avec les imports figés Source3/V8. Les replays publics V10 sont seulement préparés et gardent les mêmes harnais et seuils. Aucun PASS Tidewater ou qualification globale Source4 ; campagne V8 toujours 20/22, P05 humaine ouverte, aucun code appliqué ni commit dans le checkout principal, aucun push.


### 3 octobre — V10 construit, réaction au danger mesurée et correctif intégré

Le frontend Source4 et le candidat natif sont terminés : candidat `940e479aa61a5ba34ef94403706033299173e0929c3c814cde1d999c4f804505`, WASM `d8407b7ce2ef619e2043985ada5d904c94f551060bc06071c2c3a2d06701b7c4`. La revue des octets contrôle 1335 fichiers web et 882 sources (`reachable-flee-candidate-root-review-001/proof.json`). Ce build ne qualifie pas le checkout Primary complet.

Le premier lancement de revalidation V10 (`tidewater-gameplay-revalidation-reachable-v10-001`) échoue au démarrage : le runtime WebView2 .37 épinglé avait disparu après mise à jour. Le nettoyage forcé de ce démarrage n'est pas établi par le ledger externe et ne peut pas être déclaré absent. Les preuves de cet échec restent conservées. Une copie isolée des harnais, `webview-runtime-harness-001`, accepte une version explicitement épinglée et vérifie ses octets avant lancement ; aucun timeout, budget ou assertion physique n'est relâché. Le runtime .53 est vérifié par fichier/version/SHA. Le sidecar de nettoyage des démarrages échoués doit encore être agrégé aux ledgers pour les futurs essais.

La revalidation publique V10-002 passe avec 22 formulaires relus et acquittement Studio Save. Le checkpoint reste `19b4af3071b7c291986408c6422f140ef06be499a2617c72ff95073a687a641e`, la tête `855eb32bae6794e97624bd44090f9707035d566b67b2f9ac702b8bb27d73a348`. Source préservée et fermeture normale du processus d'essai constatées.

ForestDanger V10-002 termine le replay et la reprise dans un nouveau processus. Le lecteur causal figé valide feu, dommage de chaleur et réaction IA. À l'exposition, le Veilleur est vivant (80/100), en fuite, déplacé de 0,06225 m avec produit scalaire horizontal d'éloignement positif. Il parcourt ensuite environ 1,4 m avant de mourir de chaleur ; la position et la mort persistent après reprise. Cela démontre la réaction au danger, pas une mise en sécurité ni une survie garantie. Une seule observation de navigation distincte est capturée (10 requêtes de recherche cumulées) ; elle ne suffit pas à reconstruire tout le trajet. Les cinq sauvegardes sont vérifiées par SHA, les sources conservées et les deux processus fermés sans arrêt forcé (`reachable-flee-public-root-review-001/proof.json`).

Les cinq fichiers de code/test Source4 ont été appliqués au checkout Primary après garde des hashes et sauvegarde des octets antérieurs (`reachable-flee-primary-apply-001/proof.json`). Les changements préexistants sont préservés. Aucun commit Primary ni push. Le critère de réaction IA dispose d'une preuve publique V10 ciblée ; les 20/22 de la campagne V8 restent un résultat historique de V8. Les régressions de la boucle RPG/physique sur V10 et la revue humaine P05 restent requises avant tout verdict global. Gate 3 ouverte, CI distante NOT RUN selon ADR-0006.


### Régression P01 V10 — lancement 001 échoué, 002 en cours

Le harnais isolé `webview-runtime-harness-002` conserve stage001 intact et agrège les sidecars de nettoyage de démarrage dans les ledgers : toute tentative d'arrêt forcé est signalée, toute fermeture non constatée refuse le succès, et tout échec de démarrage force le statut failed_closed. Parse PowerShell et self-tests gameplay/physique passent ; le test synthétique vérifie les deux variantes de clé de fermeture (`validation-proof.json`). Aucun budget ou assertion gameplay relâché.

P01 V10-001 échoue avant CDP, aucun résultat RPG : source préservée, processus PID2832 terminé, arrêt forcé correctement rapporté dans le ledger principal. P01 V10-002 est lancé avec chemin WebView2 .53 normalisé par GetFullPath, même forme que les deux replays V10 réussis. Le chemin avec slashs de 001 est une cause suspectée, pas une cause démontrée. Le receipt de lancement et les octets du navigateur sont archivés sous `webview-runtime-harness-002`. Aucun PASS P01 V10 acquis tant que le replay et la revue des reçus ne sont pas terminés. Gate 3 demeure ouverte.


### P01 V10-002 — blocage réel à la sortie intérieure, diagnostic 003 lancé

La normalisation du chemin permet bien le démarrage .53 de 002. Le parcours vainc le gardien, récupère le Sceau, affiche la quête Completed et entre sur le sol canonique de l'intérieur. Il échoue ensuite à sortir : joueur X46,08426 / Z-147,51738, ancre Y6,64962, porte ouverte, entrée active, demande Z-0,05 m et résolution horizontale nulle durant douze secondes. Aucun PASS P01 V10 ni sauvegarde/reprise acquise par ce parcours incomplet. Les sources sont préservées et le processus fermé normalement sans arrêt forcé.

Le Messager est observé stationnaire hors du couloir (X48,82251 / Z-150,35439), reached, commandes et déplacements horizontaux nuls sur trois ticks frais, tuiles requises prêtes. Il ne constitue donc pas l'obstacle dans l'enveloppe vérifiée. Le collider exact qui bloque le retour n'est pas identifié.

Dans `webview-runtime-harness-003`, le même parcours après quête ajoute uniquement un alignement latéral intérieur à ±0,05 m avant le segment de sortie inchangé. L'occupation au sol est à nouveau exigée ; aucune géométrie, assertion de sortie, temporisation ou budget n'est relâché. Le self-test passe. Replay 003 en cours, centrer le joueur reste une hypothèse diagnostique et pas une correction produit démontrée. Stages 001/002 et leurs preuves restent intacts. P05 et Gate 3 ouvertes.


### P01 V10-003 — perte de focus avant retest du seuil, 004 en cours

003 échoue pendant le retour au village : document visible mais non focalisé, pointeur libéré et entrée inactive. Le centrage intérieur n'a pas été exercé ; l'essai ne tranche donc pas le diagnostic de collision. Source préservée, fermeture normale, aucun arrêt forcé. Le ledger et les captures sont conservés.

Le stage isolé 004 renforce la readiness avant le clic de capture : réactivation de la même cible après le cooldown puis attente de document.hasFocus et visibilité, sous le timeout existant. Tous les critères de capture et de passage physique restent exigés. Self-test PASS ; 004 lancé avec le même parcours après quête et le même centrage précis préparé dans 003. Aucun PASS P01 V10 ni qualification du centrage tant que le replay est incomplet. Stage003 inchangé. Gate ouverte.


### P01 V10-004 — centrage exercé, sortie toujours bloquée ; Source5 diagnostic

Le focus renforcé permet de poursuivre le parcours. Le centrage latéral intérieur est cette fois exercé et vérifié ; la sortie reste bloquée (X45,98 / Z-146,61, demande Z-0,05 m, résolution horizontale nulle pendant douze secondes). 004 est failed_closed, sources préservées, processus fermé normalement, aucun arrêt forcé. Le centrage ne constitue pas une correction suffisante. Les essais 002 et 004 ne prouvent pas que le collider de porte est en cause : le compte public de contacts inclut le sol et ne donne pas son identité.

Source5 `source-snapshot-005-interior-collision-diagnostic` est créée depuis Source4 propre `8baa5027…`, 2010 fichiers vérifiés. Deux fichiers isolés ajoutent uniquement des témoins du dernier output KCC réel : tick, translations demandée/résolue, entités et normales de contact ; la télémétrie publique dérive nom, position du collider et état ColliderDisabled depuis le World. Aucun comportement de mouvement changé, aucun fichier Source5 appliqué au Primary. Formatage terminé, Clippy en cours, candidat diagnostic non construit et aucun résultat runtime Source5.

Quatre profils WebView2 inactifs et régénérables de 002/003 et ForestDanger ont été supprimés après contrôle des chemins et commandes des processus. Les 247 fichiers hors profils (sources, sauvegardes, captures et ledgers) sont vérifiés inchangés. Preuve `obsolete-webview-profiles-cleanup-001/proof.json`. Le profil du replay 004 était protégé pendant son exécution. Gate 3 ouverte ; P01 V10 et P05 non acquis.


### Source5 — 22 tests joueur PASS, diagnostic web en construction

Source5 est propre au commit privé `f65ff84d151080432b3daf8442bc5174d1d49a64`. Deux fichiers changés uniquement : témoins du dernier output KCC et publication dans la télémétrie publique. Le champ ColliderDisabled envisagé dans la préparation précédente est retiré pour éviter une dépendance Rapier directe supplémentaire dans Studio ; les contacts exposent entité runtime, nom, position et normale, sans prétendre à un identifiant persistant.

Formatage et Clippy natif ciblé PASS. Une première invocation de test utilisait le nom du fichier au lieu du nom du module et exécutait zéro test ; elle n'est pas une validation. Le filtre corrigé `player_controller::tidewater::tests` exécute 22 tests, tous PASS, zéro ignoré. Les tests couvrent le mouvement KCC sur sol/pentes, murs bloquants, mouvement tangent, retrait/déplacement d'obstacle, marche/nage et échelles. L'exécutable réellement exécuté est archivé SHA `5ff225cedca9619a996bc84cbe0c81783bc01a82b28bb988b37f8e3582b9e39a`. Les 2010 fichiers Source4 sont vérifiés inchangés ; Source5 diffère uniquement dans les deux fichiers annoncés. Preuve `interior-collision-diagnostic-preparation-001/validation-proof.json`.

Le frontend `tidewater-interior-diagnostic-011-frontend` est en construction avec imports figés V10. La publication des témoins dans le web reste à compiler puis à mesurer dans un replay réel ; les tests natifs ne qualifient pas ce chemin WASM. Candidat natif Source5 non construit, aucun code Source5 appliqué au Primary, aucun résultat de passage du seuil nouveau. Le deuxième nettoyage retire cinq profils WebView2 inactifs de campagnes terminées, avec 263 fichiers hors profils contrôlés inchangés (`obsolete-webview-profiles-cleanup-002/proof.json`). Aucun cache Cargo actif supprimé. Gate 3 et P05 ouvertes, aucun push.


### Frontend Source5 — échec de stockage conservé, retry 002 lancé

L'essai frontend 001 termine avec code 101 : `rustc-LLVM ERROR: IO failure on output stream: no space on device` pendant la production de forge_studio_frontend. Aucun frontend valide ni candidat natif n'en est issu. Les logs, source-before et dossier d'essai sont conservés ; la source privée f65ff84d reste inchangée.

Après inventaire des profils et audit des commandes des processus actifs, seuls 20 profils WebView2 régénérables de 18 anciennes campagnes inactives sont supprimés. Les 1110 fichiers hors profils sont vérifiés inchangés, aucune source ou sauvegarde supprimée (`obsolete-webview-profiles-cleanup-003/proof.json`). 5,76 Go libres mesurés après nettoyage. Aucun cache Cargo ni processus externe touché.

Le retry frontend utilise un nouveau dossier `tidewater-interior-diagnostic-011-frontend-002`, le même Source5 propre et les mêmes imports V10 figés. Il est en cours. La revue d'octets préparée pointe vers 002 ; elle ne sera exécutée qu'après construction native appariée. Les replays checkpoint/P01 V11 sont préparés, non lancés. Les 22 tests natifs restent PASS dans leur portée ; aucun PASS web/runtime nouveau, aucune sortie intérieure qualifiée, Gate 3 ouverte.


### Frontend Source5 retry 002 construit ; candidat natif en construction

Le retry termine avec code 0 : release frontend 4m22s, worker 8,02s. WASM `d13c000b6e87dbc9287449fcb5c9ef369ca001c2daf91ba8f6b5d0fcea0cc047`, worker `084dfd02043b39c9141026d6e43038c3b3f866ef37b9df33055bb649b2e875e4` inchangé ; manifest de worker `705279bf8024f44c90239e57b00117be1545cb8dc877017e3a2fa61c9c53750a`. Les sources sont identiques avant/après compilation. L'avertissement préexistant de compatibilité future proc-macro-error2 2.0.1 est conservé ; aucun échec Rust de ce retry. Preuve `tidewater-interior-diagnostic-011-frontend-002/frontend-build-proof.json`.

Le candidat natif `tidewater-interior-diagnostic-011-candidate-001` est en construction à partir du même Source5 propre f65ff84d et du frontend 002. Cela ne prouve pas encore la publication des contacts dans une session jouable, leur cause ni le franchissement de l'intérieur. Les replays V11 restent non lancés. Le harnais et les critères physiques n'ont pas changé. Gate 3 ouverte, aucun code Source5 appliqué au Primary et aucun push.


### Source5 V11 construit et checkpoint public revalidé

Le candidat natif termine avec code 0. La revue Root vérifie les octets de 1335 fichiers web et 886 sources : statut BUILD_ONLY_ROOT_BYTE_VERIFIED, candidat ef6914e7396081ef95c0cf16f11100c0b8fbde8ab01838551152f9119ff17d64. Le checkpoint public V11 termine passed, avec source préservée, même head complet et fermeture normale sans arrêt forcé. Les contacts KCC ajoutés restent diagnostiques ; aucun mouvement n'a été corrigé.

Le replay P01 V11 après quête est lancé dans une fixture isolée (gate3-v11-interior-diagnostic-p01-001). Son résultat demeure en attente au moment de cet enregistrement. Aucun franchissement de porte, qualification complète ou acceptation P05 n'est déduit du build et du checkpoint. Source5 n'est pas appliqué au Primary ; Gate 3 ouverte, aucune publication.


### P01 V11 terminé en échec physique ; contact plancher identifié

Le replay public après quête termine failed_closed (code 1) au segment interior-exit-kcc : aucune progression horizontale pendant douze secondes. La source est préservée et tous les processus possédés sont fermés sans arrêt forcé. La capture finale publie le retour KCC au tick 11849 : demande [0, -0.0027251, -0.0500031], déplacement effectif [0, 0.0001, 0]. L'unique contact rapporté est Building 45e57010-0a4b-58b7-8ce0-403f04bf2167 / Floor, normale [0, 0.99999994, 0], position [46, 6.5496173, -145]. Preuve Root : interior-diagnostic-public-root-review-001/proof.json, ledger 13066802480b8510cac79a1f4dded4055f1d1ec2f215a92bca69887a5fc9d252.

Cette observation localise le contact au plancher ; elle ne prouve pas encore le paramétrage causal du contrôleur ni toute la séquence précédente. Le franchissement reste non corrigé et la qualification P01 du nouveau candidat échoue. Prochaine étape : reproduire ce cas avec le contrôleur physique natif et examiner sa résolution sur le plancher. Gate 3 ouverte ; aucune acceptation P05, aucun push.


### Source6 : première reproduction native négative, matrice de contacts en cours

Une nouvelle copie isolée part du Source5 f65ff84d ; les 2010 fichiers de référence sont conservés, seul tidewater_player_tests.rs reçoit un test. Première compilation : erreur E0599 dans le test (Entity traité comme Option), corrigée. Le balayage natif suivant termine avec 1 test passé : à la position observée en jeu, le cuboid dimensionné produit une normale légèrement oblique et conserve environ 0,050005 m de progression. Cette première fixture ne reproduit pas l'échec Web ; son source, exécutable et log sont archivés avec hashes dans raw-floor-test-002-proof.json.

L'analyse float32 des équations du Rapier installé, alimentée par la normale verticale réelle [0, 0.99999994, 0], calcule un résidu tangent Y de -4.656612873077393e-10. Ce résidu sélectionne la branche nonslip qui supprime la tangente horizontale lorsque normal cross up est nul. Il s'agit d'une inférence numérique bornée, pas encore d'une preuve de tous les paramètres runtime. Le test est élargi à 20 balayages (positions et dégagements verticaux), avec le cube redimensionné comme le plancher Forge ; compilation en cours, raw-floor-test-003.log. Aucun changement de mouvement ni application Source6 au Primary. Gate 3 ouverte.


### Blocage KCC reproduit en natif ; correction Rapier isolée en validation

La matrice termine en échec attendu avant correction : 1 balayage sur 20 annule l'avancée, à X45.954227/Z-146.72815 et 0.0001 m de dégagement. Normale [0, 0.99999994, 0], déplacement [0, 0.00009999999, 0] : même signature que l'échec public V11. Le source, le log et l'exécutable natif sont archivés et hashés dans raw-floor-test-003-proof.json. L'échec n'est pas compté comme un PASS.

Source6 propose une copie locale du Rapier déjà verrouillé 0.35.0-glamx0.2 : 217 fichiers du paquet registre vérifiés contre l'archive dont le checksum est celui du Cargo.lock ; licence Apache-2.0 conservée. Un seul fichier Rapier change : compute_hit_info normalise normal1 avant le calcul des pentes avec fallback pour une normale dégénérée. Le graphe, les versions et les dépendances transitives sont identiques ; seule l'origine du paquet Rapier change. La métadonnée VCS du paquet indique dirty et ne constitue pas une revendication de checkout upstream propre. Provenance : rapier-patch-preparation-proof.json et third_party/rapier-normalization-NOTICE.md.

Les 23 tests natifs du joueur, dont la régression, sont lancés (patched-player-tests-001.log). Aucun résultat correctif n'est encore revendiqué. Build WASM, candidat, replay P01 après quête, régressions navigation/physique/Studio et P05 restent à qualifier. La proposition de maintenance est consignée dans decision-draft.md ; elle ne remplace pas l'ADR0009 accepté. Aucun changement Source6 appliqué au Primary, Gate 3 ouverte, aucune publication.


### Correction Rapier : 23 tests natifs passent, Clippy retry en cours

La suite joueur réelle termine avec 23 passed, 0 failed, 0 ignored. Le nouveau test exécute les 20 balayages physiques et n'en trouve plus aucun bloqué après normalisation. Les 22 cas existants couvrant pentes, murs, autostep, saut, obstacles déplacés/supprimés, nage et échelles passent également. Exécutable, source et log archivés avec hashes dans patched-player-tests-001-proof.json. Cette portée reste native et ciblée.

Clippy --lib --tests -D warnings échoue d'abord sur 6 diagnostics de notation des littéraux du nouveau test. Ils sont corrigés sans suppression de lint ; la réduction proposée de 0.050003052 à 0.05000305 est vérifiée bit pour bit en f32. Retry clippy-002.log lancé, session 49279. Les builders isolés sont préparés et passent l'analyse AST PowerShell ; ils incluent le paquet Rapier complet et son NOTICE dans les pins avant/après. Aucun build Web/candidat ni replay public de la correction lancé à ce stade. Primary préservé, Gate 3 ouverte.


### Source6 natif propre ; frontend corrigé V12 en construction

Clippy --lib --tests -D warnings termine code 0 en 41,08 s après la correction de notation des littéraux du test. Snapshot privé propre 30f5e83f39a2c8b412d2de081090650e2d7381d4, avec ADR-0021 proposé et NOTICE de maintenance ; aucune acceptation propriétaire de cette proposition revendiquée. native-validation-proof.json vérifie les 2010 fichiers de référence (hors trois modifications annoncées), les 216 fichiers Rapier inchangés, les 23 tests natifs passés et Clippy strict. Les sources exactes exécutées des tests restent archivées ; les derniers changements de littéraux conservent les mêmes bits f32.

Pour réserver la place au build Web, cleanup-004 retire 13 profils WebView inactifs de 12 campagnes V4 failed_closed. Chemins résolus vérifiés dans l'evidence root, inventaire complet des processus contrôlé avant suppression ; 754 fichiers hors profils byte-préservés. Environ 6,94 Go libres après nettoyage. Aucune source, sauvegarde, capture, ledger, cache Cargo ou application étrangère supprimé.

Frontend corrigé tidewater-floor-correction-012-frontend-001 lancé depuis le même Source6 propre, avec imports gelés du frontend V11 et pins incluant tout Rapier. Session 78503, frontend-build-001.log. Résultat Web et candidat natif encore en attente ; aucun PASS public du correctif. Gate 3 ouverte, Primary préservé, aucune publication.


### Frontend corrigé V12 construit ; candidat natif en construction

Le build Web termine code 0 avec sources stables. WASM frontend 9a714b4fb3127b0c03a7f7c62eb3efccada1cad0a704ae24f5ee864ae57a0ad1 ; worker e5cbbce76142441b63d99ac12cdef14d978a697a2b71669110320a17d0a135fb ; manifest 4c7a9bca72247aedbdb8f44989e1aac86fa5d25e01531696733fd4b22a2207f8. Le worker change lui aussi : aucune qualification runtime de son ancien binaire n'est héritée. L'avertissement de compatibilité future préexistant est conservé dans les logs, sans échec de compilation.

Le candidat tidewater-floor-correction-012-candidate-001 est lancé à partir de ce frontend et du Source6 propre 30f5e83f, session 61240, native-build-001.log. La revue Root préparée vérifiera les fichiers empaquetés, tous les pins source et les 218 fichiers du paquet Rapier. Le lecteur P01 V12 préparé exige le même ordre après quête, les positions réelles dedans/dehors, les reçus de dégâts/pickup/quête, les octets sauvegardés et la reprise dans un nouveau processus. P02 et contrôles machine complets sont préparés, non exécutés. Le build ne prouve pas encore le franchissement public corrigé. Gate 3 ouverte ; Primary préservé et aucune publication.


### V12 construit et checkpoint public passé ; P01 après quête lancé

Le build natif termine code 0 en 3m19s. Revue Root exécutée : 1335 fichiers web empaquetés et 1101 fichiers source vérifiés, dont 218 fichiers du paquet Rapier ; source/frontend concordants et snapshot 30f5e83f propre avant/après. Candidat f11e0edba6358a1a2aa4ef13a07ec92d4130a3d97c1bc22688ad74daf7ee82fd. Preuve floor-correction-candidate-root-review-001/proof.json, statut BUILD_ONLY_ROOT_BYTE_VERIFIED.

Le checkpoint Studio public V12 termine passed avec source préservée, public_save_ack_observed et complete_head_identical vrais ; processus possédé fermé normalement, aucun arrêt forcé. Ledger tidewater-gameplay-revalidation-floor-v12-001/gate3-revalidated-gameplay-authoring-ledger.json. Aucun résultat de runtime antérieur n'est hérité.

P01 V12 est lancé dans gate3-v12-floor-correction-p01-001, session 34178, public-p01-001.log. Même visite après quête avant sauvegarde, même approche physique finale et reçus de contact du gardien exigés. Franchissement, sauvegarde/reprise et P02 restent en attente. Les contrôles machine complets et P05 ne sont pas remplacés par le checkpoint. Gate 3 ouverte ; Source6 non appliqué au Primary, aucune publication.


### P01 V12 échoue au recentrage avant sortie ; arrêt en mouvement documenté

P01 V12 termine failed_closed avant interior-exit-kcc. Source préservée, processus possédés fermés normalement, aucun arrêt forcé. Le recentrage atteint X45.975685 (erreur 2,43 cm) mais la vitesse vaut encore -2.977216 m/s ; le mouvement KCC est réellement résolu sur le plancher (-0.0496 m environ). Après relâchement et stabilisation, X45.536839 : déplacement supplémentaire -0.4388466 m, hors de la borne finale 5 cm. Le joueur reste grounded au Y canonique 6.649617. La condition en échec est donc l'alignement final, pas la perte du support au sol.

Preuve floor-correction-public-p01-failure-review-001/proof.json : arrivée, vitesse, KCC et pose finale extraits du journal public et hashés. La sortie originale n'est pas exercée ; aucun PASS P01, sauvegarde/reprise ou P02 n'est revendiqué. Prochaine correction bornée : le pilotage public du harnais doit obtenir une pose stabilisée au centre avec les mêmes tolérances et limites, sans modifier le candidat ni la géométrie. Les harnais exécutés restent immuables.

Comparaison des artefacts V11/V12 archivée dans build-impact-proof.json : frontend -4518 octets, worker même taille (hash différent), candidat natif -2048 octets. Durées observées : frontend 14 min, worker 7,59 s ; caches froids et builds étrangers concurrents empêchent d'en déduire une régression contrôlée de temps ou de performance runtime. Gate 3 ouverte, Primary préservé, aucune publication.


### Harnais005 de recentrage stabilisé ; P01 V12 tentative002 lancé

Copie isolée du harnais004, tous les fichiers byte-préservés sauf Invoke-Gate3GameplayPlaytestSmoke.ps1. Le pilote de recentrage utilise des touches WASD publiques relâchées après chaque impulsion (10 ms près du centre, 40 ms plus loin), puis attend trois observations fraîches de position stable et vitesse horizontale <=0.01 m/s avant de juger la distance. Position et vitesse proviennent des champs DOM publics. Arrivée seulement dans la même tolérance 0.05 m ; budget de temps, assertions de support/occupation, parcours de sortie et candidat inchangés. Les harnais déjà exécutés restent immuables. Analyse AST PowerShell et SelfTest existant passent ; aucune qualification runtime du nouveau pilote déduite de ces contrôles.

P01 V12 tentative002 est lancé, gate3-v12-floor-correction-p01-002, session 81007, public-p01-002.log. Même candidat f11e0edb, checkpoint V12 passé et visite après quête. Le lanceur P02 dépend de ce nouveau P01 passé ; il reste non exécuté. Un échec du recentrage ou du franchissement restera failed_closed sans relâcher le critère. Gate 3 ouverte, aucun changement produit supplémentaire, Primary préservé et aucune publication.


### P01 V12 tentative002 : échec forêt avant recentrage ; hypothèse de jonction en cours

La tentative002 termine failed_closed au segment forest-waypoint-5-step-4, avant gardien/intérieur. Les touches W restent demandées et la vitesse source vaut presque -3 m/s sur Z ; pourtant la demande après adaptation vers le KCC n'est plus que -9e-7 m environ. Aucun contact dans la dernière sortie KCC ; les dernières observations distinctes précédentes portent le collider runtime 682v0, position [64,0,-192], normales de pente environ Y0.806/Z0.591. Source préservée, tous les processus possédés fermés normalement sans arrêt forcé. Le nouveau pilote de recentrage n'est pas exercé et aucune conclusion sur lui n'est tirée.

Position finale Z-255.710434, soit environ 0.289566 m de Z-256. Le code de collisions samplees construit des heightfields de largeur 128 ; la position du collider est compatible avec une tuile dont le bord est Z-256. C'est une hypothèse de jonction, pas une preuve d'identité du collider ni de cause. Preuve Root floor-correction-public-p01-forest-review-001/proof.json. Aucun replay identique relancé pour contourner l'échec.

Source7 isolé part du Source6 propre 30f5e83f et conserve 2230 fichiers, y compris les fichiers ignorés du paquet Rapier. Seul un test physique est ajouté : deux heightfields adjacents sur une pente marchable de 35 degrés, avec le vrai KCC et la même capsule. Test natif raw-seam-test-001.log lancé, session 85358 ; résultat encore en attente. Aucun changement du mouvement produit. V12 et les harnais exécutés restent intacts, P02 et qualification globale en attente, Gate 3 ouverte et aucune publication.


### Diagnostic natif Source7 : erreur de compilation corrigée et tuiles réelles préparées

Le premier test termine code 1 avec E0277 : l'indice de colonne i32 ne se convertit pas par f32::from. C'est une erreur de compilation du test, sans verdict physique. Source et log exécutés conservés sous raw-seam-test-001. L'indice est corrigé en u16 dans le test suivant ; aucun code de mouvement produit ne change.

Deux heightfields réels de 129x129 échantillons autour de Z-256 sont préparés depuis le blob Tidewater vérifié be03f5db, avec l'interpolation pixel-center bilinéaire et l'ordre column-major de production. Centres [64,0,-320] et [64,0,-192], largeur 128 m, position initiale exactement issue du DOM public en échec. Le nouveau cas fait 60 sweeps du vrai KCC avec la capsule du joueur ; le cas synthétique 35 degrés est conservé. Preuve de préparation real-tiles-preparation-proof.json. Ces fixtures ne modifient ni la map ni le runtime public.

Les 25 tests natifs du contrôleur sont lancés, session 68338, raw-seam-test-002.log. Le runner archive les sources avant/après, le journal et l'exécutable réellement compilé. Résultat encore en attente ; ni l'identité du collider public ni la cause de l'arrêt forêt ne sont prouvées. Gate 3 ouverte ; V12, Primary et les harnais exécutés restent préservés, aucune publication.


### Source7 : franchissement KCC brut réel confirmé ; test du filtre de murs lancé

Le run002 termine code Cargo 101 : 24 tests passent, un test synthétique échoue. Sources stables, 2234 fichiers vérifiés ; exécutable effectivement lancé archivé, SHA c723f6da051ebd725513d001a1e817806ff545fb28d2bf51b56299e046a1bac2. Les 23 tests préexistants passent. Le cas Tidewater réel passe : depuis Z-255.71043, 60 sweeps du KCC brut aboutissent à Z-257.5923, contact de pente normal Y0.811817/Z0.583729. Le KCC brut traverse donc ces deux tuiles réelles depuis la pose capturée. Les 129 échantillons du bord partagé sont identiques bit à bit, preuve shared-edge-proof.json. Aucune identité du collider public ni cause globale de l'arrêt forêt n'est déduite de ces faits.

Le cas synthétique traverse également la jonction X0 et atteint X-0.330785, mais son assertion X<-0.5 exigeait davantage que le franchissement recherché. Cette borne ignorait la diminution de projection horizontale en montée. Le test suivant exige que toute la capsule de rayon RADIUS ait franchi le bord : X<-RADIUS. L'ancien échec reste archivé et n'est pas réétiqueté PASS.

Un troisième cas appelle directement constrain_wall_drive à la position réelle, avec les deux identifiants de tuiles comme contacts antérieurs possibles, puis exige que la revalidation physique fraîche préserve une demande vers Z négatif. Cette liste de candidats est une précondition native contrôlée ; elle ne prouve pas la liste historique du runtime public. Les 26 tests sont lancés en run003, session 78159 ; résultat en attente. Aucun mouvement produit changé, aucune modification Primary, Gate 3 ouverte et aucune publication.


### Source7 reproduit le veto du filtre ; correction Source8 en tests natifs

Run003 termine code Cargo 101 : 25 PASS, un échec du test constrain_wall_drive. Le franchissement brut réel et celui de la capsule synthétique passent. Avec les deux tuiles comme candidats antérieurs possibles, la demande [0,-0.002725,-0.05] est réduite à [0.000105333,0.000003819,-0.0000002235] ; une tuile est retenue. Sources et exécutable archivés, SHA 3c4c00d722122f3a46abf7c638341bf68929202ce879c369c6b1cc14e15fcf40. Cette reproduction native ne prétend pas connaître la liste historique du DOM public.

Le même exécutable est rejoué avec le diagnostic existant FORGE_WALL_DIAGNOSTIC, sans modification ni recompilation. Le vrai balayage prédit grounded, effective Z-0.03116123/Y0.02296952 ; son unique collision est la pente marchable de la tuile 40v0, normale Y0.80644935/Z0.59127444. Le veto retient pourtant l'autre tuile 39v0. Les sorties avec/sans autostep sont identiques : aucune marche à attribuer. Le filtre confond la projection réduite sur pente avec un blocage dû au bord voisin. Preuve wall-guard-diagnostic-001-proof.json et log conservés.

Source8 isolé conserve les 2234 fichiers du Source7 exécuté puis corrige uniquement l'attribution du veto dans tidewater_player.rs : le candidat doit aussi porter un contact non marchable opposé à la demande, observé pendant le vrai balayage prédit. Les casts frais, le contrôle autostep, la projection tangente et les critères de jeu sont conservés. Après formatage des deux fichiers Rust concernés, les 26 tests sont lancés, session 2992, patched-player-tests-001.log. Murs, coins, pente, marche, saut, eau et échelle restent couverts par les régressions existantes. Résultat en attente, aucune qualification publique ni build V13 encore. Source7/V12/Primary préservés, Gate 3 ouverte et aucune publication.


### Source8 : 26 tests natifs et Clippy strict passent ; frontend V13 lancé

Les 26 tests passent sans échec ni test ignoré, sources stables. Le test du filtre sur la jonction réelle conserve exactement la demande [0,-0.002725,-0.05] ; les cas de murs, coins, pente, autostep, saut, nage et échelle restent verts. Exécutable natif archivé SHA e9a1002a1c0bd13488b6ea6a69b051b272484c585f1a37b5ae3a1859f1186f71, log et source exécutée conservés.

Formatage code 0 ; premier Clippy strict code 101 pour deux usages chunks_exact(4) dans les lecteurs de fixtures de test. Correction de style uniquement : as_chunks::<4>() parcourt les mêmes groupes de quatre octets, sans reste, vérifiés sur les deux fixtures ; moteur et fixtures restent identiques aux tests physiques exécutés. Les tests physiques ne sont pas rejoués après cette correction de syntaxe. Deuxième formatage et Clippy --lib --tests -D warnings passent, source stable ; preuves strict-native-002/proof.json et test-style-correction-proof.json. Aucun PASS workspace complet ni WASM strict déduit.

Snapshot privé propre add12b3503ce8ebb4738c6756e843b89ce7e21d7, 2234 fichiers qualifiés et pins archivés. Commit local exclusivement dans Source8 ; aucun commit Primary ni push. Frontend tidewater-terrain-seam-correction-013-frontend-001 lancé, session 73196, frontend-build-001.log. Imports repris de l'archive frontend V12 pour conserver les mêmes assets et comparer la correction physique. Candidat natif V13 encore non construit.

Revue de concordance source/frontend/candidat et lanceurs publics V13 préparés, non exécutés. Checkpoint de départ = V12 authoring passé ; P01 conserve le même ordre après quête, le pilote de recentrage stabilisé du harnais005, la tolérance 5 cm et les assertions de franchissement. P02 dépend de P01 passé sur ce candidat. Gate 3 ouverte ; qualification complète, runtime public et P05 restent à faire, Primary préservé et aucune publication.


### Frontend V13 construit ; candidat natif en construction et autres contrôles préparés

Le frontend V13 termine code 0, source stable. WASM 768861d47867e8ece8e15a0a5c8a0c194df2a31830b1f7af843ae350c3a7a2fd. Worker e5cbbce76142441b63d99ac12cdef14d978a697a2b71669110320a17d0a135fb : octets identiques au worker V12 ; manifest 078daaac5b126193e0ae043d95361358681230a7e5800ba6e461b2cb03cc130f. Cette identité de worker ne remplace pas les contrôles publics du nouveau candidat. Avertissement de compatibilité future proc-macro-error2 préexistant conservé dans les logs ; aucun échec du build.

Construction native tidewater-terrain-seam-correction-013-candidate-001 lancée, session 65926, native-build-001.log. Revue Root préparée exige snapshot propre add12b35, concordance complète des pins source/frontend, 218 fichiers Rapier et mêmes preuves des 26 tests natifs/Clippy. Aucun runtime ni critère supplémentaire encore validé par le build.

Lanceur launch-public-check.ps1 préparé pour EnvironmentQuest, EnvironmentFireQuest, ForestDanger, ForestThrow, ShorePush, ShoreWater, Navigation, P03 et P04. AST PowerShell PASS uniquement ; aucune exécution publique. Il exige revue Root du binaire et checkpoint Studio passé sur le même candidat, vérifie navigateur et hash source, puis appelle les harnais005 existants sans modifier critères, timeouts ou recettes. Preuve public-check-preparation-proof.json ; manifeste/lock et les 218 fichiers Rapier sont byte-identiques au Source6. Contrôles machine complets Source8 et lecteur P01 V13 préparés, non exécutés. Gate 3 ouverte ; Primary et preuves précédentes préservés, aucune publication.


### V13 construit et vérifié ; checkpoint Studio public lancé

Le build natif termine code 0 en 4m17s. Revue Root exécutée : 1335 fichiers web empaquetés, 1105 fichiers source, dont les 218 fichiers du paquet Rapier ; snapshot add12b35 propre avant/après et concordance frontend/native vérifiée. Candidat SHA 34791bb522b25635acee535d674e52675ea73e91a2cd6799a8ea235078ecc3cd. Preuve terrain-seam-candidate-root-review-001/proof.json, BUILD_ONLY_ROOT_BYTE_VERIFIED. Aucun verdict runtime tiré du build.

Cinq profils WebView temporaires supprimés des campagnes V11/V12 effectivement terminées, source_preserved et owned_closed vrais, aucun arrêt forcé dans leurs ledgers. Les chemins résolus restent sous les parents explicitement sélectionnés, processus recontrôlés avant chaque suppression. Les 397 fichiers hors profils sont byte-préservés ; aucune source, sauvegarde, capture, exécutable, cache Cargo ou preuve supprimée. Preuve obsolete-webview-profiles-cleanup-005/proof.json ; espace libre final environ 2,87 Go.

Checkpoint Studio public V13 lancé dans tidewater-gameplay-revalidation-seam-v13-001, session 27848, public-revalidation-001.log. Il part du checkpoint V12 authoré et passé ; aucune qualification de celui-ci n'est héritée pour le V13. P01/P02 et autres contrôles publics attendent ce checkpoint. Contrôles machine complets et P05 en attente ; Gate 3 ouverte, Primary préservé et aucune publication.


### Checkpoint Studio V13 passé et revu ; P01 après quête lancé

Le checkpoint V13 termine passed. Revue Root des octets exécutée : 22 lectures publiques d'entités, public_save_ack_observed et complete_head_identical vrais, archive authorée 19b4af30 et head 855eb32b conservés ; même binaire 34791bb5. Objet sauvegardé et parent ledger hashés, six copies de scripts chargés et transcript vérifiés. Source préservée, processus possédé fermé normalement sans arrêt forcé. Preuve checkpoint-root-review-proof.json, ledger SHA 1fb827cbf0139b765c4b480c45415aedc616448eefed18d8f3bcd9c18cecbfcc. Aucune qualification runtime antérieure héritée.

P01 V13 est lancé dans gate3-v13-terrain-seam-correction-p01-001, session 3218, public-p01-001.log. Même visite de l'intérieur après quête avant sauvegarde, touches publiques, approche physique, impulsions de contact du gardien et recentrage stabilisé avec tolérance 5 cm. Le passage forêt, la sortie de l'intérieur, les reçus sauvegardés et la reprise dans un nouveau processus restent en attente. P02 attend P01 passé ; contrôles publics complémentaires, contrôles machine complets et P05 encore à exécuter. Gate 3 ouverte, Primary et sources préservés, aucune publication.


### P01 V13 tentative001 échoue au contact gardien ; arrivée stabilisée préparée et tentative002 lancée

La tentative001 termine failed_closed avant l'intérieur. Quête acceptée et trajet forêt/points d'approche effectués, mais première attaque publique refusée exactement « no physical contact confirmed ». Les 68 impulsions suivantes n'obtiennent pas le contact frais exigé en quinze secondes. Source préservée, processus possédés fermés normalement, aucun arrêt forcé. Preuve Root terrain-seam-public-p01-failure-review-001/proof.json ; aucun P01/sauvegarde-reprise/P02 PASS, ni verdict sur le recentrage intérieur non exercé.

Position finale source [16.2526855,58.1580544,-270.6100159], gardien authoré X16/Z-270. Le contact effectivement enregistré tick5206 porte « Gameplay object · Support piège », normales presque -Z, position [16,59.546619,-270]. Le dernier feedback KCC tick5842 est un sweep au sol sans déplacement horizontal ; le texte HUD de dernier mouvement reste au tick5206 et ne prouve pas un contact frais du gardien. La dernière étape hors piège atteint la tolérance 0.35 m encore en mouvement, X16.30/Z-271.17, puis arrive à 25 cm de décalage latéral. Les touches choisies à partir du HUD arrondi restent S ; le même calcul avec l'ancre exacte choisit S+A. Identité du support démontrée par son nom dans le témoin réel ; cause globale et correction publique encore non prouvées. Aucun changement physique du candidat effectué pour contourner ce support.

Harnais006 isolé : 415 fichiers hors Main byte-préservés ; nouveaux line endings Main normalisés, harnais005 exécuté conservé intact. Seule la dernière étape hors piège, au même point existant X16/Z-271.3, utilise le pilote public d'impulsions avec arrivée stabilisée à 5 cm au lieu de 35 cm. Même budget trente secondes pour cette étape et quinze secondes pour les contacts ; attaques confirmées, reçus et ordre après quête inchangés. AST et SelfTest PASS uniquement ; pas de qualification runtime par ces tests.

Tentative002 lancée dans gate3-v13-terrain-seam-correction-p01-002, session 95202, public-p01-002.log. Même candidat 34791bb5 et même checkpoint V13 passé ; aucune modification des sources ou de la map. P02 dépendra de cette nouvelle tentative passée. Gate 3 ouverte ; contrôles machine complets, autres replays et P05 en attente, Primary préservé et aucune publication.


### Tentative002 : compteur de stabilité bloqué par les ticks DOM répétés ; harnais007 et tentative003 lancés

P01 V13 tentative002 termine failed_closed pendant guardian_outside_staging, avant l'attaque. La borne 5 cm à l'arrêt n'est pas atteinte en trente secondes. Source préservée, processus possédés fermés normalement sans arrêt forcé. Position finale [15.010210,58.402161,-271.220551], vitesse source horizontale presque nulle ; aucune conclusion sur le gardien ou la sortie intérieure non exercés.

Trace examinée : 218 observations de staging, 102 répétitions du même tick DOM et zéro impulsion envoyée. Le pilote remettait son compteur à zéro sur chaque répétition, empêchant les trois observations fraîches stables d'être accumulées. Le replay de la trace, avec exactement les seuils existants et en ignorant les répétitions, atteint trois observations fraîches stables à l'index7/tick4911. Ce replay indique seulement qu'une première impulsion aurait été éligible ; il ne prédit pas la trajectoire future ni un PASS runtime.

Harnais007 isolé : 415 fichiers hors Main byte-préservés. Un tick répété est journalisé, n'avance pas le compteur et ne l'efface plus ; une régression de tick est rejetée. Aucun seuil de position/vitesse, coordonnée, budget ou assertion de contact modifié. AST et SelfTest existant PASS ; harnais005/006 exécutés conservés. Même candidat V13, aucune modification produit ou map.

Tentative003 lancée dans gate3-v13-terrain-seam-correction-p01-003, session 51265, public-p01-003.log. Les contrôles effectifs de contact, cinq attaques, quête, visite après quête, sauvegarde et reprise restent requis. Gate 3 ouverte ; P02, autres replays, contrôles machine complets et P05 en attente. Primary et preuves préservés, aucune publication.


### V13 P01 complet PASS ; P02 retouches conservées, traversée intérieure échouée

P01 tentative003 termine PASS local et fait l'objet de gate3-v13-p01-root-review-001/proof.json : cinq reçus distincts de dégâts avec contact physique confirmé, 20 dégâts chacun, quête terminée persistée, visite et sortie intérieure après quête, sauvegarde et reprise dans un nouveau processus. Les huit entrées source vérifiées sont inchangées ; fermeture normale sans arrêt forcé. Ce résultat qualifie P01 sur V13, pas la Gate entière ni P05.

P02 tentative001 sur les mêmes octets V13 termine failed_closed dans cross_interior_kcc. La réplique et la porte déplacée de 5 cm sont sauvegardées, les identités/liens et le marqueur intérieur conservés. La reprise compatible démarre et la quête est conservée ; la traversée puis le second processus restent incomplets. Sources d'entrée byte-préservées, processus possédés fermés normalement, sans arrêt forcé.

Au seuil, l'ancre réelle est [46.559715,6.512135,-148.276443], contre X porte retouchée 46.049999 : décalage latéral 0.509716 m. Le feedback KCC réel pendant l'arrêt au seuil contient EntranceRight. Le pilote conserve une approche avec tolérance historique large puis vise directement le marqueur intérieur ; un centrage public à l'arrêt en amont doit être qualifié avant un nouveau replay. Aucun défaut produit supplémentaire n'est établi par cet échec. Preuve bornée : current-campaign-progress-060/p02-failure-review.json.

Impact build V12/V13 archivé dans terrain-seam-wall-guard-correction-001/build-impact-proof.json : frontend +589 octets, exécutable natif +12800 octets, worker identique. Les temps proviennent de chemins/cache et charges distincts ; ils ne constituent pas une comparaison contrôlée de performance runtime.

Gate 3 ouverte : P02, autres replays sur candidat courant, contrôles machine complets, adoption finale des corrections isolées et P05 restent requis. Aucun push ou verdict humain implicite.


### P02 : centrage amont public, erreur d'encodage préflight conservée, replay003 lancé

Le calcul de touches existant rejoué sur le seuil réel donne uniquement S : deltaX -0.559715, deltaZ +3.276443, yaw 0. Le pilote ne corrige donc pas l'offset avant d'engager le montant. Harnais009 conserve 415 autres fichiers byte-identiques et remplace uniquement l'approche finale P02 par deux centrages publics à l'arrêt : couloir sud existant puis seuil 1.5 m devant la porte, précision 5 cm. Les budgets existants par segment et assertions de reprise/franchissement restent inchangés ; une étape de pilotage supplémentaire est explicitement ajoutée, sans modification du candidat ou de la map. AST et SelfTest PASS. Le diff sémantique et le replay pur de touches sont archivés ; ils ne valent pas preuve runtime.

Harnais008/tentative002 a été refusé avant lancement de processus car la copie avait décodé les accents avec le codec Windows par défaut. Cette erreur de préparation reste archivée avec son log ; une nouvelle copie lit explicitement UTF-8. Aucun résultat gameplay n'est attribué à cette tentative.

P02 tentative003 termine failed_closed au démarrage : cible CDP authoring absente au bout du budget de démarrage, avant tout parcours. Le processus possédé pid6864 a nécessité un arrêt forcé, confirmé fermé dans le receipt de nettoyage ; les sources sont préservées. Ce résultat ne teste pas le centrage ni le franchissement. Session42560 terminale ; log p02-entry-centering-preparation-002/public-p02-003.log. Diagnostic du démarrage requis avant un nouveau lancement. Gate 3 ouverte. Aucun verdict P05 ou publication.

Le préfixe documentaire antérieur à progress060 est également restauré byte-sémantiquement depuis la sauvegarde UTF-8 : le script060 avait utilisé le codec Windows pour la lecture du document. L'ajout060 est conservé ; cette réparation de texte ne modifie aucune preuve runtime.


### P02 démarrage : dumps WebView archivés, profils inactifs libérés, logs instrumentés

Deux dumps Crashpad du profil003, horodatés pendant le démarrage échoué, sont copiés et hash-vérifiés dans p02-startup-crash-review-001. Lecture bornée de leurs streams d'exception : code 0x0517a7ed dans msedge_elf.dll, code 0x80000003 dans msedge.dll, version chemin154.0.4258.53. Sans symboles ni pile qualifiée, aucune cause racine n'est déduite. Le processus pid6864 est absent de l'inventaire CIM courant ; environ 31 Go de mémoire physique libre au diagnostic.

Cleanup006 exécuté : six profils WebView inactifs de cinq campagnes V13 terminales, toutes sources préservées et fermeture normale selon leurs ledgers. Contrôle CIM complet et chemins résolus avant chaque suppression ; 395 fichiers hors profils byte-préservés. Libre E après opération : 2093346816 octets. Le profil003 et ses dumps sont exclus de ce nettoyage.

Harnais010 ajoute uniquement --enable-logging=stderr et --v=1 aux arguments WebView de démarrage ; 415 fichiers hors Main byte-identiques à009, mêmes candidat, route, seuils et budgets. AST et SelfTest PASS. Tentative004 lancée, session49395, gate3-v13-terrain-seam-correction-p02-004, log p02-startup-logging-preparation-001/public-p02-004.log. Résultat runtime en attente ; aucune déduction de performance. Gate 3 reste ouverte, aucun P05 ou push implicite.


### Tentative004 : crash de démarrage reproduit ; qualification native complète lancée

Session49395 terminale exit1, failed_closed avant la cible CDP. Deux nouveaux dumps sont archivés et hash-vérifiés dans p02-startup-crash-review-002 : mêmes codes, modules et offsets que tentative003. Les logs stdout/stderr de lancement restent vides malgré les flags diagnostics. Le reçu confirme fermeture du processus possédé après arrêt forcé ; source d'entrée préservée. Le centrage P02 n'est toujours pas exercé. Aucun lien causal avec la mémoire, l'espace disque, les changements produit ou une map n'est établi. Aucun nouveau replay identique lancé.

Qualification machine Source8 native lancée, session89464, source8-machine-native-001. Source8 clean add12b3503ce8ebb4738c6756e843b89ce7e21d7, environnement enfant isolé, locked/offline/j1, hashes avant/après requis. Le contrôle inclut fmt workspace puis la commande Clippy workspace/all-targets/all-features du protocole strict antérieur ; son résultat reste en attente. Il ne remplace pas les tests workspace, WASM, acquisitions ou replays publics restants. Gate 3 ouverte, P01 PASS conservé, P02 et P05 incomplets, aucune publication.


### Source8 : natif/WASM stricts et dépendances PASS ; tests workspace lancés

Session89464 terminale exit0 : fmt workspace et Clippy workspace/all-targets/all-features, locked/offline/j1, warnings refusés, PASS. Session54193 terminale exit0 : Clippy release WASM sur huit crates, mêmes contrats web et avertissements refusés, PASS. Revue Root des logs JSON, fin de build réussie, hashes et égalité des 2234 fichiers épinglés : source8-machine-root-review-001/proof.json. Ces contrôles complètent les 26 tests physiques ciblés ; ils ne prouvent pas l'exécution des tests workspace.

Supply010 exécuté, session8928 terminale exit0 : cargo audit et cargo deny PASS sur Source8. Base RustSec officielle fraîche ef6173cbc5c50ec8166f9a5b28f07834144373ee, tree233e2e0a668c8960b0921682ed003b7f2e568b80, deux checkouts propres. Rapport audit count0 ; règles, targets et exceptions deny d'origine préservés, aucun ignore ajouté. Outils/inputs/config stables, TLS vérifié et environnement restauré. Revue Root de la politique TOML, rapports, hashes sources complets et commits : source8-supply-root-review-001/proof.json. Cette réussite ne vaut pas adoption propriétaire du fork ni validation gameplay.

Tests workspace Source8 lancés, session26302, source8-machine-workspace-001 : cargo test --workspace --no-fail-fast --locked --offline -j1. Résultat en attente ; les quatre tests d'acquisitions ignorés exigent ensuite un replay explicite sur les exécutables réellement produits par ce run.

Précision du diagnostic WebView : selon le header Breakpad upstream, 0x0517a7ed est WIN_SIMULATED, utilisé par DumpWithoutCrash, et 0x80000003 WIN_BREAKPOINT. Les deux rapports par tentative ne sont donc pas assimilés à deux crashes indépendants. Source : https://chromium.googlesource.com/breakpad/breakpad/src/+/refs/heads/master/google_breakpad/common/minidump_exception_win32.h (blob4b5d57c857ecbe7036340f14667f1a42761e7d69). Une analyse des annotations est confiée à Luna max, lecture seule ; aucune cause racine encore attribuée.

Portée P01 : le PASS V13 consigné concerne le smoke classique complet (combat, quête, intérieur après quête, sauvegarde/reprise). Le critère R3-P01 exige aussi les deux approches de quête ; les replays environnementaux courants, la consolidation de tous les critères et P05 restent requis. Aucun checkbox de clôture n'est déduit du smoke classique seul. Gate 3 ouverte ; Primary et preuves préservés, aucune publication.


### Diagnostic P02 : annotations sandbox/GPU vérifiées par Root ; prochain lancement préparé

Revue Luna max terminée en lecture seule ; Root a ensuite lu indépendamment les structures Crashpad 0x43500001 des quatre dumps archivés, avec contrôle des bornes, RVAs, types et SHA. Projection bornée dans p02-crashpad-annotations-root-review-001/proof.json. Les dumps simulés portent SBOX_ERROR_CANNOT_CREATE_RESTRICTED_TOKEN, LastError87, SandboxTypekService, edge_sandbox_win.cc ligne709. Les dumps breakpoint portent le fatal GPU process isn't usable, gpu_data_manager_impl_private.cc ligne436, last_gpu_crash_exit_code STATUS_ACCESS_DENIED. Watson metadata confirme un seul PID WebView par paire ; les métadonnées originales sont copiées/hash-vérifiées. Cette preuve localise le refus de lancement enfant ; le contexte d'exécution ou le verrou cache ne sont pas encore établis comme cause racine.

Référence de format : https://chromium.googlesource.com/crashpad/crashpad/+/refs/heads/main/minidump/minidump_extensions.h , blob55af5fa25b68e47ccabf0aa3fdc3162e8b5e7228. Le champ layout_source du premier reader incorpore ce blob comme ref ; utiliser le lien de format présent ici, avec le blob explicitement consigné. Aucun contenu de métriques utilisateur nécessaire n'est promu dans la projection.

Prochaine expérience préparée, pas exécutée : lancement P02 via l'outil require_escalated soumis à auto-review pour vérifier l'hypothèse du contexte Windows restreint. Même candidat34791, même WebView154.0.4258.53 épinglé, même harnais010/centrage/critères ; nouveau profil005 isolé. Pas de flag --no-sandbox ni de désactivation GPU, pas de cache tiers touché. AST du lanceur PASS. Le lanceur refuse tant que les tests workspace actuels n'ont pas terminé PASS. Preuve : p02-execution-context-preparation-001/stage-proof.json. Les refus éventuels de l'outil seront consignés ; aucune approbation ou réussite anticipée.

Les quatre acquisitions ignorées sont préparées dans source8-acquired-input-preparation-001 : mêmes quatre cas exacts et assertions, lecteur adapté au schéma/log Source8, exécutables uniques du run workspace courant exigés. Pas encore exécutées. Session26302 reste en cours, compilation forge_engine observée ; aucun restart. Gate 3 ouverte ; autres replays courants et P05 restent incomplets.


### Workspace001 terminal LNK1180 ; stockage récupéré et retry002 lancé

Session26302 terminale exit101, avant qualification complète : linker gameplay_physics signale explicitement LNK1180, espace disque insuffisant. Source8 add12 propre, 2234 fichiers inchangés avant/après. Ce résultat n'est pas un PASS de tests et ne permet pas le lancement des acquisitions ni P02. Les contrôles stricts natif/WASM et supply déjà PASS restent distincts.

Cleanup007 exécuté : deux profils WebView de démarrage échoués terminés, après copie/hash-vérification intégrale Crashpad. 142 fichiers hors profils byte-préservés ; aucun cache Cargo/source/save supprimé. Le premier préflight refuse le PID6864 réutilisé ; l'inventaire prouve conhost.exe, puis le guard vérifie l'image de ce PID et refuse tout candidat actuel actif. Aucun processus arrêté par ce nettoyage. Les dumps, métadonnées et ledgers sont conservés.

Relocalisation contrôlée exécutée sous auto-review : 116 anciens .exe/.pdb du seul cache Cargo Gate3, horodatés avant le run workspace001, copiés sur F:/OpenRPG_Gate3Cache_20261003/older-test-binaries-001 et hash-vérifiés avant retrait des copies E:. 1471117312 octets préservés ; bibliothèques et sorties workspace actuelles conservées, cible inactive vérifiée par CIM. Manifest source/destination/hash dans cargo-old-binaries-relocation-001/copy-verified-manifest.json ; preuves historiques de cache consultables via cette archive. Les sources et candidats publics ne sont pas déplacés.

Retry workspace002 lancé, session95558 : mêmes source, commandes locked/offline/j1 et cible Cargo, seuls dossier de preuve et TEMP/TMP enfant changés, temporaires F:/OpenRPG_Gate3Cache_20261003/source8-workspace-002-temporary. Source8-machine-workspace-001 reste intact. Contrôles futurs P02 et acquisitions adaptés à la preuve PASS de retry002 dans p02-execution-context-preparation-002 et source8-acquired-input-preparation-002 ; AST PASS, pas encore exécutés.

Inspection statique Root de deux captures P01 V13 : forêt/terrain/végétation lisibles dans run1-guardian_defeated.png ; intérieur largement noir autour d'une ouverture montrant l'extérieur dans run1-interior_inside.png, malgré HUD/quête completed visibles. Proof p05-v13-capture-inspection-001 consigne cette limite de lisibilité. Le premier assert du reader confondait SHA de capture PNG avec le SHA du witness UI JSON de Root P01 ; corrigé pour vérifier le witness JSON réel. Les PNG ont leurs hashes propres, aucune preuve runtime altérée. Cette inspection ne vaut ni partie complète observée ni P05 humain. La Gate 3 reste ouverte ; aucune publication.


### Workspace002 terminal LNK1318 ; archive WASM et retry003

Session95558 terminale exit101 : LNK1318, erreur PDB LIMIT (12), sur debug/examples/tidewater_plant_probe.pdb. Les 2234 fichiers Source8 add12 restent inchangés. Le PDB constaté mesure 63442944 octets ; aucune limite de 4 Go ni cause précise n'est démontrée. L'espace E: est très faible ; le message Microsoft décrit un échec d'ouverture/lecture/écriture de PDB, sans identifier à lui seul la cause. Source8-machine-workspace-002/proof.json et le log restent intacts. Aucun PASS workspace ni acquisition n'en est déduit.

Archive inactive WASM terminée : 1542 .rlib/.rmeta, 4270386333.0 octets copiés sur F:/OpenRPG_Gate3Cache_20261003/wasm-libraries-001, SHA vérifiés avant retrait des seuls originaux générés. CIM vérifie la cible Root inactive ; aucune bibliothèque native, source, preuve, candidat ni fichier .wasm supprimé. Manifest dans cargo-wasm-libraries-relocation-001. Le PDB de l'échec002 est séparément archivé byte-identiquement avant retrait de son seul exemplaire de cache ; workspace002-failed-pdb-preservation-001 conserve le chemin, hash et date réels. Aucun service PDB partagé ou processus étranger arrêté.

Retry003 lancé via source8-workspace-storage-retry-preparation-002/run-workspace.py : mêmes Source8, cible Cargo, commande complète locked/offline/j1 et RUSTFLAGS ; dossier de preuve frais, TEMP/TMP enfant F:/OpenRPG_Gate3Cache_20261003/source8-workspace-003-temporary. Les gardes P02 et acquisitions préparées003 exigent le PASS réel de ce run, sans changer assertions ni budgets. Les échecs001/002 restent visibles. Pas de nouveau replay P02 exécuté ; la lisibilité intérieure P05 reste ouverte, comme le reste de la matrice et la Gate 3. Aucune publication.


### Workspace003 terminal E0786/1455 ; diagnostic ressources

Session63514 terminale exit1 du runner, commande Cargo exit101. Le compilateur signale E0786 : mmap de libforge_engine-de612f9469b20640.rlib refusé par Windows, fichier de pagination insuffisant (os error 1455), avant exécution complète des tests. Les 2234 fichiers Source8 restent identiques. Ce message ne démontre pas une corruption de bibliothèque et aucun source/paramètre de test n'est modifié pour le contourner.

Diagnostic en lecture seule archivé dans workspace003-resource-diagnosis-001/proof.json : E: presque plein, pagination configurée sur E:, marge virtuelle faible malgré mémoire physique disponible. Deux renderers WebView appartiennent aux profils F: studio-live-environment-20261003-v1 et studio-shared-lighting-20261003-v1 ; leur mémoire privée observée représente environ 30 Go. Ils ne sont pas les processus des replays Gate3 terminés. Aucune causalité précise de croissance du pagefile n'est revendiquée ; aucun réglage système ni processus étranger modifié. Demande propriétaire envoyée pour faire sauvegarder/fermer ces instances dans leurs tâches respectives ; réponse requise avant toute fermeture par Root.

Archive complémentaire du cache Root lancée, session30141 : seules anciennes .rmeta >=4MiB avec .rlib correspondante conservée, cutoff antérieur à workspace001, copie/hash avant retrait et CIM cible inactive. Préparation/inventaire : 275 fichiers, 3205062061 octets ; ce nombre n'est pas encore promu en résultat d'exécution. Proof final attendu sous cargo-paired-native-metadata-relocation-001. Les bibliothèques de lien restent présentes. Aucun nouveau test/replay relancé tant que les ressources ne sont pas rétablies ; workspace003 et tous les échecs restent intacts. Les gardes P02/acquisitions003 refusent ce run échoué. Native/WASM strict et supply restent PASS dans leur seule portée ; Gate3, P05 et publication restent non acquis.


### Archivage metadata terminé ; audit de clôture et attente mémoire

Session30141 terminale exit0. 275 anciennes .rmeta, 3205062061 octets, copiées sur F:/OpenRPG_Gate3Cache_20261003/paired-native-metadata-001 et vérifiées avant retrait des copies E:. Manifest/hash archivés, rlib correspondantes toujours présentes. Cache mutable uniquement ; tous les candidats, preuves et sources conservés. Après archivage E: dispose de 3234443264 octets.

Lecture système du 2026-10-03T22:57:35.9349795Z : FreeVirtualMemory=702720 KiB, pagefile E: alloué 6203 MiB, mémoire physique libre 33449204 KiB. Les deux renderers des autres Studios sont toujours ouverts, mémoire privée respectivement 20857249792 et 9040084992 octets. Un shell nouveau avait échoué à créer CoreCLR (HRESULT 0x8007000E) pendant l'archive ; aucun verdict de compilation supplémentaire déduit. L'espace récupéré ne prouve pas le rétablissement de la marge virtuelle. Pas de nouvelle compilation/replay lancé sous cette pression ; aucun processus étranger arrêté, aucune autorisation de fermeture reçue.

Audit exécuté source8-completion-audit-001/audit.py : 21 exigences R3-A/R3-B/R3-P explicitement présentes dans la roadmap, aucune clôture complète démontrée. Hashes des preuves strictes native/WASM, supply, classic P01, échec workspace003 et capture P05 consignés. Classic P01 ne couvre qu'une approche ; matrice publique V13 (EnvironmentQuest/EnvironmentFireQuest/ForestDanger/ForestThrow/ShorePush/ShoreWater/Navigation/P03/P04) encore sans preuve exécutée. P02 a échoué puis les essais de centrage ont échoué au démarrage ; P05 n'a pas de revue humaine de partie complète et conserve la limite de lisibilité intérieure. ADR0021 reste proposé, qualification/intégration Primary à terminer. La lecture des helpers du launcher confirme que Navigation/P03/P04 conservent le WEBVIEW2_BROWSER_EXECUTABLE_FOLDER du parent ; leur absence de paramètres navigateur n'est pas à elle seule un défaut .37. Aucun harnais modifié sur cette hypothèse écartée. Gate3 reste ouverte, CI distante NOT RUN sous ADR0006, aucune publication.


### Ressources : audit d'impasse après récupération sûre

Le même blocage externe de ressources a persisté sur trois tours Goal consécutifs. Les actions indépendantes ont été achevées : archivages cache hash-vérifiés, préservation du PDB, préparation des replays et audit des 21 critères. Tous les handles possédés sont terminaux : workspace003 session63514 exit1 ; archive metadata session30141 exit0 ; aucun processus de compilation/candidat Gate3 de cette tâche actif au dernier CIM. Les autres Studios restent ouverts ; aucune réponse propriétaire à la demande de sauvegarde/fermeture n'a été reçue.

Dernier inventaire : FreeVirtualMemory=2245600 KiB, E: libre 1603416064 octets. Ces marges ne démontrent pas une reprise fiable du workspace complet ou des replays Studio, après les échecs 1455 et CoreCLR. Aucun nouveau retry aveugle, aucune réduction du périmètre des tests, aucun arrêt de processus étranger. La prochaine étape exécutable est la reprise de qualification complète après libération effective des ressources, avec nouveau dossier de preuves et garde des sources/candidats existants. L'objectif de clôture demeure inchangé ; la Gate3 n'est ni clôturée ni abandonnée. Statut Goal bloqué à demander dans l'application, distinct d'une pause volontaire. Aucune publication.


### Reprise du 4 octobre : ressources rétablies, workspace004 lancé

Observation réelle 2026-10-04T06:53:38.9754492Z : FreeVirtualMemory=22731868 KiB, E: libre 120635248640 octets, anciens renderers19668/30784 absents. Aucun processus étranger ni réglage système modifié par Root. L'objectif est de nouveau actif ; l'audit d'impasse précédent décrit l'état historique du 3 octobre, pas un abandon de Gate3.

Workspace004 lancé, session69530, via source8-workspace-storage-retry-preparation-003/run-workspace.py. Même Source8 add12 propre, mêmes commandes locked/offline/j1, flags et cible Cargo ; seul dossier de preuves et TEMP/TMP enfant frais changent. Les échecs001/002/003 restent intacts. Resource-recovery-001/proof.json archive un second relevé effectué pendant ce run. Compilation en cours, aucun PASS de tests déduit de son lancement. Les gardes des acquisitions et de P02 sont préparées004 avec les mêmes assertions, et le reader Root du workspace est préparé002 ; ces contrôles attendent un PASS réel de workspace004. Les sources Primary modifiées par les autres travaux demeurent intactes, en particulier éclairage/matériaux/terrain. Gate3 reste ouverte ; aucune publication.


### Diagnostic intérieur borné et qualification toujours en cours

Workspace004 session69530 confirmé actif par son handle ; compilation des dépendances en cours, aucun test PASS encore revendiqué. Le champ ancien owned_qualification_processes_active=false de l'impasse historique est remplacé par true dans le pointeur courant ; les dernières marges de ressources proviennent du relevé resource-recovery-001, distinct des métriques du 3 octobre.

Revue source P05 exécutée en lecture seule, p05-interior-lighting-source-review-001 : la projection de pièce utilise StandardMaterial brun/roughness0.9 ; l'éclairage de travail Source8 est déclaré actif par défaut, point light enfant de caméra100000 lm/range24 et ambient250, limité aux caméras actives ; caméra joueur initiale ambient95. Ces trois sources correspondent aux hashes source-before de workspace004. La capture intérieure est revue visuellement et son hash correspond à l'inspection statique précédente. Ces déclarations ne démontrent ni les composants effectifs, ni l'exposition, ni la cause du noir. Aucun correctif arbitraire de lumière ni changement du candidat entrepris. Après machine/P02, diagnostic public prévu du contrôle temporaire d'éclairage et de la caméra/rendu dans l'intérieur ; pas une revue humaine P05. Les changements d'éclairage/matériaux/terrain du propriétaire restent préservés. Aucun verdict visuel ou publication.


### Lecteurs des logs colorés préparés pour workspace004

Le nouveau log Cargo contient des séquences SGR de présentation. Source8-workspace-root-review-003 et source8-acquired-input-preparation-005 retirent uniquement ces séquences en mémoire pour leurs regex ; octets originaux et hashes des logs restent intacts, mêmes critères/commandes et même preuve workspace004 exigée. Les lecteurs précédents sont conservés. Génération et AST du lecteur PowerShell PASS ; le préparateur Python a émis un SyntaxWarning d'échappement dans sa chaîne de génération, sans erreur et sans modification du code produit. Aucun résultat de tests ni qualification acquis par ces préparations. WebView2 154.0.4258.53 est encore présent et son exe porte toujours SHA30285e24a33be3c0b2d0a7c3909fa1cc21d2fc0f0c2ab4eb79e46a9304b4256d. Workspace004 session69530 demeure en compilation ; prochain reader Root préparé003 et acquisitions préparées005 après son seul PASS réel. Gate3 reste ouverte.


### Entrées acquises revérifiées pendant workspace004

Verify-acquired-inputs-001.py exécuté sur une sortie fraîche acquired-input-preflight-source8-resume-001.json : quatre groupes, 74 payloads et leurs manifestes conformes aux hashes attendus. Océan, shore-sim, water-surface et sept shaders acquis requis sont toujours présents et intacts après les changements de ressources externes. Il s'agit d'un préflight des entrées, pas du passage des quatre tests ignorés ciblés ni d'une qualification runtime. Ces tests attendent le PASS du workspace004 et ses exécutables réels. Session69530 confirmée active ; compilation poursuivie jusqu'aux dépendances renderer naga/naga_oil/Bevy, sans verdict terminal de tests. Aucune mutation source ni publication.


### Workspace004 terminé en échec — diagnostic NTFS et fuite PNJ, 4 octobre 2026

La session69530 est terminale (exit1 du runner, exit101 de Cargo). Commande complète cargo test --workspace --no-fail-fast --locked --offline -j1 : 1 495 tests PASS, 19 FAIL et 10 ignorés sur 99 résumés de cibles ; 11 cibles en échec. Les 2 234 fichiers source sont inchangés. Dix cibles projet totalisent 17 erreurs « Fonction incorrecte. (os error 1) » ; deux tests navigation expirent. Le PASS workspace n'est pas acquis et les gardes d'acquisition/P02 restent fermées. Le nombre précédemment annoncé oralement de 18 erreurs filesystem était incorrect : le log en compte 17.

Diagnostic exécuté source8-failure-diagnosis-001 : le même exécutable forge_project, SHA 0f3f2853cdb2d80be416975333ffb43105d5b89ac57e0a385162ea748117acf0, rejoue exactement object_read_charges_actual_bytes_and_rejects_insufficient_budget. FAIL sous TEMP/TMP frais F: exFAT, PASS sous TEMP/TMP frais E: NTFS ; seule cette paire de variables enfant change. ZoneObjectStore::publish utilise fs::hard_link pour publier sans remplacement. Cette comparaison isole une incompatibilité du répertoire temporaire exFAT pour ce test ; elle ne transforme pas les 16 autres échecs en PASS sans replay. Le prochain workspace complet doit utiliser un TEMP/TMP NTFS.

Le même exécutable navigation fed2d09aee2ce7a511f6552653ef30930f445ea4153f34a4281f7152b1804c14, rejoué exactement sur NTFS, passe enclosed_heat_escape_caches_failed_queries_and_never_requests_horizontal_fallback mais échoue selected_escape_goal_survives_source_warmup_without_actuating_a_stale_route. L'acteur termine Disabled, sans tuiles, avec quatre requêtes de destination ; la cause navigation reste à établir. Copie navigation-warmup-diagnostic-001 préparée avec instrumentation du seul test, tous les octets de production Source8 préservés, après normalisation explicite des fins de ligne issues du checkout. Son build/replay diagnostique session46233 est actif ; il ne qualifie pas le candidat. Source8, les échecs et leurs logs sont conservés. Aucune publication Git, aucun verdict global Gate3.


### Causes des échecs établies et replay navigation PASS — Source9 test seul

Rejeu NTFS complet des dix anciennes cibles projet en échec : 111 tests PASS, zéro FAIL, incluant les 17 anciens échecs filesystem. Les exécutables originaux et les 2 234 entrées source sont inchangés avant/après ; leurs logs et hashes sont dans source8-project-ntfs-replay-001. Cela ne remplace pas une qualification workspace complète.

Navigation : l'instrumentation diagnostique localise l'échec après sélection effective de la destination, pendant l'attente du rebuild provoqué par NavigationPolicy::replace. Le helper ManualDuration avance 1/60 seconde simulée par app.update, tandis que les jobs asynchrones utilisent le temps réel. Trace réelle : 101 713 updates pendant le budget de 30 secondes, soit 1 695,217 secondes simulées ; tick final101816, PNJ et joueur à vitalité0, navigation Disabled. Le helper de test surconsommait les ticks de chaleur pendant la reconstruction. Cette trace ne démontre pas un défaut production d'actuation périmée. source8-failure-root-review-001 vérifie les preuves et leurs hashes.

Correction test-only : ajout de step_until_paced pour les six attentes des fixtures chaleur. Tous les systèmes production FixedUpdate restent actifs ; la simulation ne tourne plus plus vite que sa cadence de 60Hz. Aucun changement de vitalité, dommages, délais30s ou assertions ; les autres scénarios nav gardent leur helper initial. Replay réel de la cible complète avec concurrence par défaut : 18 PASS, zéro FAIL/ignoré/filtré, 1,71s (compilation8,10s). cargo fmt --all --check PASS. La correction a été intégrée à l'unique fichier test Primary après contrôle du hash db7ba624... ; aucune autre modification propriétaire écrasée. Source5–8 production ne sont toujours pas intégrées globalement à Primary.

Source9 figée localement : d69e17db7687f534761a5f2997326ecbdbd20607, racine physique navigation-warmup-diagnostic-001/source, manifest source-snapshot-009-test-timebase-001.json. Un seul fichier test diffère de Source8 ; les 2 233 autres fichiers vérifiés, incluant production et dépendances, sont byte-identiques. Le candidat public V13 reste donc inchangé ; aucune nouvelle build runtime ou nouvelle qualification publique n'est revendiquée. Le commit est dans la copie isolée, sans push ni commit Primary. La réinitialisation de l'index de cette copie a seulement renormalisé les entrées Git et le diff committé est limité au test ; les octets physiques source sont préservés.

Qualification complète relancée sur Source9, TEMP/TMP NTFS E:, mêmes paramètres Cargo/RUSTFLAGS/cache : source9-machine-workspace-001, session22492 active, sans verdict terminal. Le replay complet reste nécessaire malgré les succès ciblés. Contrôles stricts natifs Source9 ensuite ; preuves WASM/supply et candidat Source8 à relier par comparaison explicite des entrées inchangées. source9-downstream-preparation-001 prépare le lecteur Root, les quatre tests acquis exacts et la reprise publique P02 ; syntaxe des deux scripts PowerShell vérifiée, aucun de ces parcours exécuté. Les gardes P02 exigent workspace Source9 PASS revu Root, checks natifs PASS, acquis PASS et équivalence byte du produit Source8. P02, neuf autres parcours publics, diagnostic visuel intérieur/P05, décision propriétaire ADR0021 et clôture entière Gate3 restent non acquis.


### Comparaison d'entrées Source9 / produit V13 vérifiée — qualification complète encore active

source9-product-equivalence-root-review-002 exécuté PASS : 2 234 fichiers Source8 et Source9 revérifiés physiquement ; seule la fixture gameplay_navigation.rs diffère, les 2 233 autres entrées restent identiques. Les 1 335 fichiers web empaquetés, l'exécutable V13, les deux WASM et les reçus de build ont leurs hashes attendus. La première revue001 s'est arrêtée sur une assertion trop large « test absent du manifeste » : le manifeste de build couvre effectivement 1 105 fichiers, dont ce test. Elle reste conservée sans preuve PASS. La revue002 compare explicitement les 1 104 entrées du manifeste inchangées et classe l'unique différence par la topologie Cargo réelle (metadata --no-deps --locked --offline archivée) : gameplay_navigation est une cible séparée kind=test, et aucun target non-test ne prend ce fichier comme racine. Ce test d'intégration n'est pas compilé dans le produit empaqueté ni dans le check WASM --lib.

Les reçus/logs stricts WASM Source8 sont revérifiés avec leurs inputs ; le graphe et la politique supply sont inchangés, cargo audit compte zéro vulnérabilité et cargo deny ne contient aucune erreur, DB d'avis au même commit/arbre propre. Ces preuves sont réutilisées dans leur portée d'entrées identiques ; aucun nouveau check WASM, audit/deny ou build candidat Source9 n'est revendiqué. Les contrôles natifs de tests Source8 ne sont pas hérités pour la fixture modifiée : Source9 doit encore passer le workspace entier et Clippy natif strict.

source9-native-root-review-001/review.py et source9-public-qualification-guard-001/launch.ps1 préparés, non exécutés. Ce dernier exige les relectures Root workspace/natif PASS, l'équivalence produit ci-dessus et les quatre tests acquis exacts PASS avant P02 et les neuf autres parcours publics ; syntaxe PowerShell contrôlée. La session22492 est encore active, compilation forge_runtime observée ; processus rustc PID5504 confirmé en activité et ressources disponibles. Aucun redémarrage concurrent, aucun verdict de tests global ou Gate3, aucune publication.


### Workspace complet Source9 PASS et acquis exacts PASS — contrôles stricts natifs actifs

Session22492 terminée exit0. Commande inchangée cargo test --workspace --no-fail-fast --locked --offline -j1, Source9 d69e17db7687f534761a5f2997326ecbdbd20607, TEMP/TMP NTFS E:. Du 4 octobre08:45:22Z à09:04:39Z ; source2 234 fichiers stable, arbre propre. Raw log SHA cd47cb08ba913b5d66b322b7a8ae44a47a50c89142befae004e8e625e3ec0cd0. source9-workspace-root-review-001 exécuté PASS : 1 514 tests réussis, zéro échec, 10 ignorés, toutes les cibles workspace et doctests consignées ; aucun résultat filtré accepté dans le total. Les 19 anciens échecs restent archivés dans workspace004 ; le nouveau résultat ne les efface pas.

source9-acquired-input-tests-001 exécuté via les deux véritables exécutables identifiés dans ce workspace : quatre tests ignorés ciblés exécutés exactement une fois chacun, tous PASS (océan, shore-sim, water-surface, shaders source avec imports Bevy réels). Inputs avant/après : quatre groupes, 74 payloads, manifestes identiques ; exécutables inchangés et environnement restauré. source9-acquired-input-root-review-001 relit stdout/stderr et hashes, revérifie les 2 234 sources et le HEAD propre : PASS. Cette preuve ne qualifie pas les effets visuels/GPU ou le runtime public.

Opt-in real_studio_archive_can_continue_without_losing_its_history rejoué seul avec l'exécutable workspace réel, sur copie byte-identique de l'archive du checkpoint Studio public V13 19b4af3071b7c291986408c6422f140ef06be499a2617c72ff95073a687a641e. PASS : reprise/édition/réouverture en mémoire, 39 commandes conservées, archive initiale1 811 247 octets, delta234 279, après édition234 856 ; archive source et copie isolée inchangées avant/après. Aucun nouveau parcours UI ou changement du checkpoint n'est revendiqué. source9-studio-archive-replay-001 contient les logs/arguments/hashes.

Audit des 10 tests ignorés : cinq rejoués PASS, cinq non exécutés (deux tests de catalogue Vault réel externe et trois sondes CPU explicites sans seuil de qualification GPU/renderer). source9-ignored-test-scope-audit-001 archive nom, raison déclarée et portée de chacun. Aucun PASS fictif ni dérogation propriétaire n'est attribué aux cinq non exécutés. La qualification publique Gate3 et P05 restent séparées.

Contrôles stricts natifs Source9 lancés après les acquis, session64502 active : cargo fmt --all --check puis Clippy --workspace --all-targets --all-features --keep-going --locked --offline -j1 --message-format=json -- -D warnings. Aucun verdict terminal ; source9-native-root-review-001 attend le vrai résultat. Le guard public préparé impose maintenant aussi la relecture Root des quatre tests acquis, en complément des checks workspace/natif et de l'équivalence produit Source8. Aucun parcours P02/P03/P04 ou environnemental nouveau lancé ; Gate3 reste OUVERTE — NON QUALIFIÉE. Aucun push, aucun commit Primary.


### Source9 strict natif PASS et P02 public V13 PASS — 4 octobre 2026

Les contrôles natifs Source9 se sont terminés exit0 le 4 octobre à 09:28:23Z (départ 09:06:08Z) : fmt et Clippy workspace/all-targets/all-features avec -D warnings. La revue source9-native-root-review-001 vérifie 889 artefacts compilateur, aucun diagnostic, build terminé et les 2 234 sources stables au HEAD propre d69e17db7687f534761a5f2997326ecbdbd20607. Le log Clippy SHA est 9a753c68c877ab876864350074dbc99bf47b718f135e1e32d0aed6b68197521f. Le workspace complet reste 1 514 PASS, zéro échec, dix ignorés, dont cinq rejoués exactement PASS et cinq non exécutés explicitement classifiés. WASM/supply sont réutilisés exclusivement selon la revue d'équivalence des entrées Source8/Source9 ; aucun nouveau build WASM ou audit/deny Source9 n'est revendiqué.

P02-005 V13 termine exit0 : retouche Studio de la réplique et de la porte par Preview/Apply/Save, refus de reprise ordinaire sur changement de source puis reprise compatible explicite, Save et nouveau processus. La revue gate3-v13-p02-root-review-001 vérifie le ledger 8de253496310d1a156eb41610803c7d7db3a66727dbe5ba547b8adef0df07d5d, les dix instantanés source d'entrée et les objets conservés dans la copie isolée. La comparaison brute des deux têtes montre exactement trois différences : texte du dialogue, X de la porte (+0,049999 m), révision 40→42. Tous les liens/identités, la place intérieure et les autres champs restent identiques. Archive retouchée 618c19b4c85275c3ef25b32ffa128456a168b0b1f12336d07d07d6b039f6e733 ; ancien objet 19b4af30… inchangé. Quête Completed, réplique retouchée et absence d'action Sceau consommé observées dans le deuxième processus.

Franchissement public KCC acquis : seuil (46,07 ; -149,49), intérieur (46,07 ; -145,32), personnage au sol et contact Rapier avec le plancher de la pièce. Cette preuve reste bornée au passage et à l'arrivée dans la pièce. Elle ne qualifie pas toute la navigation intérieure ni P05. Les deux instances ferment normalement, sans arrêt forcé ; contrôle CIM archivé sans processus natif/WebView appartenant au dossier P02. Le lancement en contexte ordinaire autorisé a réussi ; les anciens crashs restent conservés, leur cause n'est pas déclarée résolue par cette seule différence de contexte.

La Gate 3 reste OUVERTE — NON QUALIFIÉE. Prochaine étape : rejouer les neuf contrôles publics environnement/navigation/P03/P04 sur le même V13, acquérir la seconde approche de quête, puis revoir P05. ADR0021 reste proposée ; les corrections production Source5–8 ne sont pas intégrées à Primary. Aucun commit Primary ni publication.


### EnvironmentQuest V13 PASS — deux approches R3-P01 acquises localement

EnvironmentQuest-001 termine exit0, session97533. La revue causale Root constate un lancer Player au tick5972, un impact réel au tick6003 (vitesse normale4,957 m/s, énergie368,584 J), avec 100 dégâts de contact au gardien. Les snapshots pré-combat démontrent vitalité positive avant et zéro après, sans reçu d'attaque préalable. Le joueur récupère le Sceau, termine la quête, sauvegarde puis reprend dans un nouveau processus. Cinq saves sont byte-vérifiés et acquittés/cohérents ; quête Completed, gardien vaincu, Sceau consommé et champs durables des objets demeurent après Resume. Chute, destruction du support et états lancés sont également observés et persistants. Aucun feu/eau/danger_ai n'est qualifié par ce scénario.

gate3-v13-public-environmentquest-001/root-causal-proof.json archive la relecture des reçus, provenance des six scripts chargés, transcript et huit fichiers source de l'entrée/copie isolée. Les deux instances ferment normalement, sans force ; le contrôle CIM ne trouve plus leurs processus. Les petits retries internes Save restent dans le transcript, seuls les cinq vrais fichiers commités acquittés constituent les preuves PASS.

gate3-v13-p01-two-approaches-root-review-001/proof.json rapproche ce scénario de P01 combat, même candidat34791bb5… et même checkpoint Studio19b4af30…/855eb32b…. Les liens source Village→Forêt→Intérieur→Village sont valides ; P01 prouve la visite publique de l'intérieur. R3-P01 (deux approches avec conséquence persistante) est PASS local sur ce candidat. R3-C est également acquis par le parcours environnemental complet, indépendamment d'une somme de fragments. P02 reste PASS local. Ces résultats ne qualifient pas globalement l'arbre Primary ni P05.

Observation Git courante : branche Rust, HEAD6b001dbed0fdf1c002d43cf507c60eece1119130, nombreux travaux existants sur assets/éclairage/import. Le champ ancien primary_head_unchanged du pointeur conserve son contexte historique ; il ne décrit pas ce HEAD actuel. Aucun commit/push/reset du parent dans cette reprise. Suite : EnvironmentFireQuest, autres contrôles publics, décision ADR0021/intégration bornée et P05. Gate3 reste OUVERTE — NON QUALIFIÉE.


### Parcours public EnvironmentFireQuest V13 en cours

Lancement public neuf autorisé par le guard Source9, même candidat V13 et runtime WebView2 épinglés. Session66840 confirmée active par son handle ; phase forest-route après démarrage, save initial et acceptation de quête. Dossier gate3-v13-public-environmentfirequest-001, copies isolées, scripts réellement chargés archivés. Aucun verdict PASS/échec terminal à cette observation. Le lecteur causal Root préparé devra vérifier les dégâts de chaleur avant combat, la quête et la persistance après reprise. Ne pas relancer ce scénario sans vérifier le handle et sa terminaison réelle. Goal actif, aucune clôture de gate ni publication.


### EnvironmentFireQuest V13 PASS et préparation bornée de l'intégration

Session66840 terminée exit0. EnvironmentFireQuest-001 a produit huit véritables saves cohérents/acquittés et byte-vérifiés. Le lecteur causal Root confirme l'ignition, les expositions en ligne de vue et les dégâts de chaleur sur joueur/gardien, gardien vivant avant puis neutralisé par environnement avant tout combat, Sceau récupéré/consommé et quête Completed après nouveau processus Resume. Le retrait commence à653,9995 ms sous le budget inchangé1 000 ms ; les six invariants de chronologie/capture/save/retrait passent. Fermeture normale des deux instances sans force, absence des processus propres vérifiée par CIM. Les sources d'entrée/copie demeurent byte-identiques. gate3-v13-public-environmentfirequest-001/root-causal-proof.json archive le verdict et les reçus.

Début de réaction danger observé : fleeing false→true, vitalité100→79, déplacement3D0,062254 m avec produit scalaire horizontal d'éloignement0,059754. C'est une preuve bornée de mouvement sous chaleur ; elle ne revendique pas une sortie complète de la zone dangereuse ni toute la navigation. ForestDanger et Navigation restent à rejouer séparément. Aucun impact/eau/push/destruction n'est hérité de ce parcours par le feu.

source9-primary-integration-preparation-001 prépare hors Primary le diff Source4→Source9 : 229 fichiers examinés, 223 nouveaux, quatre remplacements dont les bytes normalisés correspondent à la baseline, un test déjà identique en Primary et un ajout de bloc kcc_feedback dans authoring_imported_shadows.rs. Le fichier Studio est construit à partir des bytes Primary actuels ; retirer le seul bloc ajouté redonne exactement le fichier existant, préservant ses travaux d'éclairage. Aucun conflit non résolu dans cette préparation, aucun fichier Primary appliqué. Tous les hashes Primary devront être revérifiés au moment de l'intégration. ADR0021 reste proposée et nécessite acceptation propriétaire ; qualification après intégration reste nécessaire. Aucun push/commit Primary.

Gate3 reste OUVERTE — NON QUALIFIÉE. Suite : ForestDanger, ForestThrow, ShorePush/ShoreWater, Navigation, P03/P04, puis décision/intégration et P05.


### ForestDanger V13 PASS borné — six capacités R3-B relues

Session67267 terminée exit0. ForestDanger-001 : cinq saves cohérents/commités, chaleur/dégâts sur joueur et gardien, retrait880,1846 ms sous le budget inchangé1 000 ms, nouvelle instance Resume et fermeture normale sans force. Sources et scripts byte-vérifiés. Début de fuite du Veilleur : fleeing false→true, vitalité100→74, déplacement horizontal>0,05 m et éloignement positif du feu. Cette observation ne prouve pas une fuite complète ni la survie ; la navigation globale reste distincte. Le contrôle CIM de fermeture est archivé. root-causal-proof.json et le ledger a9ca2c5aa079efb014d97eb1c0bd37914b6b3e6d028ecd37e57db5de92fb35e2 restent les preuves autoritaires.

gate3-v13-world-as-weapon-root-review-001 vérifie explicitement six capacités R3-B au même candidat V13 : lancer, impact, chute/piège, destructible sur EnvironmentQuest avec états durables après reprise ; feu et début de réaction physique au danger sur ForestDanger. Aucun seuil n'est relâché. Push et eau/extinction restent ouverts. ForestThrow n'est pas rejoué car le parcours environnemental complet démontre déjà ses critères avec reçus causaux et persistance. ShorePush est lancé en copie neuve, session78287 confirmée active ; aucun résultat terminal revendiqué. Selon ses preuves, le test ShoreWater distinct pourra être couvert par la même observation causale complète ou devra être exécuté. Ce choix évite les répétitions sans retirer un critère de Gate3.

Navigation, P03, P04, intégration qualifiée/ADR0021 et P05 restent nécessaires. Gate3 OUVERTE — NON QUALIFIÉE ; aucune publication.


### Huit capacités environnementales et Navigation V13 relues PASS — P03 lancé

ShorePush-001 terminé exit0 : déplacement d'objet0,223613 m, six saves commités/acquittés/cohérents byte-vérifiés, feu de rive allumé puis éteint après quatre échantillons d'eau frais/valides aux étapes3693,3706,3768,3780, extinction conservée après nouvelle instance Resume. Sources/scripts conservés, fermeture normale sans force et CIM propre. gate3-v13-world-as-weapon-root-review-002 vérifie les huit critères R3-B avec les limites de début de fuite IA. Pas de test ForestThrow/ShoreWater supplémentaire : leurs critères causaux/persistants sont déjà intégralement observés sur EnvironmentQuest/ShorePush. Aucun critère de Gate3 retiré.

Navigation001 : échec préflight navigation_actors absent de la projection ledger. Harnais011 distinct : GUID/roles/patrols lus dans le document authoré byte-épinglé ; budgets/assertions inchangés, SelfTest PASS archivé. Navigation002/003 : pas de cible CDP aux délais boot60/120 secondes, aucun échantillon runtime. Sources conservées, arrêt forcé de démarrage selon cleanup malgré indicateur ledger incomplet : ces échecs ne sont pas présentés comme fermetures normales. Le lanceur ajouté avait passé le chemin browser C:/... non normalisé ; Navigation004 utilise GetFullPath, mêmes harnais/candidat/source/boot120 et fenêtre de navigation60. Succès après cette correction observé ; aucune preuve causale d'un défaut WebView2/sandbox n'est inventée. Les processus étrangers restent préservés.

Navigation004 termine exit0, session41545. gate3-v13-navigation-root-review-001 réexécute les lecteurs sur les véritables observations archivées et la sauvegarde. PASS : 33 fichiers de diagnostic SHA vérifiés ; patrouilleur déplace ses pieds de14,1958 m et change de cible avec retours KCC frais au même tick ; joint z=-134,4 traversé sur deux tuiles ready avec mouvement réel/sol/profil cohérents. Messager stationnaire NoPath derrière porte fermée puis sortie physique par la porte et Reached après ouverture ; reçus de tuile modifiés après Open puis Close, deux reçus distincts de porte et frame physique cohérente dans le save47240645fa958c6c33d6bebe896feed5b351133c5d887bc8616f75d6a587c5cf. Huit fichiers source/copie byte-vérifiés, helpers/harnais/samples contrôlés, fermeture normale sans force, aucune receipt de startup-failure et CIM propre. Portée : parcours PNJ/tuiles/porte authoré observé, pas toutes les routes possibles de la carte.

P03 lancé après fermeture Navigation004 via guard004, même candidat/checkpoint/browser Windows normalisé et copie isolée neuve. Session37117 confirmée active lors du lancement ; verdict courant consigné dans le pointeur (un éventuel terminal reste à relire par Root). P04, intégration/ADR0021 et P05 restent nécessaires. Actualisations085/086 ont refusé l'écriture lorsqu'un essai se terminait entre observation et staging ; elles n'ont modifié aucun document courant, scripts/sauvegardes conservés. Actualisation087 part des hashes084 vérifiés. Gate3 OUVERTE — NON QUALIFIÉE, aucune publication.


### R3-P03 V13 PASS — diagnostic public de référence manquante

Session37117 terminée exit0. Studio refuse la suppression du Sceau car la quête Rapporter le Sceau n'a plus de source pour la quantité requise, et demande de retoucher liens/conditions. L'objet concerné est sélectionné dans le contrôle public actif (GUID aac11dc8-b3a8-5905-8044-fcc1bd789c57), Apply désactivé, refus/objet/quête/quantité présents. gate3-v13-p03-root-review-001 vérifie le diagnostic DOM réel et son HTML, screenshot SHA, les GUID source pickup/quest, les douze instantanés source/copie et les états initiaux/finals strictement identiques : aucune nouvelle révision/receipt/commande, source et objet managed inchangés. Fermeture native confirmée sans force. Le lecteur archive le harnais réellement hashé. Cette preuve qualifie le diagnostic menant à l'objet à corriger ; aucune correction effectivement commitée ni acceptation humaine n'est revendiquée.

R3-P04 lancé via guard004 après terminaison P03, copie neuve et stockage de sauvegarde isolé, même V13/checkpoint Studio. Session29429 confirmée active lors du lancement. Le pointeur consigne soit attente publique, soit terminal à relire par Root si le harnais finit pendant cette actualisation ; aucun PASS Root P04 prématuré. P05, acceptation ADR0021/intégration et qualification Primary restent nécessaires. Gate3 OUVERTE — NON QUALIFIÉE ; aucune publication.


### R3-P04 V13 PASS — deux départs isolés, même révision publique

Session29429 terminée exit0. gate3-v13-p04-root-review-001 vérifie deux vrais nouveaux processus : choix public/reset puis Jouer à cet endroit depuis (40,-115), observations initiales identiques à la tolérance conservée, révision publique40 et même zone dans les deux runs. Le premier se déplace horizontalement d'environ1,40 m et sauvegarde publiquement ; le second repart de l'état choisi sans cliquer Resume. Tous les instantanés source/copie (seize entries) sont relus au hash actuel et avant/après identiques. Le store game-save source est absent et demeure absent ; la seule copie isolée contient save21eee549ff1e354257d8689cdd78833afe8585aa91a2993b26e381fcb1a71230 et son lock vide byte-vérifiés. Pas de nouvelle révision authorée. Deux fermetures normales sans force et CIM propre. Aucune simulation bit-identique/déterministe ni nouveau calcul de ContentHash interne n'est revendiqué.

Les parcours publics V13 P01 (deux approches), P02, P03, P04, la navigation PNJ bornée et huit capacités environnementales ont maintenant leurs revues Root ; les contrôles machine Source9 gardent leurs preuves/limites. R3-P05 demeure sans acceptation humaine. L'intérieur sombre constaté sur la partie complète requiert observation/diagnostic public de rendu ; déclarer une lumière dans le code ne prouve pas son effet. La prochaine étape est ce contrôle de lisibilité, puis la décision propriétaire ADR0021 et l'intégration bornée avec requalification du contenu réellement intégré. Aucun fichier production Source5–8 appliqué à Primary. Goal actif, aucun processus de qualification Root restant actif à cette observation. Gate3 OUVERTE — NON QUALIFIÉE, aucune publication.


### P05 — comparaison publique sombre, omission d'installation corrigée sur Source10

Le probe002 termine normalement (session75385 exit0), sur une nouvelle copie du save P02 retouché. La reprise ordinaire refuse le changement de source ; le bouton public de reprise avec retouches compatibles permet de continuer. Le joueur entre réellement dans la pièce et reste au même ancrage/cap, au sol avec contact Floor. Les captures case décochée puis cochée sont inspectées : murs presque noirs dans les deux cas, extérieur visible par la porte. Sur le rectangle de mur (300,350)-(700,700), la luminance encodée moyenne est 4,722 puis 4,738 sur255 ; ce sont des statistiques descriptives, pas un seuil d'acceptation ni une mesure photométrique. p05-lighting-root-review-001 vérifie les hashes actuels des huit fichiers source/copie, leur égalité avant/après, les pins des helpers, le script exécuté, les captures et les observations publiques. Save et sources préservés, fermeture normale sans force, vérification CIM des seuls processus natifs/WebView du probe vide.

Le code qualifié V13 comporte deux chemins de lancement du viewport. authoring_viewport.rs installe authoring_lighting, tandis qu'authoring_imported_terrain.rs crée son propre App sans installer ce système. Cette omission est établie dans le code ; la relation causale au rendu reste à confirmer par replay du candidat corrigé. Pas de diagnostic fondé sur un simple changement de luminosité arbitraire.

Source10 est un checkpoint détaché privé b9eb9aff0b649487a934f66ef1359f174dd1a7f3, dérivé de Source9 d69e17db. Une seule modification : trois lignes dans authoring_imported_terrain.rs pour installer le système d'inspection commun, sans branche par nom de carte. cargo fmt --all --check et git diff --check passent. Les builders appariés sont archivés dans p05-imported-work-light-correction-001. Compilation frontend V14 lancée, session84273 : verdict encore en attente à cette mise à jour. Le candidat courant qualifié reste V13 ; Source10/V14 n'hérite pas implicitement des PASS runtime ou machine Source9.

Primary et ses modifications propriétaire sont préservés ; sa préférence d'éclairage modifiée n'est pas écrasée par la copie isolée. Aucune correction production Source10 appliquée à Primary. Prochaine étape : terminer la compilation appariée, refaire la comparaison publique et inspecter les pixels, puis préparer la revue humaine P05 complète. ADR0021 et intégration/requalification restent ouverts. Gate3 OUVERTE — NON QUALIFIÉE ; aucune publication.


#### Source10 — périmètre physique du checkout et prochain replay préparé

Le contrôle initial « un seul fichier byte-différent » échoue : ce nouveau worktree Windows contient aussi des conversions de fin de ligne de checkout. Le diagnostic suivant compare les2233 fichiers suivis :1087 différences physiques, dont1086 exclusivement LF/CRLF et une seule différence après normalisation des fins de ligne, authoring_imported_terrain.rs. tracked-byte-diagnostic.json archive les hashes exacts et tracked-byte-review-note.md consigne la limite. Aucune source n'est réécrite pendant le build ; les inventaires avant/après du builder portent sur les bytes réellement compilés. Le delta Git de trois lignes ne signifie donc pas une identité physique de tous les autres fichiers avec Source9.

Le launcher natif apparié, le lecteur Root indépendant des bytes source/packagés et le probe003 sont préparés. Le probe003 ne change que le chemin candidat et le guard de hash/HEAD revu ; même fixture P02 clonée, reprise compatible, mouvement, case Off/On, captures, préservation et fermeture que probe002. Aucun replay V14 exécuté à ce stade. Après terminaison vérifiée de la session84273, lancer p05-imported-work-light-correction-001/launch-native.ps1, puis imported-work-light-candidate-root-review-001/review.py avant le probe003. Un build seul ne qualifie ni son rendu ni P05. Goal actif ; Gate3 non qualifiée, Primary préservé, aucun push.


### Audit de complétude du 4 octobre — preuves V13, clôture toujours non démontrée

gate3-v13-foundations-root-review-001 relit le ledger P01 et la sauvegarde réelle au hash49f444..., les actions publiques et les receipts cohérents. Joueur vitalité100/endurance75/capacité24 ; choix de dialogue de quête active, cinq dégâts de contact20 sur ennemi authoré vitalité100, pickup quantité1, dialogue de remise et quête completed, inventaire consommé et nouveau processus de reprise/revisite. La navigation possède son lecteur Root séparé. Huit fondations soutenues dans la portée locale V13 ; profil de règles minimal, pas une implémentation DND complète ni un recalcul indépendant des hashes fonctionnels internes.

gate3-completion-audit-20261004-001 reprend les21 cases exactes de la roadmap et R3-C :22 exigences au total. Les21 autres exigences ont des preuves locales V13 bornées, et P05 reste incomplet. Il vérifie les hashes des preuves liées, leurs checks causaux, la validation propriétaire ADR0018 et les dix maillons de provenance depuis l'authoring public Studio jusqu'au checkpoint V13. Ce compte n'est ni une clôture Gate3 ni une qualification V14 ou Primary. La réaction IA prouve le début physique de fuite ; pas une échappée complète ni une survie. P05 exige encore lisibilité/rythme de partie complète et décision humaine. Le système temporaire d'inspection n'est pas une modification de l'environnement final du jeu.

source10-primary-integration-preparation-001 étend la préparation Source9 sans écrire dans Primary. Les229 guards Source9 sont toujours concordants à cette observation (aucune dérive) ; le fichier propriétaire authoring_imported_terrain.rs reçoit uniquement les trois lignes d'installation de lumière dans une copie staged. Retirer ce bloc restitue exactement les bytes actuels propriétaire. Le HEAD Primary reste6b001dbed0fdf1c002d43cf507c60eece1119130. Les guards devront être relus au moment de toute application ; la décision propriétaire ADR0021 et la requalification du contenu réellement intégré restent requises. La compilation frontend V14 session84273 reste vivante à l'observation ; le replay corrigé n'a pas encore eu lieu. Aucun push, Gate3 OUVERTE — NON QUALIFIÉE.


### V14 — frontend terminé, candidat natif en compilation

Session84273 termine exit0. Frontend release WASM compilé en43m51s, puis worker release en58,50s ; proof frontend BUILD_ONLY avec source stable, WASM5634d821133263b01cd84d9c010bd1f6e799420f069d93546fb0650b13be22f1 et worker6d4ee304a6e5563329cdcf26029fd10eae904e9cb7a56d13196d0f9d2bc206d8. Cargo émet une incompatibilité future de proc-macro-error2 v2.0.1 sans échec de ce build. Ni runtime ni qualification stricte complète Source10 acquis par ces builds.

Le premier launcher natif termine exit1 avant compilation/création du candidat : Build-StudioCandidate.ps1 exige une branche Rust, et Source10 est détaché. Ce guard n'est pas supprimé. p05-native-branch-clone-preparation-001 prépare une copie Git indépendante locale sur Rust au même commitb9eb9aff, sans changer les refs Primary. Ses2233 fichiers suivis sont physiquement identiques à Source10 et les1104 pins source du frontend sont vérifiés. Un premier guard de copie signale un fichier inchangé modifié dans l'index ; git diff ne révèle aucun changement de contenu, git add rafraîchit uniquement l'index privé, le diff staged reste vide et tous les bytes sont revérifiés. Le builder nouveau et le lecteur Root non encore exécuté sont archivés. Compilation native réelle lancée, session71212 vivante à cette observation.

v14-vendor-input-scope-review-001 explique le seul fichier V13 absent du nouvel inventaire : third_party/rapier3d-0.35.0-glamx0.2/Cargo.lock, automatiquement généré et ignoré par .gitignore:7. Le NOTICE d'acquisition spécifie217 fichiers de package ; tous sont conservés et identiques à V13 après normalisation de fins de ligne. Le verrou workspace utilisé par --locked est inchangé après cette même normalisation. Le verrou standalone supplémentaire V13 est byte-archivé séparément. Le lecteur Root exige maintenant les217 fichiers d'origine avec cette revue explicite, pas une identité physique fictive de218 fichiers. Aucune source modifiée pendant les builds. Probe003 et lecteur de captures p05-lighting-root-review-002 prêts mais non exécutés. Prochaine étape : terminaison native, revue appariée des bytes source/web, puis comparaison publique Off/On et inspection. P05 humain, ADR0021 et intégration/requalification demeurent ouverts. Gate3 non qualifiée, aucun push.


### V14 terminé, échec public conservé ; V15 complet en reconstruction native

La session native71212 termine exit0. Le lecteur imported-work-light-candidate-root-review-001 vérifie le natif e64b6e43…, les artefacts WASM/worker et1280 fichiers web : BUILD_ONLY, pas une qualification runtime. Probe003 termine exit1, avec fermeture normale sans force et sources conservées : le diagnostic de locomotion manque au recentrage malgré simulation prête et joueur au sol. Aucune comparaison Off/On acquise par cette tentative.

Le frontend employait un import incomplet :55 fichiers des packages water_surface et shore_sim manquaient et leurs références dans buildings.json étaient null. Le profil complet de Tidewater dépend de ces packages. v15-complete-water-packaging-preparation-001 restaure les1306 fichiers d'import aux hashes du candidat V13 et conserve exactement les WASM/worker V14. Le premier launcher V15 refuse les inventaires1104/1108. Les seuls ajouts sont quatre schémas Tauri générés sous apps/forge_studio/gen/schemas, ignorés par Git ; les1104 entrées compilées sont toutes identiques. Ces schémas sont archivés et hashés séparément.

Deux dérivés de packaging V15 (candidate001/002) ont été préparés et relus sur fichiers ; ils ne sont pas des candidats runtime : Tauri embarque frontendDist lors du build, donc remplacer les assets adjacents ne remplace pas les assets embarqués. Cette limite est consignée dans v15-embedded-native-retry-preparation-001. Aucun replay sur ces dérivés n'est revendiqué. Le candidat002 restaure aussi les11 fichiers du kit gouverné ; cette vérification sert au nouveau lecteur, sans faire passer ce dérivé pour un build.

Le nouveau builder conserve la comparaison stricte de toutes les entrées source et contrôle séparément les quatre schémas générés. Reconstruction native réelle lancée pour tidewater-imported-work-light-015-candidate-003 (session74181 vivante à cette observation), avec frontend complet et kit gouverné. Lecteur Root003 et probe public006 prêts, non exécutés. La comparaison visuelle reste à acquérir ; P05 humain, décision ADR0021 et intégration/requalification Primary sont toujours ouverts. Aucun push ; Gate3 non qualifiée.


### V15 : échec shader conservé ; Source11 physique corrigé et frontend V16 lancé

La reconstruction native74181 termine exit0 en1m23s. Le lecteur complete-water-work-light-candidate-root-review-003 passe :1336 fichiers web,1104 entrées source,217 fichiers de package Rapier. Probe006 termine exit1 : simulation suspendue àTick0, projection périmée, sans comparaison Off/On. Fermeture normale sans force ; lecteur Root v15-public-readiness-failure-root-review-001 revérifie les8 fichiers d'entrée source/copie conservés. Les packages complets activent le mode Tidewater marche, mais aucune qualification runtime V15 n'est acquise.

La capture d'échec inspectée affiche dans le journal : « Projection importée refusée : underwater endpoint shader anchor changed ». Le code tidewater_underwater.rs compose le shader include_str et exige exactement une occurrence de DISTANCE_GUARD avec LF. Source10 a un WGSL enCRLF et zéro occurrence ; Source9 possède le même texte normalisé mais des octetsLF, avec une occurrence. L'équivalence après normalisation des1086 fichiers n'était donc pas une équivalence d'entrées runtime pour include_str : les builds seuls ne révélaient pas ce refus. L'échec reste conservé, aucun guard n'est supprimé.

source11-physical-input-restoration-001 prépare une copie Git indépendante sur Rust au même commitb9eb9aff, avec2233 fichiers suivis : octets de Source9 restaurés, sauf la seule insertion sémantique de trois lignes d'éclairage déjà autorisée.1087 fichiers restaurés physiquement, shader avec exactement une ancreLF, index privé rafraîchi sans diff staged ni changement de HEAD ; aucun fichier Primary remplacé. Compilation frontend V16 réelle lancée sur cette copie (session19332 vivante à cette observation), avec l'argument Imports explicite vers les1306 fichiers complets V13. Ni frontend terminé, ni natif V16, ni replay V16 revendiqués. P05 humain, ADR0021 et intégration/requalification restent ouverts. Gate3 non qualifiée ; aucun push.


### Prévention du retour CRLF et replay V16 préparé

source11-wgsl-checkout-policy-preparation-001 ajoute en staging uniquement « *.wgsl text eol=lf » à la .gitattributes actuelle, en préservant ses octets. Un véritable git checkout-index vers une sortie isolée, avec core.autocrlf=true et cette règle, produit le WGSL sous-marinLF byte-identique à Source11. Les230 guards précédents de Primary concordent, aucun drift ; le guard de .gitattributes est enregistré séparément. Aucun fichier Primary ni snapshot en compilation n'est changé. Cette règle doit accompagner l'intégration pour prévenir la réapparition de l'ancre CRLF ; intégration et requalification restent requises.

source11-v16-native-replay-preparation-001 prépare un builder natif et des lecteurs Source11/V16, ainsi que probe007 et lecteur de captures006. Les assertions et le flux public restent ceux de probe006. Le lecteur vérifiera aussi les1087 restaurations physiques, les1306 fichiers d'import complets et les11 fichiers du kit. Ces préparations ne sont pas exécutées : frontend19332 encore vivant à cette observation, log de compilation forge_engine ; aucune terminaison frontend/native ni capture V16 revendiquée. Gate3 demeure ouverte.


### Frontend V16 terminé ; natif lancé et décision Rapier présentée

Session19332 termine exit0 : release WASM14m29s et worker7,46s. Frontend BUILD_ONLY, inventaires source-before/after identiques. Les1306 fichiers d'import sont revérifiés aux octets V13. Warning futur proc-macro-error2 v2.0.1 conservé ; pas d'échec de compilation. source11-preparation-root-review-001 vérifie indépendamment2233 fichiers suivis, dont2232 byte-identiques à Source9 et la seule insertion de trois lignes d'éclairage ; cargo fmt --all --check termine exit0 dans cette copie. Aucune qualification stricte complète Source11 n'est revendiquée par le fmt seul.

Le builder natif Source11/V16 est lancé réellement, session28441 vivante à cette observation. Candidat tidewater-physical-input-restored-016-candidate-001 en construction, lecteur Root et probe007 non exécutés. Aucune capture Off/On V16 ni P05 humain acquis.

adr0021-owner-decision-packet-001 rassemble dix preuves Root machine/runtime/intégration, vérifie les217 fichiers Rapier de Source11 byte-identiques à Source9 et présente la responsabilité de maintenance du fork. La première préparation échoue sur un nom de clé absent total_failed ; le script original et son échec sont conservés, finish.py agrège les failed réels de test_summaries, garde les assertions1514 PASS/10 ignorés et génère le dossier. Question propriétaire ADR0021 envoyée via request_user_input_async : réponse encore absente à cette observation, jamais déduite de l'accusé technique accepted=true. Cette décision est distincte de l'accord ADR0009, ne clôture pas Gate3 et n'autorise pas publication. Les travaux de build/replay isolés continuent indépendamment. Gate3 reste ouverte, intégration Primary et requalification encore requises.


### V16 : effet d’éclairage observé ; qualification de la copie combinée lancée

Session native28441 termine exit0, build3m26s. physical-input-work-light-candidate-root-review-001 vérifie1335 fichiers web,1104 entrées source et217 fichiers Rapier. Probe007 termine exit0 : reprise compatible explicite, déplacement public jusqu’à l’intérieur, checkbox Off puis On, fermeture normale sans force et sources conservées. p05-lighting-root-review-006 vérifie les captures, leurs positions/cap/contact sol, les28 pins helpers et les8 fichiers source/copie aux hashes conservés. Les images ont été réellement inspectées : Off, les murs sont presque noirs ; On, murs bruns, jonction plafond et cadre de porte visibles. Le ROI descriptif passe de4,52655 à74,95199 en luminance encodée moyenne ; aucune calibration ou seuil d’acceptation n’en est déduit.

p05-lighting-visual-root-review-001 consigne l’effet visible du contrôle temporaire. Des brins de végétation restent visibles au sol de la pièce. Ce test ne qualifie pas un éclairage permanent du jeu, la partie complète, l’intégration Primary ni P05 humain. Vérification CIM scoped : aucun processus natif/WebView de V16/probe007 restant, autres processus non modifiés. Les refus V14/V15 restent archivés.

integrated-primary-rehearsal-preparation-001 prépare une copie indépendante de Primary :2270 fichiers versionables capturés et préservés hors230 overlays guardés. Le premier clone échoue sur la propriété du git-dir du worktree ; le retry fait confiance uniquement aux checkout/git-dir/common-dir exacts dans sa commande, sans changement global. Le deuxième guard diff--check signale deux espaces terminaux dans les commentaires upstream rigid_body.rs aux lignes1724/1742 ; les octets originaux sont archivés, ces seuls suffixes sont retirés dans la copie et une note de provenance est ajoutée. Le correctif fonctionnel Rapier reste la normalisation de contact. Primary n’est pas modifié.

Le contrôle fmt de la copie signale uniquement deux lib.rs, dont les ajouts propriétaire fish_props restent conservés. integrated-primary-rehearsal-format-001 applique rustfmt dans la copie, vérifie les autres octets inchangés, fmt/check et diff--check PASS, puis checkpoint privé b5ba0576… (parent6e4f56e7…). La copie combinée contient2493 fichiers suivis. Une vraie suite cargo test --workspace --no-fail-fast --locked --offline -j1 est lancée, session59459 vivante à cette observation, sur ce checkpoint avec TEMP/TMP et cible Cargo isolés. Aucun résultat machine hérité de Source9 n’est revendiqué pour cette copie ; strict natif, WASM, package/replay intégrés et P05 humain restent à acquérir. ADR0021 toujours en attente de réponse propriétaire, aucun apply Primary/push. Gate3 ouverte.


### Copie combinée : workspace vérifié, qualification native refusée

Le premier workspace (001, session59459 terminale exit1) a compilé puis échoué sur9 tests fish_props faute de chemins de fixtures ;1647 tests passaient,16 étaient ignorés. Cet échec reste archivé. integrated-rehearsal-acquired-inputs-001 fige259 fichiers déjà acquis : package poisson avec licence/provenance, référence de pose GPU antérieure, référence de culling/LOD et WGSL de scène. Tous les hashes source/copie sont vérifiés ; aucun script asset n’est exécuté, aucun test/assertion n’est affaibli. Les quatre chemins sont injectés dans le seul environnement enfant.

Le replay workspace002 (session16443 terminale exit0) exécute réellement cargo test --workspace --no-fail-fast --locked --offline -j1. integrated-rehearsal-workspace-root-review-002 confirme1656 PASS,0 FAIL,16 ignorés,2493 fichiers source stables et259 fixtures conservées. La comparaison CPU aux références acquises ne constitue pas une nouvelle exécution GPU ni une qualification visuelle. Les16 tests ignorés ne sont pas revendiqués PASS.

Le contrôle natif002 (session47433 terminale exit1) passe fmt mais Clippy workspace/all-targets/all-features avec -D warnings termine101. Les diagnostics complets sont archivés dans current-campaign-progress-099/native-diagnostics.json ; ils concernent notamment source_fragment, pier_motion, les tests source_local_lights et asset_library_native. La source2493 fichiers est restée stable. Le checkpoint privé b5ba0576… n’est donc pas qualifié natif. Les corrections et replays requis restent à réaliser sans modifier ce résultat ni le promouvoir en PASS.

Précision d’isolation : TEMP/TMP sont dédiés à chaque exécution ; la cible Cargo NTFS est un cache réutilisé de campagne, pas un cache exclusif. Aucun apply de code sur Primary, aucun push. ADR0021 attend toujours la réponse propriétaire ; WASM/package/replay combinés, partie complète et P05 humain restent à qualifier. Gate3 ouverte.


### Copie combinée : corrections initiales préparées, second refus natif conservé

integrated-rehearsal-clippy-correction-001 conserve les scripts échoués et prépare un nouveau clone privé2493 fichiers, physiquement identiques avant correction. Le rafraîchissement de l’index Git et le réglage local autocrlf aligné sur la source résolvent le préflight de fins de ligne ; aucune modification de configuration globale ni de shaders. Le checkpoint privé5291cdfa… modifie six fichiers : corrections de style, arguments privés empruntés, extraction bornée du chargement catalogue, motifs explicites des deux ignores Vault, justifications locales des comparaisons exactes/wire Serde. Les comptes d’assertions/tests/ignores sont conservés, fmt/check et diff--check PASS. Cela n’est pas une qualification fonctionnelle ni un apply Primary.

Le replay natif003, session3848 terminale exit1, vérifie2493 sources stables ; fmt passe puis Clippy strict termine101. La compilation progresse au-delà des premiers fichiers corrigés et expose108 diagnostics erreur, avec répétitions entre cibles, dans forge_engine. current-campaign-progress-100/native-diagnostics.json conserve les messages complets ; native-failure-root-review.json vérifie hashes, commandes, source/head propre et build-finished false. Le reader de PASS native-root-review-003 reste préparé et non exécuté. Le checkpoint5291cdfa… n’a pas encore de replay workspace, WASM ou public runtime. La preuve1656 PASS de workspace002 reste attachée au checkpoint précédent b5ba0576…, sans héritage automatique.

Suite requise : corriger les diagnostics engine en préservant les contrats de provenance et les retouches, rejouer le socle machine, puis qualifier le Studio/WASM et le parcours Tidewater combinés. ADR0021 et P05 propriétaire restent en attente. Gate3 ouverte, aucun push.


### Copie combinée : natif strict vérifié, workspace corrigé relancé

Les corrections engine sont préparées dans integrated-rehearsal-engine-correction-001 :18 fichiers, renommage local des variables sans changer les équations de motion, style/imports, conversions contrôlées des compteurs de géométrie et GPU. Les signatures de systèmes Bevy, comparaisons exactes et séquences de vérification gardent des expectations Clippy locales documentées ; aucune désactivation globale des lints ni tolérance nouvelle dans les assertions. Les comptes d’assertions/tests/ignores restent conservés. Le replay natif004 (session41600 terminale exit1) vérifie2493 sources stables, passe les modules précédemment refusés et expose21 diagnostics dans trois exécutables de vérification Tidewater.

integrated-rehearsal-example-correction-001 corrige ces trois exemples en conservant valeurs, comparaisons exactes et ordre acquisition/dispatch/readback. Le replay005 termine101 sur deux erreurs E0277 : la correction as_chunks fournit des tableaux fixes, alors que deux lectures SH utilisaient encore TryInto depuis une tranche. Cet échec est conservé. integrated-rehearsal-example-array-fix-001 corrige uniquement ces lectures vers les tableaux de quatre octets typés. Format/check et diff--check passent à chaque checkpoint privé.

Le checkpoint privé final894f5df4… contient2493 fichiers suivis, avec27 fichiers modifiés depuis la copie combinée b5ba0576… ; les autres octets sont identiques. Le replay native006 termine exit0 : cargo fmt --all --check et cargo clippy --workspace --all-targets --all-features --keep-going --locked --offline -j1 --message-format=json -- -D warnings. integrated-rehearsal-native-root-review-006 vérifie les commandes/logs,2493 sources stables et propres,259 fichiers acquis et900 compiler-artifacts, aucun compiler-message diagnostic, build-finished true. Les anciennes preuves de refus restent historiques, sans promotion en PASS.

La suite cargo test --workspace --no-fail-fast --locked --offline -j1 est réellement relancée sur894f5df4… avec les mêmes fixtures pinées (workspace006, session72943 vivante à cette observation). Le résultat1656 PASS de workspace002 concerne encore b5ba0576… ; aucun héritage du résultat sur le nouveau checkpoint. Strict WASM, package et replay public combinés, audit des16 ignores et P05 humain restent requis. ADR0021 toujours sans réponse propriétaire, aucune intégration de code Primary ni publication. Gate3 ouverte.


### Copie combinée : les16 ignores sont comptabilisés, fixtures props prêtes

integrated-rehearsal-ignored-scope-preparation-001 relève réellement les16 tests ignorés de workspace002, sans hériter leurs replays Source9 pour le checkpoint894f5df4…. Onze replays exacts sont nécessaires sur les exécutables du workspace006 : six tests des packages beach/village, quatre tests des entrées ocean/ShoreSim/water_surface/shaders et un test d’archive Studio. Deux tests de Vault externe et trois sondes CPU restent explicitement NOT RUN ; aucun verdict GPU/performance/gate n’en est déduit.

integrated-rehearsal-prop-fixtures-001 copie les vrais packages depuis le candidat propriétaire existant, vérifie722 fichiers source/copie, les manifestes pinés0a08054a…/d03cd51f…, commit source1438b1ab… et les deux licences MIT originales conservées. Aucun téléchargement, script asset exécuté, conversion ou écriture Vault/index ; ces copies ne prouvent pas leur qualification runtime. Les six replays props sont préparés mais non exécutés. Workspace006, session72943, reste vivant et compile forge_engine à la dernière observation ; les contrôles partageant sa cible Cargo restent sérialisés.


### Copie combinée : workspace et onze replays passent ; WASM échoue avant build Studio

Le checkpoint privé894f5df4… possède désormais la revue Root de workspace006 : 1656 tests réussis, zéro échec,16 ignorés. Les onze replays exacts requis passent sur trois exécutables réellement issus de cette suite puis copiés et hashés ;1232 entrées originales/copiées restent inchangées. Les deux tests de Vault externe et trois sondes CPU restent NOT RUN. Les preuves natives strictes restent distinctes des tests et du runtime public.

Les2500 fichiers des imports du candidat propriétaire sont figés avec17 références d’index et19 fichiers de licence/notice conservés. Les builders intégrés002 passent leur contrôle de syntaxe et de parité des2493 entrées suivies ; aucun candidat combiné n’est encore construit. Le contrôle WASM strict machine-wasm001 est terminal en échec101, source inchangée : dix diagnostics dans trois fichiers Studio web (mut inutile, type complexe, longueur de deux fonctions, clones et points-virgules). Ses logs sont conservés. Une nouvelle copie privée005 prépare les corrections sans modifier le checkpoint natif qualifié004 ni le code Primary. Aucun PASS WASM, rendu GPU ou parcours public du candidat combiné n’est acquis. ADR0021 et validation propriétaire P05 restent en attente ; Gate3 reste ouverte et non qualifiée, sans publication.


### Copie combinée005 : WASM strict passe après corrections isolées

Le checkpoint privé f3b7b119… corrige uniquement trois fichiers Studio web par alias de type, deux extractions de fonctions préservant l’ordre d’exécution, clone_from et corrections de mut/points-virgules. Aucun lint supprimé, aucune assertion ou limite de validation modifiée. Le clone avait d’abord refusé le rafraîchissement d’index après restauration des octets physiques ; git add --update dans cette seule copie a confirmé un index sans différence, avec2493 fichiers identiques avant correction. L’échec de préparation et le premier échec WASM restent archivés.

machine-wasm002 passe le contrôle release strict des huit bibliothèques prises en charge, avec les features web explicites et -D warnings. La revue Root vérifie la matrice exacte, les artefacts des huit bibliothèques, zéro diagnostic, build-finished success et2493 octets source stables. Ce PASS ne constitue pas une preuve navigateur/rendu. Native007 est lancé sur la même source005 (session99167, dernier contrôle vivant) ; les résultats native006/workspace006/onze replays restent attachés historiquement à source004 et ne sont pas promus à source005. La suite workspace, les replays exacts et le candidat Studio doivent encore être requalifiés pour le nouveau checkpoint. Primary non appliqué, ADR0021/P05 en attente, Gate3 ouverte et non qualifiée.


### Copie combinée005 : natif strict et builders passent ; workspace007 lancé

Native007 est terminal PASS sur f3b7b119…, avec revue Root : formatage, Clippy workspace/all-targets/all-features -D warnings,900 artefacts, zéro diagnostic,2493 fichiers suivis stables. Ce checkpoint dispose donc de contrôles stricts natif et WASM actuels. Workspace007 est lancé avec les259 entrées poisson/acquisitions réellement vérifiées ; session62900 vivante à la dernière observation, compilation en cours. Aucun compte de tests du checkpoint004 n’est hérité pour005.

Les builders003 ciblent explicitement source005 ; contrôle de syntaxe/parité frontend-native PASS sur2493 fichiers. Les quatre sorties de schéma Tauri sont identifiées comme ignorées et hashées séparément. Les lecteurs workspace007 et runner/lecteur des onze replays002 sont préparés avec les mêmes assertions et matrices ; ils ne sont pas exécutés. build-frontend.py prépare le candidat017 et exige les quatre revues Root du même HEAD (natif, WASM, workspace, replays exacts), les2493 hashes source et l’ensemble exact des2500 imports figés avant lancement. Ce build est préparé mais non exécuté, ainsi que son runtime public. Primary non appliqué, ADR0021/P05 en attente ; Gate3 ouverte/non qualifiée, aucune publication.


### Copie combinée005 : audit supply frais requis avant candidat017

Les trois entrées racine Cargo.toml/Cargo.lock/deny.toml sont identiques à supply010, mais sept manifestes Cargo supplémentaires diffèrent entre Source8 et la copie combinée005. Aucun PASS supply n’est hérité. integrated-source005-supply-preparation-001 prépare supply011 à partir du runner exécuté Source8, avec seulement le chemin du snapshot changé ; fetch de bases fraîches, contrôles yanked, politique deny, versions d’outils et transport TLS vérifié conservés. Le lecteur Root vérifie aussi les2493 hashes source actuels. Ces deux scripts sont préparés, non exécutés ; les commandes partageant Cargo restent sérialisées après workspace007.

Le lancement frontend017 préparé exige maintenant cinq revues du même HEAD, dont le nouveau supply. Sa nouvelle empreinte remplace celle enregistrée au suivi105, avant toute exécution. Le lecteur frontend017 contrôle les2493 entrées source, les deux WASM, les pins/artifacts du worker et les2500 imports copiés. Le lancement natif017 exige ce frontend vérifié et fige les entrées du kit gouverné avant compilation Tauri. Préparations de syntaxe uniquement, aucun build/runtime candidat017 acquis. Workspace007 session62900 est encore vivante ; dernière sortie : compilation forge_engine. ADR0021 et P05 restent en attente ; Gate3 ouverte/non qualifiée.


### Copie combinée005 : tests, onze replays et audit frais passent ; frontend017 lancé

Workspace007 est terminal PASS et revu Root :1656 tests réussis,zéro échec,16 ignorés,2493 fichiers source inchangés et259 entrées acquises vérifiées. Les onze replays exacts002 passent sur trois exécutables réellement issus de cette suite puis copiés/hashés ;1232 fichiers fixtures originaux/copiés inchangés. Root confirme les onze commandes/tests exacts et conserve explicitement cinq NOT RUN (deux Vault externe,trois sondes CPU), sans waiver ou preuve GPU fictive.

Supply011 est réellement exécuté puis revu Root sur ce même HEAD :audit zéro vulnérabilité,deny PASS,policy inchangée sauf chemin des bases privées,versions/outils/TLS et environnement restaurés. Les deux bases nouvellement acquises pointent vers ef6173cb…/233e2e0a… ; ce résultat n’est pas hérité de supply010. Les2493 hashes source sont revérifiés ; le laboratoire de navigation hors workspace reste hors portée de ce contrôle supply.

Le frontend017 est maintenant lancé (session28404 vivante au dernier contrôle), après préflight des cinq revues du même HEAD et de l’ensemble exact des2500 imports figés. Il compile le vrai WASM Studio puis le worker de navigation ; aucun BUILD_ONLY final ni candidat natif017/runtime public n’est encore acquis. Les lecteurs frontend/natif017 sont préparés et non exécutés. Primary non appliqué, ADR0021 et validation propriétaire P05 en attente ; Gate3 reste ouverte/non qualifiée, sans publication Git.


### Candidat017 : reprise publique préparée sur helpers et checkpoint figés

integrated-public-runtime-preparation-017 fige réellement416 fichiers de helpers existants et65 fichiers du checkpoint d’authoring V13 préservé. Les scripts de revalidation publique et de gameplay sont byte-identiques à ceux exécutés précédemment ; leurs succès ne sont pas hérités. Le navigateur EdgeWebView154.0.4258.53 est présent avec version observée conforme. revalidate.py est préparé (non exécuté) et exige le candidat017 construit/revu Root, les hashes des helpers et du parent authoring avant de lancer le helper public. Celui-ci clone le checkpoint, lit les formulaires Studio et sauvegarde par la commande publique ; il ne réécrit pas les documents authorés hors UI. Cette revalidation ne remplace ni les nouveaux parcours de gameplay ni P05 propriétaire.

Frontend017, session28404, reste vivant ; dernière sortie observée : compilation forge_engine en release WASM. Aucun frontend/candidat natif017 final ou runtime public acquis. Les65 fichiers originaux et les helpers figés resteront contrôlés après le replay. Primary non appliqué ; ADR0021/P05 en attente, Gate3 ouverte/non qualifiée et aucune publication.


### Candidat017 : frontend construit et revu Root ; natif lancé

Le frontend017 est terminal BUILD_ONLY, puis revu Root sur le checkpoint f3b7b119… :2493 fichiers suivis byte-identiques avant/après et au checkpoint, WASM Studio et navigation valides/hashés, manifest/artifacts/pins worker conformes, ensemble exact des2500 imports figés copié sans différence. Les cinq revues machine préflightées restent attachées au même HEAD ; aucune preuve navigateur/GPU/runtime public n’en est déduite.

Le natif017 est maintenant lancé, session88747 vivante au dernier contrôle. Son preflight confirme le frontend vérifié, les mêmes2493 hashes source et fige les fichiers du kit gouverné avant le vrai build Tauri qui embarque ce frontend. Aucun candidat natif017 final ni runtime public n’est encore acquis. La revalidation publique préparée attend la revue Root du candidat construit. Primary non appliqué ; ADR0021/P05 en attente, Gate3 reste ouverte/non qualifiée, sans publication.


### Candidat017 : natif revu Root, revalidation publique PASS ; parcours P01 lancé

Le vrai candidat natif017 est terminal BUILD_ONLY puis revu Root : binaire6f003ecb…, WASM Studio c80136ec…, worker e5cbbcec…, mêmes2493 entrées source,2500 imports Tidewater,11 fichiers de kit gouverné et2529 fichiers frontend embarqués vérifiés. Le frontendDist relatif pointe vers le bundle de ce candidat ; aucun remplacement de fichiers sans recompilation n’est présenté comme un nouveau binaire.

La revalidation publique017 est réellement exécutée et revue Root :22 formulaires d’entités (6 acteurs,12 objets,4 lieux), noms/positions et propriétés actor/physics vérifiés indépendamment, Save public confirmé, head complet sauvegardé byte/structure-identique au parent,65 fichiers originaux et416 helpers conservés. Le processus appartenant au test se ferme normalement, sans arrêt forcé. Les captures avant/après sont hashées et la capture après Save a été regardée : grille d’authoring avant chargement3D, donc aucune qualification visuelle Tidewater ou gameplay héritée. Les anciennes commandes de création ne sont pas revendiquées comme rejouées.

P01 est maintenant lancé sur ce candidat et ce parent publiquement revalidé, session81037 vivante au dernier contrôle. Le helper figé reçoit les options existantes de parcours physique final, capture après Ready, contacts de combat bornés et visite intérieure après quête/avant Save ; il doit acquérir réellement quête/combat/ramassage/intérieur puis nouvelle instance Resume. Aucun PASS P01 actuel, parcours alternatif, P02–P04, monde comme arme ou P05 propriétaire n’est encore acquis pour017. Primary non appliqué, ADR0021/P05 en attente ; Gate3 ouverte/non qualifiée, aucune publication.


### Candidat017 : premier P01 échoue au démarrage CDP ; retry atteint la 3D

P01-001 est terminal FAILED_CLOSED avant gameplay : cible CDP exacte absente sur le port réservé52109, dernier appel HttpClient local expiré à2s. Le helper applique son cleanup de démarrage forcé au seul PID27028 ; reçu closed=true, puis observation CIM indépendante ne trouve ni ce PID ni enfant WebView avec le profil de cet essai. Le wrapper et la revue Root vérifient binaire/helpers/fixture d’origine conservés. Aucun screenshot ni parcours de gameplay n’est acquis dans cet essai ; cette fermeture forcée ne devient pas un succès de fermeture normale.

Le retry P01-002 est réellement lancé, session12336 vivante au dernier contrôle, avec nouveau profil/output et chemin du navigateur canonique Windows aligné sur la revalidation017 réussie, même hash de l’exécutable WebView. Timeout180s, critères de cible exacte et assertions gameplay inchangés, aucune source produit modifiée. La cause du premier démarrage n’est pas prouvée. Ce nouvel essai a dépassé le démarrage, atteint Ready et la capture input. run1-start_ready.png a été regardée : terrain/plantes/bâtiments Tidewater effectivement visibles en3D et silhouette capsule de personnage visible, HUD de quête disponible. Cela prouve ce point de démarrage rendu, pas une acceptation visuelle P05 ni un parcours complet. Quête/combat/intérieur/Save/Resume restent en cours ; aucun PASS P01 final encore acquis.

La revue publique de22 formulaires et Save inchangé du suivi110 reste valide/distincte. ADR0021/P05 restent en attente, Primary non appliqué et Gate3 ouverte/non qualifiée ; aucune publication.


### P01-002 terminal : centrage échoué ; P01-003 lancé avec transport clavier mesuré

Le retry002 est terminal FAILED_CLOSED avant combat au centrage guardian_outside_staging : la limite inchangée de 0,05 m n'est pas atteinte dans les 30 s. Studio ferme normalement, sans arrêt forcé. La revue Root conserve les 12 impulsions : les dernières impulsions nominales de 40 ms séparent leurs observations de 28 à 70 ticks, avec des déplacements dépassant un mètre. Ces intervalles contiennent transport/observations ; ils ne mesurent pas directement la durée des touches dans le navigateur. Ni une faute physique ni la cause du premier échec CDP ne sont prouvées.

L'inspection montre que le helper original attend chaque réponse CDP avant d'envoyer la touche suivante puis le relâchement. Une nouvelle copie de 416 helpers ne change qu'un fichier : au centrage, les événements publics Input.dispatchKeyEvent sont envoyés en ordre, avec relâchement avant attente des réponses ; toutes les réponses sont ensuite vérifiées. Les temps d'envoi et de réception sont journalisés, sans les présenter comme des temps d'événement navigateur. Autocontrôle PowerShell PASS. Tolérance, délai, critères de combat, sources produit et binaire inchangés. P01-003 réellement lancé dans un nouveau profil/output, session78033 vivante au contrôle initial ; aucun résultat final acquis.

Les preuves machine et la revalidation publique des 22 formulaires restent distinctes. ADR0021 et P05 ne sont pas acceptés, Primary non appliqué, Gate3 ouverte/non qualifiée, aucune publication.


### P01-003 terminal échoué ; réception clavier navigateur mesurée dans P01-004

P01-003 échoue encore au centrage 0,05 m / 30 s avant combat, avec fermeture normale et binaire/helpers/fixture conservés. La revue Root inspecte 15 impulsions et toutes leurs réponses CDP. Les relâchements sont envoyés en 41–58 ms pour les impulsions nominales de 40 ms, alors que toutes les réponses arrivent en 105–167 ms. Les intervalles d'observation sont de 15–32 ticks ; ils ne sont pas la durée effective des touches. Certains échantillons passent à 0,026–0,029 m de la cible à vitesse non nulle, puis dépassent la cible avant trois observations stables : aucun de ces passages ne satisfait l'arrivée. Aucun P01 complet promu.

P01-004 est lancé, session12760 vivante au contrôle initial. Nouveau helper privé, autocontrôle PASS : impulsions de 1 ms à distance <=0,4 m, 40 ms au-delà ; même limite 0,05 m, même délai 30 s et même stabilité. Un observateur passif consigne les événements clavier publics reçus (isTrusted, timeStamp, performance.now) et les intervalles requestAnimationFrame. Il ne déclenche aucun input et ne modifie pas le gameplay, les sources, le rendu ou le contenu authoré. Les seules mutations sont les reçus temporaires de diagnostic dans le navigateur privé. Cause produit/transport non conclue, aucun succès final acquis.

ADR0021/P05 restent non acceptés, Primary non appliqué, Gate3 ouverte/non qualifiée, aucune publication.


### P01-004 terminal : touches reçues dans la même frame ; régression multi-tick préparée

P01-004 échoue au même centrage 0,05 m / 30 s, avant combat, avec fermeture normale, sans arrêt forcé et avec sources/binaire/fixture conservés. La revue Root vérifie les 14 impulsions, événements publics isTrusted=true et paires pression/relâchement. L'intervalle entre callbacks navigateur est de 0–0,3 ms ; les timeStamp des événements gardent des écarts différents (horodatage émis, pas preuve d'une touche maintenue aussi longtemps dans le runtime). Les intervalles requestAnimationFrame observés atteignent 354,3 ms. Les petites impulsions ne résolvent donc pas le centrage ; aucun PASS P01 promu.

Lecture du contrôleur : axis_value utilise pressed || just_pressed, sample_player_input soumet cette valeur comme held_move_axis/held_raw_move_axis, puis drain_tick ne relâche pas les axes lors des ticks physiques suivants de la même frame. Le commentaire et le test existant annoncent une pression observable pour une commande, mais le test ne consommait qu'un tick. Une nouvelle copie privée source006 est en préparation (session29490 vivante au contrôle), avec extension de ce test pour vérifier le deuxième tick et l'axe brut relâché ; aucune correction production encore appliquée ni régression exécutée à ce point. Cette hypothèse doit être confirmée par l'échec ciblé puis une correction générique et les replays affectés, sans promouvoir une causalité uniquement sur les timings.

Source005 et candidat017 restent archivés ; leurs résultats machine ne seront pas hérités par une source006 corrigée. ADR0021/P05 non acceptés, Primary non appliqué, Gate3 ouverte/non qualifiée, aucune publication.


### Source006 privée créée ; régression ciblée en compilation

La préparation source006 est terminale PASS (session29490 terminée) : copie de source005, les 2493 fichiers physiques vérifiés avant modification, HEAD parent f3b7b1197a8fe250621bc0303460a4c0ef020943, arbre initial propre. Seul player_controller.rs diffère par l'extension du test existant, pas par une correction production. Les avertissements Git CRLF/LF sont conservés ; git add --update n'a produit aucun diff staged avant cette extension et aucune source Primary n'a été écrasée.

Le test réel est lancé via integrated-input-tap-regression-001/run-red.py, session12381 vivante au contrôle : cargo test -p forge_engine --lib, filtre exact du test same-frame, --locked --offline -j1. La compilation progresse ; aucun résultat RED/GREEN encore acquis. Le fichier apply-after-red.py prépare la correction générique dans source006, mais refuse d'agir sans un résultat RED confirmé. Il est seulement préparé, non exécuté : conservation des touches tenues, relâchement de l'axe tap après le premier tick, test mixte touche tenue/tap diagonal. Toute correction exécutée devra être qualifiée à nouveau et le parcours public rejoué.

Pas de fermeture Gate3 ni de publication ; les décisions ADR0021/P05 restent distinctes et non acceptées.


### Reprise : test RED toujours vivant ; helper post-correction figé sans verdict runtime

La session12381 du test RED est confirmée vivante par polling du handle ; la compilation progresse dans les dépendances géométrie/images/rendu. Aucun red-proof.json n'est encore disponible : la correction source006 reste non appliquée. Les scripts run-green.py et checkpoint-after-green.py sont préparés avec leurs gardes, et prepare-after-green.py pourra générer les six runners/readers WASM/natif/workspace depuis les matrices strictes antérieures après un véritable checkpoint GREEN. Le générateur est préparé, pas exécuté ; aucun PASS machine source006 n'est acquis.

Le helper post-correction integrated-public-runtime-harness-018-timed-input-005 est créé et ses 416 fichiers sont épinglés ; seul le script gameplay diffère du helper004. Autocontrôle PowerShell observé PASS. Pression publique de 200 ms loin de la cible (>0,4 m), tap de 1 ms près de la cible : une correction consommant le tap sur un seul tick ne doit pas transformer le déplacement éloigné en une succession de micro-pas. Les reçus passifs sont vidés avant chaque impulsion pour éviter d'attribuer à celle-ci des touches du parcours ou du contact précédents. Limite 0,05 m, délai 30 s, trois échantillons stables et assertions combat inchangés. Ce helper est préparé, sans nouvelle exécution publique ni causalité complète promue.

Les décisions propriétaire et la qualification globale restent ouvertes ; aucune modification de la copie principale du contrôleur, aucune publication.


### Chaîne source006 lancée, gardée par le résultat réel du test RED

La régression session12381 reste vivante au contrôle, compilation bevy_render. Son runner a utilisé le profil standard (RUSTFLAGS/CARGO_INCREMENTAL non imposés), contrairement aux validations antérieures avec debuginfo=0/incremental=0 : compilation plus lourde, aucun redémarrage pour ce seul motif. Les résultats ne sont toujours pas acquis.

La chaîne integrated-source006-qualification-chain-001/run.py est réellement lancée, session90093 vivante au contrôle initial, état WAITING_FOR_RED_PROOF. Elle attend uniquement le reçu terminal produit après la fin du subprocess cargo du test ; aucun second cargo lancé en concurrence à ce point. Sans RED confirmé elle s'arrête sans appliquer la correction. Après RED confirmé, elle pourra appliquer la correction privée, exécuter les tests du contrôleur générique/Tidewater, créer le checkpoint privé seulement après GREEN, générer les runners puis exécuter séquentiellement WASM strict, revue des reçus WASM, natif strict, revue des reçus natifs, workspace et revue des reçus workspace. Arrêt au premier échec, hashes des scripts et logs conservés. Les revues sont des vérificateurs locaux automatisés ; le verdict global et humain reste distinct.

Cette chaîne ne construit pas encore de nouveau candidat Studio ni de preuve publique complète, n'applique pas les changements au Primary, ne valide pas ADR0021/P05, ne clôture pas la gate et ne publie rien. État courant : test RED et chaîne d'attente actifs, correction non appliquée.


### Source006 : entrées de dépendances inchangées, portée de l'audit capturé conservée

La revue indépendante integrated-source006-dependency-input-review-001 vérifie source006 actuelle face aux bytes source005 : 22 fichiers Cargo.toml/Cargo.lock/deny/config/toolchain identiques, et 2492 autres fichiers suivis inchangés hors player_controller.rs. Rapports audit/deny de supply011 relus, zéro vulnérabilité dans le rapport capturé et aucun diagnostic deny error ; politique effective équivalente hors chemin de DB, deux clones DB à leurs commits/trees propres et hashes des outils revérifiés. L'audit source005 est daté 2026-10-04T16:05:36.3978969Z. Il n'y a ni nouvel audit ni nouveau fetch d'avis, ni revendication de derniers avis actuels. Cette preuve d'égalité des entrées permettra de réévaluer la portée de cet audit sur le checkpoint006 final, qui n'est pas encore vérifié. Le LAB reste hors portée.

Régression12381 et chaîne90093 revérifiées vivantes au contrôle ; compilation progresse dans les bibliothèques restantes. Aucun RED/GREEN, checkpoint ou candidat006 final acquis à ce point. Les critères de parcours, décisions propriétaire et statut ouvert de Gate3 restent inchangés, aucune publication.


### Source006 premier correctif : RED confirmé, 46 tests PASS, WASM/natif stricts PASS

La régression001 est terminale : cargo exit101, un test échoué, axe brut Vec2(0,1) conservé après le tick au lieu de Vec2(0,0), 683 tests filtrés. Le wrapper confirme l'échec attendu (pas un PASS du produit). Le premier correctif privé est appliqué, fmt et 46 tests du contrôleur générique/Tidewater passent sans échec/ignore. Checkpoint local source006 HEAD3adf0c466f1bde134f2e493315c0f228d0e9146f, 2493 fichiers épinglés propres, seul player_controller.rs changé face à source005. WASM strict8libs : 717 artefacts, zéro diagnostic, revue locale PASS ; natif strict workspace/all-targets/all-features : 900 artefacts, zéro diagnostic, revue locale PASS. Ces résultats ne qualifient pas le runtime public ni toute la gate.

La revue de ce correctif relève un cas supplémentaire à reproduire : si PreUpdate rééchantillonne une frame avant qu'un FixedUpdate consomme le tap, submit remplace l'axe et efface la métadonnée du tap. Les tests actuels consomment un tick immédiatement ; ils ne prouvent pas la conservation entre frames graphiques sans tick physique. Une régression supplémentaire doit vérifier ce cas et le mélange avec une touche maintenue avant le verdict final.

La chaîne90093 est arrêtée au niveau du parent seulement. La première tentative Stop-Process a émis une erreur .NET et n'a pas terminé le PID38460 ; son reçu initial affirmant un arrêt n'est pas valide. L'observation CIM puis Win32_Process.Terminate retour0 confirment l'absence du parent dans verification-001.json. L'enfant28284 est préservé et vérifié : integrated-rehearsal-workspace-preparation-008/run.py, contrôle workspace du premier checkpoint en cours, aucun Cargo tué. Le snapshot006 reste inchangé jusqu'à la fin de ce contrôle ; les scripts/preuves exécutés restent archivés. La chaîne n'a pas produit de verdict global final, et la revue workspace devra être exécutée après la fin réelle de l'enfant.

Root principal actif, Gate3 ouverte/non qualifiée, ADR0021/P05 non acceptés, Primary non appliqué, aucune publication.


### Premier checkpoint source006 : workspace terminé, régression interframes lancée

Le contrôle workspace008 est terminal : cargo test --workspace --no-fail-fast --locked --offline -j1, exit0. Revue locale des logs, des 2493 fichiers source et des 259 fixtures PASS : 1657 tests réussis, zéro échec, 16 ignorés. Ce résultat porte sur HEAD3adf0c466f1bde134f2e493315c0f228d0e9146f, avant la nouvelle régression. Aucun PASS public ou humain ajouté.

Après cette terminaison, deux tests sont ajoutés uniquement dans le snapshot privé : conservation d'un tap relâché entre plusieurs PreUpdate sans tick physique, et mélange de ce tap avec une nouvelle touche maintenue opposée. Le test RED002 est lancé, session46428 observée vivante ; résultat encore absent. Une correction002 et ses vérifications sont préparées, sans application avant RED confirmé. Les résultats001 et workspace008 restent archivés dans leur portée initiale.

À la demande explicite du propriétaire, la suite se déroule avec le seul agent principal, sans sous-agents. Gate3 ouverte/non qualifiée, ADR0021/P05 toujours sans acceptation constatée, copie principale du contrôleur préservée, aucune publication.


### Régression interframes confirmée et deuxième correction privée appliquée

RED002 est terminal : cargo exit101, les deux tests échouent comme attendu, 685 filtrés. Après plusieurs PreUpdate sans physique, W relâché produit zéro au lieu de Y ; avec S nouvellement maintenu, l'axe est -Y au lieu de zéro. Logs et contrôleur avant correction archivés.

Correction002 appliquée seulement dans source006 : masque privé de touches brièvement pressées, conservé entre frames graphiques jusqu'au premier drain physique, puis consommé ; touches maintenues et annulation de focus conservées. Le changement reste limité à player_controller.rs, avec deux tests supplémentaires, assertions précédentes intactes. Aucun changement de vitesse, physique, vitalité ou seuil du parcours public.

Fmt PASS ; les 48 tests du contrôleur sont lancés, session42161 observée vivante, compilation encore en cours au relevé. La chaîne002 session30088 attend le reçu GREEN réel avant checkpoint local et vérifications strictes WASM004, natif009, workspace009 avec leurs lecteurs. Elle s'arrête au premier échec. Aucun résultat001/008 n'est promu comme preuve du deuxième correctif.

Gate3 reste ouverte/non qualifiée ; le futur candidat Studio et les parcours publics restent à exécuter. ADR0021 et P05 sans nouvelle acceptation constatée, Primary non appliqué, seul agent principal utilisé, aucune publication.


### Deuxième correction source006 : 48 tests GREEN, contrôles stricts lancés

Fmt et les 48 tests du contrôleur générique/Tidewater sont terminés avec exit0, aucun échec ou ignore. Les deux tests interframes reproduits en RED passent désormais, ainsi que les tests de tap sur un tick, touches maintenues, focus et contrôleur existants. Source stable durant les tests. Checkpoint privé propre c1fad943054f2f9bd45e9f2aa63ba7cfe72c6872, parent3adf, 2493 fichiers épinglés, seul player_controller.rs changé.

La chaîne002 a exécuté le checkpoint et généré les six runners/lecteurs sans reprendre les anciens PASS. Session30088 revérifiée vivante, étape3 : WASM strict004. Les contrôles natif009 et workspace009 attendent leur tour ; aucun résultat futur acquis à ce relevé. Les résultats workspace008 (1657 PASS) restent ceux du premier correctif.

Le candidat Studio apparié au nouveau checkpoint et le parcours public complet restent à construire/exécuter ; la preuve publique017 n'est pas promue. Gate3 ouverte/non qualifiée, ADR0021/P05 sans acceptation nouvelle, Primary non appliqué, aucun sous-agent utilisé, aucune publication.


### Checkpoint c1fad : WASM strict PASS, audit capturé applicable aux mêmes entrées

WASM004 termine exit0 sur les huit bibliothèques requises ; revue des 2493 fichiers, des logs et de la matrice PASS : 717 artefacts compilateur, zéro diagnostic, HEADc1fad943054f2f9bd45e9f2aa63ba7cfe72c6872. Ce résultat reste une vérification de compilation stricte, sans nouveau runtime navigateur.

La revue dependency-input002 vérifie ce checkpoint propre et les 2493 pins, puis confirme 22 fichiers de dépendances identiques à source005 et 2492 autres fichiers inchangés hors contrôleur. Les rapports, bases aux commits propres et outils de l'audit capturé le 2026-10-04T16:05:36.3978969Z sont relus. Aucun nouvel audit/fetch, aucune affirmation d'avis plus récents ni inclusion LAB.

Chaîne002 session30088 revérifiée vivante ; étape5, natif strict009 en cours au relevé, workspace009 pas encore qualifié. Candidat Studio et parcours publics restent à exécuter. Gate3 ouverte/non qualifiée, décisions ADR0021/P05 sans nouvelle acceptation, Primary non appliqué, aucun sous-agent utilisé, aucune publication.


### Checkpoint c1fad : natif strict PASS, pipeline candidat018 et replay public lancés sous conditions

Natif009 termine fmt et Clippy workspace/all-targets/all-features/warnings-denied avec exit0 ; revue locale PASS, 900 artefacts, aucun diagnostic, même HEADc1fad et 2493 fichiers propres. Chaîne machine002 session30088 revérifiée vivante à l'étape7 : workspace009 en compilation, résultat encore absent.

Les runners des onze tests ignorés acquis sont préparés pour les exécutables réels de workspace009 : même liste, 1232 fixtures, copies immuables des exécutables après terminaison de la suite. Les scripts de build frontend et natif sont appariés au checkpoint006 ; leur parsing et la parité des 2493 entrées passent. Six scripts de replays/build/lecteurs épinglés et diffs archivés, sans reprise des PASS source005.

Chaîne Studio session59241 lancée et revérifiée vivante, attendant le verdict terminal réel de la chaîne machine002 ; ensuite replays exacts, lecteurs, build frontend018, lecteur, build candidat018 et lecteur, arrêt au premier échec. Aucun second Cargo lancé pendant workspace009 par cette chaîne.

Préparation publique018 réutilise les 416 helpers de revalidation déjà figés et les 65 fichiers authorés V13 inchangés ; le helper gameplay018-timed-input005 est séparément épinglé, avec les seuils/délais/assertions de combat conservés. La chaîne publique session60288 est lancée et attend uniquement le verdict build018 vérifié : elle revalidera les 22 formulaires et Save, lira le reçu, puis exécutera P01 sur une nouvelle copie isolée. Aucun nouveau runtime public exécuté à ce relevé ; aucune création ancienne ni qualification017 héritée. Le résultat de commande publique devra encore être revu, même s'il passe.

La copie principale reste modifiée par des travaux préexistants vérifiés en lecture seule ; aucun écrasement ni application du contrôleur privé. Gate3 ouverte/non qualifiée, ADR0021/P05 sans nouvelle acceptation constatée, seul agent principal utilisé, aucune publication.


### Vérifications complémentaires018 préparées ; compilation workspace toujours active

Les sessions30088 (machine), 59241 (build) et 60288 (public) sont revérifiées vivantes. La compilation workspace009 a atteint forge_runtime après forge_engine ; aucun verdict terminal de suite ni candidat018 disponible au relevé. Pas de redémarrage, de modification du snapshot ou de second Cargo lancé pendant ce contrôle.

Les helpers publics P02/P03/P04 figés passent chacun leur SelfTest (résultat outil exécuté, sans nouveau parcours Studio). Les wrappers018 vérifient le candidat et la revalidation de 22 formulaires, les hashes de helpers et de fixture, le runtime navigateur exact et la terminaison de la chaîne publique avant toute exécution. P02 exige le nouveau P01 terminé et sa vraie sauvegarde ; aucun ancien save/runtime promu. Les scénarios EnvironmentQuest, ForestDanger et ShorePush sont préparés sur ce même checkpoint pour produire les deux approches de quête et les preuves causales R3-B, sans affirmation d'évasion complète/survie de l'IA.

Quatre lecteurs de preuves P01/P02/P03/P04 sont préparés à partir des contrôles exécutés V13, mais avec les identités018. P01 relira les cinq attaques directement dans la vraie sauvegarde en plus du reçu de la commande publique. P04 exige encore un nouveau reçu d'absence de processus et sa revue. La compatibilité structurelle des champs de reçus est vérifiée sur l'ancienne sauvegarde ; cela n'est pas un PASS du nouveau candidat.

Chaîne de contrôles complémentaires session20562 lancée et revérifiée vivante, attendant le verdict terminal public018. Après PASS, elle relira P01 puis exécutera séquentiellement les trois scénarios causaux, P02/P03 avec leurs lecteurs et P04, arrêt au premier échec. Aucun de ces parcours018 n'a démarré à ce relevé. Les revues regroupées des huit critères R3-B, des deux approches et la revue finale P04 restent à effectuer sur leurs nouvelles preuves.

Gate3 ouverte/non qualifiée, ADR0021/P05 sans nouvelle acceptation, Primary préservé, aucun sous-agent utilisé, aucune publication.


### Checkpoint c1fad : workspace1659 PASS et onze replays acquis PASS ; frontend018 en construction

La chaîne machine002 est terminale, session30088 exit0, huit étapes exit0. Workspace009 et sa revue terminent : 1659 tests réussis, zéro échec, 16 ignorés, même checkpoint propre c1fad et 2493 fichiers vérifiés stables. Les résultats natif009 et WASM004 appartiennent à ce même checkpoint. Les tests ne prouvent pas un nouveau parcours public ni une revue humaine.

La chaîne build59241 a exécuté les onze tests ignorés acquis sur les nouveaux exécutables workspace009 copiés et épinglés : 11 PASS et revue locale PASS, 1232 fixtures vérifiées stables, aucun ancien résultat/exécutable hérité. Les cinq autres tests restent NOT RUN (deux catalogues/vault, trois sondes CPU) ; aucune dérogation propriétaire ajoutée et aucune nouvelle exécution GPU revendiquée.

La construction frontend018 a réellement commencé après ces contrôles. Preflight2493 entrées source appariées et2500 imports figés PASS ; compilation des crates Forge en cours, session59241 revérifiée vivante. Frontend, exécutable Studio, revalidation publique et parcours restent non qualifiés à ce relevé. Les chaînes publiques60288 et complémentaires20562 attendent leurs reçus réels. Aucun processus public018 encore lancé, aucune publication ni application au Primary. Gate3 ouverte/non qualifiée, décisions ADR0021/P05 sans nouvelle acceptation, CI distante NOT RUN selon ADR0006, seul agent principal utilisé.


### Écart avec le Primary courant : changements propriétaire préservés, trois brouillons de fusion relus

Deux audits en lecture seule comparent les 2270 chemins du Primary capturé initialement et les 2493 fichiers du snapshot006 au dépôt actuel. HEAD Primary demeure6b001dbe. Le second relevé classe2213 chemins identiques,260 deltas du candidat sur base Primary inchangée,17 changements Primary à préserver,12 chemins propres au Primary et3 chevauchements : fish_props_scene.rs, authoring_imported_shadows.rs et forge_assets/src/lib.rs. Ces nombres sont ceux du relevé ; le Primary continue d'évoluer. Les modifications de poissons, plantes, shadows, histoire/persistance et leurs documents ne font pas partie d'une nouvelle qualification globale par les tests du snapshot006.

La première tentative de brouillon refuse le hash périmé du fichier poissons avant écriture ; la deuxième archive14 conflits textuels dans la fusion à trois voies ; la troisième refuse encore un nouvel écart de hash. Aucun Primary ni source006 modifié. Une quatrième capture immédiate des trois entrées produit un brouillon privé : code propriétaire complet d'ombres masquées conservé, corrections de lint web réappliquées, alias du cache de géométrie ; observation plant_caster_selection conservée avec le bloc KCC ; export tidewater_game_fish conservé avec les modules qualifiés.

Rustfmt des seuls fichiers de brouillon et revue de leurs fonctions/projections/export PASS. Les fonctions d'installation, cleanup et publication d'ombres sont comparées au code propriétaire capturé après formatage ; la projection d'observation reste le code propriétaire plus le seul bloc KCC. Ce contrôle est une revue de syntaxe et de préservation de corps, sans compilation ni runtime de la future fusion. Les entrées capturées et les trois étapes refusées restent archivées. Avant application, les changements courants devront être reguardés/réconciliés, l'acceptation ADR0021 constatée et le résultat fusionné complètement requalifié ; aucun ancien PASS006/018 ne couvrira automatiquement ces changements.

La chaîne build59241 reste vivante à l'étape frontend018 (forge_engine) au relevé ; les chaînes60288/20562 attendent les résultats réels. Snapshot006 testé inchangé, nouveau candidat/runtime018 non acquis, Gate3 ouverte/non qualifiée, ADR0021/P05 sans nouvelle acceptation, aucun sous-agent utilisé, aucune publication.


### Candidat018 réellement construit et vérifié ; revalidation publique en cours

La chaîne build59241 est terminale exit0, six étapes exit0. Frontend018 et candidat natif018 sont reconstruits depuis HEADc1fad : 2493 fichiers source stables,2500 imports,11 fichiers de kit et2529 fichiers frontend packagés revérifiés. Exécutable SHAa2b2f5233a84cfc48fd712bfc419cbb2619c739c150b57a85a96fd12aedd4ce7 ; WASM Studio2b140c254812e4672ac3628556c94f4e9b3694ab1ea85b95b9392bf53d698597 ; worker e5cbbce76142441b63d99ac12cdef14d978a697a2b71669110320a17d0a135fb. Le worker est effectivement reconstruit et relu, même si son hash égale l'artefact précédent. Résultat BUILD_ONLY, sans promotion du runtime017 ni du brouillon de fusion004.

La chaîne publique60288 est revérifiée vivante à son étape1 : nouvelle revalidation des formulaires et Save, clone authoring propre018 et entrées de scripts archivées, résultat encore absent au relevé. P01 et les scénarios suivants attendent leurs reçus actuels. La chaîne20562 attend le verdict public avant ses contrôles complémentaires. Aucune revue humaine acquise.

Le runner de navigation018 et ses deux lecteurs sont préparés pour le même candidat/ledger : provider/profil/politique publics, porte fermée NoPath, ouverture/sortie physique/Reached, traversée de tuiles et révisions, vraie sauvegarde et fermeture native actuelle. Le SelfTest est réellement exécuté : contrats de refus des observations périmées, faux support/KCC, faux franchissement de tuile, porte sans révision et reçus de sauvegarde invalides PASS. C'est un test synthétique du lecteur, pas une nouvelle preuve de navigation. Le replay réel et le reçu frais d'absence de processus restent nécessaires.

Gate3 ouverte/non qualifiée ; candidat018 couvre le snapshot006 et ne qualifie pas les modifications ultérieures du Primary ni la fusion004. ADR0021/P05 sans nouvelle acceptation, Primary non appliqué, aucun sous-agent utilisé, aucune publication.


### Candidat018 : premier lancement public échoué, nouvelle tentative isolée

La chaîne publique60288 est terminale exit1 : cible CDP exacte absente sur le port55819 après le délai de démarrage. Le candidat n'a fourni aucun des22 formulaires et aucun Save public ; P01 n'a pas commencé. La chaîne complémentaire20562 est également terminale exit1, arrêtée sur sa garde exigeant le PASS amont, sans scénario complémentaire exécuté. Les résultats machine et BUILD_ONLY018 restent distincts de cet échec public.

Le reçu de nettoyage indique PID36424, fermeture forcée et processus fermé. Une observation CIM indépendante confirme l'absence de ce PID et de ses enfants directs (chunk c285e3). Le candidat, les helpers et la fixture originale sont inchangés. Le helper gelé avait déjà positionné le dossier WebView2 154.0.4258.53 : son omission dans l'environnement du wrapper n'est donc pas une cause démontrée. Les journaux natifs vides ne prouvent aucune cause.

Une nouvelle tentative002 utilise le même candidat SHAa2b2f523, les mêmes416 helpers gelés, la même fixture parent et les mêmes assertions. Seul l'environnement enfant reçoit aussi le chemin canonique de WebView2 avant les imports. Les anciennes preuves sont conservées ; sortie publique002 et lecteur002 frais. Il s'agit d'un replay contrôlé, sans correctif causal déclaré ni PASS anticipé. La revue indépendante des22 formulaires et du Save reste à exécuter si le lancement passe.

Gate3 ouverte/non qualifiée. ADR0021/P05 sans nouvelle acceptation, Primary non appliqué, aucun sous-agent utilisé, aucune publication Git.


### Candidat018 : revalidation publique003 PASS relue, boucle RPG en cours

La tentative002 avec environnement navigateur canonique est terminale exit1, même absence de cible CDP, PID16232 fermé après nettoyage. L'observation native montre la bibliothèque EmbeddedBrowserWebView154.0.4258.53 chargée, aucune fenêtre principale et seulement un enfant conhost. Le chemin de navigateur seul n'a donc pas résolu l'échec.

La tentative003 exécute le même wrapper, candidat, helpers et assertions hors du bac à sable, après approbation automatique. Session48693 terminale exit0. Le lecteur indépendant003 est effectivement exécuté :22 formulaires publics vérifiés,65 fichiers de fixture originale et416 helpers inchangés, Save acquitté, référence et head complet identiques au parent, fermeture native normale sans arrêt forcé. Ledger SHA9920c662a818fe67ae5b57aaea9666bc4aea871179726a1e409b429c34813386. Ce contraste localise une condition de lancement sensible au contexte d'exécution ; la primitive Windows exacte n'est pas démontrée.

La capture après Save a été réellement regardée : coque Studio, grille vide avant chargement de la 3D importée et champs auteur, sans preuve visuelle de Tidewater jouable. Aucun replay de création ni PASS gameplay n'est hérité de cette lecture/Save.

Le runner P01 est adapté au ledger003 relu et lancé hors du bac à sable, session22800, sortie publique018-p01-001 fraîche, helper timed-input005 inchangé et candidat018 épinglé. Il doit réaliser quête, combat physique, intérieur, loot, Save et reprise ; aucun résultat n'est anticipé. Les anciennes chaînes échouées restent archivées. Les scénarios complémentaires attendent les nouveaux reçus actuels.

Gate3 ouverte/non qualifiée ; ADR0021/P05 sans nouvelle acceptation, Primary non appliqué, aucun sous-agent, aucune publication.


### P01 actuel : gardien franchi et seuil intérieur atteint, verdict encore attendu

Session22800 revérifiée vivante (poll50s, chunk439c1c), même candidat018, sans redémarrage. Les captures publiques start_ready, guardian_defeated et interior_closed_threshold existent et sont archivées par hash dans p01-progress-witnesses.json. La capture start_ready a été réellement regardée : village Tidewater rendu et capsule joueur, quête disponible. La capture guardian_defeated montre simulation prête, joueur au sol, quête active et disparition de l'action Frapper Gardien de la forêt ; elle ne remplace pas les cinq reçus de dommages et leur sauvegarde à relire dans le ledger final. Le seuil intérieur est atteint ensuite. Save/reprise et fermeture restent à confirmer sur le résultat terminal.

Les runners complémentaires frais018-retry003 sont préparés pour le vrai authoring003, avec garde exigeant le lecteur indépendant P01 courant plutôt que l'ancienne chaîne échouée. Scripts compilés syntaxiquement ; aucun scénario complémentaire exécuté ni assertion de gameplay assouplie. Gate3 ouverte/non qualifiée, Primary non appliqué, aucun sous-agent ni publication.


### P01018-001 terminal échoué : dégagement après ouverture ; replay002 corrigé côté pilote

Session22800 terminale exit1. Le ledger réel décrit l'échec interior-player-exit-clearance : cible(46,-151), tolérance10cm, délai120s. Le gardien et le seuil intérieur ont été atteints mais le parcours intérieur/Save/reprise n'est pas achevé. Fixture source, candidat et helpers inchangés ; fermeture native normale sans force. Observation CIM indépendante : aucun processus du chemin exact candidat018 (chunk e70a73).

La revue des282 échantillons de cette phase trouve tous les états prêts et au sol, distance minimale0.240416m et nombreux changements de direction autour de la cible. Les derniers retours KCC montrent des translations horizontales effectives et des contacts de terrain ; cette trajectoire ne démontre pas un obstacle physique bloquant. Le pilote continu maintient ses touches entre lectures et oscille autour d'une cible fine.

Helper006 privé : seul l'appel de dégagement après ouverture utilise désormais Move-PlaytestSettledPulses, déjà exécuté près du gardien, au lieu du pilote continu. Cible, tolérance10cm et délai120s identiques ; trois observations fraîches stables sont exigées avant arrivée ou nouvelle impulsion, sans écriture de position, source produit ou état gameplay. Aucun critère de sol/intérieur/navigation/combat/save n'est supprimé. Les416 fichiers sont épinglés ; seul le script gameplay change. SelfTest réellement exécuté exit0 (chunk423f1d). Cette correction de pilotage reste à qualifier par le replay complet.

Runner018-retry004 lancé hors sandbox sur fixture002 fraîche, candidat018 et authoring003 inchangés, session48392. Les preuves échouées001 restent conservées. Les contrôles complémentaires devront référencer le ledger002 actuel si son résultat et le lecteur indépendant passent. Gate3 ouverte/non qualifiée ; ADR0021/P05 toujours sans nouvelle acceptation ; Primary non appliqué, aucun sous-agent ni publication.


### P01018-002 terminal : contact manqué par le lecteur ; régression RED/GREEN007 et replay003

Session48392 terminale exit1. Avant d'atteindre le dégagement intérieur corrigé, le pilote gardien n'a trouvé aucun état collision/blocage frais avec attaque active dans ses15s. Fixture, candidat et helpers préservés ; fermeture native normale. Observation CIM indépendante confirme aucun processus du candidat018 (chunk29cfcb). Aucun résultat positif de la correction de dégagement n'est déduit de ce run.

Les22 paires avant/après sont archivées. Les observations avant impulsion présentent des contacts frais entre les lectures, postérieurs au tick de la lecture après précédente, dont de nombreux contacts nommés Gardien de la forêt et Support piège. Le pilote ne consultait sa décision de contact qu'après chaque nouvelle impulsion, puis perdait ces états intermédiaires. Helper007 conserve le prédicat de contact, le délai15s, les impulsions40ms et les assertions de dégâts : il vérifie également l'état public courant avant la prochaine impulsion, avec fraîcheur strictement postérieure au dernier tick après impulsion. Un arrêt reste un indice de position, jamais un succès de combat ; les acquittements réels demeurent requis.

SelfTest007 exit0 réellement exécuté. Régression de contrôle sur observations archivées, transport et action disponible synthétiques : ancien006 reproduit l'échec attendu ; nouveau007 s'arrête avant la seconde impulsion et rejette un tick périmé. Aucun runtime lancé dans ce test. La première assertion GREEN comptait les keyDown plutôt que les impulsions et échouait sur les deux touches d'une direction diagonale ; son script original est conservé. Le test corrigé compte les événements d'impulsion : RED002 et GREEN002 terminalexit0, sans changement du pilote entre ces tests.

Runner018-retry005 lancé hors sandbox, session27176, sortie P01018-003 fraîche et même authoring003/candidat018. Source produit inchangée, helper007 épinglé416 fichiers. Résultat complet encore attendu. Les runners complémentaires005 et lecteurs causaux/ deux approches actuels sont préparés. Quatre scénarios sont requis : EnvironmentQuest, ForestThrow (chute/piège/destructible), ForestDanger et ShorePush ; un agrégateur des huit R3-B est préparé et ne peut passer sans leurs vrais ledgers/reçus. Aucun de ces scénarios n'est encore exécuté sur018.

Gate3 ouverte/non qualifiée ; ADR0021/P05 sans nouvelle acceptation, Primary non appliqué, aucun sous-agent, aucune publication.


### P01018-003 : intérieur traversé, Save et reprise prête ; chaîne complémentaire conditionnelle

Le même replay27176 est revérifié vivant. Les captures guardian_defeated, interior_inside, interior_closed_after_exit, save et run2-resume_ready existent : le dégagement corrigé a permis de poursuivre la traversée de l'intérieur et le retour à l'extérieur ; un second processus a atteint la reprise prête. Le replay doit encore terminer la revisite des conséquences et sa fermeture, puis passer le lecteur indépendant des reçus. Aucun PASS complet n'est anticipé.

Les captures intérieur et reprise ont été regardées : HUD de quête Completed, ouverture de porte puis reprise préparée. La capture de reprise contient une grande capsule bleue masquant une large partie du viewport ; son identité n'est pas inférée. Observations/pixels et hashes archivés dans current-public-p05-observation-draft-018 : revue intermédiaire, sans acceptation propriétaire P05 ni validation du rythme ou de tout l'éclairage intérieur.

Chaîne complémentaire002 lancée hors sandbox, session25531 revérifiée vivante : elle attend le vrai launch-proof du P01018-003, exige ensuite le lecteur indépendant P01, et ne lance qu'alors quatre scénarios physiques avec leurs relectures causales, agrégateur huit R3-B, deux approches P01, retouche P02, diagnostic P03 et replay isolé P04. Arrêt au premier échec. Navigation, fermeture indépendante P04 et revue finale de gate restent séparément nécessaires. Tous les scripts sont épinglés à l'entrée de la chaîne.

Le lecteur P02 préparé archive désormais les vrais helpers007 avec pins pré/post du runner actuel, au lieu d'un ancien dossier webview010. Sa garde exige le ledger P01018-003 passé et sa fixture exacte. C'est une correction de provenance du lecteur, pas un PASS anticipé. Gate3 ouverte/non qualifiée ; ADR0021/P05 sans nouvelle acceptation, Primary non appliqué, aucun sous-agent ni publication.


### Parcours complet P01018-003 PASS relu ; scénario environnemental bloqué ; retouche P02 lancée

Session27176 terminale exit0 à22:03:49UTC le4octobre (5octobre local). La chaîne25531 a réellement exécuté le lecteur P01 : PASS indépendant. Ledger fb7b73c4c10549d9667041c44a6857046c838755c5f6183ea65101faa4acdb56 ; sauvegarde e396ea4449715572250960bcdb01a2fc29ac5e32bf57e63b1078dce756e6d79e. Cinq command_id distincts, cinq dégâts de contact20, séquences publiques égales aux reçus effectivement sauvegardés, pickup et quête Completed persistés, intérieur traversé et porte refermée, reprise dans un second processus et conséquences revisitées. Les cinq vrais reçus sont à nouveau relus ici depuis le fichier de sauvegarde. Sources conservées, fermeture normale des deux processus. Ce PASS concerne le parcours avec combat ; la seconde approche environnementale et la qualification globale restent à établir.

La chaîne complémentaire25531 est terminale exit1 à son étape2 EnvironmentQuest, après le lecteur P01 réussi. Le scénario a démarré normalement puis échoué sur forest-approach-waypoint-0-step-1 : départ vers(38.5,-117), arrêt observé(39.4098,-117.6697), état prêt/au sol, translation horizontale demandée mais résolue nulle, deux contacts de terrain non nommés. Le délai de non-progression12s est conservé. Aucun PNJ bloquant ni défaut précis de moteur n'est déduit de ces seuls contacts. Le scénario a conservé ses sources et fermé normalement ; CIM indépendant confirme aucun candidat018 actif après sa fin (chunk0dd4de). Les autres scénarios de cette chaîne n'ont pas été lancés.

Le contrôle P02 indépendant est lancé sur une copie de la vraie sauvegarde P01018-003 : runner005, session95991. Réplique et porte retouchées dans Studio, refus de reprise ordinaire puis reprise compatible explicite et état de quête conservé restent à vérifier sur son résultat et son lecteur actuel. Aucun PASS anticipé ; le diagnostic du scénario environnemental reste nécessaire.

Gate3 ouverte/non qualifiée ; deux approches P01, huit R3-B, navigation, P03/P04 et revue propriétaire P05 restent sans PASS courant complet. ADR0021/P05 sans nouvelle acceptation, Primary non appliqué, aucun sous-agent ni publication.


### P03 et P04 PASS relus ; P02 échoué ; diagnostic physique natif en cours

Le 5 octobre local, P03 est confirmé par le lecteur indépendant : suppression du Sceau refusée parce qu'elle retirerait la quantité nécessaire à Rapporter le Sceau, diagnostic dirigé vers l'objet, Apply désactivé, aucune révision appliquée. P04 est confirmé par son lecteur indépendant exécuté : deux démarrages publics au même point choisi et à la même révision40, déplacement et Save dans le premier essai, nouveau départ distinct d'une Resume dans le second, sources et sauvegardes initiales préservées, fermeture normale des deux processus. CIM exact du candidat018 vérifié absent à22:25:27UTC ; reçu process-closure.json archivé avec le lecteur P04. Ces résultats sont des preuves locales bornées, sans acceptation visuelle propriétaire.

P02 session95991 est terminale exit1. Les sources originales sont conservées et le processus fermé normalement. Le trajet vers la porte épuise son budget global120s : dernier segment vers(43.545,-150.93), environ2s restantes. Le lecteur d'échec recalcule les distances depuis les ancres exactes publiques : nombreux passages près des cibles puis éloignement sous touches maintenues ; dernier segment distance minimale0.347m, finale0.661m et translation effective non nulle. Ceci ne suffit pas à diagnostiquer un défaut précis du contrôleur. Aucun élargissement de tolérance ni PASS P02 n'est appliqué.

Un LAB séparé contient deux tests natifs du vrai terrain village, à la pose bloquée du scénario EnvironmentQuest : translation oblique capturée et comparaison cardinale. Session8228 toujours vivante au dernier poll, compilations Bevy observées. Source qualifiée006 conservée, aucun code de production modifié ; aucun PASS anticipé et aucun replay public remplacé par ces tests.

Navigation018 a été réellement tentée puis a échoué avant lancement public : la propriété navigation_actors n'existe pas dans le ledger de revalidation003. Candidat, scripts et fixture inchangés vérifiés par le runner. Ce défaut d'entrée du harnais nécessite un raccord explicite aux acteurs du checkpoint courant ; aucun résultat ancien n'est hérité.

Gate3 ouverte/non qualifiée. P03/P04 locaux acquis, mais P02, seconde approche P01, R3-B, navigation, intégration Primary et validation propriétaire P05 restent incomplets. ADR0021 sans nouvelle acceptation ; aucun sous-agent, commit ou push.


### Navigation002 publique : patrouilleur NoPath immobile, géométrie installée différente

Le raccord du harnais a été corrigé dans un nouvel arbre figé navigation008,416 fichiers dont seul Invoke-Gate3NavigationPlaytestSmoke.ps1 change : identités lues dans l'objet courant épinglé après validation de son SHA et du workspace, paire et patrouilles contrôlées. SelfTest réel PASS des contrats synthétiques, aucun PASS public déduit. Runner006, session71099, replay navigation002 réellement lancé puis terminal exit1 à22:31:54UTC : délai fermé60s sans traversée physique du raccord de tuile.

Lecteur indépendant d'échec exécuté :83 fichiers d'observation byte-vérifiés,83 échantillons frais du patrouilleur, tous NoPath, déplacement horizontal maximal0m. Tuiles prêtes et requête de route présente ; la connexion n'est pas acquise. Sources et clone auteur conservés, fermeture normale sans arrêt forcé, absence native confirmée séparément par CIM. Le checkpoint auteur est exactement le même que V13 réussi, le SHA déclaré du fournisseur identique et11 fichiers Rust de navigation présents dans les arbres comparés byte-identiques. La politique est empty dans les deux replays : elle ne peut pas à elle seule expliquer cette différence. Neuf reçus de géométrie installée diffèrent, avec nombres de triangles différents. La cause exacte de projection, de chargement ou de dépendance physique reste à établir ; ne pas attribuer cela au fournisseur sans preuve.

Lecteurs de succès navigation002 préparés séparément mais NON exécutés sur cet échec. Ils exigent les rapports sémantiques complets et tous les fichiers réellement observés, sans imposer l'ancien nombre33 issu de la planification du replay V13. Le LAB physique village session8228 demeure vivant au poll courant et compile naga29 ; aucun verdict natif anticipé. Prochain travail : comparer les surfaces installées/imports avec V13 et examiner les résultats du LAB sans changer les sources qualifiées ni les travaux propriétaire.

Gate3 demeure ouverte/non qualifiée, aucune publication Git, aucun sous-agent.


### Imports et forks comparés ; P02 retouche observée, reprise complète encore ouverte

Comparaison réelle des imports V13 et candidat018 :1305 anciens fichiers byte-identiques, aucun ancien fichier absent, seul buildings.json modifié parmi les fichiers communs ; toutes ses anciennes références conservées et1194 fichiers ajoutés dans le candidat actuel. Les paquets de décor ajoutés ne sont pas retirés pour obtenir un PASS. Cela ne prouve pas l'égalité des colliders installés. Les entrées Cargo.lock sélectionnées Bevy/Rapier/Parry/Rerecast/Landmass/glam sont identiques. Comparaison des forks locaux :240 fichiers byte-identiques ; Rapier rigid_body.rs diffère uniquement par deux espaces de fin de commentaires, et le Cargo.lock interne ancien est absent du snapshot intégré. L'assertion initiale d'identité stricte a correctement refusé ces différences ; version initiale du lecteur conservée, différences relues explicitement dans dependency-proof.json. Aucun changement d'algorithme physique n'est établi par cette comparaison.

LAB de connectivité village préparé et lancé, session91249, en attente réelle du verrou Cargo détenu par la compilation session8228. Il utilise deux heightfields129² échantillonnés dans le blob courant épinglé, positions du patrouilleur(50,-117) vers(50,-135.5), franchissement physique exigé Z<-134.4. Les bâtiments/décors absents dans ce test isolent le terrain ; ce diagnostic ne remplace aucun critère public de scène complète. Les deux tests de blocage du joueur session8228 restent vivants et compilent Bevy PBR au dernier examen. Aucun code de production qualifié ni Primary modifié.

P02 replay002, session46648 terminale exit1 : pilotage corrigé sur copie figée helper009, touches relâchées avant centrage près d'une cible ; mêmes waypoints, tolérance0.35m et délai120s. SelfTest réel et régression de la fonction de pilotage sur transport synthétique PASS, refus d'une observation finale hors tolérance confirmé. Replay public : premier trajet réellement terminé en64.707496s, Save à l'arrêt acquitté, Preview/Apply/Save de la retouche et réplique modifiée observés. Second trajet vers la porte épuisé ; fermeture normale et sources originales conservées, résultat P02 global NON PASS. Le lecteur d'échec vérifie ces seuls jalons depuis le ledger actuel.

Helper010 corrige une attente redondante : Move-PlaytestSettledPulses conserve son focus initial par défaut, mais le trajet P02 peut réutiliser une entrée publiquement déjà active. Le lecteur vérifie toujours grounded/ready/input_active, ticks frais et trois observations stables. Régression exécutée de la vraie fonction sous transport synthétique : focus par défaut acquis une fois, focus actif réutilisé, perte de focus refusée, tolérance inchangée ; SelfTest P02 PASS. Replay public003 sur copie fraîche lancé, session77028, résultat encore inconnu. Lecteur indépendant current004 préparé pour ce ledger/runner/helper précis, NON exécuté avant résultat. Aucun délai ni tolérance augmenté.

Gate3 ouverte/non qualifiée ; navigation, environnement, P02 et validations propriétaires restent incomplets. Aucun sous-agent, commit ou push.


### P02 délai opérationnel explicite ; troisième diagnostic natif sur bâtiments réels

P02 replay003 session77028 terminale exit1 à son second trajet : centrage0.35m à l'avant-dernier tronçon waypoint4-step10, délai global120s épuisé. Premier trajet terminé, Save à l'arrêt, retouche et réplique modifiée observés ; reprise complète encore non démontrée. Sources originales et arbre figé du harnais/candidat conservés, fermeture normale sans arrêt forcé. La réutilisation du focus améliore la progression observée sans produire un PASS global.

Les exigences R3-P02 de MIGRATION_ROADMAP et G3-09 du plan ont été relues : elles demandent retouche Studio et reprise sans perte de texte/liens/quête, sans délai de trajet120s. Le plafond autorisé existant InteractionTimeoutSeconds300 est retenu pour le replay004, avec décision et SHA des spécifications dans integrated-public-p02-timeout-preparation-011/proof.json. Même candidat018, même harnais010, même trajet et tolérances, mêmes reçus/Save/refus ordinaire/Resume compatible/état de quête/interior KCC ; seule attente opérationnelle120→300s. Les échecs restent archivés. Aucune qualification de performance, fluidité ou P05 n'est déduite de cette attente étendue. Runner008 session66156 vivant au dernier poll, lecteur current005 adapté à ce timeout précis et NON exécuté avant résultat.

Diagnostic natif supplémentaire lancé session81858, en attente du même verrou Cargo : terrain village réel plus maillages des bâtiments réellement contenus dans le paquet commun épinglé et placements canoniques du checkpoint. Production BuildingPackage::load vérifie les octets ; production meshes/surface construit les vrais colliders triangles sous leurs transforms. Ce test isole une projection CPU, n'exécute pas le cycle de rendu Studio complet et n'inclut pas les autres props ou la salle canonique ; aucun critère public n'est remplacé. Sessions8228 et91249 toujours vivantes, compilation8228 a atteint forge_assets et forge_world. Aucun résultat natif anticipé.

Vérification indépendante exécutée de préservation :2497 fichiers de baseline de la source qualifiée006 toujours byte-identiques. Dans le LAB, seul tidewater_player_tests.rs diffère parmi ces fichiers, au SHA préparé pour les deux tests ; nouveaux tests de navigation et objet auteur copiés sont propres au LAB. Le candidat natif018 conserve son SHA qualifié. Primary n'est pas modifié par ces diagnostics. Gate3 ouverte/non qualifiée, aucun sous-agent ni publication Git.


### P02 courant PASS relu ; deux tests KCC natifs PASS sans reproduction du blocage public

Replay P02018-004 session66156 terminale exit0 à23:13:05UTC le4octobre, durée totale environ422s pour toutes les opérations ; attente opérationnelle300s explicite, sans dérogation à un critère métier ni qualification de performance. Lecteur current007 réellement exécuté à23:16:50UTC, PASS indépendant. Ledger f8a978437232e4b5b92717d5f422b6d589d573b09abdf2b5b270d66d331307b0, candidat natif a2b2f5233a84cfc48fd712bfc419cbb2619c739c150b57a85a96fd12aedd4ce7. Les fichiers sources/sauvegardes d'entrée sont conservés, copie retouchée rev40→42, seuls texte de réplique et X de porte(+0.05m) changent dans le head. Refus ordinaire source-changed observé, reprise compatible explicite dans un second processus, quête Completed, Sceau consommé absent, réplique retouchée et IDs/liens conservés relus. Fermeture normale des deux processus et absence CIM native vérifiée séparément.

Deux hypothèses de lecteur refusées puis corrigées dans de nouveaux lecteurs conservés : current005 supposait que le marqueur nommé threshold était forcément extérieur ; il était déjà au sol de la salle. Current006 ne considérait que les observations en mouvement et omettait les vrais reçus d'arrivée et de mouvement terminé. Current007 utilise l'observation publique extérieure du corridor, les observations fraîches ordonnées WASD, l'arrivée puis mouvement terminé ; marges de porte ±0.35m inchangées, translation KCC effective et demandée positive, tick concordant, contact du vrai Floor, puis pose intérieure et état deuxième processus requis. Aucun point intérieur synthétique ni assouplissement de la traversée ; les lecteurs échoués et leurs SHA restent archivés.

Session8228 native terminale exit0 :2 tests raw KCC sur le vrai terrain village PASS,0 fail,0ignore ; trajectoire oblique capturée et comparaison cardinale déplacent réellement la capsule. Lecteur indépendant du log, du blob, du tile et du source de test exécuté PASS. Le blocage EnvironmentQuest public n'est donc pas reproduit dans ce montage natif isolé ; cela ne prouve pas que le wrapper WASM, la scène complète ou d'autres contacts sont corrects. Le replay EnvironmentQuest demeure échoué et la cause précise à établir. Tests natifs navigation91249 et bâtiments81858 toujours vivants au dernier poll, premier recompilant le normal library forge_engine et second attendant le verrou ; aucun PASS anticipé.

Scénario public ShorePush lancé indépendamment sur candidat018/helper007, copie neuve et fixture auteur courant épinglé, session1901. Il vise les effets de poussée/eau, non une clôture déduite des tests natifs. P02/P03/P04 locaux acquis sur candidat courant ; seconde approche P01, R3-B complet, navigation, Primary et P05 encore ouverts. Gate3 non qualifiée, aucun sous-agent, commit ou push.


### Actualisation 141 — diagnostics natifs de navigation et échec ShorePush

Les sessions 91249 et 81858 sont terminales, code 0. Trois essais natifs distincts sont désormais revus sur sources et journaux SHA-256 : terrain réel seul, terrain et 23 placements de bâtiments importés (111 parties, 65 363 triangles), puis même géométrie avec colliders enfants sous les racines transformées, sans RigidBody explicite, comme le staging produit. Chacun franchit physiquement z=-134,4 avec retour physique frais et grounded. Preuve : `.gate3-evidence/20261002/integrated-village-navigation-diagnostic-root-review-002/proof.json`.

Ces diagnostics ne reproduisent pas le NoPath du replay public et ne qualifient pas sa navigation. Ils excluent encore le cycle complet du Studio, les autres props, la pièce canonique et WASM. L'hypothèse d'une incapacité intrinsèque du terrain réel ou de la seule hiérarchie de bâtiments n'est pas corroborée par ces essais ; la cause publique reste à établir.

La session ShorePush 1901 est terminale, code 1. Le journal constate un dépassement de l'approche `shore-approach-waypoint-4-step-4`, avant les interactions poussée/eau. Aucun PASS physique n'est déduit de ce replay. Journal conservé : `integrated-public-followup-preparation-018-retry-005/shorepush/run.log` ; ledger conservé : `tidewater-integrated-primary-018-public-shorepush-001/gate3-physics-environment-quest-ledger.json`. Sa revue causale détaillée reste à réaliser.

Gate 3 demeure ouverte, non qualifiée. Aucun changement de code produit intégré au checkout propriétaire et aucune publication Git dans cette actualisation. Suite : examiner l'installation effective des surfaces dans le parcours public, puis corriger et rejouer les critères affectés.


### Actualisation 142 — accessoires résolus et gameplay entier en diagnostic natif

Deux diagnostics supplémentaires PASS ont été exécutés et revus sur sources/journaux épinglés : bâtiments et accessoires hérités résolus (27 village, 43 plage), puis même scène avec le gameplay entier du checkpoint. Le PNJ extérieur franchit z=-134,4, grounded, avec retour physique frais. Session 59283 terminale code 0. Preuve et comparaison : `.gate3-evidence/20261002/integrated-village-resolved-scene-navigation-root-review-001/proof.json` et `comparison.md`.

Les bâtiments sont présents dans le replay public : une absence générale de leurs colliders n'est pas étayée. Certaines tuiles village ont désormais les mêmes empreintes géométriques natives/publiques, tandis que d'autres diffèrent encore. Aucun de ces résultats ne résout le NoPath public. Les héritages sont fournis au diagnostic après contrôle du pin raster ; plants, structures restantes, cycle Studio et WASM sont encore exclus. Suite ciblée : compléter les projections des tuiles divergentes puis comparer construction et liens si les entrées concordent.

Les préparations accessoires 001 (assertion erronée sur placements explicites) et 002 (attribut Rust mal placé) sont archivées. Elles sont des échecs de préparation, sans verdict de produit ; 003 utilise le binding de source exact et le résolveur métier.

Gate 3 ouverte, non qualifiée ; aucun changement produit intégré et aucune publication Git.


### Actualisation 143 — projection canonique et accessoires de place

Deux diagnostics additionnels ont été exécutés : `BuildingProjection::replace` accepté sur le head réel, puis ajout des accessoires de place résolus par le binding source et le résolveur métier. Sessions 35755 et 90822 terminales, code 0. Le PNJ extérieur traverse z=-134,4 avec retour physique frais et grounded. Lecteurs exécutés : `integrated-village-canonical-room-navigation-root-review-001/proof.json` et `integrated-village-plaza-navigation-root-review-001/proof.json`.

La comparaison cible six tuiles village. Trois ont maintenant une empreinte extraite exactement identique au replay public : [1,-5], [2,-5], [2,-4]. Les obstacles courbes concordent en nombre sur les six tuiles. Les triangles restant à expliquer : [0,-5] 11 375 natifs / 11 447 publics ; [0,-4] 15 945 / 16 113 ; [1,-4] 13 727 / 13 886. Le NoPath public reste non reproduit ; aucune cause précise ni correction produit n'est démontrée. Le checkpoint n'a pas de chemins acceptés. Suite : compléter les structures source restantes (notamment échoppe et poissons), puis vérifier sorties et connectivité si les entrées concordent. Plants, cycle Studio complet et WASM restent hors de ces diagnostics.

Gate 3 ouverte, non qualifiée ; aucun code produit intégré, aucun push.


### Actualisation 144 — NoPath public reproduit par ajout des passerelles

Le diagnostic natif ajoutant les trois passerelles résolues échoue : session 64717 terminale, Cargo code 101, un test échoué après 30,45 s. Le diagnostic précédent sans ces passerelles passe. Le lecteur vérifie que l'ajout du bloc de staging des passerelles est l'unique différence comportementale entre les deux tests. Le PNJ extérieur réel `2b352831-7da6-5610-ba2c-a3a93c817be0` reste NoPath, grounded, vitesse nulle, pieds [50,4,2882576,-117], correspondants au relevé public à 1e-5 m. Le fournisseur rapporte le même échec de chemin entre départ et cible.

Preuve exécutée : `integrated-village-boardwalk-failure-root-review-001/proof.json`. Cela établit l'ajout des passerelles comme déclencheur dans ce diagnostic ; la forme précise et le mécanisme algorithmique restent à isoler. Aucun placement produit retiré et aucune correction démontrée. Le lecteur initial confondait code du wrapper et code Cargo : version initiale archivée, lecteur corrigé vérifie explicitement 101 et le résultat du test.

Trois essais d'exclusion diagnostique d'une passerelle à la fois ont été lancés, session 57684 ; résultats à lire avant tout verdict. Ces exclusions ne modifient ni les sources canoniques ni la fixture de qualification. Les acteurs forêt sortent du terrain limité de ces diagnostics et ne sont pas qualifiés par eux. Gate 3 ouverte, non qualifiée, sans intégration produit ni publication.


### Actualisation 145 — déclencheur isolé : passerelle source n°1

Session 57684 terminale, Cargo 101 : exclusion diagnostique de la passerelle 1 PASS ; exclusion de 0 ou 2 FAIL NoPath. Le lecteur vérifie les trois sections de test, le journal et les sources épinglés. Preuve : `integrated-village-boardwalk-isolation-root-review-001/proof.json`. Placement déclencheur : `tidewater:1438b1abfcaee3267092b75573014f4d9b4a983c:boardwalk:1`, racine [36,8,0,-110,6], dix boîtes solides source. Aucun placement produit retiré.

Cela isole le placement, pas encore la forme précise ni le défaut algorithmique. Son emplacement est éloigné de la route directe du PNJ [50,-117] vers [50,-135,5]. Suite : deux tests retirant chacun cinq boîtes uniquement dans la scène diagnostique ont été lancés, session 1694. Résultats à lire avant verdict. Le code produit, le checkpoint et la Gate ne sont pas corrigés par ces exclusions. Gate 3 ouverte, non qualifiée, aucune publication.


### Actualisation 146 — origine verticale de grille : RED/GREEN sur tuiles réelles

Session 1694 terminale : retirer les cinq boîtes source 0–4 de la passerelle 1 rétablit la traversée ; retirer les cinq autres laisse NoPath. Preuve : `integrated-village-boardwalk-box-isolation-preparation-001/review-proof.json`. Ces retraits sont uniquement diagnostiques.

Session 29287 terminale PASS : quatre captures d’entrées et sorties Rerecast, avec/sans passerelle 1 ; empreintes d’entrée contrôlées contre les reçus du runtime. L’origine verticale de [1,-4] change de 2,9198785 à 2,8936577 m ; [1,-5] conserve 4,4994874 m. Preuve : `integrated-village-navmesh-capture-preparation-001/review-proof.json`.

Session 70550 terminale PASS : les deux mêmes entrées réelles échouent avec les origines actuelles (`NoPathFound`) et trouvent un chemin après alignement du minimum vertical sur la grille mondiale au pas réel radius/4. Aucun retrait de géométrie ni assouplissement de tolérance de lien. Preuve : `integrated-navigation-height-grid-experiment-003/review-proof.json`. Les préparations expérimentales 001/002 ayant échoué à compiler sont conservées ; 003 répare les imports et utilise le pas vertical réel vérifié dans ConfigBuilder.

Correction appliquée uniquement au provider du laboratoire `integrated-village-kcc-diagnostic-source-001`, original archivé dans `integrated-navigation-height-grid-native-replay-001/provider.before.rs`. Replay natif physique avec toutes les passerelles lancé, session 89348. Aucun PASS public, aucun changement du candidat figé ni du code propriétaire. Gate 3 ouverte, non qualifiée ; replays physiques, publics et qualification élargie encore requis.


### Actualisation 147 — régression indépendante du package et compilation active

Un test comportemental versionnable a été préparé dans le laboratoire : deux sols praticables voisins inchangés, avec une petite surface verticale basse éloignée donnant des minima différents. L'assertion demande un chemin réel entre les deux sols via Rerecast/Landmass, sans ajuster les tolérances. Test : `crates/forge_navigation/tests/vertical_tile_lattice.rs` dans le laboratoire ; préparation et empreinte dans `integrated-navigation-height-grid-regression-001/proof.json`. Il reste à exécuter et à établir son RED sur l'ancien provider avant de le qualifier comme régression.

Les handles 89348 (replay natif physique corrigé) et 21970 (nouvelle régression) ont été vérifiés encore actifs. Le premier compile forge_engine ; le second attend le verrou partagé de Cargo. Aucun redémarrage et aucun PASS déduit de l'attente. Le changement de provider est couvert par le fingerprint existant build.rs, donc un futur candidat doit être reconstruit et qualifié sur sa nouvelle empreinte. Aucun code propriétaire ni candidat public changé. Gate 3 ouverte, non qualifiée ; publication absente.


### Actualisation 148 — variants synthétiques de régression RED/GREEN préparés

La préparation 001 utilisait le minimum de la tuile sans passerelle, c'est-à-dire sa phase initialement passante. Elle est conservée et son résultat reste en attente. La variante 002 reprend le minimum réel après ajout de la passerelle (2,8936577 m avant rasterisation, marge initiale de 0,1 m ajoutée à l'entrée synthétique). Elle vérifie la même traversée entre sols inchangés après ajout d'une petite surface basse distante. Session 98213 lancée, actuellement en attente du cache partagé.

La version RED utilise exactement le même test comportemental et les mêmes minima que 002 avec une copie inchangée du provider original qualifié. Préparation : `integrated-navigation-height-grid-regression-red-001/proof.json` ; essai lancé. Une erreur de compilation ne comptera pas comme RED et un PASS de l'ancien provider invaliderait la pertinence de ce test. Aucun résultat de ces variantes n'est encore déclaré validé.

Replay physique corrigé 89348 toujours actif, en compilation forge_engine. Aucun redémarrage. Candidat public et checkout propriétaire inchangés ; Gate 3 ouverte, non qualifiée, sans publication.


### Actualisation 149 — correction de navigation intégrée, replay public restant

Sessions 89348, 21970 et 98213 terminales PASS. Le replay physique du PNJ réel franchit z=-134,4 avec les trois passerelles intactes. La régression 002 passe avec le provider corrigé ; session 40773 terminale Cargo 101 sur la même assertion avec le provider original, `NoPathFound` (pas une erreur de compilation). Lecteur exécuté : `integrated-navigation-height-grid-correction-root-review-001/proof.json`, vérifiant sources, logs, cas RED/GREEN et retour physique frais. Session 21366 terminale PASS : sept contrats du provider corrigé.

La crate navigation du checkout propriétaire Rust était propre et le provider correspondait exactement à la base vérifiée. Intégration ciblée de deux fichiers uniquement : `crates/forge_navigation/src/provider.rs` (minimum vertical aligné au pas mondial radius/4) et nouveau `crates/forge_navigation/tests/vertical_tile_lattice.rs` (régression comportementale sans package). Inventaire avant/après et preuve : `navigation-height-lattice-integration-packet-001/apply-proof.json`. Aucun autre fichier de cette crate changé, aucune modification préexistante écrasée. Validation des sept contrats et de la régression dans le checkout propriétaire lancée, session 16286 ; résultat encore en attente.

Cette intégration ciblée ne constitue pas l'intégration complète des travaux précédents ni la qualification du candidat public actuel : celui-ci conserve son ancien provider. Reconstruction du candidat sur une source consolidée, machine checks et replay public navigation restent requis. Les autres échecs physiques, P05 et la décision de maintenance restent à traiter. Gate 3 ouverte, non qualifiée, aucun commit/push dans cette actualisation.


### Actualisation 150 — validation propriétaire ciblée PASS et nouvelle source propre 007

Session 54749 terminale PASS : sept contrats de navigation et une régression sur le checkout Rust, après normalisation des seules fins de ligne des deux fichiers intégrés. Les octets précédents sont archivés dans `primary-navigation-height-lattice-validation-002/normalization-proof.json`. Le diff ciblé ne présente plus d'erreur d'espacement. Résultats : `primary-navigation-height-lattice-validation-002/run-proof.json` et journal épinglé.

Nouvelle copie propre `integrated-primary-rehearsal-source-007`, commit privé de snapshot `cf83bd9148fe513be39b63832f28307cfaf326ba`, parent `c1fad943054f2f9bd45e9f2aa63ba7cfe72c6872`. Inventaire de 2 494 fichiers épinglés ; différence limitée à provider.rs et nouveau vertical_tile_lattice.rs. Preuve : `integrated-navigation-height-grid-source007-preparation-001/proof.json`. Ce commit existe uniquement dans la copie de qualification ; aucun commit du checkout propriétaire ni push. Ce snapshot ne consolide pas les autres évolutions du checkout propriétaire.

Qualification neuve lancée, session 7721 : clippy strict des huit bibliothèques WASM, check natif workspace tous targets/features, clippy natif strict, tests workspace avec les fixtures archivées et contrôlées. Étape actuelle wasm-strict, résultats à examiner. Source, HEAD, arbre propre et fixtures sont reguardés ; tout échec interrompt la chaîne. Aucun PASS du snapshot précédent transféré à 007. Replays ignorés ciblés, nouveaux binaires et replay public navigation restent à faire. Gate 3 ouverte, non qualifiée, sans publication.


### Actualisation 151 — échec Clippy conservé, correction des littéraux et qualification source008

Session 7721 terminale FAILED_CLOSED : WASM strict PASS et formatage PASS ; Clippy natif tous targets/features échoue sur deux `unreadable_literal` du nouveau test. Les tests workspace de cette chaîne n'ont pas été lancés. Preuve conservée : `integrated-source007-machine-qualification-001/proof.json`. Correction de l'actualisation 150 : l'étape nommée native-check exécutait `cargo fmt --all --check`, pas un check de compilation natif distinct. Clippy natif couvre la compilation de son périmètre mais n'est ici pas passé.

Seuls les deux littéraux du test propriétaire ont été réécrits avec séparateurs, sans changer leur valeur ni les assertions. Original archivé ; session 23820 terminale PASS, sept contrats et une régression : `primary-navigation-height-lattice-validation-003/run-proof.json`. Le provider reste inchangé. La première préparation de source008 a refusé la comparaison d'octets à cause des fins de ligne CRLF du checkout Git, avant toute modification des sources clonées ; reprise sur le même clone après vérification HEAD/arbre propre et comparaison normalisée.

Source008 propre : `4292a37233b534869fdca5a12691388f76202027`, parent source007, 2 494 fichiers épinglés. Diff Git : deux lignes du test uniquement. Preuve : `integrated-navigation-height-grid-source008-preparation-001/proof.json`. Commit privé de la copie de qualification, aucun commit ni push du checkout propriétaire. Qualification neuve lancée session 40537, huit bibliothèques WASM strictes, formatage, Clippy natif workspace strict tous targets/features, puis tests workspace avec les 259 fixtures contrôlées. Le lecteur préparé distingue désormais explicitement le résultat de formatage sans événements compiler-artifact.

Aucun résultat source007 transféré comme verdict source008. Replays ignorés, reconstruction et replay public corrigé restent requis ; consolidation complète propriétaire, autres échecs physiques, P05 et décision de maintenance restent ouverts. Gate 3 ouverte, non qualifiée ; aucune publication.


### Actualisation 152 — contrôles stricts source008 PASS, dépendances et échec rivage relus

Source008 : WASM strict, formatage et Clippy natif strict tous targets/features terminés avec exit 0 et source stable. Journaux et empreintes : `integrated-source008-machine-qualification-001/{wasm-strict,native-check,native-strict}-proof.json`. Les tests workspace compilent encore, session 40537 vérifiée active ; aucun verdict global anticipé. La chaîne et le lecteur distinguent le formatage d'un check de compilation.

Relecture des 22 entrées de dépendances et des rapports/outils/bases archivés exécutée : `integrated-source008-dependency-input-review-002/proof.json`. Dix-huit entrées sont identiques octet pour octet ; quatre diffèrent uniquement par LF/CRLF (Cargo.lock, Cargo.toml, deny.toml, manifeste du vendor landmass_rerecast). Comparaison des textes normalisés et objets TOML égale, source006 archivées vérifiées contre leurs hashes. L'échec de comparaison byte-only 001 est conservé. Aucun nouvel audit/fetch effectué, aucun avis de vulnérabilité le plus récent revendiqué ; l'audit capturé du 4 octobre reste explicitement daté.

Scripts de suivi source008 préparés sans exécution : onze replays exacts et leur lecteur ; frontend/native dans `integrated-source008-studio-build-preparation-002`, avec compteur frontend corrigé de 2 493 à 2 494 avant tout build. La préparation précédente 001 conserve son compteur frontend ancien et ne doit pas être exécutée. Lanceur frontend candidat019 : `integrated-source008-studio-launch-preparation-001/build-frontend.py`, bloqué volontairement sur PASS machine relu et onze replays relus, puis contrôle des 2 500 imports figés. Parité exacte des entrées frontend/native obligatoire.

Ancien ShorePush source018 relu : `integrated-public-shorepush-failure-review-018/proof.json`. Échec sur l'approche avant poussée/eau, 1 473 échantillons tous prêts et capture d'entrée active, déplacement horizontal effectif non nul. Minimum observé 0,6173875 m pour tolérance 0,6 m ; positions parcourues X 29,61–30,97, Z -110,98–-107,22. Le journal et les empreintes candidat/scripts/transcript sont contrôlés. Ce constat ne prouve ni un défaut moteur ni un défaut du harnais. Aucun seuil élargi et aucun PASS ; nouveau replay requis sur source008.

Gate 3 ouverte, non qualifiée. Navigation publique corrigée, parcours environnemental, R3-B, P05, décision de maintenance et consolidation complète propriétaire restent ouverts. Aucune publication Git.


### Actualisation 153 — chaîne source008 vers candidat019 et replay public lancée

Session 40537 vérifiée active : les tests workspace compilent forge_studio/forge_cli après forge_engine. Aucun redémarrage. Les dix scripts suivants ont été contrôlés syntaxiquement et épinglés dans `integrated-source008-qualification-to-public-chain-001/started-proof.json` : lecture machine, onze replays ignorés, lecture ignorés, build frontend019, lecture frontend, build natif019, lecture candidat, revalidation Studio publique019, lecture authoring et replay navigation public019.

Chaîne lancée session 26214 ; elle attend le fichier terminal de la session machine actuelle, exige PASS complet et le HEAD source008, puis exécute les étapes séquentiellement et s'arrête au premier échec. Aucune compilation concurrente de suivi n'est lancée avant le résultat des tests workspace. Journaux par étape et preuves d'entrée/sortie ; aucun ancien verdict runtime repris. Les lecteurs des builds contrôlent les octets produits, parité frontend/native et les 2 500 imports.

Revalidation préparée depuis les helpers figés de Studio et les 65 fichiers du checkpoint parent : `integrated-public-runtime-preparation-019/proof.json`. Elle exige le candidat019 effectivement construit et relu. Le lecteur authoring019 revérifie 22 formulaires, sauvegarde identique et fermeture normale. Le replay navigation019 utilise les helpers contact007 épinglés, fenêtre de navigation 60 s et critères précédents inchangés ; aucune preuve native transférée comme verdict public. Son résultat command-pass restera à relire avant qualification.

Cette chaîne ne clôt pas la Gate : replays RPG/physiques restants, deux approches, P05, décision de maintenance et consolidation complète du checkout propriétaire restent à traiter. Aucun sous-agent, commit du checkout propriétaire ni push ; Gate 3 ouverte, non qualifiée.


### Actualisation 154 — lecteur public navigation019 préparé et brouillon propriétaire rebasé

Sessions 40537 et 26214 vérifiées actives. Les tests workspace avancent vers les binaires forge_runtime ; la chaîne de suivi attend toujours leur résultat terminal. Inspection des processus Cargo/rustc/link effectuée sans interruption. Aucun verdict global machine ni replay public nouveau revendiqué.

Lecteur `integrated-public-navigation-root-review-019/run-review.py` préparé, non exécuté : vérifie les empreintes des helpers réellement archivés avant leur évaluation, relit poses KCC, NoPath intérieur fermé, mouvement extérieur et franchissement des tuiles, sortie par porte, Reached, révisions et reçus Save. Il reprend les contrats précédents, mais utilise le nombre réel de captures au lieu du nombre historique fixe 33, ainsi que le hash natif effectivement vérifié du candidat019. Fermeture du processus et conservation des sources restent obligatoires. La chaîne lancée reste inchangée ; cette lecture sera lancée après un éventuel PASS command navigation.

Les trois chevauchements du brouillon 004 ont été reguardés contre le checkout propriétaire actuel : fish_props_scene.rs et forge_assets/lib.rs correspondent toujours ; authoring_imported_shadows.rs a changé (diagnostics d'horloge séparés, delta plafonné et vent partagé). Brouillon privé 005 : `integrated-source008-overlap-reconciliation-005/proof.json`, ajout du seul bloc KCC qualifié au nouveau fichier propriétaire, retrait de ce bloc restituant exactement son texte. Les changements propriétaire sont préservés ; les deux autres brouillons sont retenus sans écrasement. Aucun fichier propriétaire ni source008 changé. Brouillon non appliqué, non compilé ; nouveau contrôle des empreintes et qualification complète nécessaires avant intégration.

Gate 3 ouverte, non qualifiée ; aucune publication Git ni sous-agent.


### Actualisation 155 — source008 machine et onze replays PASS relus ; frontend019 en reconstruction

Session 40537 terminale exit 0. Lecteur machine exécuté : `integrated-source008-machine-root-review-001/proof.json`, HEAD `4292a37233b534869fdca5a12691388f76202027`, 2 494 sources vérifiées ; WASM strict huit bibliothèques (717 événements compiler-artifact, zéro diagnostic compiler-message), formatage PASS et Clippy natif workspace tous targets/features strict PASS (901 événements compiler-artifact, zéro diagnostic compiler-message). Tests workspace : 1 660 PASS, zéro FAIL, 16 ignorés ; 100 résumés de tests relus, incluant les doc-tests. Les avis textuels de future incompatibilité des dépendances restent dans les logs, distincts des événements de diagnostic comptés.

Onze replays exacts exécutés sur trois nouveaux exécutables immuables copiés depuis cette compilation, puis relus : `integrated-source008-ignored-root-review-001/proof.json`. Les 1 232 entrées de fixtures et les octets des binaires/journaux sont contrôlés. Onze PASS, aucun ancien verdict transféré ; deux tests optionnels vault et trois probes CPU restent NOT RUN, sans dérogation propriétaire revendiquée. Pas d'exécution GPU ou publique inférée de ces tests.

Chaîne 26214 vérifiée active, étape 4 : reconstruction du frontend019. Préflight `integrated-source008-studio-launch-preparation-001/preflight-proof.json` exige PASS machine et ignorés relus, dépendances vérifiées, sources stables et 2 500 imports figés. Build frontend en cours, aucun PASS de build anticipé ; lectures frontend/natif, candidat019 et replays publics suivent uniquement en cas de réussite. Source008 ne consolide pas tous les changements propriétaire et son PASS machine ne qualifie pas le checkout propriétaire global.

Gate 3 ouverte, non qualifiée : navigation publique corrigée et autres parcours RPG/physiques restent à rejouer ; P05, décision de maintenance et consolidation complète restent ouverts. Aucun sous-agent ni publication Git.


### Actualisation 156 — replay RPG neuf préparé après la navigation019

Chaîne 26214 vérifiée active ; dernière étape observée 4 : build-frontend.py. Les résultats de source008 restent machine/fixtures uniquement tant que les nouveaux builds et replays n'ont pas terminé.

Préparation `integrated-public-p01-preparation-019/proof.json` : helpers contact007 et manifeste de 416 fichiers contrôlés, replay complet village–forêt–intérieur, cinq attaques avec contacts réels, quête/pickup, Save puis Resume dans un nouveau processus. Lecteur019 adapté au HEAD et candidat réellement construits ; vérifie launch/log, cinq reçus de dégâts de 20, sauvegarde, traversée intérieure, conservation des sources et fermeture normale. Critères/routes inchangés, aucun ancien résultat repris. La seconde approche environnementale reste séparément requise et n'est pas satisfaite par ce seul replay.

Suite lancée session 91775 : `integrated-source008-navigation-to-rpg-chain-001/started-proof.json`. Elle attend un PASS terminal de la chaîne 26214, relit ensuite les observations de navigation019 et le Save, puis exécute le replay RPG019 et son lecteur. Trois étapes séquentielles, scripts et entrées épinglés, arrêt au premier échec ; aucun replay concurrent de la chaîne précédente.

La capture de reprise018 a été regardée de nouveau : la grande capsule bleue masque une partie importante du viewport. Le code de projection générique exclut le joueur de ses capsules de PNJ ; cela renseigne le type de marqueur, pas l'identité du personnage montré ni la qualité du futur rendu019. Aucun changement visuel ni acceptation P05 inféré. Partie complète à observer sur le candidat final.

Gate 3 ouverte, non qualifiée : replays environnementaux/R3-B, P02–P04 sur nouveau candidat, P05, décision de maintenance et consolidation complète restent requis. Aucun sous-agent ni publication Git.


### Actualisation 157 — quatre parcours physiques019 et lectures causales préparés

Chaînes 26214 et 91775 vérifiées actives ; reconstruction frontend019 encore à l'étape 4 de la première, seconde en attente. Aucun build PASS ni nouveau replay public revendiqué.

Préparation `integrated-source008-world-replays-preparation-001/proof.json` exécutée : runner des quatre scénarios EnvironmentQuest, ForestThrow, ForestDanger, ShorePush ; lecteur causal, couverture combinée des huit critères R3-B et lecteur des deux approches de quête adaptés au HEAD source008 et candidat019 effectivement construit. Les hashes natifs historiques sont remplacés par celui du candidat019 relu, et les nouveaux logs/launch proofs sont contrôlés. Helpers contact007 et lecteur causal archivés restent épinglés, contrôles de contact, sauvegarde/reprise, conservation des sources et fermeture normale inchangés.

Ces scripts sont préparés, non exécutés : ils exigent le P01 neuf et l'authoring019 relus, sans transfert de verdict source018. La seconde approche exige toujours neutralisation environnementale avant combat, conséquence persistante et reprise ; les huit critères R3-B ne sont pas qualifiés par cette préparation. Gate 3 ouverte, non qualifiée ; P02–P04 neufs, P05, maintenance et consolidation complète restent requis. Aucun sous-agent ni publication Git.


### Actualisation 158 — P02–P04 sur candidat019 préparés, frontend encore actif

Sessions 26214 et 91775 vérifiées actives ; rustc travaille sur forge_studio_frontend avec activité CPU mesurée. Aucun redémarrage, nouveau PASS build ni résultat public revendiqué.

Préparation `integrated-source008-p02-p04-preparation-001/proof.json` : runner neuf et lecteurs des retouches compatibles P02, diagnostic invalide P03 et isolation P04, liés au P01 et à l'authoring019 relus. Helpers retouch010 épinglés ; P02 conserve son timeout opérationnel de 300 s précédemment explicité, P03/P04 conservent 120 s. Tolérances, comparaisons des sources, refus ordinaire/Resume compatible, sélection du diagnostic et départ neuf isolé restent inchangés. Capture read-only de fermeture des seuls processus du candidat préparée ; aucun arrêt forcé. Ces scripts sont préparés, non exécutés, et n'héritent d'aucun verdict source018.

Gate 3 ouverte, non qualifiée ; nouveaux replays publics, consolidation complète, P05 et décision de maintenance restent requis. Aucun sous-agent ni publication Git.


### Actualisation 159 — frontend019 PASS relu, branche privée corrigée et build natif repris

Frontend019 et worker de navigation construits, puis relus : `integrated-studio-frontend-root-review-019/proof.json`, 2 494 sources et 2 500 imports vérifiés. WASM frontend `6c7503e0c1a86e8874dbf531408652294fab15499faeb3f95e340d2564b1730b`, worker `16d8ec5846eb742cbd6995e22f71178c4d727ed467235c2388ac598587462425`. Build-only ; aucun verdict public/GPU de parcours déduit.

Chaîne 26214 terminale FAILED_CLOSED à l'étape 6, avant compilation native : `Build-StudioCandidate.ps1` exige la branche Rust ; le clone privé était en HEAD détachée. Journaux et proof conservés. Chaîne dépendante 91775 également terminale FAILED_CLOSED sur ce résultat ; aucun replay public/RPG lancé.

Réparation `integrated-source008-native-branch-repair-001/proof.json` : création de la seule référence de branche Rust dans le clone privé source008, HEAD inchangé `4292a37233b534869fdca5a12691388f76202027`, 2 494 empreintes source inchangées, arbre propre. Aucun fichier/code propriétaire changé, aucun nouveau commit source ni publication. Le garde de branche du builder reste intact, aucune dérogation au contrôle.

Nouveau build natif lancé session 21476 : `integrated-studio-native-launch-preparation-019-retry-002/build-native.py`, frontend019 vérifié réutilisé, nouveaux journaux et refus d'écraser une sortie existante. Lecteur candidat retry002 préparé, non exécuté. Build en cours ; les chaînes arrêtées ne sont pas considérées encore actives et les suites publiques seront reprises depuis des preuves neuves après réussite. Gate 3 ouverte, non qualifiée ; aucune publication Git ni sous-agent.


### Actualisation 160 — candidat019 et authoring public relus ; relance navigation corrigée

Build natif session21476 terminal PASS. Lecteur réellement exécuté `integrated-studio-candidate-root-review-019-retry-002/review.py` : 2 494 sources, 2 500 imports, 11 fichiers kit et 2 529 fichiers frontend emballés vérifiés. Candidat019 SHA-256 `c9f59d62d1a063e2e32155d0a052d000b0bdc3d8fa62417734ff6f0c19d9bb06`. Copie exacte de son reçu dans le chemin canonique root-review-019 ; le lecteur originel inutilisé n'est pas présenté comme exécuté. Build-only, aucun verdict de parcours déduit.

Revalidation publique019 PASS et relue : `integrated-public-revalidation-root-review-019/proof.json`, 22 formulaires publics, 65 fichiers fixture, checkpoint complet inchangé et fermeture normale. Capture Studio avant chargement 3D importé : aucune acceptation visuelle P05 déduite.

Continuation11208 terminale FAILED_CLOSED étape5 : helper navigation contact007 lit `navigation_actors` absent des deux ledgers018 et019 ; arrêt avant lancement de l'application. Aucune sortie navigation001 créée et aucun P01/world/P02–P04 exécuté. Journaux et preuves conservés.

Préparation `integrated-public-navigation-preparation-019-retry-002/preparation-proof.json` : copie figée des 416 helpers, seul le script navigation remplacé par les octets archivés réellement exécutés sur018-nav002 (SHA `501e63069b8e19a4f6bd1ad2b4d07467732f5eba99c3dd2666a47d24b79e5bc4`). Ce script extrait les deux acteurs du checkpoint canonique après vérification des hashes ; ni champ ajouté au ledger, ni changement produit, ni résultat018 hérité. Les autres 415 fichiers sont identiques. Relance neuve session17508, sortie navigation002 et lecteurs dédiés ; résultat public en attente au moment de cette actualisation.

Gate 3 ouverte, non qualifiée. Navigation corrigée publique, boucle RPG, parcours physiques, P02–P04, consolidation propriétaire, P05 et décision de maintenance restent à établir. Aucun sous-agent ni publication Git.


### Actualisation 161 — première preuve publique du raccord corrigé ; sortie intérieure encore incomplète

Navigation019-002, session17508 terminale FAILED_CLOSED pendant l'approche du joueur. Relecture `integrated-public-navigation-failure-review-019-002/proof.json` : 16 captures vérifiées, contrôles archivés rejoués pour immobilité NoPath derrière porte fermée, déplacement extérieur réel (16,06 m maximum) et franchissement au sol du raccord z=-134,4 entre [1,-4] et [1,-5]. Première preuve publique du raccord corrigé sur candidat019, sans héritage natif. Scénario complet NON PASS : arrêt joueur (42,63,-141,69) pendant approche, aucune ouverture ni Save. Candidat, fixture et helpers conservés, fermeture normale vérifiée.

Navigation019-003, session63940 terminale FAILED_CLOSED après ouverture : mêmes points, tolérance intermédiaire resserrée 0,75 vers 0,35 m et attente de stabilisation entre segments. Approche et action publique d'ouverture réussies. Relecture `integrated-public-navigation-failure-review-019-003/proof.json` : 114 captures vérifiées, mêmes trois contrôles partiels et révision de tuiles après Open rejoués PASS. PNJ intérieur passe en Moving puis reste au seuil ; destination non atteinte dans 60 s, fermeture et Save non exécutés. Dernière séparation horizontale joueur–PNJ environ 0,82 m ; hypothèse d'évitement, pas une preuve causale. Fermeture normale vérifiée, captures et échecs conservés.

Relance neuve navigation019-004, session17028 : `integrated-public-navigation-preparation-019-retry-004/preparation-proof.json`. Recul public du joueur vers south_clear_z après ouverture, avec le helper de pulses stabilisées déjà utilisé par le scénario RPG ; retour public vers exterior_z avant fermeture. Aucun acteur déplacé artificiellement, aucune modification géométrique ou produit, mêmes critères de sortie/Reached/révisions/Save et même fenêtre 60 s. Résultat en attente au moment de l'actualisation ; aucune réussite anticipée.

Gate 3 ouverte, non qualifiée. Navigation complète, nouveaux P01/parcours physiques/P02–P04, consolidation propriétaire, P05 et maintenance restent requis. Aucun sous-agent ni publication Git.


### Actualisation 162 — recul joueur et destination PNJ observés ; capture du passage manquante

Navigation019-004, session17028 terminale FAILED_CLOSED. Relecture `integrated-public-navigation-failure-review-019-004/proof.json`, session31976 terminale PASS partiel : 114 captures vérifiées, immobilité porte fermée, extérieur/raccord, révision après Open et trois observations finales Reached rejouées avec les mêmes fonctions archivées. Après recul public du joueur, le messager atteint (49,00088 ; 6,69414 ; -150,20689). Sources/fixture/helpers stables et fermeture normale vérifiée. Aucune sauvegarde ni fermeture de porte exécutée.

Le contrôle de traversée intérieure reste NON PASS : le premier échantillon de phase ouverte est au tick4272, acteur déjà Reached dehors ; aucune capture dans l'encadrement pendant le recul joueur. L'échec est conservé et le contrôle inside/doorway/outside n'est pas assoupli. Le résultat indique que le recul permet l'arrivée dans cette exécution ; aucune isolation causale complète du mécanisme d'évitement n'est revendiquée. Prochaine action : échantillonner les observations publiques pendant les pulses de recul et inclure ces échantillons frais dans le même lecteur de passage, sans modifier les positions, profils, géométrie ou seuils.

Continuation003, session39152 terminale FAILED_CLOSED sur le résultat navigation004, avant toute étape dépendante. Aucun P01, parcours world, P02–P04 exécuté. Tous les processus suivis de cette actualisation sont terminaux ; aucune attente ni replay encore actif déduit des seuls fichiers. Gate 3 ouverte, non qualifiée ; consolidation propriétaire, P05 et décision de maintenance restent également requis. Aucun sous-agent ni publication Git.


### Actualisation 163 — capture pendant le recul ajoutée ; position après freinage désormais contrôlée

Préparation005 ajoute au seul helper navigation des observations publiques avant et pendant les pulses de recul. Copie de la fonction de pulses existante : retrait des deux appels observateur restitue exactement son corps original. Les captures fraîches de la même phase sont validées et incluses dans le même lecteur physique inside/doorway/outside ; aucune tolérance ni seuil du lecteur changé. SelfTest PASS (contrats synthétiques seulement).

Navigation005, session78206 terminale FAILED_CLOSED avant Open ; continuation004/session90852 également terminale FAILED_CLOSED avant toute étape dépendante. Le premier point est atteint à (41,26,-118,21), puis le freinage stabilise réellement le joueur à (42,27,-118,80). Le segment suivant maintient essentiellement W et bute devant le mur à (42,29,-141,69). Le journal expose donc un point atteint avant freinage qui ne reste pas atteint après celui-ci. Relecture `integrated-public-navigation-failure-review-019-005/proof.json`, 17 captures : trois contrats partiels porte fermée/extérieur/raccord PASS, fermeture normale et sources préservées ; aucun verdict intérieur/Save anticipé.

Préparation006 : mêmes points et trajectoire publique, correction/validation du point stabilisé avec le helper inchangé Move-PlaytestSettledPulses et tolérance 0,10 m après chaque segment. Observation pendant recul conservée, acteurs/géométrie/produit inchangés. SelfTest PASS ; nouveau replay session74089 lancé, résultat en attente. Pas de relaxation du lecteur physique ni de verdict hérité.

Gate 3 ouverte, non qualifiée : navigation complète, nouveaux replays RPG/physiques/P02–P04, consolidation propriétaire, P05 et maintenance restent requis. Aucun sous-agent ni publication Git.


### Actualisation 164 — navigation publique019 complète PASS relue ; boucle RPG lancée

Navigation019-006, session74089 terminale PASS. Relecture réellement exécutée `integrated-public-navigation-root-review-019-retry-006/run-review.py` : status `LOCAL_PUBLIC_SOURCE008_NAVIGATION019_PASS_ROOT_REVIEWED`, 64 captures byte-vérifiées, lecteur physique et lecteur Save PASS, fermeture normale indépendante vérifiée. Candidat SHA `c9f59d62d1a063e2e32155d0a052d000b0bdc3d8fa62417734ff6f0c19d9bb06`, HEAD source008 `4292a37233b534869fdca5a12691388f76202027`.

Porte fermée : messager immobile NoPath avec feedback KCC du même pas ; patrouilleur en mouvement et franchissement au sol du raccord des tuiles [1,-4]/[1,-5]. Porte ouverte : captures fraîches pendant le recul du joueur incluent intérieur, encadrement et extérieur, puis trois observations Reached. Révisions après ouverture et fermeture contrôlées. Sauvegarde publique confirmée, deux commandes de porte distinctes et ordonnées, événements cohérents et porte finale fermée ; SHA Save `cd215c9fd17dd566aef0c77d44cb479f9f2cc5c4f876d0585049972ada320553`. Sources/checkpoint cloné préservés, aucun arrêt forcé. Les essais précédents restent archivés FAILED_CLOSED et ne sont pas rétroactivement promus.

Preuve limitée à la patrouille et à la pièce canoniques observées : aucun PASS de navigation de toute la carte, de tous les PNJ, ni qualification globale du checkout propriétaire. Aucun héritage de résultat natif ou source018, aucune acceptation P05 inférée.

Continuation005/session46945 vérifiée active, étape2 `integrated-public-p01-preparation-019/run.py`. Étape1 relecture navigation006 PASS ; boucle RPG neuve lancée. La chaîne prépare ensuite les deux approches, quatre scénarios R3-B et P02–P04, uniquement séquentiellement après réussite, arrêt au premier échec. Aucun résultat futur anticipé.

Gate 3 ouverte, non qualifiée : boucle complète et physique/authoring restants, consolidation propriétaire, P05 et maintenance restent requis. Aucun sous-agent ni publication Git.


### Actualisation 165 — combat et approche environnementale PASS ; lancer causal002 PASS

P01 public019 : relecture réellement exécutée `integrated-public-p01-root-review-019/proof.json`, cinq contacts infligeant chacun 20 dégâts, quête terminée sauvegardée/reprise dans un nouveau processus, visite de l'intérieur canonique et porte fermée après sortie. EnvironmentQuest019 : neutralisation environnementale avant combat, quête terminée sauvegardée/reprise et dégâts d'impact vérifiés par le lecteur causal archivé. Ces preuves portent sur le candidat019/source008 et le checkpoint Studio019 ; aucune qualification des modifications ultérieures du checkout propriétaire n'est inférée.

Continuation005/session46945 terminale en échec à l'étape7 : ForestThrow001 libère et persiste l'objet lancé, mais échoue aux dégâts d'impact, piège/chute, destruction et persistance du piège. Diagnostic archivé `integrated-public-forestthrow-failure-review-019-001/proof.json` : l'approche précise expirait puis le script lançait malgré ce refus, avec une impulsion environ 22 degrés hors axe. Échec conservé ; aucune assertion physique affaiblie.

ForestThrow002/session69883 terminale PASS, puis relecture causale réellement exécutée : lancer, dégâts d'impact, chute/piège, destruction, état de l'objet lancé et états du piège sauvegardés/repris PASS. Le harnais utilise l'approche stabilisée existante, la même cible et la tolérance de 0,025 m ; le refus d'approche interdit désormais le lancer. Produit, géométrie, dégâts et lecteur causal inchangés. Preuve : `tidewater-integrated-primary-019-public-forestthrow-002/root-causal-proof.json`.

Continuation006/session22867 lancée séquentiellement après ce résultat ; ForestDanger observé en cours. Les contrôles feu/IA, poussée/eau, synthèse des huit critères R3-B, deux approches et P02–P04 restent soumis aux résultats réellement obtenus. Aucun résultat futur anticipé.

Consolidation : audit frais `integrated-source008-primary-content-drift-review-002/proof.json`, 1218 différences exclusivement LF/CRLF préservées, 255 deltas candidat sur base propriétaire inchangée, 50 changements propriétaire préservés, 32 chemins exclusivement propriétaire, huit chevauchements réconciliés dans une copie privée. `integrated-source008-overlap-reconciliation-006/proof.json` : huit fichiers formatés, sans application au checkout propriétaire ni compilation de l'ensemble réconcilié. Un nouveau contrôle des hashes propriétaire est obligatoire avant intégration.

P05 : observations de captures limitées consignées ; elles ne prouvent pas une partie complète lisible ni une acceptation propriétaire. ADR0021 reste en attente de décision propriétaire. Gate 3 ouverte et non qualifiée ; consolidation globale, critères restants et revue humaine requis. Aucun sous-agent ni publication Git.


### Actualisation 166 — deux approches P01 relues ; échec de trajet feu ; copie combinée009 en qualification

Relecture réellement exécutée `integrated-public-p01-two-approaches-root-review-019/review.py` : `LOCAL_SOURCE008_R3_P01_TWO_APPROACHES_PASS_ROOT_REVIEWED`. Combat avec cinq reçus de contact, approche environnementale neutralisant l'ennemi avant combat, objectifs terminés sauvegardés/repris, visite de l'intérieur canonique et liens village/forêt/intérieur contrôlés sur candidat019/source008. Aucune acceptation humaine P05 inférée.

Continuation006/session22867 terminale en échec à l'étape2. ForestDanger001 échoue avant allumage au trajet `forest-fire-northwest-flank-waypoint-2-step-1` : cible (22,33 ; -264,98) non atteinte en 42 s ; position enregistrée environ (21,72 ; -264,63), contact terrain, joueur au sol, aucune action feu dispatchée. Fermeture normale, candidat/harnais/source/checkpoint préservés ; échec archivé et relu dans `integrated-public-world-preparation-019-retry-003/failure-review.json`. Aucun défaut produit isolé par cette seule trace, aucun PASS feu/IA.

ForestDanger002/session66764 lancé sur le même candidat, mêmes waypoints et tolérances, avec l'approche stabilisée existante et touches relâchées entre impulsions. Le lecteur causal et les contrats de feu/dégâts/IA restent inchangés ; géométrie et acteurs inchangés. Résultat réel puis relecture requis avant verdict. ShorePush, synthèse R3-B et P02–P04 non exécutés dans cette continuation arrêtée.

Le garde de consolidation a refusé une modification propriétaire supplémentaire dans `tidewater_clouds/live.rs` avant toute copie. Stage007 conserve exactement le champ propriétaire `atmosphere_frames: None` et les annotations de tests qualifiées. Audit propriétaire frais003 puis copie combinée009 réalisée : 2527 fichiers byte-vérifiés, dont 2264 préservés depuis Primary, 255 deltas source008 et huit chevauchements réconciliés. Provenance fichier par fichier dans `integrated-primary-rehearsal-source009-preparation-002/file-provenance.json`. Aucun fichier du checkout propriétaire ni de source008 modifié par cette copie.

Qualification machine009/session8464 lancée : `cargo fmt --all --check` PASS, sources stables ; contrôle WASM strict observé en cours. Les compilations natives et tests globaux suivront seulement après réussite des étapes précédentes. Cette copie009 n'est pas encore compilée intégralement, n'a aucun candidat public qualifié, et n'est pas appliquée à Primary. Les résultats publics019/source008 ne sont pas transférés à009.

Gate 3 ouverte et non qualifiée. Restent les critères publics non vérifiés, la qualification de la consolidation, la revue complète P05 et la décision propriétaire ADR0021. CI distante NOT RUN sous ADR0006 ; aucun sous-agent, commit de Primary ni publication Git.


### Actualisation 167 — copie009 stricte refusée puis corrigée en privé ; suite publique007 en attente

Qualification machine009/session8464 terminale FAILED_CLOSED au WASM strict (exit101), après formatage PASS. Trois diagnostics Clippy réels : `chunks_exact_to_as_chunks` dans `physics/manual_triangle.rs`, `too_many_lines` dans `tidewater_clouds/validation.rs` (102 lignes) et `tidewater_object_shadow/graph.rs` (165 lignes). Les étapes natives/tests n'ont pas été exécutées. Logs et preuve001 conservés.

Correction privée `integrated-source009-style-fix-001/proof.json` : indexation par tableaux de trois indices après les mêmes gardes, extraction des validations de textures atmosphériques et des passes de profondeur avec le même corps et le même ordre d'appel. Trois fichiers seulement modifiés et avant/après archivés ; aucune modification de Primary/source008, aucune suppression de garde ni d'assertion. Formatage des trois fichiers PASS. Qualification machine009-002/session98052 lancée sur le nouveau manifeste : résultat réel et relecture encore requis, aucune qualification héritée du refus précédent.

Continuation publique007/session10710 attend le résultat réel de ForestDanger002/session66764, confirmée active. Après PASS du command runner, relecture causale du feu/IA, puis ShorePush, synthèse R3-B et P02–P04 séquentiels, arrêt au premier échec. La relecture des deux approches P01 déjà exécutée n'est pas répétée. Les lecteurs R3-B pointent explicitement vers ForestThrow002 et ForestDanger002, sans changement de critères. Aucun PASS futur anticipé.

Gate 3 ouverte/non qualifiée ; P05 et décision propriétaire ADR0021 restent distincts des essais automatisés. Aucun sous-agent ni publication Git.


### Actualisation 168 — feu002 refusé après allumage ; treize diagnostics Studio009 ; poussée/eau lancé

ForestDanger002/session66764 terminale FAILED_CLOSED : trajet stabilisé et préflight d'allumage atteints, mais joueur vaincu pendant le recul et Save d'exposition incapable d'attester des dégâts réels sur un joueur vivant. Chronologie réellement relue `integrated-public-forestdanger-failure-review-019-002/proof.json` : première touche acquittée 4081,5114 ms après le clic d'allumage, budget inchangé 1000 ms, vitalité finale0. Capture/reprise/persistance ne sont pas promues PASS. Fermeture normale, sources et scripts byte-vérifiés ; aucun défaut produit isolé par cette seule trace. Continuation007/session10710 terminale FAILED_CLOSED avant toute relecture causale ou étape suivante. Pas de nouveau replay feu lancé sans diagnostic.

Machine009-002/session98052 terminale refusée au WASM strict : conversion de collection et fonction draw encore109 lignes. Correction privée002 : to_vec sur les mêmes triangles validés et extraction exacte de mise à jour du mouvement végétal, avant passes de profondeur. Machine009-003/session32606 réellement exécutée puis terminale FAILED_CLOSED : treize diagnostics dans forge_studio, archivés dans `current-campaign-progress-168/source009-studio-diagnostics.json` ; visibilité d'interface, dead-code configure, cast de longueur, must_use, conditions imbriquées, motifs unit, complexité de type, import wildcard, longueur VaultActions et documentation. Formatage PASS, sources stables, étapes natives/tests non exécutées. Les refus précédents restent archivés ; aucune compilation globale PASS de009.

ShorePush019/session5645 lancé séparément sur candidat019/source008 et le même checkpoint public. Aucun build en concurrence avec ce replay. Résultat réellement obtenu et relecture poussée/eau encore requis ; P02–P04 restent non exécutés sur019.

Gate 3 ouverte/non qualifiée. P01 deux approches et lancer/piège/destruction002 gardent leurs preuves limitées PASS ; feu/IA, poussée/eau, authoring restant, consolidation009, P05 et décision ADR0021 restent ouverts. Aucun sous-agent, modification de Primary par les corrections privées, ni publication Git.


### Actualisation 169 — WASM009 strict et compilation native PASS ; compatibilité native en réparation ; P02 lancé

Corrections privées Studio003/004 : conversion bornée de longueur, visibilité cohérente du catalogue et de son réexport, imports explicites, alias de géométrie, motifs/conditions et documentation ; le wrapper d'aperçu est réutilisé pour le même layout, et la préparation du placement Vault est extraite en gardant le contrôle de révision et Preview/Apply. Aucun changement de Primary. Machine009-005/session69537 réellement exécutée : formatage, WASM strict et compilation native PASS, puis native-strict FAILED_CLOSED. Les tests workspace n'ont pas été exécutés.

Native-strict005 a révélé une incompatibilité d'emprunt dans une fixture, des erreurs de style des sondes, ainsi que des comparaisons flottantes exactes d'horloges source. Correction privée005 : emprunts de fixture, extractions de lecture de référence et de préparation/rendu de sonde, imports explicites, découpage par tableaux, séparateurs de littéraux, enregistrement des systèmes des sondes seulement sous test ; les comparaisons exactes restent exactes avec une annotation locale motivée. Les lecteurs causaux et assertions métier ne sont pas affaiblis.

Machine009-006/session95210 terminale FAILED_CLOSED au native-strict, après formatage/WASM strict/compilation native PASS. Les extractions avaient laissé deux imports manquants et une conversion incorrecte de &[u8;4] : refus archivé puis correction privée006 dans deux fichiers, avant/après byte-archivés. `integrated-source009-style-fix-006/proof.json` est le manifeste courant. Qualification007 préparée mais non lancée, afin de ne pas compiler pendant les replays publics. Aucun PASS global de009 ni candidat public009 ; tests workspace et compilation stricte complète encore requis.

ShorePush001/session5645 terminale FAILED_CLOSED après ordre de poussée accepté, avant contact d'eau. La trace identifie le collider réel `Gameplay object · Feu de rive` à la position X40/Z-32, avec normale horizontale opposée au trajet. Relecture conservée dans `integrated-public-world-preparation-019-retry-004/failure-review.json`. Contournement sud préparé sans déplacer/supprimer le collider, même point d'eau (39,47 ; -32,38) et tolérance0,13 m.

ShorePush002/session13016 terminale FAILED_CLOSED avant ordre de poussée : approche précise à (41,5006 ; -32,5640) expirée après12 s, impulsions et dérive du joueur enregistrées, aucune action dispatchée. Relecture conservée dans `integrated-public-world-preparation-019-retry-005/failure-review.json`. Essai003 préparé avec approche stabilisée existante, même offset0,575 m/tolérance0,025 m et contournement sud ; non lancé, aucun PASS poussée/eau inféré.

P02 public019/session36789 lancé sur la sauvegarde P01 effectivement qualifiée. Continuation008/session4022 attend son résultat réel, puis collecte la fermeture et exécute le lecteur P02, P03 et P04 séquentiellement avec arrêt au premier échec. Aucun résultat feu/eau anticipé ou requis artificiellement pour ces contrôles indépendants. Aucun résultat futur de retouche/diagnostic/isolation anticipé.

Gate 3 ouverte/non qualifiée. P01 et lancer/piège/destruction conservent leurs preuves limitées ; feu/IA, poussée/eau, P02–P04, consolidation009, revue complète P05 et décision propriétaire ADR0021 restent ouverts. Aucun sous-agent ni publication Git.


### Actualisation 170 — 5 octobre : P02 et P03 publics019 PASS Root ; correction du contrôle natif009

État courant de l'intégration : candidat019 construit depuis Source008 privée qualifiée, puis authoring, navigation bornée, P01 deux approches, lancer/piège/destruction, P02 et P03 vérifiés publiquement. Les résultats V13/Source9 ci-dessus sont historiques ; ils ne remplacent pas la qualification du candidat019 ni de la nouvelle consolidation Source009.

P02/session36789 termine PASS commande, mais continuation008/session4022 s'arrête sur le lecteur P02. Le lecteur ne recueillait que la phase `door_threshold-door`, et omettait la phase réelle suivante `cross_interior_kcc`. Le journal original contient le franchissement ordonné extérieur→intérieur, KCC positif et contacts au sol intérieur. Nouveau lecteur `integrated-public-p02-root-review-019-retry-001/review.py`, exécuté PASS : seuls les noms de phases acceptés changent ; marges0,35 m, contacts, ticks, sauvegarde/reprise et comparaison brute des sources restent identiques. Échec et correction archivés. Retouche de réplique + déplacement X de porte0,05 m, reprise ordinaire refusée puis reprise compatible explicite ; quête terminée et objet consommé absents des actions dans une nouvelle instance. Aucune acceptation visuelle humaine inférée.

P03/session21818 termine PASS commande et lecteur Root : suppression du Sceau refusée, diagnostic public nomme la quête et la quantité requise, objet sélectionné corrigeable et Apply désactivé. Pas de nouvelle révision ni receipt ; aucun commit de correction revendiqué. Preuve `integrated-public-p03-root-review-019/proof.json`.

Qualification Source009-007/session83895 terminale FAILED_CLOSED : formatage et WASM strict PASS, native-strict refusé sur deux conversions usize→u32 dans une fixture GLB. Correction privée007 utilise les conversions vérifiées, sans modifier les assertions, sources avant/après archivées. Qualification008 préparée, non lancée pendant P04.

RECTIFICATION : les étapes intitulées `native-check` des qualifications Source009001–007 exécutaient en réalité `cargo fmt --all --check`, par mauvaise sélection de commande dans le runner. Les mentions précédentes « compilation native PASS » de Source009 sont retirées. Les journaux restent intacts. WASM strict est démontré, les contrôles Clippy natifs sont des refus, aucun PASS global natif009. Le runner008 prépare désormais un vrai `cargo check --workspace --all-targets --all-features --locked --offline -j1 --message-format=json`, avant Clippy strict et tests workspace. Cette rectification ne retire pas les preuves indépendantes Source008.

P04/session49580 lancé, résultat et revue encore attendus au moment de cette consignation. Aucun build lourd concurrent, aucun sous-agent. Gate3 ouverte/non qualifiée : feu/IA, poussée/eau, P04, consolidation et requalification, P05 complet et décision propriétaire ADR0021 restent ouverts. Aucun changement de code Primary ni publication Git.


### Actualisation 171 — P04 public019 PASS Root ; qualification machine009-008 réellement lancée

P04/session49580 terminale PASS commande, fermeture normale indépendante puis lecteur Root PASS dans `integrated-public-p04-root-review-019/proof.json`. Deux départs publics au point choisi (40 ;-115), déplacement réel de1,8 m puis sauvegarde au premier passage, deuxième nouvelle partie depuis le même départ (pas Resume), révision40 conservée. Documents authorés, objets archivés et stockage de sauvegarde source byte-vérifiés inchangés. Aucune identité bit-à-bit de physique ni revue visuelle humaine revendiquée.

Continuation privée `integrated-source009-after-p04-continuation-001/run.py`, session68329 : les étapes fermeture/revue P04 ont passé, puis le runner machine009-008 est lancé. Formatage et WASM strict ont passé ; vrai `cargo check` workspace/all-targets/all-features en cours à cette observation. Clippy strict, tests workspace et lecteur Root009 encore requis. Lecteur préparé vérifie cinq commandes exactes, artefacts/diagnostics, bilans et tests ignorés, ainsi que les2527 fichiers courants ; non exécuté avant le résultat réel. Aucun replay public simultané au build.

P01–P04 du candidat019 disposent maintenant de preuves Root bornées. Gate3 demeure ouverte/non qualifiée : feu/IA, poussée/eau, consolidation009 et requalification de l'intégration finale, partie complète P05 et acceptation propriétaire ADR0021 encore nécessaires. Aucun code Primary modifié, aucun sous-agent et aucune publication Git.


### Actualisation 172 — compilation native009 et Clippy strict effectivement PASS Root, tests en cours

Qualification machine009-008 : formatage PASS ; WASM strict8libs717artefacts/0diagnostic ; véritable cargo check workspace/all-targets/all-features904artefacts/0diagnostic ; Clippy natif strict904artefacts/0diagnostic. La revue indépendante `integrated-source009-native-check-strict-review-001/proof.json` vérifie les commandes, résultats build-finished, hashes des journaux et2527 fichiers privés. Cette preuve corrige le manque de contrôle natif identifié dans les essais001–007, sans transformer leurs anciennes traces de formatage en compilation.

Session68329 encore active sur l'étape tests workspace : compilation des exécutables de test observée, aucun bilan tests encore disponible et aucun PASS workspace009 revendiqué. Lecteur global prêt dans `integrated-source009-machine-root-review-001/review.py`, à exécuter après le résultat terminal réel. Aucun replay public concurrent au build. P01–P04 publics019 gardent leurs preuves Source008, non transférées au futur candidat009. Gate3 ouverte/non qualifiée ; aucune publication et aucun sous-agent.


### Actualisation 173 — diagnostic des délais feu préparé ; tests009 toujours en cours

La dernière observation de session68329 confirme l'exécution encore active du contrôle workspace009-008 : compilation des exécutables de test, aucune terminaison ni bilan revendiqués. Le résultat machine en cours est conservé ; pas de restart sur observation lente et aucun replay public simultané.

Harnais privé `integrated-public-runtime-harness-019-fire-timing-003`, préparé dans `integrated-public-fire-timing-preparation-019-001/proof.json` et auto-test PowerShell exécuté PASS : instrumentation des commandes CDP à partir du clic Ignite. Les mesures séparent encodage de requête, attente d'envoi et attente/décodage de réponse, avec timestamps et acquittement. Collecte en mémoire, archive ajoutée au ledger après le replay ; aucune écriture de fichier pendant la fenêtre critique. Quota4096 avec compteur d'échantillons perdus visible. Syntaxe des trois fichiers modifiés validée ; les413 autres helpers sont byte-identiques.

Essai ForestDanger003 préparé, non lancé : mêmes gestes, points, budget1000ms, joueur vivant, dégâts réels, Save/Resume, protection de focus et lecteur causal figé. Aucune optimisation de parcours ni changement produit, aucun résultat feu transféré d'un autre candidat et aucun PASS anticipé. Cette instrumentation vise à distinguer les délais de transport/lecture du harnais du comportement runtime, après la mort observée dans002. ShorePush003 garde sa préparation indépendante et attend aussi la fin de la compilation.

Gate3 reste ouverte/non qualifiée, avec P01–P04 publics019 prouvés dans leur portée ; aucun sous-agent, modification du code Primary ni publication Git.


### Actualisation 174 — nouveaux travaux Primary conservés en Source010 privée ; suite physique séquentielle démarrée

Audit frais en lecture seule `integrated-source009-primary-fresh-drift-review-001/proof.json` : douze changements propriétaire depuis la capture de Source009 (huit fichiers modifiés et quatre nouveaux), plus le document de progression modifié par cette conversation. Les évolutions portent principalement sur les horloges océan/nuages, leurs références et scripts d'observation. Aucun chevauchement de correction privée : Source009 contenait les anciennes versions Primary des huit fichiers modifiés. Les fichiers courants sont archivés et leur stabilité avant/après est vérifiée ; aucune écriture sur Primary.

Préparation réelle `integrated-primary-rehearsal-source010-preparation-001/proof.json` : copie privée Source010 de2531 fichiers, corrections privées009-fix007 conservées et treize entrées actualisées depuis les bytes Primary capturés, dont quatre nouveaux fichiers. Provenance par fichier et manifeste SHA archivés. Primary et Source009 inchangées. Source010 n'est ni compilée ni qualifiée ; le PASS strict009 ne lui est pas transféré. Runner machine010 et lecteur Root préparés mais non lancés.

Tests009/session68329 toujours actifs, compilation atteignant Studio/CLI ; processus descendants identifiés en lecture seule, dont rustc forge_engine avec temps CPU réel. Aucune interruption ou relance. Continuation séquentielle `integrated-source009-world-after-tests-continuation-001/run.py`, session34043, lancée : attend le résultat terminal réel de machine009, exécute son lecteur Root si PASS, puis ShorePush003 et ForestDanger003 instrumenté sur le candidat019, avec lecteurs causaux uniquement en cas de succès. Ces replays Source008 sont indépendants du résultat machine009 mais commencent seulement après sa terminaison pour éviter une compilation lourde simultanée. Les échecs restent archivés et aucun résultat futur n'est anticipé.

Gate3 ouverte/non qualifiée. P01–P04 publics019 restent bornés à leur candidat ; intégration finale tenant compte de Primary courant, feu/IA, poussée/eau, P05 et décision propriétaire ADR0021 restent requis. Aucun sous-agent, modification de code Primary ni publication Git.


### Actualisation 175 — décision Rapier actualisée et présentée ; aucun accord inféré

`adr0021-owner-decision-packet-004/decision-packet.md` actualise la proposition Rapier avec les preuves Source008/candidat019, strict009 et copie010 réellement préparée. Les217 fichiers du package sont byte-identiques Source008↔Source010 ; comparaison historique exhaustive : changements LF/CRLF et deux espaces de fin de commentaire dans rigid_body.rs, contrôleur identique après normalisation LF. Les gardes d'identité byte historique puis LF-seule ont refusé les préparations002/003 ; leurs échecs sont conservés, aucun dossier faux publié.

L'accord ADR0009 reçu reste conservé. La question ADR0021 distincte porte sur l'adoption du package Rapier local avec le patch général KCC et sa maintenance jusqu'à correction upstream équivalente. Le dossier concret et les obligations sont présentés au propriétaire ; question asynchrone envoyée, aucune réponse ni acceptation inférée. AGENTS.md34–36 conserve l'exigence d'approbation des changements structurels. Cet accord n'autorise ni clôture de Gate3 ni publication.

Tests009/session68329 et continuation physique34043 toujours actifs aux dernières observations : aucune terminaison observée, aucun restart et aucune revendication de PASS workspace. Source010 reste préparée/non qualifiée. Les contrôles indépendants continuent pendant l'attente de décision ; aucune application Primary du fork avant accord, aucune publication et aucun sous-agent.


### Actualisation 176 — Source010 formatage PASS et checkpoint privé propre ; tests009 actifs

Contrôle exécuté `cargo fmt --all --check` sur Source010 : PASS, journal vide,2531 fichiers vérifiés avant/après inchangés. Preuve `integrated-source010-format-root-review-001/proof.json`, portée formatage uniquement, aucun PASS compiler ou workspace010 inféré.

Checkpoint Git créé uniquement dans la copie privée010 : branche Rust, HEAD83cecdfbb45c726a9007e90e4794c2f3ccbd193e,2531 chemins suivis égaux au manifeste, état propre, aucun remote. Commit racine local de qualification, sans ancestry Primary ; bytes sources inchangés. Identité de commit technique Forge Qualification, aucun commit du dépôt Primary ni publication. Preuve `integrated-source010-private-git-checkpoint-001/proof.json`. Une première commande de préparation avait une erreur de syntaxe Python avant toute exécution ; commande corrigée, les opérations réelles sont journalisées dans le dossier de checkpoint.

Tests009/session68329 encore actifs ; descendant rustc construit maintenant vertical_tile_lattice après compilation des crates moteur/Studio/runtime/CLI. Continuation34043 attend leur résultat terminal avant replays ShorePush003 et ForestDanger003 ; aucune relance ni compilation lourde supplémentaire. Accord ADR0021 toujours en attente de réponse explicite, aucun accord inféré. Gate3 ouverte/non qualifiée, aucun sous-agent.


### Actualisation 177 — poussée/eau003 PASS Root ; tests009 terminaux avec un test de permissions obsolète corrigé

Machine009-008/session68329 terminale FAILED_CLOSED au workspace :1683 tests PASS,1 FAIL,21 ignorés,100 bilans de tests incluant doctests. Échec unique `tests::gate0_capability_grants_no_privileged_tauri_command` : le fichier de capabilities autorise déjà le transfert binaire gouverné `allow-read-asset-model`, mais le test attend encore trois identifiants. Format/WASM/cargo check natif/Clippy strict009 restent leurs PASS réels ; aucun PASS workspace global009. Relecture `integrated-source010-capability-test-fix-001/source009-failure-review.json`.

Correction privée010 : test seul mis à jour pour la liste exacte de quatre identifiants, dont trois commandes applicatives typées et `core:window:deny-set-title`. La lecture modèle utilise IDs/références gouvernés, taille bornée, chemin résolu dans la bibliothèque et empreinte vérifiée ; aucune commande core privilégiée ajoutée et aucun changement production. Formatage PASS. Checkpoint privé0103705f469c393d78b436f72612a349ba7942cc5a5 propre, bytes hors de ce test démontrés identiques.

Même correction limitée appliquée au test existant de Primary : guard exact du bloc ancien, capabilities actuelles vérifiées, sauvegarde byte de main.rs avant écriture, production hors du bloc et capabilities inchangées. Preuve `primary-typed-model-capability-test-correction-001/proof.json`. Aucun fork Rapier intégré, aucune modification des autres retouches propriétaire, aucun commit Primary ni publication. Les tests Primary n'ont pas été exécutés ; le PASS privé futur ne leur sera pas transféré.

ShorePush003 du candidat019 réellement exécuté PASS commande et Root : poussée et extinction au contact de l'eau démontrées par le lecteur causal figé, saves cohérents et reprise en nouvelle instance. Preuve `tidewater-integrated-primary-019-public-shorepush-003/root-causal-proof.json`. La suite34043 est passée au ForestDanger003 instrumenté, actuellement actif, sans build concurrent. Feu/IA encore non qualifié à cette observation.

Suite `integrated-source010-after-world-continuation-001/run.py`, session61253, lancée en attente du résultat terminal des replays physiques : puis machine010-002 et lecteur Root, arrêt au premier refus de qualification. La première préparation machine010-001/lecteur001 reste non exécutée et basée sur l'ancien manifeste ; seuls runner002/lecteur002 utilisent la correction courante. Source010 demeure non qualifiée, ADR0021 en attente de réponse, partie complète P05 et requalification finale encore nécessaires. Aucun sous-agent ; Gate3 ouverte/non qualifiée.

### Actualisation 178 — diagnostic feu003 archivé ; compilation010 corrigée en cours

Continuation monde/session34043 terminale avec refus feu003, après PASS ShorePush003 Root. Revue `integrated-public-forestdanger-failure-review-019-003/proof.json` : première touche à2769,6516ms, budget1000ms inchangé, joueur mort après tentative de retrait, fermeture normale, sources/helpers/transcript/saves archivés byte-vérifiés, aucun échantillon CDP perdu. La cohérence d'une archive de save n'est pas déduite de sa seule empreinte.

La chronométrie distingue les délais : encodage des requêtes généralement≈0,1ms, envoi≈0,1–0,2ms ; acquittements mouse/Runtime.evaluate généralement≈90–190ms, résolution UI Save avec deux frames≈344ms. Les commandes d'activation/focus Target/Page sont≈0,3–0,5ms et n'expliquent pas le retard principal. Les trois acquittements de chaque clic, la libération répétée avant Save et les lectures de capture forment la séquence réelle ; aucun défaut produit précis encore isolé. Aucun budget de retraite ou exigence de joueur vivant assoupli, aucun PASS feu/IA revendiqué.

Suite010/session61253 est passée de l'attente des replays à machine010-002 : formatage et WASM strict PASS commande, vrai cargo check natif en cours. Clippy strict, tests workspace et lecteur Root doivent terminer sur les2531 bytes corrigés et checkpoint privé3705f469c393d78b436f72612a349ba7942cc5a5. Aucun replay simultané au build. Les résultats009 ne sont pas transférés à010. Accord ADR0021 toujours attendu explicitement ; Primary ne contient que la correction limitée de son test de permissions, aucun fork intégré. Gate3 ouverte/non qualifiée, aucun sous-agent ni publication.


### Actualisation 179 — strict010 refuse une sonde trop longue ; extraction et permission dérivée épinglées, qualification004 lancée

Machine010-002/suite61253 termine FAILED_CLOSED au Clippy natif strict, après formatage/WASM/cargo check natif PASS : seule fonction run_package de la sonde nuages compte111 lignes pour limite100. Aucun test workspace010 exécuté dans002. Extraction privée de la vérification des frames et du rapport JSON dans un helper empruntant App/manifest, avec mêmes chargements atomiques, assertions, comptages et erreurs.

Le guard du checkpoint003 refuse un fichier Tauri non suivi généré pendant la compilation : permissions/autogenerated/read_asset_model.toml. Aucune modification Git par ce checkpoint refusé. La qualification machine003, fondée sur les hashes sources, a néanmoins été lancée avant examen de ce refus de checkpoint ; elle n'est pas décrite comme qualification d'un arbre Git propre. Son Clippy strict termine FAILED_CLOSED sur le &manifest devenu inutile dans le helper extrait. Refus conservés ; correction privée suivante retire seulement ce double emprunt, mêmes bytes hashés, sans annotation de suppression de lint.

Fichier généré vérifié via parser TOML : exactement allow-read-asset-model/commands.allow=[read_asset_model] et deny-read-asset-model/commands.deny=[read_asset_model], aucune commande core ou permission supplémentaire. Générateur build.rs épinglé, fichier dérivé archivé puis inclus au manifeste2532. Preuve `integrated-source010-cloud-probe-fix-002/proof.json`. Source010 checkpoint privé propre f67494046b03a1437a1a25171743220862f39d15, aucun changement supplémentaire Primary ni publication.

Machine010-004/session72135 réellement lancée sur ce manifeste2532. Lecteur Root004 préparé contrôle aussi le HEAD privé exact, branche Rust et arbre propre après qualification. Tests workspace et lecteur restent attendus ; aucun PASS futur revendiqué. Feu003 conserve son refus budget/vie et sa chronométrie archivée ; préparation d'une optimisation de gestes publics reste à faire sans assouplir les critères. ADR0021 toujours en attente de réponse explicite ; partie complète P05 et intégration finale non qualifiées. Gate3 ouverte, aucun sous-agent.


### Actualisation 180 — strict010 PASS Root, tests actifs ; gestes publics feu004 préparés sans modification physique

Machine010-004/session72135 : formatage, WASM strict, vrai cargo check natif et Clippy natif strict PASS, journaux/artefacts/absence de diagnostics et2532 fichiers vérifiés dans `integrated-source010-native-strict-root-review-001/proof.json`. Workspace encore actif en compilation d'exécutables de tests ; aucun PASS global010 revendiqué.

Harnais privé feu004 préparé `integrated-public-fire-fast-input-preparation-019-001/proof.json`, auto-test exécuté PASS. Même trajectoire, points, géométrie, source019 et critères physiques ; budget1000ms inchangé, joueur vivant dans la vraie chaleur et après retraite, transaction Save cohérente et résolue toujours exigés par le lecteur causal figé.

Changement opératoire ciblé : conserver toute la sécurité1500ms de recapture, mais attendre son reliquat avant Ignite, alors que le feu est éteint. Clics d'exposition : mêmes événements CDP réels mouseMoved→mousePressed→mouseReleased envoyés dans l'ordre, puis chaque acquittement attendu par ID, comme le helper clavier existant ; secours mouseReleased si envoi interrompu, erreur CDP refuse le flux. Receipts de chaque envoi/acquittement conservés séparément. Aucun événement DOM synthétique ni mutation d'état métier.

Capture avant Save : lecture fraîche confirme pointeur et entrée déjà libérés ; sinon même Escape/release existant. Cible exacte/unique/visible/activée/centre exposé encore vérifiée ; scroll+deux frames restent requis si hors viewport, évités si déjà entièrement visible. Recapture : même query des points exposés, focus/visibilité, sécurité de délai, pointer lock/Exploration active/entrée active exigés ; fallback complet historique si garde ou refus, timeout refuse toute touche sans capture. Les deux fichiers modifiés sont épinglés, les414 autres helpers byte-identiques.

Essai004 non lancé tant que tests010 compilent. Les résultats des auto-tests du harnais ne qualifient ni gestes dans le navigateur ni feu/IA. Aucun PASS anticipé. ADR0021 toujours en attente de réponse explicite, P05 et intégration finale encore ouverts ; aucune nouvelle modification Primary, aucun sous-agent ni publication.


### Actualisation 181 — poursuite solo et seize replays Source010 préparés, non exécutés

Instruction propriétaire maintenue : aucun sous-agent créé ou relancé. Session72135 vérifiée vivante ; suite workspace010 encore active, aucun PASS global anticipé.

`integrated-source010-ignored-replay-preparation-001/proof.json` prépare seize tests exacts ignorés, sur les exécutables issus du journal workspace010 après succès et revue Root. Les onze tests acquis précédents sont complétés par quatre tests du lecteur GameFish et le test de ses trois LOD. Le paquet réel a le manifeste f26aceb2fdcee7f9e2261f90bd7e75fa7d66b672eb854dc66caa453c904c456d ;249 fichiers incluant la licence copiés et vérifiés octet par octet. Aucun test exécuté à cette étape ; cinq autres tests facultatifs restent non exécutés.

Les gardes imposent2532 fichiers Source010 stables, HEAD privé f67494046b03a1437a1a25171743220862f39d15 propre, journal et preuve machine010-004, fixtures préservées et résultats exacts. Aucun transfert des PASS Source008. Replay feu004 toujours préparé et non lancé durant la compilation ; ADR0021 et P05 restent ouverts, Gate3 non qualifiée, aucune publication.


### Actualisation 182 — raccord preuve/commit corrigé avant replay, tests010 encore actifs

Revue pré-exécution : la preuve machine010 ne comporte pas de champ HEAD ; celui-ci est vérifié par la revue Root indépendante puis Git réel. Le runner préparé001 cherchait à tort ce champ dans la preuve machine. Nouvelle préparation002 conserve001, lie explicitement le hash de la preuve machine et du manifeste source à la revue Root et prend son HEAD vérifié. Gardes de sources, fixtures, journaux, résultats exacts et Git propre inchangées. Aucun replay exécuté, aucun PASS revendiqué.

Session72135 vérifiée encore vivante ; inspection en lecture seule des processus observe cargo test workspace (38040→5640), rustc32872 et linker4360. D’autres compilations indépendantes sont actives dans des caches F:, sans intervention. Ne pas relancer les tests sur un délai d’observation. Feu004 attend toujours ; Gate3, ADR0021 et P05 restent ouverts.


### Actualisation 183 — machine010 et seize replays acquis PASS Root ; feu004 lancé

Session72135 terminée code0. Revue indépendante `integrated-source010-machine-root-review-004/proof.json` PASS : formatage, WASM717 artefacts sans diagnostics, vrai cargo check natif904 sans diagnostics, Clippy natif strict904 sans diagnostics, workspace1690 PASS/0 FAIL/21 ignorés/100 résultats incluant doctests.2532 fichiers et HEAD privé f67494046b03a1437a1a25171743220862f39d15 propres vérifiés. Ce PASS est celui de la copie Source010, pas Primary.

Runner de replay002 exécuté code0 ; `integrated-source010-ignored-root-review-001/proof.json` vérifie seize tests exacts PASS,1481 fichiers de fixtures et trois exécutables copiés immuables liés au journal workspace actuel. Les seize tests correspondent exactement aux21 ignorés moins les deux tests Vault et trois sondes CPU, encore non exécutés. Aucun héritage de PASS Source008, aucune preuve GPU supplémentaire.

Feu004 lancé session79069 via le runner épinglé ; handle vérifié actif, run1-start. Candidat019 Source008 conservé, scénario inchangé et budget1000ms maintenu. Aucun résultat physique anticipé ; il ne qualifie pas un candidat010 encore à construire. ADR0021, P05, intégration/requalification Primary et fermeture propriétaire restent ouverts. Gate3 non qualifiée ; aucune publication.


### Actualisation 184 — feu004 échoue avant Ignite ; approche publique005 lancée

Session79069 terminée code1. Revue `integrated-public-forestdanger-failure-review-019-004/proof.json` : blocage horizontal12s au village, phase forest-approach-waypoint-1-step-2, aucune action physique et aucune exposition au feu atteinte. Source,416 helpers, candidat et transcript vérifiés ; processus fermé normalement sans arrêt forcé. La trace signale aussi un timeout transitoire de Save initial ; aucune cohérence Save promue. Aucun verdict sur le budget première touche004.

Les observations montrent la position37.80/-117.16 à la transition du premier point, puis40.12/-119.20 et blocage39.50/-118.47 durant correction diagonale ; aucune cause production extrapolée. Préparation `integrated-public-fire-settled-route-preparation-019-001/proof.json` : mêmes trois points village, approche par helper existant SettledPulses, tolérance resserrée0.10 au lieu0.75, budget total420s partagé puis même route restante.415 autres helpers conservés, aucune source/géométrie/critère feu modifié. Auto-test PASS, pas une preuve navigateur.

Replay005 lancé session37879 et handle vérifié vivant. Machine010 et seize replays acquis PASS restent valides sur leur périmètre ; candidat010, ADR0021, P05 et intégration finale encore ouverts. Gate3 non qualifiée, aucun sous-agent ni publication.


### Actualisation 185 —005 atteint Ignite mais garde capture refusée ; correction encodage006 lancée

Session37879 terminée code1. Revue `integrated-public-forestdanger-failure-review-019-005/proof.json` : approche publique atteint le feu, Ignite séquence1, trois clics CDP complets acquittés, mais capture non validée et aucune touche retraite envoyée. Budget1000ms non validé. Source/helpers/transcript préservés et fermeture normale vérifiés. Succès réel du pointer lock non prouvé ; aucun PASS feu ou cohérence Save promu.

Diagnostic concret dans Focus-PhysicsExposureCanvas : ses expressions JavaScript nouvelles utilisent deux chaînes françaises mojibake, qui ne reconnaissent pas le texte normal entrée active true / Capture refusée. Préparation006 remplace seulement ces deux chaînes par des échappements Unicode JS,415 autres helpers identiques. Auto-test PASS et contrôle de l’expression exacte extraite PASS sous Node : vrai texte français accepté, absence de pointeur et entrée inactive refusées, refus capture reconnu. Ce test simule seulement le DOM et ne prouve pas une capture navigateur. Replay006 lancé session94920, handle vérifié actif ; route005, points, géométrie et budgets identiques.

Construction010 préparée dans `integrated-source010-studio-build-preparation-001/proof.json`, deux builders parsés sans erreur, HEAD qualifié et2532 fichiers exigés. Aucune compilation lancée pendant replay006. `integrated-source010-studio-library-input-review-001/proof.json` vérifie3009 fichiers du stage propriétaire, dont3000 imports et GameFish f26aceb2. Stage kit9 fichiers/révision1 : six GLB identiques, trois manifestes différents du kit11/révision5. Ce dernier conserve les deux assets architecture additionnels et leurs attestations ; il reste l’entrée gouvernée à préserver pour le nouveau candidat. Aucun rendu intégral ou déploiement du stage revendiqué.

Gate3 reste non qualifiée : nouveau candidat010/replays, ADR0021, P05 et intégration/requalification Primary ouverts. Aucun sous-agent ni publication.


### Actualisation 186 —006 capture et touche acquittées, budget dépassé ; construction020 Source010 lancée

Session94920 terminée code1, fermeture normale vérifiée. `integrated-public-forestdanger-failure-review-019-006/proof.json` conserve le verdict FAIL : première touche acquittée1412.0708ms après début Ignite, maximum1000ms inchangé. Chronologie Ignite→Save acquitté→capture confirmée→touche acquittée vérifiée. Le journal observe vitalité13 après retraite et Save résolu, mais aucun PASS global de survie/Save/feu promu car le budget est dépassé. L’erreur d’encodage des gardes005 est corrigée ; le critère de délai reste ouvert.

Chaîne de construction `integrated-source010-studio-build-preparation-002` lancée session99721 après terminal006/fermeture normale. Préflight HEAD f67494046b03a1437a1a25171743220862f39d15 propre et2532 fichiers stables, machine et seize replays acquis Root PASS liés,3000 imports et kit11 copiés et épinglés, originaux conservés. Frontend020 en compilation ; natif020 séquentiel seulement après frontend PASS et nouvelles gardes de stabilité. Aucun résultat de build anticipé ni replay public pendant compilation.

Lecteur indépendant020 préparé `integrated-studio-candidate-root-review-020/preparation-proof.json`, syntaxe Python validée ; exige journaux/stabilité/HEAD/source manifests, ELF non revendiqué mais en-tête PE MZ, WASM Studio+worker et manifeste worker, copies imports3000 et kit11 exactes, configuration Tauri pointant vers frontend isolé. Non exécuté tant que build non terminé. Source010 qualifiée machine ne signifie toujours pas Primary intégrée ni runtime qualifié.

Gate3 demeure ouverte : critère feu, candidat020 et ses replays, ADR0021 propriétaire, P05 et intégration/requalification Primary restent requis. Aucun sous-agent ni publication.


### Actualisation 187 — diagnostic temporel006 et garde navigateur007 préparée, build020 actif

Session99721 vérifiée active, frontend020 en compilation. Aucun nouveau PASS de build ni replay public lancé.

`integrated-public-fire006-critical-timing-review-001/proof.json` vérifie sept lectures Runtime.evaluate avant première touche,964.2179ms cumulés dont962.3449ms attente/réception/décodage, contre1.873ms encodage/envoi. Intervalle Save acquitté→capture confirmée662.0454ms, trois lectures de la même garde. Le budget dépassé de412.0708ms reste inchangé ; les profils CDP et les clics batchés restent distincts et aucun total général extrapolé.

`integrated-public-fire-browser-poll-preparation-019-001/proof.json` prépare un changement de harnais uniquement : observer la même expression DOM en lecture seule, dans une évaluation async navigateur, toutes25ms, échéance navigateur1000ms et échéance hôte1000ms conservées, fallback terminal historique conservé. Aucun input/mutation dans le polling, aucun assouplissement des conditions pointeur/Exploration/entrée.415 autres helpers identiques ; auto-test PASS. Contrôle de l’expression exacte extraite sous Node PASS pour capture valide, pointeur absent, entrée inactive, refus terminal, capture retardée et expiration1000ms ; mock uniquement, pas preuve navigateur. Runner007 et regroupement des gardes fraîches Save restent à préparer après analyse, aucun succès prédit.

Gate3 non qualifiée ; candidat020/replays, critère feu, ADR0021, P05 et intégration/requalification Primary encore requis. Aucun sous-agent ni publication.


### Actualisation 188 — contrôle Save008 regroupé et encodage corrigé, non rejoué

Build020 session99721 vérifié vivant, frontend en compilation ; Source010 intacte, aucun replay public concurrent.

Autre chaîne mojibake observée dans Confirm-PhysicsExposureCaptureReleased : elle ignorait le texte français entrée active true si le pointeur était déjà libre. Harnais008 corrige cette chaîne et ajoute une option RequireReleasedCapture au resolveur exact du bouton Save. Un même instantané DOM confirme pointeur libre/entrée inactive et cible unique/visible/centre exposé/activée. Si pointeur ou entrée actifs : Escape/release historique, puis nouvelle résolution fraîche de tous les contrôles, refus si encore actif. Le cas habituel économise une lecture CDP, sans réutiliser un ancien rectangle de bouton. Polling007 navigateur identique conservé,415 autres helpers identiques. Aucune production, géométrie, point, budget ou critère physique modifié.

`integrated-public-fire-combined-save-preparation-019-001/proof.json` auto-test PASS ; `guard-test-proof.json` expression JavaScript exacte extraite PASS sous Node pour les cas capture/entrée actives, cible absente/dupliquée/masquée/désactivée et scroll avec deux frames hors viewport. DOM simulé uniquement ; pas de capture, touche, Save ni feu réels nouvellement qualifiés. Runner008 reste à préparer pour un replay après construction020, aucun succès anticipé.

Gate3 non qualifiée ; candidat020/replays, critère feu, ADR0021, P05 et intégration/requalification Primary restent ouverts. Aucun sous-agent ni publication.


### Actualisation 189 — frontend020 release PASS Root, natif actif et périmètre de clôture réaffirmé

Frontend020 terminé code0. `integrated-studio-frontend-root-review-020/proof.json` vérifie le journal et la preuve de build,2532 fichiers source/manifests identiques, HEAD privé f67494046b03a1437a1a25171743220862f39d15, WASM Studio abcbf73c et worker f5537e2e avec en-têtes WASM, manifeste worker e077f93f,3000 imports byte-identiques et absence de liens symboliques. Verdict BUILD_ONLY, pas comportement runtime. Session99721 vérifiée toujours active ; chaîne passée au build natif020. Aucun natif PASS anticipé, aucune requalification public019 transférée.

`integrated-candidate020-closure-requirements-review-001/proof.json` capture les douze lignes officielles R3-A/R3-B/R3-P01–05 et contrôles transversaux du plan. Chaque exigence runtime020 reste à prouver ; machine1690 tests et seize replays acquis Source010 sont des preuves bornées déjà vérifiées. Les parcours Windows/WASM, diagnostics références/objectifs, partie complète P05, contrôles de dépendances/non-régression/exceptionCI, ADR0021 et intégration/requalification Primary restent ouverts. L’autorisation de l’objectif couvre la clôture une fois toutes les preuves et exigences humaines existantes satisfaites ; aucune nouvelle approbation finale supplémentaire inventée.

Gate3 ouverte/non qualifiée. Harnais008 préparé mais non rejoué ; aucun sous-agent ni publication.


### Actualisation 190 — candidat020 build complet PASS Root ; revalidation authoring publique lancée

Session99721 terminée code0, frontend puis natif séquentiels. `integrated-studio-candidate-root-review-020/proof.json` PASS BUILD_ONLY : journaux/commandes, HEAD privé f67494046b03a1437a1a25171743220862f39d15 propre,2532 sources et manifests frontend/natif identiques, exécutable PE/hash, WASM Studio/worker, manifeste worker,3000 imports et kit11 byte-identiques, frontendDist Tauri isolé vérifiés. Aucune source Primary appliquée et aucune qualification runtime déduite.

`integrated-public-runtime-preparation-020/proof.json` reprend les416 helpers figés et65 fichiers du checkpoint de référence, tous rehashés, sans modification de critères/positions/source ; runner et lecteur adaptés seulement au candidat020, HEAD2532/imports3000 et nouvelles preuves de build. Revalidation publique lancée session13723, handle vérifié actif : charge,22 formulaires, contrôle de head entier et Save ; aucun PASS anticipé et aucun transfert de résultats019. Ce replay ne refait pas encore la création de toutes les entités ni la boucle RPG.

Harnais physique008 et ses mocks restent préparés/non rejoués. Critère feu1000ms, parcours RPG020, diagnostics, P05, ADR0021 et intégration/requalification Primary restent ouverts. Gate3 non qualifiée, aucun sous-agent ni publication.


### Actualisation 191 — revalidation publique020 PASS Root après correction UTF8 du lecteur

Session13723 était déjà terminée code0 lors du poll précédant190 : son libellé actif dans190 est dépassé et corrigé ici. Commande publique PASS, puis premier lecteur020 refusé sur Vitalité maximale. Diagnostic : le ledger porte le libellé français UTF8 correct ; copie du reader019 faite avec read_text() sans encodage, Python Windows CP1252, produisant un libellé mojibake. Ancien lecteur conservé ; `integrated-public-revalidation-root-review-020-retry-001/encoding-correction.json` archive hashes et correction de copie explicite UTF8. Aucun critère ni valeur de formulaire changé, aucun échec produit extrapolé.

`integrated-public-revalidation-root-review-020-retry-001/proof.json` PASS :22 formulaires, coordonnées/stats/physique et cases validés contre checkpoint, head entier inchangé, Save public acquitté,65 fichiers originaux et416 helpers byte-identiques, captures/transcript/scripts réellement chargés épinglés, fermeture normale. Candidat020 lié à Source010 et à son hash natif. Pas de nouvelles commandes de création rejouées, pas de boucle RPG complète et pas de rendu Tidewater validé depuis capture de grille d’authoring.

Toutes prochaines copies de scripts/lecteurs utilisent un encodage UTF8 explicite ; ne pas altérer les preuves historiques. Navigation/P01–P04, environnement/feu008 sur020, P05, ADR0021 et intégration/requalification Primary encore ouverts. Gate3 non qualifiée ; aucun sous-agent ni publication.


### Actualisation 192 — navigation publique020 lancée sans héritage019

`integrated-public-navigation-preparation-020/preparation-proof.json` vérifie416 helpers de navigation figés identiques au harnais019-006, manifestes, nouveau candidat020 et revalidation publique020 Root, puis copie des scripts/lecteurs avec encodage UTF8 explicite. Points, tolérances, fenêtre navigation60s et timeout démarrage120s inchangés ; aucun produit ou contrat physique modifié.

Replay lancé session84333 et handle vérifié actif : PNJ intérieur sans chemin à porte fermée, déplacement extérieur/patrouille, ouverture puis sortie intérieure→seuil→extérieur, retour porte fermée et états persistants avec receipts/frames cohérentes à vérifier par lecteurs indépendants020. Aucun PASS anticipé. Ce cas reste borné à la salle et à la patrouille, pas tous PNJ/toute Tidewater. Source/fixture et candidat sont épinglés ; résultats019 non transférés.

Boucle RPG/P01–P04 et cas environnementaux020, critère feu1000ms, P05, ADR0021 et intégration/requalification Primary restent ouverts. Gate3 non qualifiée, aucun sous-agent ni publication.


### Actualisation 193 — navigation020 active ; P01 préparé et première observation visuelle bornée

Session84333 vérifiée active, échantillons navigation020 archivés en cours. Aucun PASS anticipé. Préparation `integrated-public-p01-preparation-020/proof.json` conserve416 helpers du parcours contact019, mêmes lieux/points/timeouts et critères5 reçus×20 dégâts, quête/visite intérieure/Save/new-process Resume ; candidat020 et revalidation020 requis, lecteurs copiés UTF8 explicitement. Le runner exige navigation020 Root PASS et fermeture normale avant lancement, aucun replay P01 exécuté et aucune alternative environnementale dispensée.

Root a inspecté la capture run1-navigation_ready.png réelle020 : village/terrain/végétation/bâtiments visibles et grande capsule bleue au centre. `candidate020-p05-initial-visual-observation-001/proof.json` lie image/UI/hash candidat et consigne seulement cette observation statique. Identité de la capsule, animation, lisibilité des interactions sur durée et défaut de caméra non établis ; aucune attribution à un acteur précis ni P05 accepté. Partie complète et jugement humain restent requis.

Gate3 non qualifiée ; parcours020 et cas environnementaux/feu008, P02–P04, contrôles transversaux, P05, ADR0021 et intégration/requalification Primary ouverts. Aucun sous-agent ni publication.

## Actualisation194 — Navigation020 : témoin manquant et nouveau replay isolé

Le replay020/001 est terminé en échec et ses processus se sont fermés normalement. Les 175 fichiers de mesures, leurs JSON bruts et les helpers exécutés ont été revérifiés par SHA-256 : 159 mesures après ouverture, dont 3 en mouvement et 156 à la cible extérieure. Une mesure intérieure et des mesures extérieures au sol existent, mais aucune ne satisfait l’enveloppe inchangée de la porte. Le trou initial de 131 ticks (2,211s) peut avoir masqué le passage ; la trajectoire non observée ne constitue pas une preuve. Save final et fermeture de porte non atteints.

Preuve : `.gate3-evidence/20261002/integrated-public-navigation-failure-review-020-001/proof.json`. Le résultat historique019/006 n’est pas transféré à020. Aucun défaut production n’est établi par ces seuls échantillons.

Le nouveau harnais020/002 collecte passivement toutes les 25ms le même diagnostic DOM pendant l’ouverture publique et le recul du joueur. Le chemin, la géométrie, les tolérances, les délais et le lecteur physique restent inchangés ; un seul des 416 fichiers du harnais a changé. Les autocontrôles PS et les tests du JavaScript réellement intégré passent, avec limites synthétiques explicites. Le replay réel vient d’être lancé (session60480) ; aucun PASS runtime ni clôture de Gate3 n’est déclaré.

Préparation : `.gate3-evidence/20261002/integrated-public-navigation-continuous-preparation-020-002/preparation-proof.json` et `observer-test-proof.json`. Lecteurs Root préparés dans `integrated-public-navigation-root-review-020-retry-002`, exécution uniquement après un résultat réel PASS. Travail mené exclusivement par l’agent principal, sans sous-agent ; aucune publication Git.

## Actualisation195 — Navigation020/002 PASS Root ; P01 lancé et dépendances fraîches

Le replay020/002 et ses lecteurs Root sont terminés PASS : les 179 fichiers archivés sont revérifiés, le lecteur physique inchangé retrouve deux témoins au sol dans la porte, l’intérieur puis l’extérieur, la cible atteinte immobile, les révisions de tuiles après Open puis Close, et deux reçus distincts sauvegardés dans un frame cohérent. Processus fermés normalement, sources et helpers préservés. Preuve : `.gate3-evidence/20261002/integrated-public-navigation-root-review-020-retry-002/proof.json`. L’échec020/001 reste conservé ; pas de qualification de toute la carte ni de jugement visuel humain.

La boucle P01020 est lancée (session21457), avec le même harnais contact007, les mêmes dommages et tolérances et un nouveau lien obligatoire vers la preuve de navigation020/002. Préparation : `integrated-public-p01-preparation-020-retry-002/proof.json`. Quête/combat/ramassage/intérieur/Save/nouveau processus sont encore en cours, sans résultat hérité019.

`cargo audit --json` vient d’être rejoué sur le lock Source010 avec une base RustSec fraîche `ef6173cbc5c50ec8166f9a5b28f07834144373ee` (dernier commit du 3 octobre). 938 paquets, zéro vulnérabilité dans la catégorie correspondante ; dix avis informatifs sont présents : neuf non-maintenance recensés dans deny.toml et `RUSTSEC-2024-0429` unsound sur glib. Ce dernier n’est pas ignoré par la politique ni déclaré résolu/inoffensif. Preuve Root : `integrated-source010-fresh-dependency-audit-001/root-review-proof.json`. La qualification globale des dépendances n’est pas acquise. `cargo deny` complet est lancé séparément (session22330), politique et sources inchangées.

Gate3 demeure ouverte ; objectif complet conservé, ADR0021/P05 et les autres replays020 encore requis. Aucun sous-agent ni publication Git.

## Actualisation196 — Contrôles cargo deny terminés et relus

Source010 : `cargo deny --locked --format json --manifest-path ... --config ... check --show-stats --hide-inclusion-graph` terminé exit0. Relecture Root du résumé : advisories/bans/licenses/sources chacun zéro erreur, 48 avertissements de versions multiples conformes à la politique existante. Graphe toutes features et trois cibles Windows/Linux/WASM. Lock, politique et sources inchangés. La base utilisée correspond au même commit RustSec frais que cargo audit. Aucune dérogation ajoutée. Preuve : `.gate3-evidence/20261002/integrated-source010-fresh-deny-001/root-review-proof.json`.

Ce PASS de la politique deny ne résout pas l’avis unsound `glib` consigné à l’actualisation195. La revue de cet avis et la revalidation des conditions ADR0006 restent à réaliser. P01 reste en cours (session21457 confirmée active), aucun PASS P01, fermeture de Gate3, jugement P05 ou publication n’est déclaré.

## Actualisation197 — P01020 terminé FAIL, contacts physiques archivés

Session21457 terminale exit1 ; processus du replay fermés normalement, candidat/helpers/fixture source inchangés. Après capture `guardian_defeated`, le segment public `pickup-detour` vers X14,7/Z-271,3 ne progresse pas pendant douze secondes. Au tick13286, le moteur reçoit une translation diagonale (-0,0354/-0,0354m) et résout (0/0), avec 20 contacts dont terrain et face inférieure de `Gameplay object · Support piège`. Le lecteur Root confirme ces contacts au même tick et les hashes source/candidat/captures. La capture d’échec après relâchement des touches montre une autre position ; elle ne remplace pas le témoin du blocage.

Preuve : `.gate3-evidence/20261002/integrated-public-p01-failure-review-020-001/proof.json`. P01, Save final, visite intérieure et reprise dans un nouveau processus ne sont pas acquis. La navigation020/002 reste PASS dans sa portée séparée. Le prochain diagnostic doit comparer le support auteur, la clearance capsule et le trajet public historique avant correction ; le rejet de stagnation douze secondes reste inchangé. Aucun défaut production isolé ni PASS partiel de la boucle n’est inventé.

Aucun replay lancé dans ce tour n’est encore actif : Navigation020/002 PASS Root, audit frais et deny relus, P01020 FAIL Root. Objectif Gate3 conservé actif, aucune pause demandée. ADR0021/P05 et avis glib restent ouverts, aucune publication Git ni sous-agent.

## Actualisation198 — CI réellement réouverte, workflow préparé, P01020/002 FAIL avant ramassage

Le replay P01020/002 termine exit1, sources préservées et processus fermés normalement. La borne inchangée de placement extérieur gardien 0,05m/30s n’est pas atteinte (dernier écart supérieur à0,9m). Aucune action `pickup-lateral` n’a été exécutée : le dégagement latéral préparé pour l’échec001 reste non testé. Aucun combat/Save/reprise PASS ne lui est attribué. Preuve : `integrated-public-p01-failure-review-020-002/proof.json`. Prochain diagnostic : arrivée réelle et mouvement résiduel avant staging, puis transport des pulses ; aucune augmentation des bornes.

Relecture GitHub actuelle : l’origine33335683606 conserve quatre jobs sans étape, mais le run Rust du3octobre37125741497 sur6b001dbe… a exécuté ses runners. Supply-chain PASS ; Windows/Linux FAIL àClippy (forge_assets source_fragment, neuf erreurs), WASM FAIL au hook worker qui exige CARGO_TARGET_DIR. L’ADR0006 expire automatiquement lorsque les runners exécutent leurs étapes. Sa substitution locale ne peut donc plus servir àclôturer Gate3 ; CI réelle sur le candidat exact encore requise. Aucun workflow lancé. Preuve : `integrated-source010-ci-exception-revalidation-001/root-review-proof.json`. Les corrections locales Source010 ne transforment pas ce run distant en PASS.

Correction réversible Primary `.github/workflows/rust-gate.yml` : CARGO_TARGET_DIR explicite depuis github.workspace pour tous les jobs, installation wasm-bindgen-cli0.2.127 dans les jobs native/WASM en concordance avec le lock. Fichier initial propre et sauvegardé, 8insertions/2suppressions, diff--check PASS ; aucun contrôle affaibli. YAML non parsé faute de module local ; exécution distante non réalisée, correction préparée uniquement. Preuve : `primary-navigation-worker-ci-correction-001/proof.json`. Le contexte canonique a reçu la nouvelle condition CI via `primary-ci-context-correction-001/proof.json`, sans changer la validation propriétaire Gate2. Source010/candidat020 restent immuables.

Clarification RustSec : `RUSTSEC-2024-0429` glib0.18.5 est déjà prévu par `docs/evidence/gate0/RUSTSEC_POLICY.json`, avec distribution_blocking=true. Le script officiel Test-RustSec.ps1 rejoué sur Source010 passe : zéro vulnérabilité, dix avis exactement conformes, un blocage de distribution conservé. Aucun nouveau waiver ni résolution de l’unsoundness. Preuve : `integrated-source010-fresh-dependency-audit-001/exact-policy-proof.json`. La revue197 de deny.toml seul était insuffisante pour décrire cette gouvernance existante.

Tous les processus lancés dans cette tranche sont terminés. Objectif complet conservé actif, pas de pause ni clôture ; aucune publication Git ou délégation.


## Actualisation199 — assets locaux PASS et replay P01020/004 FAIL au démarrage

Les corrections ciblées Primary conservent valeurs, assertions exactes et contrôles d'intégrité, avec extraction de la validation textures dans un helper privé. Les échecs001 à004 restent archivés.004 passe formatage/Clippy puis échoue au test FishProps faute de variable de paquet au lancement.005 fournit au processus le paquet réel acquis : manifest e578b44e… et250 payloads vérifiés avant/après. Relecture Root005 : formatage ciblé PASS, Clippy forge_assets toutes cibles/toutes fonctionnalités avec -D warnings PASS sans diagnostic, tests toutes cibles86 PASS, zéro FAIL, neuf ignorés.205 fichiers source et251 fichiers fixture intacts. Preuve `primary-assets-ci-real-package-check-005/root-review-proof.json`. Diff--check ciblé PASS ; aucun PASS Primary global, tests ignorés, CI distante ou Gate3. Source010/candidat020 immuables.

P01020/003 FAIL : premier point atteint au repos dans0,35m ; deuxième hors borne0,35m/30s.19 des23 pulses200ms ont des callbacks regroupés en moins de1ms. Preuve `integrated-public-p01-failure-review-020-003/proof.json`. H010 adapte uniquement le pilotage public aux intervalles d'images observés, appuis longs bornés200–600ms, micro-appuis1ms. Waypoints, tolérances, délais30s, stabilité au repos, géométrie, cinq contacts20 dégâts et Save/reprise conservés.416 helpers figés, un modifié, auto-test PASS, lecteur causal conservé. Préparation `integrated-public-p01-cadence-preparation-020-004/proof.json`.

P01020/004 FAIL au démarrage : cible CDP exacte absente après180s, journal vide, pilotage et gameplay non exécutés. Nettoyage du processus isolé avec arrêt forcé explicite ; sources/candidat/helpers/fixture intacts. Preuve `integrated-public-p01-failure-review-020-004/proof.json`. Diagnostiquer les sorties startup avant relance. Sessions6372 et1227 confirmées terminales ; aucun contrôle de cette tranche encore actif. Objectif Gate3 actif, CI réelle du candidat exact, ADR0021, P05 et autres critères toujours requis. Aucune publication Git ni délégation.


## Actualisation200 — neuf tests assets acquis, démarrage hôte vérifié, placement final encore ouvert

Primary forge_assets : les neuf tests ignorés sont maintenant rejoués explicitement avec six paquets réels (BeachProps, VillageProps, GameFish, Ocean, ShoreSim, WaterSurface). Chacun des neuf noms attendus apparaît PASS ;1497 fichiers source/fixtures épinglés intacts. Preuve `primary-assets-exact-ignored-check-006/root-review-proof.json`. Avec les86 tests005, les95 tests du crate sont acquis localement dans ces deux commandes. Aucun PASS global Primary/CI distante/Gate3, aucun héritage de Source010, aucun remplacement de preuve UI/GPU.

Relecture démarrage : mêmes candidat020/helper H010 et navigateur épinglés, lancé cette fois depuis le compte hôte. CDP expose la page exacte OpenRPG Forge Studio et son worker ; capture publique start_ready au tick345, au sol, entrées actives, simulation prête. Preuve `integrated-public-p01-host-startup-review-020-005/proof.json`. La capture montre le village Tidewater, UI RPG Vitalité100/Endurance100 et quête Available ; ce constat statique ne vaut pas P05 ni qualification personnage/animation. L'écart d'environnement est établi ; cause production non affirmée. Aucun processus004 restant n'a été arrêté par ce diagnostic, les processus étrangers sont préservés.

P01020/005 termine FAIL normalement, sans arrêt forcé, sources préservées. Les deux premiers points arrivent au repos à0,337/0,283m dans0,35m. La dernière approche0,05m/30s échoue à0,218m,72 observations,14 pulses dont12 micro-appuis1ms. Le déplacement du dernier appui long porte le seuil micro à1,081m ; la réattente de trois échantillons entre chaque micro-correction limite le nombre de corrections. Preuve `integrated-public-p01-failure-review-020-005/proof.json`. Aucun combat/ramassage/Save/reprise PASS.

H011 autorise une micro-correction suivante sur une observation fraîche proche du repos (vitesse<=0,01m/s), sans réattendre trois échantillons entre ces seules corrections. L'arrivée exige toujours trois échantillons frais stables dans la borne originale ; coordonnées, tolérances, délais30s, géométrie, dégâts et Save/nouveau processus inchangés.416 helpers figés, un modifié, auto-test PASS, lecteur causal préparé007 sans assouplissement. Préparation `integrated-public-p01-micro-pacing-preparation-020-006/proof.json`. Replay complet006 lancé depuis l'hôte ; session69081 confirmée active à cette actualisation, aucun PASS anticipé. Les checks assets006 et replay005 sont terminés.

Gate3 reste ouverte, objectif conservé actif, aucune publication Git ni sous-agent. CI réelle du candidat exact, ADR0021, P05 et autres critères non encore acquis restent requis.


## Actualisation201 — boucle006 progresse, P02/P03/P04 préparés sur020

Session69081 toujours active, aucune relance du replay. Captures006 maintenant archivées : guardian_defeated, interior_inside, interior_closed_after_exit, save et run2 resume_ready. Lecture publique sans action : quête Completed, Sceau absent de l'inventaire, joueur au sol et simulation prête avant la visite intérieure. Les deux premières captures de visite, sauvegarde et reprise ont été relues et leurs empreintes archivées dans `integrated-public-p01-live-review-020-006/proof.json`. Cela constate des étapes réelles mais ne remplace pas l'audit causal complet des cinq receipts contact20, du Save et de la seconde visite forêt. Aucun P01 PASS anticipé, aucun P05. La première tentative de lecture CDP séparée a échoué sur la sélection d'un tableau ; la seconde lit uniquement le HUD existant, sans focus/input/état injecté.

Essais actuels P03/P04 préparés dans `integrated-public-followup-preparation-020/proof.json` : candidat020 et authoring020 épinglés,416 helpers H011 inchangés, deux auto-tests PASS. Ils sont indépendants du Save P01 ; leurs validations publiques et préservation des sources restent requises. Aucun ancien PASS019 hérité. Ils attendent la fermeture de006 pour éviter la concurrence de focus. P03 applique le cas officiel roadmap « objectif inaccessible ou référence absente » via la suppression refusée du ramassage requis et le guidage sur Sceau ; sa portée reste bornée à ce diagnostic, sans prétendre une correction commitée.

P02 préparé dans `integrated-public-p02-preparation-020/proof.json`, avec dépendance stricte au vrai PASS Root P01020/006 et à son Save actuel. Lecteur associé corrigé avant exécution pour épingler le head Source010, le ledger006 et les helpers H011 ; anciennes versions du lecteur/proof sauvegardées. Critères de retouche réplique/porte, liens/identités conservés et reprise complète inchangés ; délai opérationnel300s déjà adopté dans la qualification précédente, sans performance/P05. Aucun replay de follow-up lancé.

Objectif Gate3 conservé actif, sources/propriétaire préservés, aucune publication Git ni sous-agent. CI réelle exacte, ADR0021, P05, environnement/deuxième approche et autres preuves manquantes restent requis.


## Actualisation202 — boucle combat et diagnostic P03 PASS, P02 approche FAIL

P01020/006 termine exit0 avec fermeture normale des deux processus. Root `integrated-public-p01-root-review-020-retry-007/proof.json` PASS : cinq attaques contact20, reçus concordants avec Save, quête Completed persistée, visite intérieure après quête, puis nouveau processus et visites forêt/ramassage pour vérifier les conséquences. Archives intactes. Ledger053c9acf… ; Save8e6cba1d… ; aucun ancien PASS hérité. Recheck `integrated-public-p01-source-scope-review-020-006/proof.json` :2532 fichiers Source010 intacts et snapshot Git propre au head f6749404…. Ce PASS couvre la boucle combat ; R3-P01 exige encore la seconde approche environnementale. Pas de qualification Primary globale, navigateur autonome, P05, CI distante ou Gate3.

P02020/001 termine FAIL lors de l'approche initiale de porte vers(46,-151), waypoint5-step2, délai197s restant épuisé. Dernier écart0,670m, entrée active et translation résolue non nulle avec contact terrain. Pas de blocage collision ou défaut production isolé. Preuve `integrated-public-p02-failure-review-020-001/proof.json`, fermeture normale et sources/candidat/helpers/fixture intacts. Retouche/reprise P02 non qualifiée ; diagnostiquer oscillation/temporisation avant relance sans élargir les bornes.

P03020/001 termine PASS, relecture `integrated-public-p03-root-review-020/proof.json` : suppression du ramassage requis refusée avec diagnostic quête/item/quantité, Apply désactivé, Sceau sélectionné et contrôle actif pour correction ; aucune révision/receipt/source modifiée. Fermeture normale. Portée : objectif rendu inaccessible par suppression, guidage vers son élément, sans prétendre une correction commitée ni tous les diagnostics possibles.

Environnement020 préparé dans `integrated-public-world-preparation-020/proof.json` :416 helpers, deux scripts physiques remplacés par ShorePush003/fire008 épinglés, aucune géométrie ou source produit modifiée. Auto-test physique PASS et syntaxe scénario PASS. Tentative -SelfTest non supportée par le scénario refusée avant exécution, log conservé. Lecteur causal physique et budget critique feu1000ms inchangés. Quatre scénarios non exécutés, aucun PASS hérité.

P04 lancé après fermeture de P03, départ choisi/Save/isolation des sources requis. Etat actuel consigné dans le pointeur campagne ; aucun PASS Root anticipé. Objectif Gate3 actif, aucune publication Git ni sous-agent. Seconde approche/environnement, P02/P04, navigateur autonome, ADR0021, P05, CI réelle exacte et intégration Primary finale restent à prouver.


## Actualisation203 — P04 isolation PASS Root et reprise du diagnostic P02

P04020/001 termine exit0. Root `integrated-public-p04-root-review-020/proof.json` PASS : deux nouveaux processus au départ choisi(40,-115), même révision publique40 et même zone, mouvement réel avant Save au premier passage, second départ explicitement neuf et non reprise de la partie. Save limité à la copie de test, magasin de Save source préservé, archives source/clone auteur byte-identiques, deux fermetures normales sans arrêt forcé. Observation WMI confirme zéro processus du candidat restant ; aucun processus étranger arrêté. Cette preuve ne revendique pas une physique bit-identique ou P05.

Diagnostic P02020/001 :256 observations du point bloquant, distance minimale0,585m pour tolérance0,35m, oscillation X44,30–47,36 autour de46. Les dernières directions changent alors que la capsule dépasse la cible ; mouvement effectif non nul et terrain seul sur le dernier témoin. Aucun obstacle physique bloquant ou défaut production établi. Le délai de l'échec et tous ses témoins restent archivés.

H012 apporte un mode optionnel SettledWaypoints au pilotage public de corridor P02. Même polyligne et interpolation<=3m, mêmes points/tolérances0,35m, même délai total300s et refus après douze secondes sans progrès horizontal. Appuis relâchés et observation fraîche, arrivée exigeant trois échantillons frais au repos ; aucun code produit/géométrie/Save changé. Le trajet continu demeure le mode par défaut pour les autres appels.416 helpers figés, deux modifiés, auto-test gameplay PASS ; lecteur causal P02 conservé avec les seuls bindings du nouvel essai. Préparation `integrated-public-p02-settled-preparation-020-002/proof.json`.

P02020/002 lancé après fermeture de P04, session51208 confirmée active avant cette actualisation ; état instantané consigné dans le pointeur campagne sans PASS anticipé. Scénarios environnementaux020 encore préparés non lancés, focus exclusif préservé. La boucle combat/P03/P04 restent PASS dans leurs portées séparées. Objectif Gate3 actif, pas de publication Git ni sous-agent ; critères manquants et validations propriétaire encore ouverts.


## Actualisation204 — P02 focus FAIL conservé, approche environnementale lancée

P02020/002 est confirmé terminal exit1. Le trajet par appuis relâchés s'arrête sur « Settled public centering lost canvas input focus ». Preuve `integrated-public-p02-failure-review-020-002/proof.json`, source/candidat/helpers/fixture intacts et fermeture normale sans arrêt forcé. Aucun PASS du corridor ou de P02 ; diagnostic de capture/focus à poursuivre avant nouvelle modification. Le refus de perte de focus n'est pas supprimé et les délais/tolérances restent inchangés.

Après fermeture de P02, EnvironmentQuest020/001 lancé depuis l'hôte via `integrated-public-world-preparation-020/run-world-scenario.py EnvironmentQuest` : approche environnementale, reçus physiques, résolution de quête, Save et nouveau processus requis. Préflight épinglé et état instantané archivé dans le pointeur campagne ; session31657 confirmée démarrée, aucun PASS anticipé. Aucun autre replay de cette tranche maintenu actif. P04 et boucle combat/P03 restent PASS dans leurs portées précédentes. Objectif complet Gate3 actif, aucune publication Git ni sous-agent.


## Actualisation205 — lancer accepté sans dégâts, direction réelle examinée, P02 focus rejoué

EnvironmentQuest020/001 termine FAIL avant combat : aucun reçu exact environnemental Impact/Damage, gardien toujours100 de vitalité. Root `integrated-public-environmentquest-failure-review-020-001/proof.json` confirme Save/receipts, lancer accepté/released, source/candidat/helpers/fixture intacts et fermeture normale sans arrêt forcé. Approche radiale est+0,575m/tolérance0,025m non atteinte ; le helper historique capture approach_bound puis tente l'action publique. Au lancer le joueur est à X18,568, à l'ouest de l'objet initialX19, alors que le gardien est àX16. L'impulsion réelle est X+97,91/Y90/Z-113,64 ; elle part à l'opposé du gardien sur X. Aucun Impact/Damage qualifié et seconde approche toujours ouverte. Le prochain diagnostic doit comparer la position au contact, les observations du trajet et la direction effective du lancer, sans modifier la géométrie ou supprimer le garde causal.

P02/H013 préparé dans `integrated-public-p02-focus-preparation-020-003/proof.json` : perte de focus désormais journalisée avant toute récupération ; pour le mode de corridor optionnel seulement, relâchement des touches WASD puis clic public canvas et relecture prête/au sol/active. Deadline initiale et compteur douze secondes sans progrès non réinitialisés ; toute absence de récupération reste FAIL. Trois échantillons frais de stabilité exigés après récupération.416 helpers figés, un modifié depuis H012, auto-test PASS. Aucun code produit/géométrie, point/tolérance, retouche ou critère de reprise changé. Cette préparation ne qualifie pas encore la récupération réelle.

Après fermeture du scénario environnemental, P02020/003 lancé depuis l'hôte ; session51997 confirmée démarrée, état instantané consigné dans le pointeur, aucun PASS anticipé. Boucle combat/P03/P04 restent acquis dans leurs portées séparées ; EnvironmentQuest non qualifié. Objectif complet Gate3 actif, aucune publication Git ni sous-agent.


## Actualisation206 — préparation environnement est stricte, observation P02 sans intervention

Le helper historique de contact environnemental envoie rawKeyDown, attend son acquittement, puis attend15–55ms avant keyUp. Le délai du renderer peut donc allonger l'appui réellement traité ; c'est un diagnostic de transport, pas une preuve de défaut production. L'ancien catch approach_bound permettait ensuite de tenter le lancer malgré l'échec0,025m/12s. Le garde causal post-action a correctement refusé tout PASS.

Nouvel essai préparé `integrated-public-environment-east-preparation-020-002/proof.json` pour EnvironmentQuest/ForestThrow : cible radiale est+0,575m, tolérance0,025m et borne12s conservées ; pilotage à appuis relâchés avant attente des acquittements, arrivée au repos avec trois observations fraîches et contrôle explicite X joueur>X objet. Toute approche échouée interdit désormais de lancer. Même géométrie/source produit, mêmes receipts/Saves/reprise, lecteur causal physique intact.416 helpers figés, deux modifiés (script scénario et helper gameplay H013), auto-test gameplay PASS et syntaxe scénario PASS (`syntax-proof.json`). Aucun replay environnement002 exécuté ni PASS anticipé.

Lecture séparée du seul HUD public P02020/003, sans focus/touche/état injecté : tick8221, X41,07/Z-130,91, simulation prête, entrée active, quête Completed. Observation `integrated-public-p02-focus-preparation-020-003/live-public-observation-001.json`. La session51997 est confirmée encore active avant cette actualisation ; état instantané consigné dans le pointeur sans qualifier retouche/reprise. Le nouvel essai environnement attend la fermeture de P02 pour conserver le focus exclusif. Objectif Gate3 actif et critères manquants inchangés ; aucune publication Git ni sous-agent.


### Reprise seul — P02/003 fermé en échec, EnvironmentQuest/002 lancé

Le replay P02/003 est terminal (exit 1) : le premier processus a nécessité un arrêt forcé ; le second a fermé normalement. Le journal rapporte la retouche, le refus de reprise ordinaire, la reprise compatible et la réplique retouchée, mais le lecteur complet de réussite ne peut pas être validé. P02 reste non qualifié. Le motif détaillé de fermeture n’a pas été conservé ; aucune cause produit n’est isolée. Les archives sources et le candidat sont recontrôlés inchangés dans `integrated-public-p02-failure-review-020-003/proof.json`.

EnvironmentQuest/002 est lancé dans sa copie isolée (session 84691), après vérification CIM de l’absence du candidat précédent. Le positionnement stabilisé à l’est est désormais obligatoire avant lancer, avec la même cible et la même borne de 12 secondes. Aucun impact ni seconde approche n’est encore qualifié. Aucun sous-agent ni publication Git. Gate 3 reste ouverte.


### P02/004 préparé — reçus de fermeture conservés

Une nouvelle copie figée H014 ajoute uniquement la conservation du reçu de fermeture (`Forced`, `Closed`, `GracefulCloseFailure`) aux quatre sites du pilote P02, dans le journal et le log. Les actions publiques, délais de fermeture, verdict d’arrêt forcé et lecteur complet de réussite sont inchangés. SelfTest PASS ; ce n’est pas un replay réel. Le prochain essai attend la fermeture d’EnvironmentQuest/002, toujours confirmé actif via session 84691. Preuve : `integrated-public-p02-close-trace-preparation-020-004/proof.json`. Gate 3 reste ouverte ; aucun sous-agent ni publication.


### EnvironmentQuest/002 échec avant lancer ; P02/004 actif

EnvironmentQuest/002 est fermé normalement, exit 1, sans lancer : l’approche stabilisée n’a pas atteint 0,025 m dans 12 secondes. La distance passe de 1,718 m à 0,370 m ; cinq impulsions (200, 266, 267, 1, 1 ms). Aucun contact nommé n’est observé dans cette phase, sans preuve d’absence de tout obstacle. Sources/candidat/helpers/fixture préservés et trois checkpoints rehashés ; seconde approche non qualifiée. Preuve `integrated-public-environmentquest-failure-review-020-002/proof.json`.

P02/004 est réellement lancé (session 85847), après vérification CIM de l’absence de candidat concurrent. Le résultat est attendu ; aucun PASS anticipé.

EnvironmentQuest/003 est préparé : corrections publiques proportionnelles à la distance et au déplacement relâché mesuré, seulement pour l’approche de lancer. Une observation fraîche de vitesse horizontale <= 0,01 m/s autorise la correction ; l’arrivée exige toujours trois observations stables fraîches dans 0,025 m. La cible +0,575 m, le délai 12 secondes, le garde de progression, la géométrie et les critères causaux restent identiques. SelfTest gameplay et syntaxe environnement PASS ; aucun replay réel de ce pilote. Il attend la fermeture P02. Aucun sous-agent ni publication ; Gate 3 reste ouverte.


### Couverture complète actualisée et création Studio préparée

Le relevé `integrated-candidate020-closure-requirements-review-002/proof.json` distingue les acquis locaux bornés, les critères partiels et les preuves manquantes. R3-P03 suit le « ou » du roadmap ; aucun second cas obligatoire n’est ajouté depuis le tableau « et » du plan. Stats/bornes, navigateur autonome, scénarios environnementaux complets, P05, intégration Primary et CI réelle au commit exact restent incomplets.

La création complète Studio sur candidat020 est préparée depuis `tidewater-authoring-001`, contrôlé sans gameplay authoré : 28 fichiers source rehashés, 416 helpers figés identiques au parent. Le pilote n’injecte aucun document gameplay ; les créations passent par les contrôles Studio, y compris la salle et le contrepoids. SelfTest authoring PASS ; aucun replay réel encore. La fixture utilisée par P01/P02 est conservée. P02/004 reste confirmé actif, session85847 ; les prochains replays attendent sa fermeture. Aucun sous-agent ni publication ; Gate3 ouverte.


### P02 première fermeture normale observée ; portée du pilote de création précisée

P02/004 demeure actif (session85847). Le reçu du premier processus indique Forced=false, Closed=true, GracefulCloseFailure=null ; copie de log partielle figée et preuve dans `integrated-public-p02-close-trace-preparation-020-004/first-close-observation-001`. La reprise et la fermeture du second processus restent attendues. Aucun PASS P02 anticipé.

Précision du précédent paragraphe : le pilote nommé « full-authoring » prépare la première étape de création publique, et ne recrée pas à lui seul les 22 entités du checkpoint. Les ajouts physiques, leurs corrections, les PNJ/patrouilles supplémentaires puis la revue de toutes les références devront suivre dans le Studio sur le même candidat. `integrated-public-full-authoring-preparation-020-001/scope-proof.json` consigne cette portée ; aucune qualification complète de création. Gate3 ouverte.


### R3-P02 candidat020 qualifié localement ; EnvironmentQuest/003 lancé

P02/004 terminé exit0. Revue Root `integrated-public-p02-root-review-020-retry-006/proof.json` PASS : seules réplique, porte X (+0,05 m) et révision changent ; liens et identités préservés, refus ordinaire puis reprise compatible publique, quête Completed et Sceau consommé conservés dans le second processus, réplique retouchée retrouvée. Franchissement réel extérieur/intérieur aux mêmes marges 0,35 m, ticks ordonnés, translation demandée/résolue positive et contact Floor observé. Deux fermetures natives normales, aucune instance candidate restante, sources originales inchangées.

Les lecteurs004/005 avaient respectivement exclu la phase cross_interior_kcc puis conservé un ancien chemin H010. Les corrections archivées réincluent les deux phases ordonnées du même journal et lient le lecteur aux 416 helpers H014 réellement exécutés, tous rehashés. Aucun seuil ou critère n’a été affaibli ; aucun nouveau runtime n’a été inventé. Ce PASS local ne clôture ni P01 environnemental ni P05, CI ou Gate3.

EnvironmentQuest/003 réellement lancé après contrôle CIM de fermeture, session63367, avec corrections publiques proportionnelles mesurées. Résultat attendu ; seconde approche encore non qualifiée. Création Studio complète demeure à réaliser au-delà de la première étape préparée. Aucun sous-agent ni publication.


### EnvironmentQuest/003 échec borné avant lancer ; création Studio de base lancée

Le replay environnemental003 ferme normalement (exit1, aucune force) avant lancer. Distance initiale1,812m, minimum0,165m, dernier0,172m ; la tolérance0,025m en12s reste non atteinte. Les corrections proportionnelles progressent mais aucune preuve d’impact, de neutralisation ou de seconde approche n’est acquise. Trois checkpoints rehashés ; sources/candidat/helpers/fixture préservés. Preuve `integrated-public-environmentquest-failure-review-020-003/proof.json`.

La première étape de création publique sur candidat020 est lancée réellement depuis Tidewater sans gameplay authoré, dans une copie isolée (session77764), après contrôle CIM de l’absence de candidat concurrent. Elle doit produire la base14entités puis requiert les étapes physiques/navigation restantes ; aucune recréation complète22entités n’est prétendue. Un lecteur préparé vérifie directement archives et liens : `integrated-public-base-authoring-root-review-020-001/preparation-proof.json`, syntaxe Python contrôlée seulement, pas de PASS runtime. P02 demeure qualifié localement ; Gate3 ouverte. Aucun sous-agent ni publication.


### Création Studio de base candidat020 qualifiée localement ; environnement004 lancé

La création publique de base termine exit0. Revue indépendante des archives `integrated-public-base-authoring-root-review-020-001/proof.json` PASS : 14 identités uniques (3 acteurs, 1 item, 5 objets, 3 lieux, 1 dialogue, 1 quête), liens et références vérifiés dans le document brut, support/charge, salle et porte conservés. La copie Tidewater source sans gameplay reste intacte, candidat et416helpers rehashés ; fermeture native normale confirmée, aucun processus candidat restant. C’est une création réelle actuelle, pas une reprise de verdict historique. Les ajouts physiques et PNJ/patrouilles supplémentaires restent à créer ; aucun checkpoint22entités complet ni runtime nouveau n’est acquis. La fixture des replays P01/P02 demeure préservée.

Le diagnostic environnement003 montre que les déplacements des pulses courts32–63ms (moins de0,1m) étaient exclus du recalibrage et conservaient la vitesse du pulse long. Le pilote004 prend ces réponses réellement mesurées dès5mm en compte uniquement dans le mode proportionnel explicite. Les critères d’arrivée0,025m, trois échantillons stables frais, cible+0,575m et délai12s restent inchangés, ainsi que le lecteur causal. SelfTest PASS uniquement avant lancement. EnvironmentQuest004 est réellement lancé après contrôle de fermeture, session69963 ; aucune seconde approche qualifiée à ce stade. Preuve de préparation `integrated-public-environment-short-calibration-preparation-020-004/proof.json`. Gate3 ouverte ; aucun sous-agent ni publication.


### Ajouts physiques publics préparés depuis la création de base actuelle

`integrated-public-physics-creation-preparation-020-001/proof.json` prépare la seconde étape depuis le ledger actuel de création de base020, validé Root. Les hashes du candidat, des helpers et de tous les fichiers source sont épinglés. Le mode existant CompletePhysicsFixture permet le même candidat, ajoute les éléments de lancer/piège/feu/rive/danger par les contrôles publics et préserve le parent. Le contrepoids déjà créé n’est pas redemandé. SelfTest PASS ; le replay n’a pas encore été exécuté et ne prouve aucun effet physique. Les corrections, patrouilles, sauvegarde/réouverture et qualification causale restent nécessaires.

EnvironmentQuest004 demeure confirmé actif via session69963 ; aucun autre replay natif n’est lancé en concurrence. Les chiffres14entités de base et22entités de l’ancien checkpoint n’imposent pas arbitrairement le nombre final : la couverture des capacités, liens et parcours reste le critère. La fixture des replays qualifiés est préservée. Gate3 ouverte ; aucun sous-agent ni publication.


### Environnement004 fermé en échec ; ajouts physiques Studio lancés

EnvironmentQuest004 termine exit1 et ferme normalement avant lancer. Minimum0,0706m, dernier0,1150m : ni tolérance0,025m ni stabilité finale acquises dans12s. Neuf pulses réels ont été observés ; aucune neutralisation ni seconde approche qualifiée. Trois checkpoints rehashés, candidat/helpers/source préservés. Preuve `integrated-public-environmentquest-failure-review-020-004/proof.json`. Le statut actif du paragraphe précédent correspondait au dernier poll avant sa terminaison.

La seconde étape d’authoring physique Studio est réellement lancée après contrôle CIM d’absence de candidat concurrent, session51560. Elle part de la création de base actuelle Root PASS dans une nouvelle copie ; résultat attendu, aucun effet physique ni authoring complet anticipé. Le diagnostic du centrage continue sans relâcher les critères d’acquisition. Gate3 ouverte ; aucun sous-agent ni publication.


### Ajouts physiques Studio qualifiés localement ; PNJ/patrouilles en création

L’authoring physique020 termine exit0. Revue `integrated-public-physics-creation-root-review-020-001/proof.json` PASS : 24 identités uniques (4 acteurs,12objets,4lieux,2dialogues,1item,1quête), base14entités préservée sauf Gardien.flees_danger=false explicitement authoré ; autres corrections de monde et anciens liens inchangés. Les relations plateforme/charge et support/piège, seuil destructible, propriétés feu/rive/poussable et réaction du veilleur sont contrôlés dans le document brut. Les receipts finissent au head_hash exact du workspace, sources/helpers/candidat rehashés et fermeture native normale confirmée. Cela prouve la création publique, aucun effet runtime ou navigation.

Les deux PNJ Patrouilleur du village et Messager de la salle sont en création publique sur cette base dans une nouvelle copie (session58315), après contrôle de fermeture. Les coordonnées de patrouille sont reprises comme données authorées du checkpoint020 épinglé ; aucun verdict de navigation antérieur n’est hérité. La fixture des replays qualifiés est conservée. Nouvelle préparation `integrated-public-navigation-creation-preparation-020-001/proof.json`, SelfTest PASS avant lancement ; verdict réel attendu. L’approche environnementale demeure non qualifiée, comme P05, navigateur autonome et CI réelle au commit final. Gate3 ouverte ; aucun sous-agent ni publication.


### Création patrouilles et réouverture du checkpoint qualifiées localement

Les patrouilles020 terminent exit0, Root PASS `integrated-public-navigation-creation-root-review-020-001/proof.json` : 28 identités uniques, données du parent24 et receipts préservés, deux PNJ et leurs dialogues/patrouilles contrôlés dans le document brut ; fermeture native normale confirmée. Aucun trajet runtime acquis par cette création.

Réouverture publique020 terminée exit0 et revue Root PASS `integrated-public-created-checkpoint-reopen-root-review-020-001/proof.json` : 22 formulaires éditables (6acteurs,12objets,4lieux), positions et statistiques relues, Save sans changement des archives, 28entités au total avec items/dialogues/quêtes ; fermeture sans force et candidat absent confirmés. L’adaptation explicite AllowSameCandidate conserve les vérifications complètes et la garde par défaut pour une révalidation de candidat différent.

Le head complet du checkpoint recréé depuis Studio est exactement identique au précédent checkpoint020 : head_hash855eb32bae6794e97624bd44090f9707035d566b67b2f9ac702b8bb27d73a348. Les archives diffèrent par leurs histories de création (b290d3af… vs19b4af30…) ; comparaison intégrale et hashes dans `head-equivalence-proof.json`. Les anciens «22entités» désignaient les22formulaires éditables ; le nombre global est28 dans les deux checkpoints. Aucun échec environnemental ni verdict global n’est transformé en PASS par cette équivalence.

ShorePush020/001 est réellement lancé (session44222) après vérification CIM de fermeture, dans la fixture isolée020 du même head. Résultat attendu : poussée/extinction et reprise doivent encore être examinées causalement. L’approche de lancer, les autres effets physiques, navigateur autonome, P05, Primary et CI exacte restent à qualifier. Gate3 ouverte ; aucun sous-agent ni publication.


### Rive001 bloqué avant action ; contournement public002 lancé

ShorePush001 termine exit1 et ferme normalement. À X29,4446/Z−111,4445, le KCC demande une translation positive Z mais ne résout aucun déplacement horizontal, avec20contacts ; collider894v0, position26,5/0/−112, nom absent. L’identité de l’asset n’est pas isolée. Le garde12s interrompt avant poussée/eau ; sources/helpers/fixture/candidat préservés, checkpoint rehashé. Preuve `integrated-public-shorepush-failure-review-020-001/proof.json`.

ShorePush002 contourne ce corridor par X38,5, conserve les deux premiers points publics et l’arrivée de rive, tolérance0,6m, borne360s et garde12s. Seuls les waypoints du pilote sont modifiés ; geometry, contrôles de contact, extinction, Save/reprise et lecteur causal sont identiques. Syntaxe PASS avant lancement ; replay réel actif confirmé session36925, aucun PASS anticipé.

Diagnostic de code `integrated-public-short-pulse-source-diagnosis-020-001/proof.json` : fichier player_controller Source010 contrôlé contre son manifest2532pins. Les taps relâchés enregistrés dans une frame sont conservés jusqu’au tick physique puis consommés une seule fois ; la durée nominale ne suffit pas à calibrer leur mouvement. Les receipts DOM seuls ne prouvent pas un défaut de sampling Bevy. Aucun code produit changé ni nouveau test exécuté par cette lecture. L’approche de lancer demeure non qualifiée. Gate3 ouverte ; aucun sous-agent ni publication.


### Rive002 observé actif ; point libre est avant centrage préparé

Rive002 est confirmé actif via session36925. Lecture HUD seule : tick16459, X38,87/Z−114,11, simulation prête, grounded et entrées actives. Le franchissement de la région de l’ancien blocage et les actions de rive ne sont pas encore qualifiés. La lecture ne modifie aucun contrôle ; artifact `integrated-public-shore-route-preparation-020-002/live-public-observation-001.json`.

Environnement005 est préparé dans une nouvelle copie figée : déplacement public coarse vers un point libre à+0,75m à l’est de la charge, tolérance0,05m, borne30s et garde de progression12s ; marge X contrôlée>=0,699m. Ensuite la cible précise reste+0,575m, tolérance0,025m, trois échantillons stables frais et borne12s. Cette étape de marche ajoute explicitement jusqu’à30s au temps total ; elle ne prouve ni performance ni acquisition. Aucun état joueur/objet injecté, geometry/candidat/lecteur causal inchangés. Syntaxe PASS seulement ; replay réel non lancé, en attente de fermeture Rive002. Preuve `integrated-public-environment-east-standoff-preparation-020-005/proof.json`. Gate3 ouverte ; aucun sous-agent ni publication.


### Campagne 221 — seconde approche environnementale vérifiée ; rive en reprise

EnvironmentQuest020/005 : commande native terminée (exit 0), puis lecteur causal indépendant PASS. Les preuves publiques montrent le lancer, l’impact et les dégâts au gardien avant le combat, la quête terminée et les états physiques conservés après Save/Resume. Preuve : `.gate3-evidence/20261002/tidewater-integrated-primary-020-public-environmentquest-005/root-causal-proof.json`. Cette réussite est locale, sur Source010/candidat020 ; elle ne clôture pas la Gate 3.

ShorePush020/002 s’est fermé normalement en échec avant poussée/extinction : limite globale du trajet atteinte au contact du collider à X40,2/Z−113,2, correspondant par sa position au bâtiment authoré 22. Le premier échec rencontrait le bâtiment à X26,5/Z−112. Les deux diagnostics sont conservés ; aucune qualification de rive n’en découle.

ShorePush020/003 lancé (session 59791) : hypothèse de passage entre ces bâtiments à X34, entrées WASD publiques, sans changement de géométrie ni de produit. Tolérance 0,6 m, limite totale 360 s et contrôles causaux/persistance inchangés. Résultat en attente ; lecteur préparé dans `integrated-public-physics-root-review-020-retry-006`. ADR0021, navigateur autonome, P05 propriétaire, intégration finale et CI exacte restent à qualifier. Aucune publication Git.


### Campagne 222 — bilan de couverture actualisé et prochain replay préparé

Le bilan `integrated-candidate020-closure-requirements-review-003/proof.json` rattache les preuves de création Studio publique (28 entités, 22 formulaires), de réouverture et d’égalité de tête complète, de P02 retouche/reprise, ainsi que les deux approches de quête. Ces acquisitions sont des PASS locaux bornés sur Source010/candidat020 ; elles ne valent pas qualification finale de Primary ou de la Gate 3. Le bilan conserve explicitement les critères physiques restants, navigateur autonome, ressources/inventaire, P05 humain, ADR0021 et CI finale.

ForestThrow020/001 est préparé avec les 416 helpers immuables du replay EnvironmentQuest005 et les critères stricts de contact inchangés. Aucun nouveau résultat de lancer/piège/destruction n’est encore revendiqué. Il attendra la fermeture de ShorePush003.

ShorePush003 reste vivant (session 59791 vérifiée). Lecture seule du diagnostic public : X33,23/Z−110,58, au sol, simulation prête ; dernier déplacement horizontal demandé et résolu concordant, contact avec le sol. Cela montre une position au-delà du Z des deux bâtiments rencontrés auparavant ; ni fin de trajet ni poussée/extinction n’est prouvée à ce stade. Les documents authorés et les preuves antérieures sont conservés. Aucune publication Git.


### Campagne 223 — rive 003 en échec clos ; replay physique forêt lancé

ShorePush020/003 : session 59791 terminée exit 1, fermeture native normale, aucune action de poussée/extinction. La limite du trajet est atteinte à `shore-approach-waypoint-3-step-4`, cible X34,09/Z−108,7257 ; 954 observations, distance minimale 1,9878 m. Les dernières translations peuvent être libres, mais l’ensemble de la phase contient 163 contacts non liés au sol avec trois colliders à X33,387/Z−109,707, X34,361/Z−109,967 et X35,337/Z−110,219. Les IDs de placements authorés ne sont pas établis : ne pas attribuer l’échec au seul pilotage ni nommer ces objets sans provenance. Preuves : `integrated-public-shorepush-failure-review-020-003/proof.json` et `contact-scope-proof.json`. Préservations source/candidat/helpers/fixture revérifiées.

ShorePush020/004 : hypothèse de détour à X50 préparée, syntaxe validée, non exécutée. Seuls les waypoints changent ; même tolérance 0,6 m, limite 360 s, contrôles publics et causaux. Aucun collider déplacé.

ForestThrow020/001 lancé après vérification de l’absence d’autre processus candidat, session 93211. Approche publique strictement identique à EnvironmentQuest005 ; le lecteur vérifiera séparément lancer, impact, chute/piège, destruction et Save/Resume. Résultat en attente. Gate 3 ouverte, sans publication Git.


### Campagne 224 — forêt 001 approche stricte en échec ; rive 004 lancée

ForestThrow020/001 : session 93211 terminée exit 1, fermeture native normale, source/candidat/helpers/fixture revérifiés intacts. Le point d’attente public à l’est est vérifié, mais le contact final ne respecte pas la tolérance inchangée de 0,025 m en 12 s. Aucune action de lancer n’a été exécutée ; aucune preuve supplémentaire de chute/piège/destruction n’est acquise. Preuve : `integrated-public-forestthrow-failure-review-020-001/proof.json`, journal de contact archivé. Cet échec distinct ne retire pas la réussite causale observée d’EnvironmentQuest005 ; il montre que la préparation ne garantit pas encore la reproduction de chaque contact précis.

ShorePush020/004 lancé après fermeture et contrôle d’absence d’autre processus candidat (session 78957). Détour public à X50, mêmes contrôles causaux/limites et géométrie intacte. Résultat en attente. Gate 3 ouverte ; aucune publication Git.


### Campagne 225 — rive 004 en échec ; essai forêt par axe en cours

ShorePush020/004 : session 78957 terminée exit 1, fermeture normale et préservations revérifiées, sans action de poussée/extinction. 662 observations dans la phase bloquée vers X49,91/Z−89,45 ; contacts latéraux avec des colliders vers X49,68/Z−89,00 et X49,28/Z−89,92. Leur correspondance authorée reste inconnue. Preuve : `integrated-public-shorepush-failure-review-020-004/proof.json`. Ni voie libre complète ni succès de rive ne sont revendiqués.

ForestThrow020/002 lancé, session 98361 : correction publique par axe dominant dans les derniers 0,4 m du contact uniquement. Le point d’attente, la tolérance euclidienne 0,025 m, la limite 12 s et les trois observations fraîches stables restent inchangés. Cette variante évite l’ajout simultané de deux touches lorsque la correction diagonale de 001 a dépassé la cible ; elle ne modifie ni produit, ni objet, ni critère causal. Les 416 helpers sont épinglés ; seuls le helper de pilotage et son appel de contact changent, syntaxe/autotests PASS. Résultat runtime en attente.

Les tests machine déjà archivés montrent des cas d’inventaire plein/ramassage atomique et de ressources bornées ; leur couverture précise doit encore être reliée aux preuves runtime avant tout verdict global. Gate 3 ouverte, sans publication Git.


### Campagne 226 — approche par axe mesurée ; scénario danger en échec ; lancer 003 lancé

ForestThrow020/002 : fermé normalement exit 1 avant tout lancer. Distance minimale 0,02210 m et dernière 0,02244 m, mais seulement une observation stable sur trois requises dans la limite de 12 s. Ce résultat reste FAIL, sans promotion d’une proximité momentanée en arrivée. Préservations source/candidat/helpers/fixture vérifiées et journal archivé dans `integrated-public-forestthrow-failure-review-020-002`.

ForestDanger020/001 : session 32277 terminée exit 1, fermé normalement, aucune action d’allumage ni autre action. L’approche publique de X17,05/Z−267,95 expire avant contact ; les dégâts de chaleur et la réaction IA restent non qualifiés. Preuve : `integrated-public-forestdanger-failure-review-020-001/proof.json`.

ForestThrow020/003 lancé (session 41956), après contrôle d’absence d’autre processus candidat. Une étape publique intermédiaire à +0,625 m de la charge, tolérance 0,015 m et limite supplémentaire de 30 s, prépare le contact final inchangé : +0,575 m, tolérance 0,025 m, 12 s, trois observations fraîches stables. Entrées WASD seulement, aucun déplacement privé d’objet/joueur ni modification des sources. Les effets sur le rythme/performance ne sont pas qualifiés. Résultat attendu ; Gate 3 ouverte, sans publication Git.


### Campagne 227 — correspondance géométrique des contacts et trajet de passerelle lancé

Les centres des colliders observés dans Shore003 et Shore004 correspondent à cinq boîtes des passerelles de la description `boardwalks.json` livrée dans le candidat020 : chemin latéral (`boardwalk:1`) puis chemin pier-to-plaza (`boardwalk:0`). Les transformations source sont appliquées, l’écart maximal est inférieur à 10 micromètres, le fichier et son manifeste d’import sont revérifiés. Il s’agit d’une correspondance positionnelle précise, pas d’une preuve directe d’identité ECS ; aucun défaut de production n’en est déduit. Preuve et description archivées : `integrated-public-shore-boardwalk-correspondence-020-001`. L’hypothèse de franchissement latéral d’un tablier relevé remplace le choix d’un détour sans géométrie vérifiée.

ShorePush020/005 lancé, session 82146 : approche de l’extrémité village du chemin pier-to-plaza, parcours de sa ligne centrale source à pas de 2 m, puis sortie vers la rive. 31 waypoints, mêmes tolérance 0,6 m et limite totale 360 s. Aucun asset/collider déplacé ni modifié, pas de changement du pilotage continu ni des critères causaux. Résultat en attente.

ForestThrow020/003 : session 41956 terminée exit 1, fermeture normale et préservations revérifiées. La première attente à +0,75 m échoue à la tolérance 0,05 m en 30 s ; l’étape intermédiaire et le lancer ne sont pas atteints. Preuve et journal : `integrated-public-forestthrow-failure-review-020-003`. Les critères physiques manquants restent ouverts. Gate 3 non qualifiée ; aucune publication Git.


### Campagne 228 — entrée de passerelle bloquée ; inventaire non vide ciblé

ShorePush020/005 fermé normalement exit 1, aucune poussée/extinction, préservations revérifiées. Le trajet atteint l’entrée village de la passerelle, puis s’arrête à X42,667/Z−107,566 contre sa première boîte (source center X42,754/Y3,681/Z−106,723) ; déplacement horizontal résolu nul et garde de non-progrès déclenchée. L’autostep existe dans le contrôleur configuré : l’absence de fonction n’explique pas ce résultat. Géométrie et journal sont archivés dans `integrated-public-shorepush-failure-review-020-005` pour reproduire ensemble la boîte, la capsule et le support terrain, puis distinguer solveur et préflight. Pas de défaut de production affirmé sans reproduction.

Audit indépendant des cinq sauvegardes publiques d’EnvironmentQuest005 : 30 états d’acteurs respectent vitalité/stamina/capacité/piles définies ; inventaires identiques entre after et resume. Les tests machine de ramassage atomique, refus d’inventaire plein sans consommation et réouverture sont reliés au log épinglé. Limite importante : toutes ces captures ont un inventaire vide après remise de quête. La conservation publique d’un objet détenu reste donc à prouver, de même que la couverture complète des limites de ressources. Preuve : `integrated-public-resource-inventory-review-020-001/proof.json`.

InventoryPreservation020/001 lancé (session 65852) : parcours public de forêt, ramassage du Sceau sans remise de quête, Save, fermeture et Resume dans un nouveau processus avec contrôle du HUD et du reçu unique. Résultat en attente. Un lecteur révisé lie correctement item/quantité à object.kind, d’après l’archive authorée réelle ; les critères ne changent pas.

InventoryPreservation020/002 préparé, non lancé : deux waypoints supplémentaires au nord du Sceau, tolérance 0,35 m et budget 60 s, pour établir la portée en trois dimensions depuis l’arrivée habituelle. La portée de l’action et les documents source restent inchangés. Aucun résultat d’inventaire non vide n’est anticipé. Gate 3 ouverte, sans publication Git.


### Suivi 229 — inventaire non vide conservé après reprise (2026-10-05)

Le replay InventoryPreservation020/002 et sa revue racine passent sur Source010 / candidat020 : ramassage public du Sceau, sauvegarde avec quantité 1, fermeture normale, reprise dans un nouveau processus, inventaire public Sceau × 1 et disparition de l’action de ramassage. Les inventaires de tous les acteurs concordent entre les sauvegardes après ramassage et après reprise ; le reçu de ramassage est unique, sans remise de quête. Preuve : `.gate3-evidence/20261002/tidewater-integrated-primary-020-public-inventorypreservation-002/root-inventory-proof.json`. Le lecteur initial avait une chaîne Unicode mal encodée ; le lecteur corrigé et son hash sont archivés sans modification du runtime ni des observations.

L’essai 001 demeure en échec : ramassage indisponible au point final forestier, fermeture normale, sources préservées ; la cause portée/visibilité n’est pas démontrée. La nouvelle approche nord utilise les commandes publiques. Ce PASS ferme le manque précis de persistance d’un inventaire non vide ; il ne qualifie pas tous les cas limites des ressources ni les états physiques restants. Gate 3 demeure OUVERTE — NON QUALIFIÉE ; P05 propriétaire, navigateur autonome, intégration finale et CI exacte restent à prouver, ADR0021 reste en attente. Aucune publication Git.


### Suivi 230 — reproduction CPU de l’immobilisation à la passerelle (2026-10-05)

La tuile réelle centrée en (64, 0, -64) est extraite du blob de terrain canonique dont le SHA-256 est vérifié, selon l’interpolation aux centres de cellules puis la conversion float32 du backend, sans correction de hauteur. Un probe indépendant utilise le fork Rapier épinglé de Source010, la capsule Tidewater et le seul collider observé à l’entrée. Ses dépendances concordent avec le lock Source010 ; sources et vendor restent intacts. La première invocation a échoué par mauvais répertoire courant ; la seconde a exécuté le même binaire depuis son répertoire correct.

Le mouvement public contraint (-0.01459865, 0, 0.00470483) reproduit exactement un déplacement nul pendant 180 ticks avec les normales de contact terrain et boîte observées. Une demande exploratoire constante vers le waypoint permet un déplacement, sans prouver le franchissement : elle ne rejoue ni le préflight Forge ni les boîtes voisines. Preuve : `.gate3-evidence/20261002/integrated-public-shore-entry-raw-rapier-probe-020-001/root-proof.json`. Étape nécessaire : reproduction intégrant `constrain_wall_drive` et le support avant attribution de cause ou correction de production. Aucun PASS ShorePush, aucune qualification de Gate 3, aucune publication.


### Suivi 231 — prédicats de contrainte de paroi reproduits (2026-10-05)

Le probe CPU compare les sweeps bruts avec/sans autostep et les casts frais sur le collider observé. Une demande cardinale +Z de 0.05 m (vitesse de marche 3 m/s à 60 Hz, demande amont inférée) produit une montée initiale avec un progrès négatif contre la normale de paroi. Le prédicat exigeant montée ET progrès supérieur au contrôle sans marches refuse alors l’exception autostep. La projection horizontale calculée concorde avec la demande du replay public à moins de 1 micromètre par composante ; le mouvement projeté reste nul, tandis que la demande non projetée permet de se déplacer après plusieurs ticks. Preuve : `.gate3-evidence/20261002/integrated-public-shore-entry-preflight-probe-020-002/root-proof.json`.

Cette reproduction des prédicats oriente précisément la correction mais ne remplace pas le test Bevy complet : demande amont inférée, boîtes voisines et support tangent absents. La première compilation du probe a échoué par import manquant, corrigé et archivé. Aucun code de production modifié, aucun PASS ShorePush ; conserver les garanties contre les murs, coins et pentes lors de la correction et rejouer le candidat public avant qualification. Gate 3 reste ouverte, sans publication.


### Suivi 232 — régression Bevy isolée lancée (2026-10-05)

Les 2532 fichiers épinglés Source010 sont copiés et vérifiés dans `integrated-shore-entry-bevy-diagnostic-source-011-001`, sans modification du code de production. Un test ajouté installe la tuile de terrain et le collider observés, synchronise la pose du backend, puis appelle directement `constrain_wall_drive` via `ReadRapierContext`. Il exige que la demande avant conserve un progrès suffisant plutôt que la projection immobilisante. Ce test de fonction ne prouve pas encore la traversée dans la simulation complète.

Le test exact est lancé avec un job Cargo et le cache Gate 3 ; les trois exécutables de test moteur précédents sont copiés et vérifiés avant lancement. Les builds externes observés utilisent d’autres caches et restent intacts. Session 4991, preuve de démarrage `integrated-shore-entry-bevy-diagnostic-011-001/started-proof.json`. Compilation en cours : aucun résultat ni correctif de production validé à ce stade. Gate 3 reste ouverte, sans publication.


### Suivi 233 — sélection exacte du test corrigée avant verdict (2026-10-05)

La vérification du module et des noms de tests antérieurs montre que le chemin exact est `player_controller::tidewater::tests::real_tidewater_boardwalk_entry_guard_preserves_recoverable_step`. L’invocation 001, session 4991 encore vivante lors du contrôle, compile avec un filtre de module erroné : elle ne pourra pas valider la régression, même avec exit0. Le runner 002 corrigé est préparé et exige le résultat terminal 001 avant tout lancement. Sources de compilation laissées intactes ; aucun zéro-test ne sera promu en PASS. Gate 3 demeure ouverte.


### Suivi 234 — préparation du test de traversée ECS complète (2026-10-05)

Pendant la compilation encore vivante (session 4991 repollée, aucune relance), les sources canoniques du terrain, le blob hashé et toutes les boîtes de boardwalk sont copiés dans une préparation indépendante. Le template `integrated-shore-entry-full-motion-test-preparation-011-001/full-motion-test.rs` remplace le plan de surface par le terrain source et prévoit 120 ticks de mouvement réel avec assertion de franchissement de l’entrée et de support au sol. Il n’est ni intégré aux sources en compilation ni compilé/exécuté. Limite explicite : bâtiments, acteurs et cylindres exclus. Ce test complète la régression de fonction ; il ne constitue pas un PASS. Gate 3 reste ouverte, sans publication.


### Suivi 235 — défaut de contrainte confirmé par Bevy, traversée baseline lancée (2026-10-05)

L’invocation 001 a terminé sa compilation en 7 min 19 s mais sélectionné zéro test : aucune qualification. L’invocation 002 corrigée a exécuté exactement un test, avec résultat 0 PASS / 1 FAIL. Le contrôleur Bevy appelle réellement `constrain_wall_drive` et produit la même projection immobilisante (-0.014597763, 0, 0.0047045425), au lieu de la demande avant attendue. Log, binaire et preuve racine conservés dans `integrated-shore-entry-bevy-diagnostic-011-001`; Source010 reste intacte.

Le test de traversée complète est intégré dans une NOUVELLE copie isolée `integrated-shore-entry-full-motion-source-012-001` et lancé en session 74707, avec terrain source et toutes les boîtes de boardwalk. Aucun code de production n’est encore corrigé. Ce second test permettra de distinguer le défaut de fonction de l’état de mouvement complet sur plusieurs ticks. Gate 3 reste ouverte et non qualifiée ; aucune publication.


### Suivi 236 — récupération autostep bornée étudiée (2026-10-05)

Dans le probe statique réel terrain + boîte observée, huit sweeps bruts montrent une récupération après le recul initial : au cinquième, progression contre la normale 0.07424 m, montée 0.16851 m, support grounded à chaque sweep. Cela dépasse la demande normale 0.04759 m et reste sous la limite de marche 0.4 m. Le binaire précédent est conservé ; preuve `integrated-shore-entry-step-lookahead-probe-013-001/root-proof.json`.

Une proposition isolée de code prévoit cette continuation uniquement après montée confirmée face à une paroi réellement bloquante, avec huit sweeps maximum, support requis à chaque sweep, hauteur limitée au profil et progrès supérieur à la demande plus bruit cumulé. Template `integrated-shore-entry-step-recovery-patch-preparation-013-001/tidewater_player-proposed.rs` : NON APPLIQUÉ, NON COMPILÉ, NON QUALIFIÉ. Le test full-motion baseline reste vivant, session 74707 repollée. Régressions murs/coins/pentes/saut et replay public restent requis ; aucun correctif de production validé, aucune clôture ni publication.


### Suivi 237 — PASS baseline insuffisant, correction isolée et test renforcé lancés (2026-10-05)

Le test full-motion baseline012 a effectivement exécuté un test et passé, mais ses assertions permettent de contourner les boîtes : pose finale (41.431168, 3.6953151, -105.256546), support terrain, aucun support sur le tablier exigé. La revue refuse d’en faire une preuve de traversée de la passerelle. Log, binaire et source baseline sont conservés.

La récupération bornée est appliquée dans une NOUVELLE source isolée013 ; aucune application Primary. Le test de traversée exige maintenant un contact KCC marchable avec une boîte du tablier, utilise le cap du deck et initialise le candidat de paroi observé au blocage. Les 28 tests du module locomotion, dont régression de contrainte et traversée renforcée, sont lancés en session11712. Sources formatées et manifestes versionnés ; aucun résultat encore validé, replay public toujours requis. Gate3 reste ouverte et non qualifiée, sans publication.


### Suivi 238 — couverture rafraîchie et replay danger préparé (2026-10-05)

La revue de couverture004 rehash les références et confirme la stabilité des critères roadmap/plan. Elle intègre le PASS inventaire non vide et les échecs physiques terminaux réels, sans promouvoir la correction isolée013 en preuve candidat020. Ressources et sauvegarde restent partiels ; poussée/piège/destruction/feu/eau/réaction danger ainsi que les preuves transverses restent à qualifier.

Un replay ForestDanger020/002 est préparé dans des helpers gelés distincts. Seul le pilotage de la dernière approche feu est remplacé par le pilote public stabilisé existant : cible (17.05,-267.95), tolérance0.04m et délai12s préservés, arrivée sur trois observations stables. Sources de gameplay, allumage, exposition et reprise inchangés. Parent helpers rehashés, parse PowerShell PASS ; replay NON LANCÉ. Les 28 tests de récupération restent en cours, session11712 repollée. Gate3 reste ouverte et non qualifiée ; aucune publication.


### Suivi 239 — récupération partielle, support deck toujours en échec, danger feu lancé (2026-10-05)

Les 28 tests source013 ont effectivement exécuté le module : résumé libtest 27 PASS / 1 FAIL. La contrainte d’entrée passe, comme les protections murs/pentes ; le seul échec est la traversée exigeant un contact marchable sur boîte du tablier. Le correctif isolé est donc NON QUALIFIÉ. Log, binaire et source sont conservés ; une ligne individuelle est intercalée dans le log parallèle, les comptes sont ceux du résumé libtest et des 28 tests exécutés.

Le diagnostic suivant prévoit pose, vitesse, retour KCC et rayon de support frais tous les dix ticks, sans suppression de l’assertion. Template014 préparé, non compilé, aucune modification des sources mesurées. Le replay ForestDanger020/002 est lancé en session57389 après fermeture du build et inventaire CIM confirmant aucun candidat natif existant. Approche stabilisée à critères inchangés, fixture isolée. Aucun PASS feu ni support deck encore acquis ; Gate3 reste ouverte, sans application Primary ni publication.


### Suivi 240 — danger feu002 en échec avant allumage, diagnostic support lancé (2026-10-05)

ForestDanger020/002 est terminal en échec avec fermeture normale et tous les pins sources/candidat/helpers/fixture préservés. L’approche nord-ouest échoue au segment waypoint2-step1 vers (22.2966667,-265), délai5s. Aucune action gameplay exécutée ; le dernier pilote précis stabilisé n’a pas été atteint. Cet essai ne prouve ni feu, ni dommages thermiques, ni fuite IA. Le lecteur causal préparé ajoute pour un futur replay complet la persistance du feu, l’unicité du reçu d’allumage et l’attribution de fuite à cette balise ; il n’est pas exécuté sur ce replay incomplet.

Le diagnostic de support014 est lancé dans une nouvelle source isolée, session69522. Code de production identique à la récupération013 ; seule l’observation pose/vitesse/feedback KCC/contact deck/rayon de support tous les dix ticks est ajoutée avant l’assertion inchangée. Sources et binaires antérieurs restent conservés. Aucun PASS support de passerelle, aucune application Primary ni qualification globale, aucune publication.


### Suivi 241 — échec du segment feu attribué prudemment au pilotage, reprise préparée (2026-10-05)

Les 24 observations du segment échoué002 montrent le focus actif, dix contacts marchables et aucun contact non marchable ni demande horizontale non nulle résolue à zéro. L’erreur minimale est0.24734m, finale0.80691m ; aucune preuve de blocage par paroi ne justifie une correction de production sur ce segment. Preuve `integrated-public-forestdanger-failure-review-020-002/segment-motion-proof.json`.

Le replay003 est préparé dans un nouveau harness gelé avec pilotage relâché/stabilisé de la même route nord-ouest : mêmes waypoints, tolérance0.1m, budget global120s, cinq secondes par sous-segment. Trois observations stables requises à l’arrivée ; la dernière approche0.04m/12s et tous les critères feu restent inchangés. Options ajoutées au helper de route sans modifier les autres routes par défaut ; les deux scripts PowerShell passent le parseur. Replay NON LANCÉ pendant compilation du diagnostic support014, session69522 repollée. Gate3 ouverte et non qualifiée, sans publication.


### Suivi 242 — diagnostic vertical terminal et replay feu003 lancé (2026-10-05)

Le diagnostic014 exécute un test : 0 PASS / 1 FAIL, assertion de contact marchable sur tablier inchangée. Les treize observations montrent une montée dans le déplacement effectif KCC mais une hauteur finale constamment ramenée à3.6993103m ; les treize rayons de support touchent le terrain, aucune boîte du tablier. Le recalage de walking_support sous0.06m est une hypothèse appuyée par le code, à confirmer par un replay correctif ; ce constat ne qualifie pas le franchissement. Source, log et binaire natif sont rehashés et archivés dans integrated-shore-entry-support-diagnostic-014-001/root-proof.json.

ForestDanger020/003 est lancé en session72341 après vérification CIM de l’absence du candidat existant. Même parcours et mêmes budgets, pilote stabilisé préparé précédemment ; fixture et helpers gelés vérifiés par le runner. Aucun PASS feu revendiqué avant résultat terminal et revue causale. Gate3 reste ouverte et non qualifiée, sans application Primary ni publication.


### Suivi 243 — feu003 terminal, préservation de montée KCC en test isolé (2026-10-05)

ForestDanger003 est terminal en échec avant allumage au premier sous-segment du premier waypoint du flanc nord-ouest. Fermeture normale, aucun reçu gameplay, sources/candidat/helpers/fixture rehashés et préservés. Les observations du segment montrent des contacts avec Gameplay object · Support de charge à(22,59.44654,-268) ; ces contacts ne suffisent pas à attribuer entièrement le délai du pilote. Preuve intégrée : integrated-public-forestdanger-failure-review-020-003/proof.json. Aucun PASS feu, dommages, fuite ou reprise feu.

Une nouvelle copie isolée015 conserve les poses ascendantes résolues par KCC quand le déplacement vertical effectif dépasse la demande positive et le bruit de contact. Le recalage plat reste actif hors de cette montée. Le test de traversée amorce désormais l’historique du collider capturé après le tick de synchronisation au repos ; ses exigences de support marchable sur tablier, progression et stabilité sont inchangées. Les28 tests du module locomotion sont lancés en session78576 après terminaison du replay public, sans concurrence native. Aucun résultat correctif acquis avant terminaison et revue. Sources et binaire014 restent archivés. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 244 — franchissement deck local acquis sur correction015, détour feu004 lancé (2026-10-05)

Les28 tests du module locomotion015 passent, sans échec ni test ignoré. Le test de traversée conserve ses exigences et termine à(44.16906,3.9474077,-102.525246), grounded=true, avec contact normal Y=0.99999994 sur une boîte du tablier et rayon final touchant cette boîte. Source et log rehashés, binaire archivé, seuls tidewater_player.rs et tidewater_player_tests.rs diffèrent de014. C’est une preuve locale Bevy/Rapier sur tuile canonique et boîtes de passerelles, sans les autres colliders du monde. Elle ne qualifie pas le candidat public020, le navigateur ni l’intégration finale. Clippy strict de la correction encore NON LANCÉ. Preuve : integrated-shore-entry-rise-preservation-015-001/root-proof.json.

Le détour feu004 passe le parseur PowerShell. Le support auteur reste à(22,59.44654,-268), demi-extents(1,0.5,0.5). Le nouveau parcours vise d’abord(20.5,-271), puis(25,-271), avant les points précédents ; tolérance0.1m, budget120s, sous-segments5s et précision finale0.04m/12s inchangés. Les critères gameplay et reprise restent intacts. Essai004 lancé en session54432 après terminaison du build et vérification CIM de l’absence du candidat natif. Aucun PASS feu revendiqué avant terminaison et revue. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 245 — revues feu004 et lint correction015 préparées (2026-10-05)

Le lecteur causal004 est une copie versionnée du lecteur002, seul le rattachement au dossier de lancement/helpers004 change. Il conserve les contrôles allumage, dommages thermiques, déplacement IA attribué à cette balise, persistance du feu, vitalité positive, reçu unique conservé à la reprise et immutabilité source. Python compile PASS ; lecteur NON EXÉCUTÉ avant résultat public complet.

Le runner strict015 prépare cargo fmt --all --check puis cargo clippy -p forge_engine --all-targets --all-features --locked --offline -j1 --message-format=json -- -D warnings. Source015 immuable, garde exigeant un résultat terminal du replay004 avant compilation ; runner NON LANCÉ. Le processus public004 est repollé vivant en session54432. Aucun PASS supplémentaire, aucun changement Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 246 — détour004 progresse mais délai automate insuffisant, contrôle strict015 lancé (2026-10-05)

Le replay004 est terminal en échec avant allumage, fermé normalement ; sources/candidat/helpers/fixture rehashés et préservés. Le premier segment dispose de neuf observations et trois impulsions relâchées : erreur initiale2.12975m, finale1.02141m. Aucun contact nommé Support de charge dans ces observations ; les contacts recueillis sont du terrain. Cette progression ne qualifie pas le scénario mais motive une adaptation du plafond automate. Preuve : integrated-public-forestdanger-failure-review-020-004/proof.json.

Replay005 préparé, NON LANCÉ : mêmes détours, budget global120s et tolérance0.1m ; plafond par sous-segment augmenté explicitement de5 à15s pour laisser le pilote relâché converger et constater trois arrivées stables. La précision finale0.04m/12s, les sources et critères allumage/dommages/danger/sauvegarde/reprise restent inchangés. Parse PowerShell PASS. Le contrôle format puis Clippy strict015 est lancé après terminaison du replay004, session79733 repollée vivante. Aucun verdict Clippy acquis avant résultat terminal. Aucun changement Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 247 — intégration correction015 examinée, revue feu005 préparée (2026-10-05)

Le lecteur causal005 est préparé avec les mêmes contrôles stricts que004, seul le rattachement lancement/helpers005 change. Compile Python PASS, revue NON EXÉCUTÉE, replay005 NON LANCÉ pendant Clippy79733 repollé vivant.

La revue d’intégration015 conserve un patch Source010→015 et deux comparaisons Primary→Source010. Les deux fichiers de locomotion Primary sont propres selon Git mais diffèrent réellement, au-delà des fins de ligne, de la source isolée010 : Primary ne contient notamment pas les témoins KCC et plusieurs tests ajoutés dans le chantier isolé. La future intégration doit tenir compte de ces ajouts et des consommateurs associés ; aucun remplacement n’est appliqué. Preuve : integrated-shore-entry-rise-preservation-integration-review-015-001/proof.json. Les28 PASS015 restent locaux. Clippy strict encore en cours, aucun nouveau PASS public ni qualification globale. Aucune publication.


### Suivi 248 — contrôle strict015 en échec, feu005 lancé et couverture ramassage précisée (2026-10-05)

Le contrôle015 est terminal : cargo fmt --all --check PASS ; Clippy strict forge_engine all-targets/all-features FAIL101. Trois diagnostics : resolve104/100 lignes, constrain_wall_drive103/100 lignes, conversion du nombre de contacts usize→f32 avec perte de précision possible. Source immuable vérifiée, logs et diagnostics archivés dans integrated-shore-entry-rise-preservation-strict-015-001/root-proof.json. Les28 tests locaux restent PASS, mais ne neutralisent pas cet échec strict. La prochaine correction doit extraire les responsabilités et effectuer une conversion bornée, sans masquer les avertissements.

Replay feu005 lancé en session84765 après terminaison de Clippy et vérification CIM de l’absence du candidat natif. Même détour et budget global120s, plafond sous-segment15s explicite, critères gameplay et précision finale préservés. Aucun PASS public revendiqué avant terminaison et revue causale.

La revue integrated-pickup-domain-coverage-review-020-002/proof.json rehash la source et le log machine010 : quatre tests de refus hors portée/blocage, ramassage atomique/idempotent, inventaire plein et sauvegarde immuable passent. Elle rapproche le PASS public inventaire non vide sans qualifier tout R3-A ; refus cible absente/raison visible, inventaire plein UI et comportements de focus/retour édition restent à vérifier publiquement. Gate3 reste ouverte, aucune application Primary ni publication.


### Suivi 249 — correction lint016 en validation, feu005 en échec au second segment (2026-10-05)

Une source016 distincte conserve015 intacte. Elle extrait la détection de montée, la gestion de l’historique de parois au sol et la recherche du collider ; le nombre de contacts est borné àu16 puis converti exactement en f32. Aucun avertissement n’est masqué. La préparation terrain/passerelles du test est extraite en helpers, désérialisation directe f32 des valeurs de fixture ; assertions de support marchable, progression, stabilité et finitude conservées. Rustfmt PASS,2536 pins gelés. Format workspace, Clippy strict forge_engine all-targets/all-features puis28 tests locomotion sont lancés en session19077 après terminaison du replay005. Aucun verdict016 acquis avant résultats et revue.

ForestDanger005 est terminal en échec avant allumage, avec fermeture normale et pins source/candidat/helpers/fixture préservés. Premier détour terminé ; segment waypoint1-step1 suivant échoue à la stabilisation15s, erreur minimale et finale0.150479m pour tolérance0.1m. Aucun reçu gameplay ; feu, dommages, danger IA et reprise feu restent non qualifiés. Preuve : integrated-public-forestdanger-failure-review-020-005/proof.json. La prochaine décision de pilotage doit partir de ces observations, sans promouvoir les essais incomplets. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 250 — diagnostics production résolus, corrections tests017 en validation (2026-10-05)

Le contrôle016 est terminal : format PASS, strict FAIL101 avec16 diagnostics finaux exclusivement dans tidewater_player_tests.rs :13 littéraux sans séparateurs, deux précisions excessives et test101/100 lignes. Les trois diagnostics production015 sont absents ; ce constat ne constitue pas un PASS strict global. Les28 tests016 ne sont pas lancés après cet échec. Source et log rehashés dans integrated-shore-entry-rise-preservation-lint-fix-016-001/root-proof.json.

Une copie017 conserve016 intacte. Seul le fichier de tests change : séparateurs et réduction de précision décimale avec vérification des mêmes bits IEEE f32 pour chaque remplacement ; extraction du bloc d’observation de passerelle dans un helper, assertions inchangées. Production et fixtures identiques à016, aucun avertissement masqué. Rustfmt PASS ;2536 pins gelés. Format, Clippy strict puis28 tests du module lancés en session7388 ; aucun verdict avant terminaison et revue. Le replay feu005 reste un échec conservé, aucune nouvelle qualification publique. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 251 — dernier diagnostic test corrigé018, budget centrage feu006 préparé (2026-10-05)

Le contrôle017 est terminal : format PASS, Clippy strict FAIL101 avec un seul diagnostic manual_is_multiple_of dans le helper d’observation. Log/source rehashés, résultat conservé ; les28 tests ne sont pas lancés après cet échec. La copie018 remplace uniquement sample%10==0 par sample.is_multiple_of(10), indices et observations identiques, production inchangée, aucun avertissement masqué. Format, strict puis module28 tests sont lancés en session80893. Aucun PASS018 revendiqué avant résultats terminaux.

Le replay feu006 est préparé, NON LANCÉ : même détour, même budget global120s, tolérance0.1m et trois observations stables ; plafond interne sous-segment20s au lieu de15s. Ce changement explicite répond aux corrections fines005 qui progressaient encore d’environ0.026m par impulsion au délai, erreur finale0.150479m. Précision finale0.04m/12s et critères feu/dommages/danger/sauvegarde/reprise inchangés ; parse PowerShell PASS. Aucun verdict gameplay nouveau. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 252 — format et Clippy strict018 acquis, module locomotion en cours (2026-10-05)

La phase018 format workspace passe ; Clippy forge_engine all-targets/all-features -D warnings termine à0, sans diagnostic, avec artefacts et build-finished success vérifiés et source2536 pins stable. Revue : integrated-shore-entry-rise-preservation-multiple-fix-018-001/strict-phase-root-proof.json. C’est un PASS de lint borné au moteur et ses dépendances, pas toute la qualification workspace/navigateur. Les28 tests locomotion sont en compilation, session80893 repollée vivante ; leur résultat018 n’est pas encore acquis.

Le lecteur causal feu006 est préparé avec les mêmes critères stricts que005, seul le rattachement de lancement/helpers change. NON EXÉCUTÉ, replay006 NON LANCÉ. Le runner de qualification élargie018 prépare tous les tests lib forge_engine puis la commande WASM stricte des huit bibliothèques déjà utilisée pour010 ; exige les trois phases018 terminales PASS et les259 inputs acquis rehashés. Ce runner est NON LANCÉ. Un nouveau candidat Studio corrigé reste à construire et à qualifier publiquement. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 253 — correction018 ciblée PASS, qualification moteur/WASM lancée (2026-10-05)

Les trois phases018 terminent à0 : format, Clippy strict all-targets/all-features moteur sans diagnostic et28 tests locomotion PASS0FAIL0ignored. La traversée conserve sa pose finale(44.16906,3.9474077,-102.525246), grounded=true, contact marchable sur boîte du tablier et rayon final source_deck=true. Sources2536 pins et logs rehashés ; binaire archivé. Revue : integrated-shore-entry-rise-preservation-multiple-fix-018-001/root-proof.json. Ce PASS ciblé n’est pas encore une qualification publique Studio ni la clôture Gate3.

La suite complète lib forge_engine puis WASM strict huit bibliothèques est lancée en session79897, après terminaison du module et garde de ses trois PASS. Inputs acquis259 rehashés avant exécution, source018 inchangée. Aucun verdict élargi avant résultats et revue. La chaîne de build020 inspectée exige frontend et natif sur même snapshot propre et mêmes inputs ; son adaptation au futur candidat corrigé nécessite un checkpoint privé018 réel, sans réutiliser le HEAD010 comme preuve de code modifié. Ce checkpoint et ce candidat ne sont pas encore créés. Feu006 reste préparé non lancé. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 254 — moteur et WASM018 acquis, feu006 lancé, builders021 prêts (2026-10-05)

La qualification élargie018 termine à0 : suite lib moteur705 tests découverts,697 exécutés PASS0FAIL et8ignored ; WASM strict release/no-default-features/web des huit bibliothèques PASS sans diagnostic, artefacts et build-finished success vérifiés. Sources2536 pins et259 inputs acquis rehashés et intacts. Revue : integrated-rise018-engine-and-wasm-preparation-001/root-proof-002.json ; cette seconde version clarifie découverts/exécutés sans promouvoir les huit tests ignorés. Aucun résultat candidat public corrigé encore acquis.

Les deux builders frontend/natif021 sont préparés et passent le parseur PowerShell. Ils exigent un checkpoint privé018 propre,2536 fichiers trackés et les hashes de son manifeste ; ils refusent de réutiliser le HEAD010 pour le code corrigé. Frontend/natif doivent correspondre au même checkpoint et aux mêmes pins ; HEAD et propreté sont contrôlés avant/après. Checkpoint privé et candidat021 NON CRÉÉS, builds NON LANCÉS.

ForestDanger020/006 lancé en session80181 après fin du contrôle WASM et vérification CIM d’absence du candidat natif. Critères et tolérances inchangés, budget global120s conservé, centrage20s par sous-segment explicite. Aucun PASS feu avant terminaison et revue causale. Aucune application Primary ni publication ; Gate3 reste ouverte et non qualifiée.


### Suivi 255 — checkpoint018 propre, chaîne build021 lancée, feu006 en échec conservé (2026-10-05)

Le replay feu006 est terminal en échec avant allumage, fermeture normale, sources/candidat/helpers/fixture rehashés et préservés. Premier segment : erreur finale0.100426m pour tolérance0.1m, minimum0.097439m et trois observations dans la tolérance mais aucune arrivée satisfaisant simultanément les trois observations stables. Aucun reçu gameplay. Revue : integrated-public-forestdanger-failure-review-020-006/proof.json. Aucune promotion du seuil ou du feu en PASS.

Le checkpoint privé018 est créé après fermeture du replay, parent f67494046b03a1437a1a25171743220862f39d15, nouveau commit376ecc0878eb50bd6720c705d0ae5aafe002cfb7.2536 fichiers sélectionnés identiques aux pins machine/module, état Git propre, aucune remote configurée, configuration CRLF locale au snapshot. Preuve : integrated-rise018-private-checkpoint-001/proof.json. Aucun commit Primary ni publication.

La chaîne build frontend021 puis natif021 est lancée en session31051, au stade garde/préparation/copie des inputs lors de la dernière observation. Elle vérifie checkpoint/source,3000 imports et11 fichiers kit puis exige le même frontend/natif ; les builders passent le parseur et leurs hashes sont gelés. Aucun artefact build qualifié avant résultats et revue. Aucun replay public du candidat corrigé encore acquis ; Gate3 reste ouverte et non qualifiée.


### Suivi 256 — frontend021 compile, branche privée compatible Studio et revue préparée (2026-10-05)

La chaîne021 a franchi les gardes et la copie des3000 imports/11 fichiers kit ; compilation frontend en cours, session31051 repollée vivante. Le script existant Build-StudioCandidate.ps1 exige la branche locale Rust. La branche du seul snapshot privé018 est donc renommée gate3-rise018-private→Rust, commit376ecc0878eb50bd6720c705d0ae5aafe002cfb7 et2536 pins inchangés, Git propre et aucune remote configurée. Provenance : integrated-rise018-private-checkpoint-001/branch-compatibility-proof.json. Aucun changement de branche Primary.

Le lecteur root021 est préparé, compile Python PASS et NON EXÉCUTÉ. Il exige les deux phases build PASS, checkpoint réel018, pins frontend avant/après et native concordants, formats PE/WASM, manifest worker et parité complète3000 imports/11 kit. Il vérifie également la propreté du snapshot Rust et le HEAD avant/après frontend/natif ; aucun PASS public n’est déduit d’une construction. Revue encore pending : integrated-studio-candidate-root-review-021/preparation-proof.json. Gate3 reste ouverte et non qualifiée, aucune publication.


### Candidat 021 — préparation du premier contrôle public Studio

Le runner public et son lecteur de preuves sont préparés pour le commit privé Source018 `376ecc0878eb50bd6720c705d0ae5aafe002cfb7`. Le harnais comprend 416 fichiers épinglés ; la fixture auteur source comprend 65 fichiers épinglés. Le parcours devra ouvrir le checkpoint Tidewater, vérifier les 22 formulaires éditables et sauvegarder sans modifier la tête canonique. Aucun résultat runtime du candidat 020 ne vaut qualification pour le candidat 021.

Preuves : `integrated-public-runtime-preparation-021/proof.json` et `integrated-public-revalidation-root-review-021/preparation-proof.json`. Exécution non effectuée à cette étape ; elle exige le contrôle racine du build frontend/natif 021. Gate 3 reste ouverte, sans publication. Travail poursuivi uniquement par l’agent principal conformément à la demande propriétaire.


### Candidat 021 — frontend et worker construits, replay du ponton préparé

Le frontend Studio et le worker de navigation ont terminé leur construction sur Source018. Les deux modules WASM et le manifeste worker sont rehashés ; le builder constate le même commit propre avant/après et des entrées source/assets stables. Preuve bornée : `integrated-studio-frontend-root-review-021/proof.json`. La construction native poursuit sa propre phase ; aucune exécution publique 021 ni qualification globale n’est acquise.

Le replay ShorePush est préparé dans `integrated-public-shore-boardwalk-preparation-021-001/proof.json`, avec 416 helpers copiés et rehashés à l’identique du parcours 020/005. Il exige le build apparié 021 et la revalidation auteur publique 021. Le trajet par le ponton, ses tolérances et les critères causaux push/extinction par l’eau restent identiques. La réussite de P01 sur 020 ne sert pas de prérequis à cet essai physique indépendant ; aucune réussite de gameplay 020 n’est héritée. Le lecteur conserve les exigences de sauvegarde/reprise et d’immutabilité auteur. Les fichiers Python passent le contrôle syntaxique ; le parcours reste non exécuté. Gate 3 ouverte, sans publication.


### Candidat 021 — build apparié et revalidation publique Studio PASS, ShorePush en cours

Le build frontend/natif est terminé et le lecteur racine vérifie 2536 entrées source, 3000 imports, 11 fichiers du kit, commit privé propre `376ecc0878eb50bd6720c705d0ae5aafe002cfb7` et concordance des modules Studio/worker embarqués. Exécutable SHA-256 `8b794b09c454015d201b2683936108a2b318b060216ebd86d99e489c3b021c07`. Preuve : `integrated-studio-candidate-root-review-021/proof.json`.

La revalidation publique 021 est terminée, code retour 0. Le lecteur `integrated-public-revalidation-root-review-021/proof.json` vérifie 22 formulaires éditables, la tête auteur complète inchangée, 65 fichiers de la fixture initiale, 416 helpers, acquittement de sauvegarde et fermeture normale. La capture après sauvegarde a été inspectée : grille Studio avant chargement 3D, sans qualification du rendu Tidewater. Aucun parcours RPG 020 n’est hérité.

Le replay ShorePush 021/001 est lancé après inventaire CIM confirmant l’absence préalable de cet exécutable. Preflight : `integrated-public-shore-boardwalk-preparation-021-001/shorepush/preflight-proof.json`, session 24052. Résultat terminal et critères causaux encore attendus. Le build session 31051 et la revalidation session 34912 sont terminés avec code 0 ; lecteur public session 53331 terminé avec code 0. Gate 3 reste ouverte ; intégration Primary, qualification transversale et décisions humaines restent séparées. Aucune publication.


### ShorePush 021/001 — ancien blocage dépassé, waypoint 8 non atteint ; replay stabilisé 002 lancé

Le replay 021/001 termine avec code 1 et fermeture normale, sans poussée ni action sur l’eau. Le lecteur `integrated-public-shorepush-failure-review-021-001/proof.json` confirme la stabilité du candidat, des helpers et de la fixture, et la progression jusqu’aux waypoints 5, 6 et 7, au-delà du blocage d’entrée 020/005. Le waypoint 8 expire après 245 secondes restantes : 654 observations avec entrée active, distance minimale 0,633964488 m, tolérance inchangée 0,6 m. Ces observations ne constituent pas une réussite globale du contrôleur ni de R3-B.

Le replay 021/002 remplace uniquement le pilotage public du trajet de rive par des impulsions relâchées et des observations stabilisées. Le même tracé, l’échantillonnage au plus 3 m, la tolérance 0,6 m et la limite globale de 360 s sont conservés ; chaque sous-segment dispose au plus de 30 s. Les critères causaux push/extinction, le produit et la géométrie sont inchangés. Deux helpers adaptés passent le parseur PowerShell, les fichiers Python passent leur contrôle syntaxique. Préparation : `integrated-public-shore-settled-route-preparation-021-002/proof.json` et `syntax-proof.json`. Lancement après CIM absence du candidat, session 62322 ; résultat terminal encore attendu.

Les écarts d’intégration locomotion vers Primary sont inventoriés dans `integrated-rise018-primary-integration-scope-001/proof.json` et `...-002/proof.json` : deux fichiers Rust, témoins KCC et garde de jonction issus de Source010 inclus dans le diff complet, sept fixtures compilées absentes de Primary. Les deux cibles Rust ne présentent aucune modification Git locale lors du relevé. Aucun code Primary n’est encore remplacé. Les changements auteur existants sont préservés. Gate 3 ouverte, sans publication.


### Intégration locomotion Source018 dans Primary ; ShorePush 021/002 arrêté au waypoint 22

Deux fichiers Rust et sept fixtures sont intégrés dans Primary après contrôle des hashes actuels et de l’absence de modifications Git sur les cibles. Les originaux des deux fichiers sont archivés, les entrées Git préexistantes préservées. Preuve : `integrated-rise018-primary-locomotion-apply-001/apply-proof.json`. Les fins de ligne des deux fichiers intégrés sont normalisées en LF ; formatage ciblé et diff-check PASS. Le texte est identique à Source018 après cette normalisation (`integrated-rise018-primary-locomotion-format-001/source-equivalence-proof.json`). Le contrôle cargo fmt global échoue sur des modifications préexistantes d’autres fichiers, notamment SourceGraph/forge_assets ; elles ne sont pas reformatées par cette correction. Aucune qualification de l’ensemble Primary n’est déduite.

ShorePush 021/002 termine code 1 et fermeture normale, sans actions push/eau. 29 sous-segments stabilisés sont acquis jusqu’au waypoint 21. Au waypoint 22, le pilote s’arrête sur un échantillon au sol false, simulation prête/entrée active, contact walkable présent ; l’observation précédant la sauvegarde d’échec retrouve au sol true. Preuve bornée : `integrated-public-shorepush-failure-review-021-002/proof.json`. R3-B reste incomplet. Le harnais 021/003 attend le support avec les touches déjà relâchées, sans nouvelle impulsion durant cette attente. Le trajet, la tolérance 0,6 m, les trois échantillons stabilisés d’arrivée et la limite globale de 360 s restent inchangés. Préparation et parseur : `integrated-public-shore-support-wait-preparation-021-003/`, parcours non exécuté.

Le premier test de locomotion Primary échoue à compiler : 12 exports forge_assets non retrouvés par forge_engine, alors que les modules sont déclarés dans la source actuelle. Log et entrée stable : `integrated-rise018-primary-locomotion-validation-001/result-proof.json` (code 101). Une réutilisation de métadonnées du cache partagé est une hypothèse, pas une cause prouvée. Le lancement 002 impose codegen-units=2 aux sept crates Forge du graphe test, sans changer leurs niveaux d’optimisation ni effacer le cache ; le log constate leur recompilation effective. 1041 entrées Rust/manifests/shaders/fixtures sont épinglées, inputs acquis épinglés séparément. Session 48848 encore active ; résultat terminal attendu. Aucun nouveau parcours public n’est lancé pendant cette compilation. Gate 3 reste ouverte, sans commit ni publication.


### Tests Primary — compilation fraîche PASS, module réel 27 PASS / 1 FAIL ; ShorePush 003 lancé

La compilation fraîche des sept crates Forge termine code 0. Le filtre initial `tidewater_player::tests` correspond au nom du fichier et ne sélectionne aucun test : 0 exécuté, 751 filtrés. Le code retour 0 du lancement 002 ne qualifie donc pas la locomotion. Le module réel `player_controller::tidewater::tests` est relancé sur les mêmes 1041 entrées stables, lancement 003 : 28 tests exécutés, 27 PASS, 1 FAIL, 0 ignoré. Le test de plancher surélevé `elevated_room_floor_preserves_raw_kcc_forward_translation` échoue ; le test réel de ponton en mouvement passe. Log et exécutable mesuré sont archivés (`integrated-rise018-primary-locomotion-validation-003/root-failure-proof.json`).

Primary reste sur Rapier du registre ; Source018 utilise le patch local proposé par ADR-0021. Cette différence de dépendance est vérifiée dans les manifests, mais n’établit pas à elle seule une causalité exclusive de l’échec courant. Aucun patch Cargo/Rapier ni acceptation ADR-0021 n’est appliqué implicitement. Les dep-info récents incluent désormais les nouveaux modules SourceGraph/Debris, absents des versions précédentes ; l’identité exacte de l’artefact utilisé pendant le premier échec n’a pas été capturée (`integrated-rise018-primary-cargo-artifact-provenance-001/proof.json`). Pas de fausse qualification ni d’effacement du cache.

La provenance de la fixture de jonction est recopiée à l’identique de Source018 et les hashes des fixtures de ponton/terrain sont consignés dans Primary (`integrated-rise018-primary-fixture-provenance-001/proof.json`). Les données numériques et géométriques sont inchangées ; aucune qualification de distribution/licences n’en est déduite.

Le replay ShorePush 021/003 démarre après fin de la compilation et inventaire CIM sans instance préalable du candidat. Il utilise la copie auteur isolée et l’attente du support sans nouvelle impulsion ; aucune modification du produit ou de la tolérance. Session 64975 encore active ; résultat terminal attendu. Gate 3 reste ouverte, sans publication Git.


### ShorePush 003 terminal ; scénario ForestThrow 021 lancé indépendamment

ShorePush 021/003 termine code 1, source et fixture intactes, fermeture normale, aucune poussée/extinction exercée. Le point d’arrêt est `shore-approach-waypoint-4-step-1` : 26 observations, distance minimale 0,640584783 m pour une tolérance de 0,6 m ; dernière pose au sol/stabilisée. 63 observations d’attente du support sont enregistrées dans l’ensemble du parcours. Le lecteur `integrated-public-shorepush-failure-review-021-003/proof.json` conserve ce diagnostic. Le passage de rive reste à résoudre ; aucun critère n’est retiré ou relâché.

Le scénario indépendant ForestThrow 021/001 est préparé avec 416 helpers identiques au harnais public de base 021. Les critères conservés exigent lancer, dégâts d’impact, chute/piège, destructible et sauvegarde/reprise des états des objets. Il utilise la revalidation auteur 021 et le candidat Source018 courant ; aucune réussite 020 n’est héritée. Dossier `integrated-public-forestthrow-preparation-021-001/proof.json`. Lancement après CIM absence du candidat, session 14208 ; résultat terminal attendu. Les tests Primary sont terminés avec les limites 27 PASS / 1 FAIL précédemment documentées, et aucun Cargo lourd ne concourt à cet essai public. Gate 3 ouverte ; pas de publication Git.


### ForestThrow 021/001–002 terminaux ; helper 003 chargé et replay lancé

ForestThrow 021/001 parcourt la forêt mais échoue avant le lancer : l’approche ancienne expire à 0,031528328 m, au-delà de la tolérance inchangée 0,025 m, puis le catch poursuit vers l’action. Le témoin KCC montre un contact avec la charge avant une dérive ; le refus de contact au moment du clic reste cohérent avec la pose courante. Aucun lancer ni dégâts ne sont acquis. Preuve : `integrated-public-forestthrow-failure-review-021-001/proof.json`.

ForestThrow 021/002 termine code 1 avec fermeture normale et sources conservées : le helper stabilisé appelle `Send-Gate3TimedKeyPulse`, absent de cette copie. Actions vides, aucun lancer ni résultat physique qualifié. L’échec du harnais est archivé dans `integrated-public-forestthrow-helper-failure-review-021-002/proof.json`. La copie 003 ajoute uniquement cette fonction de transport public depuis le harnais de rive existant. Chargement SelfTest, résolution des onze commandes utilisées et parseur PowerShell passent (`integrated-public-forestthrow-helper-preparation-021-003/dependency-proof.json`). Même approche X+0,575 m, tolérance 0,025 m, trois observations fraîches au sol/stables, mêmes six critères causaux ; aucun produit, garde ou contenu modifié. Session 19275 lancée après inventaire CIM sans candidat courant ; résultat terminal attendu. Aucun Cargo lourd concurrent.

La comparaison Rapier A/B 002 fige deux copies identiques du Primary récent, seules Cargo.toml/Cargo.lock et 217 fichiers du fork diffèrent ; versions et graphe de dépendances conservés. Le stage registre échoue à compiler sur quatre erreurs d’emprunt du nouveau scanned_scene.rs (E0499/E0502), avant tout test ; le stage fork n’est pas exécuté. Cela ne prouve aucune causalité Rapier. Le fichier Primary courant contient déjà une portée d’emprunt corrigée par un autre travail ; cette revue ne le modifie pas et ne le qualifie pas. Preuve : `integrated-primary-rapier-ab-compile-failure-review-002/proof.json`. Un nouveau snapshot sera nécessaire pour comparer cet état corrigé ; les anciens inputs et logs restent conservés. ADR-0021 toujours proposée sans acceptation déduite, aucun patch Cargo Primary. Gate 3 ouverte, sans commit ni publication.


### ForestThrow 021/003 — six critères causaux locaux PASS ; comparaison Rapier actuelle lancée

Le replay corrigé 003 termine code 0, deux processus fermés normalement, candidat/helpers/fixture stables. Le lecteur indépendant conservé vérifie les quatre garanties de flux/provenance/sauvegarde et les six critères ForestThrow : lancer, dégâts d’impact, chute/piège, destructible, état de la charge lancée sauvegardé/repris, états du piège sauvegardés/repris. Preuve : `tidewater-integrated-primary-021-public-forestthrow-003/root-causal-proof.json` ; SHA candidat `8b794b09c454015d201b2683936108a2b318b060216ebd86d99e489c3b021c07`, tête gameplay auteur `855eb32bae6794e97624bd44090f9707035d566b67b2f9ac702b8bb27d73a348`. Le lancer est accepté après approche relâchée/stable, sans catch continuant une approche échouée. Ce résultat ne prouve ni la quête environnementale complète, ni feu/eau/poussée/danger, ni navigation ADR-0009, ni revue humaine P05, ni l’intégration complète du principal. Aucun résultat ancien n’est hérité.

La préparation A/B 003 fige 2393 fichiers du Primary courant et conserve les ajouts/modifications de débris/ombres. Deux copies distinctes sans partage de fichiers, registre/fork, ne diffèrent que par Cargo.toml/Cargo.lock et les 217 fichiers Rapier ajoutés ; mêmes versions et dépendances transitives. Preuve : `integrated-primary-rapier-fork-ab-preparation-003/proof.json`. Après terminal du replay et inventaire CIM sans candidat restant, session 24437 lance séquentiellement les 28 tests de locomotion sur registre puis fork, dans les copies isolées. Résultat terminal attendu ; aucune modification Cargo Primary ni acceptation ADR-0021. Gate 3 reste ouverte, aucune publication.


### Suite complète EnvironmentQuest 021 préparée ; A/B toujours en compilation

Les états ForestThrow 003 montrent le gardien vivant avant le lancer puis vitalité zéro après, avec support détruit et charge piège libérée ; la reprise conserve ces états. Le scénario complet EnvironmentQuest 021/001 est préparé sur le même harnais public corrigé, sans modification de ses 416 fichiers. Il doit accepter la quête au village, traverser la forêt, prouver la neutralisation environnementale avant tout combat, ramasser le Sceau, revenir au village, terminer la quête, sauvegarder puis reprendre dans un nouveau processus. Le lecteur exige le flux complet et les états causaux persistés ; aucun PASS n’est déduit de la seule préparation ni du scénario ForestThrow. Preuve de préparation : `integrated-public-environmentquest-preparation-021-001/proof.json`. Essai non lancé pendant la compilation.

La session A/B 24437 est confirmée active ; le registre compile désormais forge_engine après les autres crates Forge. Le lecteur `integrated-primary-rapier-fork-ab-preparation-003/review.py` est préparé pour contrôler exactement les mêmes 28 noms de tests, l’unique échec attendu du plancher surélevé côté registre, 28 PASS côté fork, les hashes des deux exécutables archivés, les sources figées et le graphe Cargo identique. Il n’est pas encore exécuté, et le résultat attendu n’est pas présenté comme constaté. Gate 3 ouverte ; ADR-0021 non acceptée implicitement, aucune publication.


### A/B registre 003 terminal et relu ; stage fork poursuivi sans reconstruire le registre

Le stage registre termine 101 : 28 tests exécutés, 27 PASS, 1 FAIL, 0 ignoré. L’unique échec est le plancher surélevé ; l’exécutable mesuré et le log sont archivés. Le runner A/B s’arrête car son contrôle attend le nom et FAILED sur une seule ligne ; les diagnostics nocapture s’intercalent. Ce défaut de lecture est consigné, sans modifier le résultat original FAILED_CLOSED. Le lecteur `integrated-primary-rapier-ab-continuation-004/registry-root-review.json` vérifie les 28 noms, la liste finale des échecs, les comptes, le code retour et les hashes. Pas de seconde compilation registre. Le stage fork est lancé seul sur l’autre copie figée 003, session 69079, sources et dépendances inchangées, résultat attendu. Un lecteur combiné vérifie les deux snapshots et les deux exécutables avant toute conclusion causale. Aucun patch Cargo Primary ou acceptation ADR-0021.

L’analyse du journal ShorePush 021/003 (`integrated-public-shore-input-cadence-review-021-003/proof.json`) relève six impulsions au waypoint 4 ; trois appui/relâchement ont une étendue de réception navigateur inférieure à 1 ms et un déplacement stabilisé inférieur à 0,06 m. Cela ne prouve pas un blocage produit. La copie de harnais 004 change uniquement la calibration des impulsions longues de ceil(médiane+16) à ceil(2*médiane+16), avec le même clamp 200–600 ms ; micro-impulsions 1 ms inchangées. Même trajet de 31 points, mêmes limites globales/sous-segments, même tolérance et exigences d’arrivée au sol/stable, mêmes critères push/eau. Chargement SelfTest et parseur PASS (`integrated-public-shore-twoframe-preparation-021-004/`). Replay non lancé pendant le Cargo. Le parcours complet EnvironmentQuest est également prêt mais non lancé ; Gate 3 ouverte et sans publication.


### Comparaison Rapier sur le Primary figé — causalité de substitution reproduite ; quête complète 021 lancée

Le stage fork 004 termine code 0, 28 tests exécutés PASS, zéro échec/ignoré, source figée stable. Le lecteur combiné `integrated-primary-rapier-ab-continuation-004/root-comparison-proof.json` passe : exactement les mêmes 28 noms de tests que le registre 003 (27 PASS/1 FAIL, unique plancher surélevé), sources de production identiques A/B, seuls manifests/lockfile et 217 fichiers de vendoring diffèrent, versions et dépendances transitives identiques. Les logs et deux exécutables mesurés sont conservés et vérifiés. Cette substitution reproduit la correction du plancher surélevé dans le contexte du snapshot récent du Primary ; elle ne qualifie pas les modifications suivantes du Primary, n’autorise pas l’adoption du fork et ne clôture aucune gate. ADR-0021 toujours proposée, aucun changement Cargo Primary.

Après fin du Cargo et inventaire CIM sans candidat restant, le scénario complet EnvironmentQuest 021/001 est lancé, session 69592, sur la revalidation auteur 021 et une nouvelle fixture isolée. Il exige quête acceptée, traversée, neutralisation par environnement avant combat, objet récupéré, retour et quête complétée, sauvegarde/reprise dans un nouveau processus. Aucun résultat encore déduit ; les preuves ForestThrow 003 ne sont pas substituées à ce flux complet.

Le parcours navigation du candidat 021 est aussi préparé (`integrated-public-navigation-preparation-021-001/preparation-proof.json`) : observer public passif byte-identique au harnais testé, contrats synthétiques du helper réutilisés uniquement après égalité de hash. Aucune réussite runtime 020 n’est héritée. Essai navigation non lancé, futur lecteur et revue physique encore requis. Gate 3 reste ouverte, sans publication Git.


### Préparations navigation et P01 du candidat 021 ; quête environnementale active

La session EnvironmentQuest 69592 est confirmée active : quête acceptée par l’interface publique, trajet forestier en cours ; aucune réussite complète déduite. Les lecteurs navigation 021 sont préparés avec les mêmes critères physiques que le précédent protocole : observations archivées fraîches, acteur intérieur bloqué porte fermée, déplacement extérieur et jonction de tuiles, sortie physique après ouverture, stabilisation reached, révisions de navigation à ouverture/fermeture, reçus exacts sauvegardés et fermeture des processus. Dossier `integrated-public-navigation-root-review-021-001/preparation-proof.json`, syntaxe Python/PowerShell vérifiée, lecteurs non exécutés.

P01 021/001 est préparé dans `integrated-public-p01-preparation-021-001/proof.json` sur le harnais public à micro-corrections byte-identique et épinglé. Il exige d’abord un vrai PASS navigation 021 sur le même candidat et la même tête, puis le parcours P01 complet, visite intérieure, sauvegarde et reprise dans un nouveau processus. Le PASS 020 n’est pas hérité. Replay P01 non lancé et lecteur final encore à préparer. Les préparations ne constituent pas une qualification ; Gate 3 ouverte, travaux solo, aucune publication Git.


### Lecteur P01 021 préparé ; quête complète au retour village

Le lecteur `integrated-public-p01-root-review-021-001/preparation-proof.json` est préparé et syntaxiquement vérifié : mêmes cinq reçus d’attaque par contact à 20 dégâts, pickup/quête complétée exacts, visite intérieure après quête avant Save, poses réellement dedans/dehors, sauvegarde/reprise dans un nouveau processus, huit sources authorées inchangées et hash du harnais exécuté. Seuls les chemins, le candidat Source018 et le nombre de sources vérifié sont adaptés. Aucun replay/lecteur P01 encore exécuté.

La session EnvironmentQuest 69592 reste confirmée active : lancer et récupération du Sceau franchis, retour village en cours. Ces phases ne prouvent pas encore la complétion de quête et la reprise ; résultat terminal attendu. Aucun second scénario ni Cargo lourd lancé concurremment. Gate 3 ouverte ; aucune publication Git.


### Boucle environnementale complète 021 PASS local ; navigation actuelle lancée

EnvironmentQuest 021/001 termine code 0 et deux fermetures normales. Le lecteur indépendant `tidewater-integrated-primary-021-public-environmentquest-001/root-causal-proof.json` passe les quatre garanties de flux/provenance/sauvegarde et les critères quête environnementale terminée/sauvegardée/reprise, lancer, dégâts d’impact et source d’impact persistée. La neutralisation du gardien vivant par le décor est démontrée avant tout combat ; pickup, retour au village, quête Completed, Save puis nouveau processus Resume acquis. Candidat Source018 021 inchangé, authoring public conservé. Ce résultat ne qualifie pas feu/eau/poussée/IA-danger ni P01 intérieur/deux approches, P02 retouche, P03/P04, P05 humain, toute la navigation ou le Primary courant.

Après terminal et inventaire CIM sans candidat restant, navigation 021/001 est lancée, session 94234, mêmes critères physiques et reçus, aucune réussite précédente héritée. Résultat terminal attendu. Aucun Cargo lourd ou autre scénario concurrent.

P02 021/001 est préparé avec obligation d’un P01 021 PASS et de sa vraie sauvegarde (`integrated-public-p02-preparation-021-001/proof.json`). Le harnais épinglé reste identique ; retouche publique réplique/porte, refus Resume ordinaire puis reprise compatible explicite, préservation identité/liens/quête et deux fermetures normales restent requis. Le lecteur actuel (`integrated-public-p02-root-review-021-001/preparation-proof.json`) vérifie le diff source brut limité au texte/X de porte/révision, les poses extérieures/intérieures et le mouvement KCC/contact Floor réels. Il prend le chemin du harnais depuis la préparation courante, sans hériter d’un lecteur ancien non exécuté. Syntaxes vérifiées ; replay et lecteur P02 non exécutés. Gate 3 ouverte, ADR-0021 non adoptée implicitement, aucune publication Git.


### Navigation 021/001 terminale avant ouverture ; capture réellement rendue et feu/danger préparés

Navigation 021/001 termine code 1 avec fermeture normale et sources conservées. La fenêtre porte fermée/en extérieur enregistre des rapports PNJ no_path, déplacement dehors et raccord de tuiles, mais leur qualification indépendante complète n’est pas déduite ici. Le joueur échoue au premier centrage intermédiaire de 0,1 m avant Open : 225 observations, 46 impulsions relâchées, distance minimale 0,304493179 m. Les contacts des observations de centrage sont du terrain sans nom. Le lecteur `integrated-public-navigation-failure-review-021-001/proof.json` vérifie logs/launch, hashes des observations et helpers ; ouverture, sortie, fermeture et reçus Save ne sont pas achevés. La cause produit exclusive reste non isolée ; P01 reste bloqué sur sa condition de navigation réelle, sans relâcher le critère.

La capture `run1-navigation_ready.png` est personnellement inspectée et épinglée dans `integrated-public-navigation-visual-review-021-001/proof.json` : terrain, végétation, maisons, montagne et nuages Tidewater visibles ; grande capsule bleue de personnage et salle rectangulaire provisoire visibles. Une image de démarrage ne prouve ni le parcours entier, ni la fidélité/performance, ni P05 humain. Aucun PASS visuel global.

ForestDanger 021/001 est préparé (`integrated-public-forestdanger-preparation-021-001/proof.json`) pour allumage, dommages thermiques et réaction IA au danger sur le candidat actuel. Le harnais de corrections publiques proportionnelles est épinglé et byte-identique ; son précédent essai 020 échouait avant ignition et aucune réussite n’est héritée. Critères, géométrie et gardes inchangés ; replay/lecteur non exécutés. Gate 3 ouverte, aucune publication Git.


### Navigation 021/002 — corrections relâchées proportionnelles lancées, critères conservés

Le replay 001 répète notamment des impulsions de 1 ms près du point sans atteindre 0,1 m. Le harnais isolé 002 remplace uniquement le helper de centrage par sa version existante de corrections proportionnelles au déplacement réellement observé, et active l’option dans les deux sites d’appel de centrage de navigation. Les points, délais et gardes restent inchangés ; arrivée toujours dans 0,1 m, trois observations fraîches stables, vitesse horizontale au plus 0,01 m/s et dérive par composante inférieure à 0,003 m. Aucun résultat ni cause produit exclusive n’est déduit de cette adaptation. Le premier assemblage a détecté deux sites d’appel au lieu des trois supposés ; corrigé avant préparation et lancement, aucune donnée de replay modifiée.

Chargement SelfTest/dépendances et syntaxes PASS (`integrated-public-navigation-proportional-preparation-021-002/`). Les fonctions de l’observer passif start/drain restent exactement celles du helper synthétiquement testé ; aucune réussite runtime héritée. Lecteurs de physique/source/reçus 002 préparés sans changement de critères (`integrated-public-navigation-root-review-021-002/`). Après CIM absence du candidat, session 13778 lancée sur le candidat 021 et fixture auteur actuelle. Résultat terminal attendu ; P01 attend encore un vrai PASS navigation actuel. Gate 3 ouverte, aucune publication Git.


### Navigation 021/002 PASS local physique/source/Save ; P01 complet courant lancé

Le replay navigation 002 termine code 0, candidat/helpers/fixture stables, processus fermé normalement. Le lecteur indépendant `integrated-public-navigation-root-review-021-002/proof.json` passe : observations archivées et helpers épinglés, PNJ immobile no_path porte fermée, déplacement physique extérieur et raccord de tuiles, sortie physique intérieure après Open et état reached, révisions nav à Open/Close, commandes distinctes/ordonnées et porte refermée dans le vrai Save. Aucun résultat natif ancien n’est hérité. Cette preuve bornée de navigation du candidat 021 ne qualifie pas toute la carte, P05 ou le Primary courant.

Avant le premier lancement P01, son runner non exécuté est relié au vrai PASS navigation 002, mêmes critères/candidat/tête et même harnais ; anciennes préparation et source du runner archivées dans `integrated-public-p01-preparation-021-001/navigation-guard-update-002/`. Après CIM absence du candidat, session 29327 lance P01 complet : combat par contacts, quête, visite intérieure après quête, sauvegarde et nouveau processus Resume. Résultat terminal attendu, aucune réussite 020 héritée.

P03/P04 sont préparés sur le candidat 021 et l’auteur revalidé courant (`integrated-public-followup-preparation-021/`). Harnais byte-identique épinglé ; seuls les contrats synthétiques des helpers inchangés sont réutilisés. Les lecteurs courants vérifient diagnostic sélectionnable d’objectif invalide sans Apply/révision, puis replay au départ choisi, mouvement réel et Save isolé, seconde nouvelle partie même révision/zone, sources et sauvegardes auteur conservées. Dossiers `integrated-public-p03-root-review-021/` et `integrated-public-p04-root-review-021/` ; syntaxes Python vérifiées. Replays non lancés pendant P01, aucune qualification déduite des préparations. Gate 3 ouverte ; aucune publication Git.

### P01 021/001 terminal FAIL de position après reprise ; ressources locales PASS et P03 lancé

Le replay P01 termine code 1 : la première partie réalise cinq contacts de combat de 20 dégâts, pickup, quête Completed, passage intérieur puis Save. Le nouveau processus Resume préserve la quête visible et l’endurance ; le retour en forêt échoue ensuite à la condition de position inchangée (cible X16/Z−270, tolérance par axe 1,75 m). La capture finale présente une ancre X13,313/Z−268,298, grounded, sans action proposée. Les observations de relâchement montrent un déplacement après le marqueur d’arrivée ; cause produit exclusive non isolée, aucune perte de sauvegarde déduite. Deux fermetures normales, huit sources épinglées conservées, reçus réels Save vérifiés indépendamment dans `integrated-public-p01-failure-review-021-001/proof.json`. P01 complet reste NON PASS ; P02 reste conditionné à un vrai P01 PASS. La capture Resume a été personnellement inspectée, sans acceptation humaine P05.

Une régression ajoutée au Primary dans `crates/forge_gameplay/tests/first_loop.rs` vérifie la dépense de la dernière endurance, le refus atomique de l’attaque suivante, la conservation de tout l’état et des octets de sauvegarde après un nouveau GameSession Resume. Le test inventaire plein existant est conservé. La suite first_loop exécute huit tests PASS, zéro échec/ignoré ; sources, log et exécutable mesuré sont épinglés dans `integrated-resource-exhaustion-regression-001/test-proof.json`. Cette preuve domaine Primary ne remplace ni les contrôles publics ressources, ni une qualification globale, ni un replay du candidat021 avec ce nouveau test. Aucun code production changé par cette régression.

Après terminal P01, inventaire CIM sans candidat restant et terminal des tests, le replay P03 courant est lancé, session90807, dans `integrated-public-followup-preparation-021/p03/`. Il vérifie le diagnostic public d’objectif invalide, sa sélection et le refus Apply sans mutation source ; résultat terminal attendu. Gate3 ouverte ; aucune publication Git ni adoption implicite ADR0021.

### P03 021 PASS Root ; P04 isolation lancé

P03 termine code 0 avec sources/candidat/helpers conservés, puis `integrated-public-p03-root-review-021/proof.json` vérifie indépendamment le diagnostic sélectionnable d’objectif invalide et l’absence d’Apply/mutation. Verdict LOCAL_PUBLIC_SOURCE018_R3_P03_DIAGNOSTIC_PASS_ROOT_REVIEWED ; aucune acceptation P05 ni clôture Gate3 déduite.

Le chemin Windows du binaire de test first_loop n’avait pas été extrait par le premier lecteur (archive null dans son rapport, conservé). Le fichier exécuté est archivé avant tout autre Cargo avec hashes source/copie identiques dans `integrated-resource-exhaustion-regression-001/test-executable-archive-proof.json`. Les huit tests PASS restent étayés par le log original ; aucun résultat réexécuté ou modifié.

Après vérification CIM de fermeture P03, le replay P04 est lancé, session57195, mêmes entrées épinglées, pour nouvelle partie au départ choisi et sauvegarde isolée, puis seconde nouvelle partie de même révision/zone. Résultat terminal et lecteur indépendant attendus. P01 reste FAIL au contrôle de position après reprise et P02 non exécuté ; Gate3 ouverte/non qualifiée, aucune publication Git.

### P04 021 PASS Root ; P01/002 lancé avec même centrage final et observation stable

P04 termine code0 puis `integrated-public-p04-root-review-021/proof.json` passe la revue indépendante : départ choisi, mouvement réel avant Save, seconde nouvelle partie même départ/révision/zone, simulation isolée, sources et sauvegarde auteur conservées. Fermeture CIM recontrôlée avec chemin Windows exact, observation initiale aux slashs archivée ; aucun processus restant. Pas d’acceptation P05 ni de clôture Gate3.

P01/002 conserve les 416 helpers ; un seul script modifié. Une nouvelle fonction isolée copie à nom changé le helper proportionnel de navigation021/002 ; seul l’appel de centrage final retour forêt run2 est remplacé. Cible X14,7/Z−270, tolérance0,35 m et délai30 s inchangés ; trois observations fraîches stables, vitesse horizontale≤0,01 m/s et dérive d’ancre<0,003 m exigées. Garde des conséquences près de X16/Z−270, combat/contact, intérieur et Save/Resume inchangés. Aucune modification production/géométrie/critère de réussite. SelfTest synthétique PASS, pas un PASS runtime.

Le lecteur retry002 ajoute les témoins d’arrivée/observations stables aux exigences complètes. Après inventaire CIM sans candidat restant, P01/002 lancé, session62317, préparation `integrated-public-p01-preparation-021-002/`. Résultat terminal attendu, aucun autre scénario ni Cargo lourd simultané. P01/001 FAIL conservé ; P02 conditionné à un vrai P01 PASS. Gate3 ouverte/non qualifiée, aucune publication Git ni adoption ADR0021 implicite.

### P02 lié au vrai P01/002 et dossier ADR0021 préparés ; reprise P01/002 en cours

La préparation P02/002 (`integrated-public-p02-preparation-021-002/proof.json`) conserve les 416 helpers épinglés et les exigences complètes de retouche texte/porte, refus Resume ordinaire puis acceptation compatible, mouvement KCC réel, sources et sauvegarde conservées et fermetures normales. Seuls les chemins d’entrée/sortie et du lecteur changent pour consommer P01/002. Le runner refuse tout lancement sans son vrai PASS Root et son ledger complet ; aucune sauvegarde de l’échec P01/001 acceptée. Syntaxes Python vérifiées, replay P02 non exécuté.

Le dossier `integrated-adr0021-decision-packet-021-001/decision-packet.md` rassemble douze sources/preuves locales épinglées : comparaison registre/fork sur copie Primary figée, Source018 machine, candidat021, authoring, navigation, quête par décor, P03/P04 et échec P01. Décision proposée non acceptée ; P01/002, P02 et les autres conditions manquent encore. Dossier préparatoire, pas une demande d’approbation ni une qualification. Aucun Cargo/Rapier du Primary changé, aucun envoi upstream/publication Git.

Le défaut d’encodage du rapport causé par une lecture Windows a été corrigé à partir de la copie avant actualisation276, après vérification exacte de la transformation fautive et conservation des sections P03/P04 ajoutées. Preuve `current-campaign-progress-278/apply-proof.json` ; le ledger n’a pas changé pendant cette réparation. P01/002 reste live sur session62317 ; combat atteint, résultat terminal encore attendu. Gate3 ouverte/non qualifiée.

### P01/002 terminal FAIL d’approche intérieure ; contrôle feu/danger lancé, P01/003 préparé

P01/002 termine code1 avant Save : après quête Completed, l’approche ouest/sud de la salle ne progresse plus pendant12 s, autour de X42,144/Z−141,690. La correction de centrage du retour forêt après Resume n’est pas atteinte. Le lecteur `integrated-public-p01-failure-review-021-002/proof.json` vérifie l’échec, deux états source inchangés, huit fichiers source épinglés, fermeture normale et absence de Save achevé. L’arrivée west-corridor au tick24183 est X41,267, vitesse horizontale≈3 m/s ; l’étape suivante commence au tick24212 à X42,149, en dehors de la tolérance0,35 m de X41. Ce dépassement entre étapes est observé ; la cause produit exclusive du blocage n’est pas isolée. Aucun PASS P01/P02.

P01/003 conserve six points/tolérances de l’approche intérieure, mais le wrapper de déplacement attend une arrivée relâchée avec trois observations fraîches stables avant chaque changement de direction. Le coarse puis le centrage stable partagent le délai original de chaque segment : aucun temps supplémentaire alloué. Une seule invocation de boucle remplacée, un script modifié parmi416 helpers ; fonctions existantes et correction Resume002 conservées. SelfTest synthétique PASS ; lecteur complet préparé avec six témoins supplémentaires, runtime003 non lancé. Pas de modification production/géométrie.

Après terminal P01/002 et inventaire CIM sans candidat restant, le contrôle indépendant ForestDanger021/001 est lancé, session50721. Il exige allumage public, dégâts thermiques et réaction IA au danger, Save/fermeture/nouveau processus Resume, mêmes entrées/candidat et critères. Aucun ancien PASS hérité ; résultat terminal attendu. Gate3 ouverte/non qualifiée, aucune publication Git.

### ForestDanger021/001 terminal FAIL de centrage avant allumage ; P01/003 lancé

ForestDanger021/001 termine code1 avec fermeture normale et sources conservées. Le centrage `forest-fire-northwest-flank-waypoint-2-step-1` reçoit28 observations et17 impulsions ; distance minimale0,136814691 m > tolérance inchangée0,1 m, délai20 s inchangé. Les trois dernières impulsions nominales200 ms montrent quatre événements clavier trusted livrés aux handlers navigateur sur moins d’une milliseconde, malgré les intervalles de transport ; observation causale transport archivée dans `integrated-public-forestdanger-failure-review-021-001/proof.json`, pas une cause produit exclusive. Allumage, dégâts thermiques, réaction danger et reprise complète ne sont pas atteints ; aucune qualification de ces fonctions.

Après terminal et CIM sans candidat restant, P01/003 complet est lancé, session10840, préparation `integrated-public-p01-preparation-021-003/`. Les six arrivées stables de l’approche intérieure et le centrage relâché du retour forêt sont requis, sans changer points/tolérances/délais. Résultat terminal attendu ; aucune exécution concurrente ou Cargo lourd. Gate3 ouverte/non qualifiée, aucune publication Git.

### Contrôle transport ForestDanger/002 préparé, sans changement de critères ni de produit

Le pilote isolé ajoute à Send-Gate3TimedKeyPulse une option qui attend les accusés CDP des keyDown avant de commencer le maintien nominal. Le mode historique reste inchangé par défaut ; seul le site de centrage du harnais ForestDanger/002 active cette option. Les releases restent dans finally, avec annulation bornée15 s ; tous les accusés et les événements trusted observés sont journalisés. Le maintien ne commence plus avant réception des appuis côté navigateur dans ce mode expérimental. Les positions, tolérances0,1 m, trois observations fraîches stables et délais20 s/120 s restent inchangés. Aucune écriture domaine/diagnostic, aucune modification production/géométrie.

Les416 helpers sont épinglés, un seul script modifié. Syntaxe et SelfTest des contrats existants PASS, sans preuve du nouveau transport runtime. Préparation `integrated-public-forestdanger-preparation-021-002/proof.json` ; lecteur causal complet inchangé dans ses exigences feu/dégâts/IA/Save/reprise. Aucun lancement pendant P01/003 live(session10840). La trace d’échec001 et les anciens helpers restent conservés. Gate3 ouverte/non qualifiée, aucune publication Git.

### P01 021/003 complet PASS Root ; P02 courant lancé sur sa vraie sauvegarde

P01/003 termine code0 avec deux fermetures normales, sources/candidat/helpers stables. `integrated-public-p01-root-review-021-003/proof.json` vérifie les cinq reçus distincts de combat contacttrue/damage20 dans la Save réelle, quête Completed, pickup, visite intérieure après quête avant Save et nouveau processus Resume, retour aux conséquences gardien/Sceau et huit fichiers sources épinglés. Verdict LOCAL_PUBLIC_COMBINED_SOURCE018_P01_PASS_ROOT_REVIEWED. Les échecs001/002 sont conservés, aucun succès ancien hérité.

Le lecteur supplémentaire `independent-observation-review.py` recalcule depuis les véritables ancres, vitesses et ticks les six arrivées de l’approche intérieure et l’arrivée finale forêt après Resume. Trois observations fraîches successives dans la tolérance, vitesse≤0,01 m/s et dérive par axe<0,003 m sont confirmées sans croire les compteurs/distance du helper ; résultat `observation-proof.json` PASS. Aucun assouplissement de points, tolérances ou délais.

Après CIM sans candidat restant, P02/003 est lancé session61680 avec la vraie sauvegarde de P01/003. Le runner exige son PASS Root et son ledger complet avant retouche publique réplique/porte, refus Resume ordinaire puis reprise compatible explicite. Les helpers et critères sont inchangés ; résultat terminal/lecteurs attendus. Aucune exécution native concurrente ni Cargo lourd.

Le protocole négatif public des ressources est préparé dans `integrated-public-resources-negative-protocol-021-001/protocol.md`, depuis les contrôles réellement disponibles dans le candidat : clone indépendant, capacité joueur0 et vitalité gardien500 retouchées via Studio Preview/Apply/Save, vingt attaques contact consommant100 endurance puis refus atomique de la suivante, refus pickup sur inventaire plein et persistance des refus après Resume. Profil et aventure de référence conservés. Protocole non implémenté/non exécuté : les huit tests domaine ne prouvent pas encore ces refus publics. Gate3 ouverte/non qualifiée, aucune publication Git ni acceptation ADR0021/P05 déduite.

### P02/003 terminal FAIL de seconde fermeture native ; ressources authoring préparé, ForestDanger/002 lancé

P02/003 réalise la retouche publique réplique/porte, le refus de Resume ordinaire puis la reprise compatible, le passage intérieur physique et le deuxième parcours/relecture de la réplique. Mais le dialogue natif du deuxième WM_CLOSE n’apparaît pas dans le délai10 s du pilote : fermeture forcée, verdict FAILED_CLOSED. Première fermeture normale ; aucun PASS P02 complet. Le lecteur `integrated-public-p02-failure-review-021-003/proof.json` vérifie les sources/snapshots conservés et le diff brut limité au texte de la réplique, X de porte et révision40→42, puis épingle le reçu de fermeture forcée. Cause exclusive produit/pilote non isolée. Le code source de CloseDialog et le relais CloseRequested sont inspectés en lecture seule ; pas de modification production.

Après terminal/CIM sans candidat restant, ForestDanger/002 est lancé session85242. Il utilise le transport qui attend les accusés keyDown avant maintien, sans changer les tolérances, délais ou critères feu/dégâts/IA/Save/reprise. Résultat terminal attendu ; aucun autre scénario natif simultané.

Le driver public `integrated-public-resource-authoring-preparation-021-001/Invoke-ResourceAuthoring.ps1` est préparé pour les deux seules retouches de statistiques dans un clone : capacité joueur0, vitalité gardien500, par sélection/champs/Preview/Apply/Save. Syntaxe et résolution des helpers SelfTest PASS après correction de la citation d’apostrophe typographique et import explicite du helper authoring ; aucun lancement de Studio. Runner épingle candidat, navigateur,416 helpers et toutes sources avant/après. Lecteur indépendant préparé pour comparer le head complet : seules les deux valeurs et revision+2 admises, anciens reçus conservés et exactement deux nouveaux reçus, fermeture normale requise. Driver/lecteur non exécutés ; aucun refus public ni Save ressources prouvé. Gate3 ouverte/non qualifiée, aucune publication Git.

### Actualisation285 : authoring des ressources négatives qualifié, ForestDanger002 terminal

La tentative d’authoring003 passe par l’onglet public Objets puis Preview/Apply/Save. La comparaison brute de toute la tête confirme uniquement capacité joueur24→0, vitalité maximale gardien100→500 et révision40→42 ; deux reçus ajoutés, anciens reçus conservés. Fermeture normale, candidat/helpers/source préservés. [Revue](E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-public-resource-authoring-preparation-021-003/root-proof.json). Archive isolée0ffedd32a8aaf209d95c3f861517c94e6407da892d2f97ba525dea29c2e46285. Ce PASS porte sur l’authoring ; les vingt attaques, refus d’endurance et d’inventaire puis Save/Resume restent à exécuter. Les essais001/002 ont échoué avant retouche sur contrôles cachés dans l’onglet Terrain, avec fermeture normale ; leurs preuves sont conservées.

ForestDanger002 est terminal exit1, avec fermeture normale et sources préservées. Échec avant ignition au premier segment de centrage : 22 observations,14 pulses, distance minimale rapportée0,174583m pour la tolérance inchangée0,1m. Les deux dernières pulses nominales1ms ont des intervalles keydown/keyup fiables de260,3 et251,7ms dans le navigateur : la synchronisation des ACK évite la coalescence mais ne permet pas encore un centrage fin. [Revue](E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-public-forestdanger-failure-review-021-002/proof.json). Aucun PASS feu/chaleur/danger. P02 conserve son échec de seconde fermeture ; aucune clôture Gate3, adoption ADR0021 ou publication Git.

### Actualisation286 : incident RAM/VRAM, replay ressources001 arrêté

Le propriétaire signale saturation VRAM et explosion RAM pendant le test, puis environ20Go RAM/16Go VRAM libérés. Le relevé CIM avant arrêt attribue25,19Gio privés et23,28Gio résidents au renderer WebView37472, enfant6212 du candidat15900. L’arrêt de protection exact du candidat et de ses enfants libère environ26,66Gio de RAM physique disponible ; aucun autre Studio n’a été arrêté. Le pilote47860 est terminal exit1, et aucun candidat possédé ne reste actif. Les16Go VRAM constituent une observation propriétaire, non une différence mesurée par nos compteurs.

[Incident et preuves](E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-resource-runtime-memory-incident-021-001/proof.json). Zéro sauvegarde de ressources, zéro refus testé ; arrêt externe forcé, aucun PASS runtime. Les sources/helpers/candidat sont préservés. Le lecteur indépendant des vingt contacts/quatre refus/sauvegardes est préparé, non exécuté. L’authoring003 PASS ne prouve pas la consommation mémoire acceptable. La priorité devient l’audit des allocations/résidences et une instrumentation RAM/GPU avec seuils de protection avant nouveau replay ; aucune cause exclusive ni correction validée. La Gate3 reste ouverte et aucune publication n’a lieu.

### Actualisation287 : cache des pipelines atmosphère isolé et validé machine

L’inspection de la source mesurée018 révèle une création de shaders/pipelines dans chaque encodage d’atmosphère, appelée aussi pour les hauteurs observateur et mises à jour solaires. Les chemins nuages/océan voisins disposent de caches. [Audit statique](E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-atmosphere-memory-source-audit-019-001/proof.json). Source019 reprend les2536 fichiers Source018 inchangés, sauf le module atmosphère et un nouveau module de cache/test : au plus quatre pipelines par entrée de shaders immuable et appareil GPU. Sampler partagé, uniforms/LUT/shaders/qualité et ordre des passes conservés ; invalidation explicite sur remplacement d’appareil ou de génération d’entrée. Aucun code produit Primary écrasé.

[Revue machine Source019](E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-atmosphere-pipeline-cache-preparation-019-002/root-proof.json) : format, strict natif tous targets/features et strict moteur WASM sans diagnostic ;697 tests moteur PASS,0FAIL,9ignored dans le lancement général. Un de ces ignorés a été exécuté explicitement sur GPU :2400 recherches de quatre pipelines sans changement d’identité, readbacks réels3/71/109 pour nouveaux uniforms et nouvel appareil. Il reste8 ignorés sans PASS supplémentaire. Le premier contrôle strict a échoué sur un Default non typé ; correction équivalente sans suppression. Le premier test GPU a passé mais son moniteur a rencontré une course de processus déjà terminé, conservée en échec du launcher ; seconde exécution avec garde-fou002 PASS fonctionnel. Zéro échantillon mémoire sur ce test très court : il ne qualifie pas la consommation de la scène ou du navigateur.

Checkpoint Git strictement privé527fa8ef6bcbe18ad24ce52f53f0b3437a6d7b9d, parent376ecc0878eb50bd6720c705d0ae5aafe002cfb7,2537 sources épinglées, aucun remote public. [Checkpoint](E:/OpenRPG_Forge/.gate3-evidence/20261002/integrated-atmosphere-pipeline-cache-preparation-019-002/checkpoint-proof.json). Construction frontend puis candidat022 lancée, session54021 active, sources et3000 imports/11 fichiers du kit bornés à la révision conservée. Aucun exécutable Studio022 ni résultat runtime encore revendiqué.

Le garde-fou002 vérifie les identités et descendances du pilote/candidat, mesure les octets privés et la mémoire GPU possédée, conserve null quand la mesure GPU manque et arrête les seuls handles possédés en cas de seuil, erreur de mesure ou durée dépassée. Seuils de protection4Gio privés/4Gio GPU dédiés/2Gio GPU partagés et8Gio de marge de commit, distincts d’une qualification performance. [Arrêt contrôlé d’un pilote inactif](E:/OpenRPG_Forge/.gate3-evidence/20261002/memory-guard-protection-preparation-002/proof.json), aucune scène Forge lancée. La saturation WebView25,19Gio et la récupération16Go VRAM rapportée restent à expliquer/mesurer sur candidat corrigé. Aucun PASS mémoire, ADR0021 adopté, P05 validé ou Gate3 clôturée ; aucune publication.


### Mise à jour 288 — Source019 / candidat022 construit, mesure mémoire non réalisée

Le frontend WebAssembly corrigé est construit. La première préparation native
s'est arrêtée avant compilation, car le constructeur exige la branche locale
`Rust`. Un alias local `Rust` a été créé dans le seul dépôt Source019 isolé,
au même commit `527fa8ef6bcbe18ad24ce52f53f0b3437a6d7b9d`, sans changement
de fichier, sans adoption dans Primary et sans publication.

La seconde préparation native réutilise le frontend vérifié. La revue
`integrated-studio-candidate-root-review-022/proof.json` confirme les 2537
sources, les 3000 imports, les 11 éléments du kit et la parité frontend/native.
L'exécutable est dans `tidewater-integrated-primary-022-candidate-002`.
Il s'agit d'une preuve de construction, sans qualification du runtime.

Le replay ressources022 est préparé dans
`integrated-public-resource-runtime-preparation-022-001`. Son lecteur exige
désormais le texte effectivement capturé par `Save-PlaytestPublicDiagnostic`,
avec capture sans erreur, plutôt qu'un champ `feedback` absent de ce format.
Le pilote du scénario et les critères de refus restent identiques au replay
interrompu021. Aucun scénario022 n'a encore été lancé.

Les essais de suivi des processus orphelins ont arrêté les processus factices
possédés mais n'ont pas validé le cas descendant : l'acquisition de leur
identité a échoué par précaution. Les dossiers
`memory-guard-orphan-protection-preparation-003`, `004` et `005` conservent
ces résultats ; ils ne sont pas des PASS de mesure mémoire. Une comparaison
des horodatages Windows est en cours pour corriger ce contrôle avant la scène.

La baisse de RAM/VRAM sur Tidewater, la cause exclusive de l'incident et la
qualification globale Gate 3 restent non démontrées. Les résultats publics021
ne sont pas automatiquement transférés au candidat022. Gate 3 reste ouverte.


### Mise à jour 289 — Garde-fou orphelins validé, replay022 protégé lancé

La comparaison Windows a montré que CIM tronque à la microseconde les
horodatages de création du noyau (écart mesuré de 3 ticks de 100 ns). Le
garde-fou006 compare maintenant à cette précision et conserve des handles
os pour les descendants. Son ensemble d'identités reste un ensemble même
avec un seul PID. Le test réel `memory-guard-orphan-protection-preparation-007`
passe : le descendant factice survivant à ses parents est identifié et arrêté.
Les tentatives003 à006 restent archivées comme échecs ; elles ne sont pas
promues en PASS. Le SelfTest sur l'incident mémoire réel passe également.

Le replay ressources022 a été lancé via un pilote externe surveillé, session
66305, avec les limites 4 GiB de mémoire privée possédée, 4 GiB GPU dédiée,
2 GiB GPU partagée et au moins 8 GiB de réserve système de commit. Le
garde-fou peut arrêter uniquement les processus du test et conserve chaque
échantillon dans `integrated-public-resource-runtime-preparation-022-001/monitor`.
Les premiers 17 échantillons (~38 secondes) montrent un maximum de 3,954 GiB
de mémoire privée et un état courant de 3,685 GiB, 1,243 GiB GPU dédiée,
0,148 GiB GPU partagée, sans déclenchement à cet instant. Ce sont des
mesures initiales ; ni stabilité du scénario complet ni baisse durable
ni cause exclusive de l'incident ne sont qualifiées. Gate 3 reste ouverte.


### Mise à jour 290 — Replay022 arrêté par la protection mémoire au chargement

La session66305 est terminale, sortie1. Le garde-fou a déclenché sur la
mémoire privée possédée après ~67 secondes, avec 26 échantillons dont 25 GPU :
maximum privé 4 520 022 016 octets (4,21 GiB), GPU dédié 1 621 299 200 octets
(1,51 GiB), GPU partagé 208 879 616 octets (0,195 GiB). Le seuil privé
était 4 GiB. Aucun autre seuil n'a déclenché. Le dernier relevé attribue
~2,03 GiB à un processus WebView GPU, ~1,44 GiB au renderer et ~0,67 GiB
au natif. Le scénario n'a produit ni Save ressource ni refus ; il a été
interrompu pendant sa préparation/chargement.

`integrated-public-resource-runtime-preparation-022-001/failure-root-proof.json`
confirme les hashes, les entrées conservées, l'arrêt forcé par protection et
l'absence de toutes les identités de processus possédés du dernier relevé.
Il ne s'agit pas d'une fermeture normale du scénario. Le fichier
`closure-proof.json` confirme la libération des processus. La correction de
cache reste construite et testée ; cet essai ne prouve ni une fuite persistante
ni une consommation normale de référence, ni la correction complète du
problème initial. Il faut analyser le budget du chargement et poursuivre une
mesure bornée avant qualification. Aucun nouveau scénario n'est actif.
Gate 3 reste ouverte et non qualifiée.


### Mise à jour 291 — Croissance GPU rapide confirmée après chargement022

Un diagnostic séparé avec limite privée6 GiB et durée maximale300 secondes
a observé le chargement public, sans mouvement ni action RPG. La simulation
prête est effectivement apparue dans le dernier des 12 relevés navigateur.
L'essai16083 est terminal, arrêté par la protection après ~59 secondes :
25 échantillons OS/GPU, pic privé 7 423 565 824 octets (6,91 GiB), GPU dédié
3 937 153 024 octets (3,67 GiB). Les quatre derniers relevés couvrent
~6,9 secondes : privé 4,344 -> 6,914 GiB, dédié 1,245 -> 3,667 GiB.
Le tas JS du dernier relevé est 18 176 371 octets (~17,33 MiB). La hausse
privée se concentre dans un WebView, tandis que le natif reste ~1,09 GiB.
Son rôle GPU est inféré des mesures, pas épinglé par sa ligne de commande.

`integrated-memory-loading-diagnostic-022-001/failure-root-proof.json`
vérifie ces observations, les hashes, l'état public prêt, les entrées
conservées et la fermeture des identités possédées. Le pilote a été forcé
par protection avant son reçu final ; aucun reçu de fermeture normale ni
qualification de stabilité n'est revendiqué. Le cache atmosphère seul
ne suffit donc pas à démontrer la correction de l'emballement observé.

Un compteur diagnostic des appels WebGPU est préparé dans
`integrated-webgpu-allocation-diagnostic-preparation-022-001`. Il transmet
les appels originaux et relève les créations par label, les destructions
explicites et les fences de soumission. Les tests avec appareil factice
confirment arguments, identité des retours, propagation d'erreurs et
comptage ; aucune exécution réelle de cet instrument n'est encore prouvée.
Les octets estimés cumulés de création ne sont pas une mesure de résidence.
La suite doit distinguer allocations répétées et commandes accumulées,
sans nouvelle augmentation des limites ni changement de map. Gate 3 ouverte.


### Mise à jour 292 — Allocations buffers WebGPU répétées localisées

Le diagnostic instrumenté022002 (session56473 terminale) a reproduit la
croissance : limite privée6 GiB atteinte, pic6,18 GiB et dédié3,16 GiB,
25 relevés OS/GPU, 12 navigateur. La ligne de commande classifiée et
l'identité de création confirment cette fois le rôle `gpu-process` du
WebView portant la hausse. L'installation instrumentée8 méthodes n'a
produit aucune erreur. La revue `failure-root-proof.json` vérifie les
hashes, les entrées conservées et la fermeture des identités possédées.

Le dernier relevé compte25 777 buffers non nommés créés cumulativement,
dont19 629 depuis le relevé précédent, pour seulement24 soumissions
additionnelles. Leurs tailles déclarées cumulées sont121 052 496 octets ;
c'est une estimation de créations, ni octets résidents ni nombre vivant.
Les fences ont terminé les soumissions jusqu'à266 sur275, sans erreur.
Les quatre pipelines du cache atmosphère ont chacun été créés une fois
dans le navigateur : leur réutilisation est observée, la mémoire globale
reste non qualifiée.

L'audit `integrated-material-uniform-churn-source-audit-019-001` épingle
le chemin suivant : le vent modifie chaque frame tous les matériaux
Village/Plant/Terrain/LOD ; leurs bindings uniformes AsBindGroup dérivés
sont recréés par la préparation de matériaux Bevy. Cette piste correspond
aux créations observées et doit maintenant être corrigée puis rejouée,
en conservant animation, contrôles, données propres à chaque matériau
et ordre des commandes. Aucune correction de ce chemin n'est encore
appliquée, aucune cause exclusive n'est proclamée. Gate 3 reste ouverte.

### Mise à jour 293 — Correction isolée du vent partagé, validations en cours

Source020 conserve le snapshot Source019 et remplace la recopie du vent dans
tous les matériaux à chaque frame par la publication d'un buffer partagé de
32 octets. Village, terrain et plantes lisent ce binding commun lorsque le
pilotage est actif ; les valeurs propres aux matériaux restent disponibles
avant publication. Le chemin existant du mouvement des plantes est conservé.
Les shaders et assets gouvernés, Cargo.toml et Cargo.lock restent inchangés.
La correction est isolée ; elle n'est pas adoptée dans le checkout principal.

La préparation020003 épingle 2 539 fichiers. Format et Clippy natif moteur
tous targets/features passent sans suppression d'avertissement. Les deux
tentatives antérieures restent archivées : commentaires docMarkdown puis
type du SystemState dans le nouveau test GPU corrigés. La session83456
continue les tests moteur puis les contrôles WASM moteur et Studio. Le test
GPU dédié est préparé pour vérifier deux valeurs de repli distinctes puis
la conservation réelle du même buffer sur 500 mises à jour ; il n'a pas
encore été exécuté. Aucun test graphique de scène n'est actif.

Ces résultats ne qualifient pas la mémoire : une mesure bornée du candidat
apparié devra vérifier créations de buffers et consommation RAM/VRAM, sans
augmenter à nouveau les limites. La cause exclusive de l'incident et la
stabilité de la scène complète restent à démontrer. Gate 3 ouverte, aucune
publication Git et aucune nouvelle décision propriétaire acquise.

### Mise à jour 294 — Régression poissons détectée et portée du vent corrigée

La session83456 est terminale : format et strict natif passent, les tests
moteur donnent698 PASS,1 FAIL,10 ignorés. L'assemblage du matériau poissons
réutilise les receivers d'éclairage Village sans ses lectures de vent ; le
nouvel adaptateur appliqué au receiver commun refusait donc ce shader.
La préparation020004 archive le fichier et les pins précédents, puis déplace
strictement l'adaptation du vent dans stage_shader du matériau Village.
Le receiver d'éclairage partagé conserve ainsi son interface précédente.
Les assertions et le refus de dérive de shader restent actifs.

La session84733 poursuit la nouvelle passe format, strict, tests moteur,
WASM moteur et WASM Studio. Le test GPU dédié et le replay mémoire ne sont
pas encore exécutés. Aucun test graphique de scène n'est actif. La mise à
jour293 reste l'état historique de la passe précédente. Gate 3 ouverte et
non qualifiée, correction isolée, aucune publication.

### Mise à jour 295 — Source020 moteur699 PASS, image de tests archivée

La préparation020004 passe les 699 tests moteur, avec0 échec et10 ignorés,
après format et Clippy natif tous targets/features PASS. Le contrôle complet
du matériau poissons passe. L'image réellement exécutée est conservée dans
source020-measured-engine-tests.exe, avec hash vérifié par
test-executable-proof.json avant tout nouveau Cargo. Les contrôles WASM
moteur puis Studio suivent dans la même session84733.

Le test GPU partagé et les builders frontend/natif023 sont préparés, sans
exécution revendiquée. Leur lancement exige la fin de la passe machine et
la revue des preuves ; la construction exige aussi un checkpoint local
propre. Aucun nouveau test graphique de scène ni verdict mémoire complet.
Gate 3 ouverte, aucun changement du checkout produit ni publication Git.

### Mise à jour 296 — Source020 revue machine et test GPU réel PASS

La session84733 est terminale, sortie0. La revue root-proof.json confirme
les 2 539 fichiers épinglés et les14 fichiers de correction attendus,
la conservation des bytes Source019 de référence et des dépendances,
format, strict natif moteur tous targets/features,699 tests moteur PASS,
strict WASM moteur et Studio PASS. Les10 tests ignorés du lot ordinaire
restent distincts de l'exécution explicite suivante.

Le test réel partagé passe depuis l'image archivée : deux consumers lisent
leurs valeurs de repli distinctes, puis500 publications et préparations
GPU Bevy gardent le même identifiant de buffer. Les deux bindgroups d'origine
lisent ensuite [0,1,7,499]. Restaging et refus d'entrée invalide conservent
les valeurs. L'essai dure0,68 seconde : le garde-fou finit sans arrêt forcé,
avec0 échantillon et candidate_seen=false, car le processus est déjà fini.
C'est une preuve comportementale GPU, aucune mesure de mémoire de scène.

Le checkpoint privé Source020 est lancé dans la session64522 ; les builders
du candidat023 sont prêts et attendent sa preuve terminale. Aucune scène
graphique Tidewater nouvelle n'est lancée. La réduction des créations WebGPU,
la stabilité RAM/VRAM complète et les scénarios publics du candidat corrigé
restent à mesurer. Gate 3 ouverte, aucune adoption produit ni publication.

### Mise à jour 297 — Checkpoint privé Source020 propre, build023 lancé

La session64522 est terminale, sortie0. Le checkpoint privé Source020
96e324404102e3e9cfc0eea333391b157b047151 conserve pour parent Source019
527fa8ef6bcbe18ad24ce52f53f0b3437a6d7b9d. Les2 539 fichiers correspondent
aux pins machine ; branche locale Rust propre, aucun remote public ni
adoption dans le checkout produit. La correction contient14 fichiers.

La préparation integrated-wind020-studio-build-preparation-001 vérifie
la revue machine et GPU, le checkpoint et les builders appariés. La
session43792 est lancée : frontend WASM023 puis natif023, séquentiellement,
avec les3 000 imports et11 fichiers du kit gouverné de la copie épinglée
du build018. Les assets sont vérifiés avant/après ; aucun replay graphique
ni résultat mémoire023 n'est revendiqué avant revue de la construction.
Les seuils du prochain diagnostic restent inchangés. Gate 3 ouverte,
correction locale isolée, aucune publication Git.

### Mise à jour 298 — Frontend023 construit, natif en cours

La construction frontend023 termine avec sortie0 et sources/assets stables,
selon frontend-proof.json de integrated-wind020-studio-build-preparation-001.
Le worker de navigation est aussi produit par le builder. La session43792
poursuit maintenant la construction native appariée ; aucun lancement de
scène023 ni verdict mémoire du candidat n'est acquis.

Une revue statique complémentaire dans
integrated-shared-wind-consumer-review-020-001 confirme que les chemins
d'animation des ombres végétales, des pendules et l'adaptateur de mouvement
des plantes gardent leurs fichiers Source019 byte-identiques. Les deux
premiers lisent WindFrame directement ; le dernier conserve son buffer de
mouvement dédié. Cette vérification ne qualifie pas la parité visuelle.

La revue de concordance frontend/natif023 est préparée. Le diagnostic public
de chargement instrumenté reprendra le même parcours et les mêmes limites
après cette revue. Aucun nouvel essai graphique n'est actif. Gate 3 ouverte,
correction locale isolée, aucune publication Git.

### Mise à jour 299 — Candidat023 apparié PASS, diagnostic mémoire lancé

La session43792 est terminale, sortie0. La revue du candidat023 confirme
2 539 sources,3 000 imports,11 fichiers du kit gouverné, checkpoint privé
propre et mêmes entrées frontend/natif. Le natif, le WASM et le worker sont
épinglés dans integrated-studio-candidate-root-review-023/proof.json.
Le premier contrôle comparait lexicalement un horodatage UTC7 décimales
avec sa sérialisation PowerShell6 décimales : seul le zéro final différait.
La revue archive le script précédent et normalise uniquement ces zéros,
avec égalité exacte conservée pour tous les autres champs et hashes.

Les helpers du diagnostic de chargement passent leur SelfTest. La
session60059 exécute maintenant le même parcours public instrumenté sur
023 : ouverture de zone, Jouer,150 secondes d'observations chargement/idle,
puis fermeture. Les seuils restent6 GiB privé,4 dédié,2 partagé,8 de marge
commit et300 secondes maximum. Aucun élargissement des limites ni verdict
mémoire n'est revendiqué avant fin et revue. Les compteurs instrumentés
mesurent des créations cumulées, pas des objets vivants ou octets résidents.
Gate 3 ouverte, candidat isolé, aucune adoption produit ni publication Git.

### Mise à jour 300 — Wind partagé observé, autre churn matériau à corriger

La session60059 est terminale, sortie1. Le diagnostic023 a été arrêté par
la limite privée6 GiB après ~71 secondes :28 échantillons OS/GPU dont25 GPU,
pic privé6 791 045 120 octets (6,32 GiB), dédié3 768 324 096 octets (3,51 GiB),
partagé208 879 616 octets. La simulation publique prête a été observée.
Le rôle gpu-process de la plus grosse allocation est confirmé par PID et
date de création. La fermeture des identités possédées est vérifiée ; les
entrées restent inchangées. Le reçu natif Closed après l'arrêt du garde-fou
ne constitue pas une fermeture normale. Aucun essai graphique ne reste actif.

Le buffer shared scene wind a été créé une seule fois dans le navigateur.
Le mécanisme Source020 fonctionne donc, sans résoudre la consommation
globale :22 075 buffers non nommés cumulés,118 414 152 octets logiques
déclarés cumulés,279 soumissions et fence complétée259,0 erreur instrument.
Ces compteurs ne sont ni objets vivants ni mesure de résidence VRAM. La
revue failure-root-proof.json archive ce résultat négatif et ses hashes.

L'audit integrated-remaining-material-churn-source-audit-020-001 épingle
deux chemins restants : solar.advance utilise Assets.iter_mut sur tous les
matériaux Village avant son filtre de valeurs ; fish_props_scene.update_view
recopie l'éclairage dans tous les matériaux poissons sans comparaison.
Le code Bevy0.19.1 confirme que AssetsMutIterator.next émet Modified avant
le filtre, même si celui-ci ne retient aucune valeur. Ces pistes doivent
être corrigées sans désactiver le suivi des vraies retouches, puis rejouées.
Aucune cause exclusive ni correction complète n'est déclarée. Gate 3 ouverte,
Source020 conservée, aucune adoption produit ni publication Git.

### Mise à jour 301 — Filtrage des vraies modifications Source021 implémenté

Source021 copie les2 539 fichiers épinglés Source020 en conservant la source
et le checkpoint précédents. Deux fichiers moteur sont ajoutés, pour2 541
fichiers et6 changements. Les helpers de publication solaire et d'éclairage
poissons comparent les données par accès immuable, puis empruntent seulement
les matériaux réellement différents via get_mut normal. Aucun bypass du
suivi de changements. Les shaders, assets et dépendances restent inchangés.
Les traits PartialEq des trois structures de paramètres comparent leurs
champs sans changer leurs layouts. Les valeurs propres aux poissons et le
diffus SH des receivers Village sont conservés.

Deux nouveaux tests observent les vrais messages AssetEvent::Modified :100
publications identiques sans notification, lumière solaire seulement sur
les receivers communs différents,15 cas de changement d'éclairage poissons,
conservation du temps/LOD/surface et suivi de retouches auteur indépendantes.
La première passe021001 passe format mais échoue au strict sur un test de106
lignes pour limite100 ; son fichier et ses pins sont archivés. La fixture
de ce test est extraite sans retirer d'assertion ni supprimer d'avertissement.
La session51722 poursuit la passe021002 format/strict/tests/WASM moteur/Studio.
Aucun nouveau candidat ni résultat RAM/VRAM n'est encore acquis.

L'animation nécessaire des poissons continue séparément à publier temps et
index de frame dans resident.update_view ; cette piste reste à distinguer
des notifications inutiles corrigées. Aucun essai graphique n'est actif.
Gate 3 ouverte, correction isolée, aucune adoption produit ni publication Git.

### Mise à jour 302 — Source021 moteur701 et strict natif/WASM PASS

La session51722 est terminale, sortie0. La revue root-proof.json vérifie
2 541 sources,6 changements attendus, baseline Source020 conservée, shaders
et dépendances inchangés, format et strict natif tous targets/features PASS,
701 tests moteur PASS avec0 échec/10 ignorés, strict WASM moteur et Studio PASS.
L'image réellement testée est archivée et épinglée avant nouveau Cargo.
Les deux régressions contrôlent les événements Bevy réels,100 publications
identiques silencieuses et les changements d'éclairage et retouches suivis.
Aucun nouveau test GPU ignoré ni verdict mémoire de scène n'est revendiqué.

La session17885 crée maintenant le checkpoint privé Source021 avec Source020
pour parent. Les builders du candidat024 sont préparés et attendent cette
preuve terminale. Aucun nouveau test graphique n'est actif. La comparaison
RAM/VRAM reste à exécuter sur le candidat apparié construit et revu, avec les
mêmes garde-fous. Gate 3 ouverte, aucune adoption produit ni publication Git.

### Mise à jour 303 — Checkpoint Source021 propre, candidat024 en construction

La session17885 est terminale, sortie0. Le checkpoint privé Source021
e406d78f06185f331aedbad044599e044af35723 garde Source020 pour parent,
ses2 541 fichiers épinglés, branche Rust propre et aucun remote public.
La source et les preuves Source020 restent conservées. Aucun changement
du checkout produit ni publication n'est effectué.

La session35432 construit maintenant frontend024 puis natif024 depuis
ce checkpoint, avec les mêmes3 000 imports et11 fichiers du kit gouverné
épinglés. La revue de concordance et le générateur du diagnostic public
instrumenté024 sont préparés. Leur lancement attend la construction
terminale et les vérifications de hashes ; les seuils restent identiques.
Aucun essai graphique024 ni effet mémoire de Source021 n'est revendiqué.
Gate 3 ouverte et non qualifiée.

### Mise à jour 304 — Candidat024 construit ; diagnostic mémoire encore négatif

La construction35432 est terminale, sortie0. La revue024 confirme la
concordance Source021, frontend/natif et assets gouvernés. Les701 tests
moteur et contrôles stricts précédents ne constituent pas un PASS mémoire.

La session53598 est terminale après arrêt de protection :24 échantillons,
7 609 098 240 octets de mémoire privée,4 231 024 640 octets de GPU dédié
et179 519 488 octets de GPU partagé au maximum. Le seuil privé6 Gio a
déclenché l'arrêt. La vérification indépendante confirme l'absence de
toutes les identités de processus observées. Le driver a été interrompu
avant son reçu final ; aucune fermeture normale n'est revendiquée.

Les12 observations navigateur incluent la simulation prête, une seule
création du buffer de vent partagé et33 091 créations cumulées de buffers
sans label. Ce cumul ne mesure ni leur résidence ni une fuite exclusive.
Le plus gros processus est identifié comme GPU WebView par PID et date
de création. Source et entrées restent préservées. Source021 ne résout
pas la croissance mémoire ; aucune qualification runtime n'en découle.

L'audit identifie encore des écritures time/frame_index sur les deux
matériaux LOD de chaque poisson à chaque image, y compris les invisibles.
Cette voie demande attribution et correction isolée, sans supprimer les
animations. Les macros Bevy mettent déjà les textures faillibles avant
les allocations de buffers ; une attribution générale aux textures
manquantes n'est donc pas établie. Gate3 ouverte et non qualifiée.

### Mise à jour 305 — Attribution de buffers FishMaterial ; correction Source022 en test

La session90347 est terminale, sortie1, après arrêt sous protection RAM et
GPU dédié. La fermeture de toutes les identités observées est vérifiée.
Le diagnostic v2 conserve les appels d'origine, compte64 groupes maximum
et quatre piles bornées par groupe ; ses tests de forwarding, limites et
erreurs passent. Les piles réellement capturées identifient FishMaterial
unprepared_bind_group parmi les créations de buffers64,80 et176 octets.
Les groupes peuvent aussi contenir d'autres producteurs.12 423 créations
dépassent la capacité de groupes : couverture partielle, aucune attribution
exclusive de la croissance mémoire ni mesure de résidence par buffer.

Source022 est une copie vérifiée des2 541 fichiers Source021, puis7 fichiers
modifiés/nouveaux donnent2 542 fichiers épinglés. La correction isolée ajoute
un buffer32 octets partagé pour le temps et le frame_index entier des poissons
Studio ; les clocks autonomes restent indépendantes, les paramètres auteur
restent conservés et les changements réels de LOD restent suivis. Les snapshots
d'ombres conservent leur temps/index exacts. Aucun changement dans le produit.

La session4063 exécute maintenant format, Clippy strict moteur natif, tests
moteur puis contrôles stricts WASM moteur/Studio. Le format passe. Les tests
ajoutés visent le maintien des octets du clock, l'index u32 exact, les refus
atomiques et les vrais événements d'assets ; leurs résultats restent en attente.
Ni la compilation ni le mécanisme partagé ne prouvent encore une amélioration
mémoire. Aucun nouvel essai graphique après la correction n'est lancé.
Gate3 ouverte et non qualifiée ; aucune publication.

### Mise à jour 306 — Réparation stricte Source022, contrôles relancés

La session4063 est terminale, sortie101 à Clippy : float_cmp sur la comparaison
du temps et collapsible_if sur la spécialisation du matériau. Les sources
épinglées de cet essai sont restées stables. Ses7 fichiers modifiés sont
archivés avant réparation. La comparaison utilise maintenant les bits exacts
du f32 pour conserver la fidélité, et la condition est simplifiée. Le test
utilise une conversion u16 exacte, sans suppression de lint ni assertion.

La préparation022-002 conserve2 542 fichiers épinglés et lance les mêmes
contrôles dans la session68538. Aucun verdict machine final, aucune preuve
GPU de cette correction et aucun gain mémoire ne sont encore acquis.
Gate3 reste ouverte et non qualifiée ; le checkout produit est préservé.

### Mise à jour 307 — Clippy natif Source022 PASS ; tests moteur en cours

La session68538 est terminale, sortie101 : float_cmp restait dans une
assertion du nouveau test de clock. Ses octets sont archivés avant une
réparation du test seul : comparaison exacte des bits f32, sans suppression
de lint ni retrait d'assertion. La préparation022-003 garde2 542 fichiers
épinglés. La session42913 passe format et Clippy moteur natif sur toutes
les cibles/fonctionnalités ; les tests moteur compilent actuellement,
suivis des contrôles stricts WASM moteur et Studio.

La revue machine, le checkpoint privé et les builders frontend/natif025
sont préparés, avec3 000 imports et11 fichiers de kit identiques. La revue
de concordance025 et le générateur du diagnostic v2 sont aussi préparés.
Rien de cela n'est exécuté avant les résultats terminaux requis. Le futur
diagnostic garde les seuils RAM/GPU et l'arrêt des seuls processus possédés.
Aucun gain mémoire, verdict GPU ni qualification Gate3 n'est acquis.

### Mise à jour 308 — Source022 validée en machine, candidat025 en construction

La session42913 est terminale, sortie0 : format, Clippy moteur natif toutes
cibles/fonctionnalités,703 tests PASS/0 FAIL/10 ignorés, Clippy strict WASM
moteur et Studio passent. La revue vérifie les2 542 fichiers épinglés,
les7 changements, les bytes Source021 conservés, les dépendances inchangées,
les vrais tests d'événements et l'image exécutée archivée
2d3e96e0c858ae747ff399f0f513402af2e75413ff4ed76d2a0acf967b677399.
L'adaptateur shader poissons est changé ; les packages source gouvernés
restent inchangés. Aucun test GPU ciblé de Source022 n'est encore revendiqué.

La session37042 est terminale, sortie0 : checkpoint privé propre
5a1c2bca83203e3e598549a6ce96b41f2be411b3, parent Source021
e406d78f06185f331aedbad044599e044af35723, branche Rust sans remote public.
La session42090 construit maintenant frontend025 puis natif025 avec les
mêmes3 000 imports et11 fichiers de kit. La revue025 et le générateur
de diagnostic v2 sont prêts mais attendent les résultats terminaux.

Ces validations ne prouvent ni résidence GPU stable, ni gain mémoire,
ni qualification du gameplay. Aucun scénario graphique025 n'est lancé.
Le checkout produit reste préservé ; aucune publication. Gate3 ouverte.

### Mise à jour 309 — Baseline allocations analysée ; frontend025 progresse

La session42090 reste active. Le journal frontend025 atteint maintenant
la compilation forge_studio après forge_engine. Aucun nouveau build
n'est lancé et aucun verdict terminal ni diagnostic graphique025 n'est acquis.

L'analyse de la baseline024-002 conserve la timeline des12 observations
et son hash. Entre45,888 et50,917 secondes depuis la première observation,
les textures restent145 créations et637 465 513 octets logiques estimés,
tandis que les buffers passent de8 436 à36 881 créations. Les modules
shader passent131→132, les pipelines render59→60 et compute restent67.
Cette activité est cohérente avec les piles FishMaterial capturées et
motive la correction du clock ; elle ne prouve pas une cause exclusive.
Les tailles estimées ne sont pas la résidence réelle et l'absence de
destroy explicite ne prouve pas une rétention : le GC n'est pas observé.
La comparaison avec025 attend toujours son exécution réelle. Gate3 ouverte.

### Mise à jour 310 — Candidat025 concordant ; diagnostic mémoire lancé

La session42090 est terminale, sortie0. Frontend et natif025 passent,
sources et assets préservés. La revue025 vérifie2 542 fichiers source,
3 000 imports,11 fichiers de kit, checkpoint propre, hashes natif/WASM
et worker, copies frontend identiques et configuration de distribution.
Il s'agit toujours d'une construction, pas d'une qualification runtime.

Les helpers du diagnostic025 passent SelfTest. La session69700 mesure
maintenant le même chargement/Jouer et150 secondes d'observation dans
une copie isolée, avec l'observateur v2 de groupes/piles bornés. Les limites
restent6 Gio privés,4 Gio GPU dédiés,2 Gio GPU partagés,8 Gio de marge commit
et300 secondes maximum. Aucun autre candidat ou build Gate3 possédé n'était
actif au précontrôle. Le guard ferme ses seuls processus en cas de dépassement.

La revue du diagnostic est préparée : elle vérifiera fermeture indépendante,
hashes préservés, absence d'erreurs d'instrumentation, compte du buffer fish
partagé et deltas de la dernière minute. Une observation bornée complète
ne vaudra ni stabilité de toute l'aventure ni clôture Gate3. Résultat en attente.

### Mise à jour 311 — Diagnostic025 négatif ; clock réutilisé, allocations restantes

La session69700 est terminale, sortie1 sous protection privée6 Gio :
32 échantillons,16 observations navigateur,6 742 487 040 octets privés,
3 387 944 960 octets GPU dédiés et175 325 184 octets GPU partagés au maximum.
Les identités de tous les processus observés sont indépendamment absentes.
Source, candidat et helpers sont préservés ; aucune fermeture normale ni
qualification mémoire de l'aventure n'est acquise.

Le buffer fish partagé est créé une seule fois, comme le vent partagé.
La simulation est prête mais les buffers sans label totalisent48 959 créations
sur1 230 soumissions. Les piles FishMaterial.unprepared_bind_group restent
observées. La correction du clock fonctionne au niveau du compte de créations
mais ne suffit pas ; les uniforms d'éclairage/clouds/object_settings continuent
à invalider les matériaux sur leurs changements réels.

L'audit vérifie aussi le source Bevy PBR0.19.1 contre son checksum de package :
la préparation GPU des clusters crée ViewClusterBindings et
ViewGpuClusteringBuffers dans la boucle de vues. Des piles observées de création
de buffers de clustering motivent cette piste complémentaire. Aucune cause
exclusive ni correction suivante n'est revendiquée. AGENTS interdit un fork
de Bevy : aucune modification du registre ou fork n'est effectuée.
Les voies de configuration supportées et le partage des uniforms Forge restent
à analyser/qualifier. Gate3 ouverte et non qualifiée, aucune publication.

### Mise à jour 312 — Source023 partage les uniforms fish ; contrôles en cours

Source023 copie les2 542 fichiers Source022 vérifiés, sans toucher au
checkpoint5a1c2bca83203e3e598549a6ce96b41f2be411b3 ni au produit. L'image de
tests Source022 archivée rejoue avec succès le vrai assemblage du shader
fish et fournit ses déclarations exactes ; il s'agit d'un test CPU Source022,
pas d'une qualification GPU ou d'un résultat Source023.

La correction Source023 ajoute un buffer partagé624 octets pour shadows,
les5 vec4 clouds, les paramètres underwater et bounce. Les layouts originaux
et leurs uniforms fallback17/40/48/51 restent disponibles ; un flag runtime
explicite sélectionne la nouvelle binding58 pour les poissons Studio. Le
receiver de shadows GPU44 garde sa priorité. L'éclairage partagé conserve55.
Les changements de textures restent suivis, les paramètres auteur et clocks
autonomes restent conservés. Aucune option de clustering n'est modifiée,
aucun fork ni mutation du registre Bevy n'est effectué.

Les2 545 fichiers épinglés contiennent8 changements/nouveaux fichiers.
Les nouveaux tests visent le layout624 exact, l'index entier, les refus
atomiques, la dérive shader et les vrais événements de matériaux lors des
changements d'uniforms communs ou de textures. La session35256 est terminale
à Clippy101 : format_push_string et closure Mat4 redondante. Ses sources sont
archivées avant deux corrections sans suppression de lint ni d'assertion.
La préparation023-002 relance format, strict natif, tests moteur et strict
WASM moteur/Studio dans la session30945. Aucun résultat final, essai GPU
ou gain mémoire Source023 n'est encore acquis. Gate3 ouverte et non qualifiée.

### Mise à jour 313 — Source023 qualifiée côté machine, GPU et mémoire à vérifier

La session 30945 est terminale à exit 0. Format, Clippy natif moteur
all-targets/all-features, les 706 tests moteur (0 échec, 10 ignorés),
Clippy WASM moteur et Studio passent. La revue contrôle les cinq résultats,
leurs logs et hashes, les 2 545 fichiers source et les huit changements
isolés ; Cargo.toml et Cargo.lock restent identiques à Source022.
Les trois nouveaux tests couvrent les données des receivers, les adaptations
shader et les vrais événements de matériaux. Ils ne prouvent pas le rendu GPU.

Le checkpoint privé local a18f6b942d730376db3be8c4bfc672165da605ae est propre,
parent Source022 5a1c2bca83203e3e598549a6ce96b41f2be411b3, sans remote ni
publication. L'exécutable de tests réellement mesuré est conservé avant
réutilisation du cache Cargo. Il rejoue le vrai assemblage complet du shader
fish Source023 avec ses fixtures vérifiées ; binding 58 et les quatre
accessors/fallbacks sont présents. Ce replay est CPU : aucune compilation
GPU, stabilité mémoire ou acceptation visuelle n'en découle.

Après le signalement propriétaire de la forte récupération RAM/VRAM, la
vérification live ne trouve aucun processus du scénario mémoire candidat025.
La scène complète n'est pas relancée dans cette étape. La suite est une
validation GPU ciblée du shader et de ses buffers avant un nouveau diagnostic
Tidewater protégé. Le produit principal reste inchangé, le fork Bevy et la
modification du clustering restent absents. Gate 3 ouverte et non qualifiée.

### Mise à jour 314 — Receivers fish : test GPU réel PASS, candidat026 en construction

Une copie de qualification conserve les 2 545 fichiers Source023 ; seuls
un module cfg(test) et un nouveau test GPU sont ajoutés. La revue confirme
que l'implémentation de production reste identique au checkpoint Source023.
Les deux premières préparations échouent côté test : import Image absent,
puis fonction 103 lignes pour une limite Clippy 100. Leurs sources et logs
sont archivés. Import et extraction d'un helper corrigent ces erreurs sans
suppression de lint ni d'assertion ; la préparation023-003 passe Clippy
natif all-targets/all-features et la compilation des tests.

Le test exécuté depuis son image archivée passe sur le GPU réel en 1,16 s.
Il utilise les déclarations exactes de l'assemblage Source023 et son vrai
adaptateur shader pour lire les 624 octets des receivers. Deux jeux de
fallbacks indépendants sont conservés ; 500 publications conservent la même
identité de buffer GPU ; les readbacks initiaux/final, u32::MAX, refus NaN
et réinstallation sont corrects. Le reçu live confirme l'absence de processus
de cette image après sa fermeture normale. Il ne qualifie ni le fragment
fish complet en rendu ni la mémoire de Tidewater.

Le garde était configuré à 2 Gio private, 1 Gio dédiée, 1 Gio partagée et
45 s, mais le test a fini avant sa première mesure : zéro échantillon,
candidate_seen false. Aucun plafond mémoire respecté ni plateau mesuré
ne peut donc être déduit de ce test. Le verdict mémoire global reste FAIL
sur candidat025 et non acquis sur Source023.

L'observateur navigateur v3 est préparé et son test de simulation passe :
écoute uncapturederror, compteurs séparés des erreurs d'instrumentation,
16 messages maximum de 400 caractères, WeakSet de devices, sans suppression
des événements originaux. Il n'a pas encore été exécuté dans le navigateur.

Les builders appariés Source023/candidat026 et la revue de concordance sont
préparés. La session14180 construit frontend puis natif depuis le checkpoint
a18f6b942d730376db3be8c4bfc672165da605ae et les mêmes 3 000 imports/11 kit
gouvernés vérifiés. Aucun résultat de build final ni diagnostic Tidewater026
n'est acquis. Aucun scénario complet ni publication lancé ; Gate3 ouverte
et non qualifiée, produit propriétaire préservé.

### Mise à jour 315 — Candidat026 mémoire FAIL fermé ; chemin des poissons d'étal corrigé

La session14180 termine à exit0 : frontend/WASM/worker puis natif PASS,
sources et assets stables. La revue candidat026 vérifie les 2 545 sources,
3 000 imports, 11 fichiers kit, source/HEAD privé propre et concordance des
binaires. Ce build ne qualifie pas le runtime. Les helpers du diagnostic
passent leur SelfTest avant lancement.

Le diagnostic protégé75259 termine en échec mémoire après environ77 s :
33 mesures mémoire, 31 GPU, 15 observations navigateur. Pic private
6 725 816 320 octets (6,26 Gio), dédiée3 347 050 496 (3,12 Gio),
partagée175 325 184. Le garde arrête sur owned_private_bytes_limit avec
les mêmes limites6/4/2 Gio et8 Gio de marge commit. Une vérification CIM
indépendante confirme toutes les dernières identités observées absentes.
Sources, fixture auteur, candidat et harness restent stables.

L'observateur v3 est effectivement actif sur un device : zéro erreur GPU
non capturée et zéro erreur d'instrumentation sur ce périmètre. Les buffers
shared fish frame, shared scene wind et shared fish receivers ont chacun
une création. Cela ne corrige pas la croissance globale : 50 841 créations
de buffers cumulées, dont40 145 sans label. Le comptage ne représente pas
la mémoire résidente. La dernière minute mélange encore le chargement et
le rendu ; aucun plateau mémoire n'est acquis. Simulation CPU prête, aucune
action RPG, sauvegarde ou qualification visuelle exécutée dans ce diagnostic.

L'inventaire des autres utilisateurs de FishMaterial trouve le chemin des
poissons d'étal : prepare_fish_assets ne sélectionnait ni shared_frame ni
shared_receivers ; ses templates et les copies dans fish_stand_scene/lod.rs
réécrivaient temps et frame_index via get_mut à chaque image. Les stacks
réelles échantillonnées contiennent encore FishMaterial::unprepared_bind_group.
C'est un chemin concret restant, pas la preuve d'une cause mémoire exclusive.
Les recréations Bevy de clusters et d'autres matériaux restent observées.

Source024 copie le checkpoint Source023 a18f6b942d730376db3be8c4bfc672165da605ae,
modifie seulement les deux fichiers Studio des étals, active les mêmes
buffers partagés et utilise update_material pour les templates et copies
LOD. Le temps, les receivers et les images communs sont publiés normalement ;
les paramètres auteur, LOD et fallbacks moteur restent conservés. Aucun
fork Bevy, changement du clustering ou modification du produit propriétaire.
La session83579 démarre format, strict natif, tests moteur, strict WASM
moteur et Studio. Aucun résultat final Source024 ni gain mémoire acquis.
Gate3 ouverte et non qualifiée ; aucune publication.

### Mise à jour 316 — Source024 qualifiée ; candidat027 en construction

La session83579 est terminale : format, strict natif moteur, 706 tests moteur
(0 échec, 10 ignorés) et strict WASM moteur PASS. Strict Studio échoue
uniquement sur prepare_fish_assets 108 lignes pour la limite100. Son fichier
est archivé avant extraction de update_fish_materials, sans changement des
données publiées, suppression de lint ni d'assertion.

La préparation024-002 vérifie que toutes les autres sources épinglées et
dépendances restent identiques ; elle conserve les trois preuves moteur
strict/tests/WASM antérieures et leurs hashes avec une provenance explicite.
Ces trois commandes ne sont pas rejouées. La session94246 exécute format
et strict Studio WASM après le découpage, puis termine à exit0. La revue
vérifie cinq preuves, réutilisées ou nouvelles selon leur provenance,
les 2 545 sources, les deux fichiers Studio modifiés et l'image de tests
réellement mesurée, archivée dans la préparation024-001.

Le checkpoint privé c0b22d61327207a7b6a34cc4de741c593e743393 est propre,
parent a18f6b942d730376db3be8c4bfc672165da605ae, sans remote ni publication.
Les modèles des poissons d'étal sélectionnent shared_frame et
shared_receivers ; leurs copies LOD héritent ces flags. Les deux chemins
utilisent le helper commun qui garde temps/fallbacks indépendants et ne
modifie le matériau que pour une vraie retouche locale ou un changement
LOD. La publication commune continue de suivre les handles d'images.

Builders et revue candidat027 utilisent cette préparation qualifiée.
La session49729 construit frontend puis natif depuis ce checkpoint et les
mêmes imports/kit épinglés. La preuve GPU Source023 reste limitée au code
moteur et aux dépendances byte-identiques, pas à l'intégration Studio des
étals nouvellement modifiée. Les scripts du diagnostic027 sont préparés
mais ne sont pas exécutés. Aucun résultat final candidat027 ou gain mémoire
Source024 acquis. Candidat026 demeure mémoire FAIL fermé ; aucun runtime
graphique actif dans cette étape. Gate3 ouverte et non qualifiée.

### Mise à jour 317 — candidat027 construit ; mémoire FAIL et arrêt vérifié

La construction49729 est terminale à exit0 : frontend et natif PASS.
La revue027 vérifie 2 545 sources, 3 000 imports et 11 fichiers du kit.
Natif 3907acb06a530f2638142b1cdfd276f857c18ab087206b98d1f682d40575a78c,
WASM af1c511b826089d852770ad7259e28186505dac6bdde4d8a8023dde1ca15e551.
Cette parité de construction ne qualifie pas le runtime.

Le diagnostic027, session90619, est terminal en échec de protection :
57 mesures mémoire, 27 observations navigateur, arrêt au seuil de mémoire
privée6 Gio. Pics mesurés : privée 6 576 070 656 octets, GPU dédiée
3 139 948 544, GPU partagée 175 325 184. Fermeture de toutes les identités
possédées vérifiée indépendamment par CIM ; entrées et sources inchangées.
Ce n'est pas une fermeture normale du scénario. Aucun nouveau test lourd
n'est lancé lors de cette revue.

Dans l'intervalle CPU prêt et joueur immobile, limité à 57,342 secondes
avant la dernière observation navigateur, privée +804 999 168 octets,
dédiée +701 513 728. Le filtre exclut démarrage et fermeture et son test
contrôle aussi les fenêtres inconnues ; CPU prêt ne prouve pas le chargement
visuel complet. Aucun plateau mémoire acquis. Les trois buffers partagés
frame/wind/receivers ont chacun une création ; un device observé,
zéro erreur GPU non capturée observée et zéro erreur d'instrumentation.

Les créations cumulatives atteignent 87 731 buffers. Les formes échantillonnées
identifient encore les matériaux et le clustering, mais aussi les uniforms
par étape des nuages, de l'océan, du ressac et de l'atmosphère. L'audit des
six fichiers épinglés confirme ces sites d'allocation. Leurs buffers
immutables protègent les étapes distinctes encodées avant une même soumission :
une correction par réutilisation doit conserver cette propriété, ainsi que
les changements de génération/device. Pas de preuve de cause exclusive,
de nombre résident ou de fuite GPU à partir des seuls compteurs de création.

Les 20 Go RAM/16 Go VRAM libérés rapportés par le propriétaire restent une
observation propriétaire, pas une mesure attribuée à ce dernier replay.
Sources, traces et preuve d'arrêt conservées. Prochaine étape : correction
bornée des uploads récurrents avec preuve GPU multi-étapes avant reconstruction
Studio ; pas de désactivation des effets ou de fork Bevy. Les parcours RPG,
la stabilité mémoire globale, la CI du candidat exact et la revue finale
restent requis. Gate3 ouverte et non qualifiée, aucune publication.

### Mise à jour 318 — Source025 : uploads océan/ressac réutilisés, preuves ciblées

Source025 est une copie isolée de Source024, 2 547 fichiers épinglés et
cinq fichiers changés : déclaration du module, océan, ressac, helper de
buffers et son test GPU. Dépendances, shaders et fixture auteur inchangés ;
aucune adoption sur Primary ni reconstruction Studio nouvelle.

L'océan conserve deux buffers d'une étape, pour paramètres et frame. Le
ressac conserve quatre emplacements alignés dans un buffer : chaque étape
bootstrap garde ses propres paramètres avant une même soumission. La même
cadence de simulation, les offsets step/time/dt et les copies de textures
restent conservés. Les ressources sont recréées pour une nouvelle génération
ou un autre device. Le helper rejette un batch entier invalide avant tout
write_buffer et conserve les buffers entre soumissions.

Trois tentatives initiales terminent à exit101 : deux lints de style
production, puis un type de tranche dans une assertion GPU, puis un lint
de style dans ce test. Leurs cinq fichiers changés sont archivés avant
chaque réparation. La tentative025-004, session40237 terminale à exit0,
exécute les cinq contrôles : format, strict natif moteur all-targets/all-features,
708 tests moteur PASS / 0 FAIL / 11 ignorés, strict WASM moteur et Studio PASS.
Les logs, les sources et l'image réellement exécutée sont vérifiés et archivés.

Le test GPU explicite utilise cette image archivée et le helper de production :
douze soumissions sans attente intermédiaire, quatre étapes différentes par
soumission, comparaison des 34 560 octets d'historique. Identité du buffer
stable, refus invalides sans écrasement, autre device avec buffer distinct PASS.
Fermeture indépendante de l'image de test vérifiée. La portée reste le helper
et l'upload, pas un rendu complet océan/ressac ni Tidewater stable.

Les uniforms des nuages et de l'atmosphère restent à corriger, et les
allocations de matériaux/clustering à qualifier. Aucun gain mémoire global
ou passage de Gate3 déduit de ces tests. Candidat027 reste mémoire FAIL fermé.
Un essai Studio distinct, hors campagne, a été observé pendant la compilation ;
ses anciens processus étaient absents à la revalidation, sans arrêt de notre
part. Il n'est pas attribué au diagnostic027. Aucun nouveau replay Studio ici.
Gate3 ouverte et non qualifiée, aucune publication.

Le garde mémoire du test ciblé a enregistré 1 mesures ; cela ne qualifie pas la stabilité globale.

### Mise à jour 319 — Source026 qualifiée ciblée ; candidat028 en construction

Source026 prolonge Source025 dans une copie isolée : 2 550 fichiers épinglés,
neuf fichiers changés par rapport à Source025. Les chemins océan/ressac,
les dépendances, le corpus de shaders produit et le contenu auteur restent
identiques. Les uniforms des frames nuages local544/global720/observateur64
sont publiés une fois par lot et partagés entre ombre, trace, temporalité et
panorama. Les slots restent distincts pour toutes les étapes d'un lot,
capacité bornée à 256 selon la validation source. Les paramètres live des
nuages utilisent aussi un buffer résident ; les caches sont renouvelés pour
une nouvelle génération ou un autre device.

L'atmosphère utilise deux canaux résidents séparés : travail principal et
observateur peuvent être encodés avant la même soumission sans écraser les
paramètres du premier. Les images et la cadence des passes restent conservées.
La tentative026-001 termine à exit101 sur un BindingResource déplacé puis
réutilisé dans panorama ; les neuf fichiers sont archivés avant clonage
de la description du binding, sans recréer son buffer.

La tentative026-002, session86785 terminale à exit0, exécute les cinq
contrôles : format, strict natif moteur all-targets/all-features, 710 tests
PASS / 0 FAIL / 13 ignorés, strict WASM moteur et Studio PASS. La revue
vérifie aussi les sept tests existants d'événements/clock/receivers, les
2 550 hashes et l'image moteur réellement exécutée, conservée hors cache.

Les trois tests GPU explicites passent sur cette image : helper général
(34 560 octets d'historique), blocs nuages (63 744 octets), canaux atmosphère
(1 536 octets), douze soumissions par test sans attente intermédiaire.
Identités réutilisées, croissance de capacité et remplacement device,
observers facultatifs sans valeurs résiduelles, refus invalides sans
écrasement et canaux principaux/observateur distincts vérifiés. Driver et
image native sont absents à la vérification CIM indépendante. Le garde
n'a qu'une mesure : aucun pic ou plateau mémoire global qualifié.
Ces shaders de qualification vérifient les uploads, pas le rendu complet
des effets ni le nouveau viewport Studio.

Le checkpoint privé ccbb6bdf4942c1b78c1daacce60e24628c6d46ee est propre,
parent c0b22d61327207a7b6a34cc4de741c593e743393. Il regroupe treize
fichiers de delta depuis Source024, dont les corrections Source025.
Aucun remote public, aucune adoption sur Primary ou publication. Une
finalisation de préparation appelée avant sa fin a refusé le checkpoint
absent sans lancer de build ; elle est rejouée après sa vraie terminaison.

La session39780 construit maintenant le frontend puis le natif du
candidat028 depuis ce checkpoint, avec les mêmes 3 000 imports et onze
fichiers du kit épinglés. La revue finale est préparée et exige ces hashes
ainsi que les trois tests GPU Source026. Elle ne réutilise pas l'ancienne
affirmation d'identité de tout le moteur avec le probe GPU Source023.
Aucun résultat final candidat028 ni replay mémoire acquis. Les allocations
matériaux/clustering et la stabilité complète sur Tidewater restent à
qualifier. Candidat027 reste mémoire FAIL fermé. Gate3 ouverte et non
qualifiée ; aucune publication.

### Mise à jour 320 — Candidat028 construit ; nouveaux replays lourds suspendus

La session39780 termine réellement à exit0. La revue indépendante du
candidat028 confirme la parité frontend/natif/source/assets et les preuves
ciblées Source026. Aucun lancement graphique du candidat028 effectué ;
aucun plateau mémoire ou rendu complet acquis par cette construction.

Le propriétaire signale environ 20 Go de RAM et 16 Go de VRAM libérés
après le test problématique. Ces valeurs sont une observation propriétaire,
pas une mesure instrumentée attribuée au candidat028. Le contrôle CIM
actuel ne trouve plus de processus Studio de test, seulement deux hôtes
MCP Forge. Windows indique 28,79 Gio de mémoire physique libre et 17,68
Gio de mémoire virtuelle libre au contrôle. La charge historique exacte et
sa cause restent à établir ; la libération seule ne prouve pas une fuite.

Les nouveaux replays lourds sont suspendus pendant le diagnostic de cette
saturation. Les traces et candidats restent conservés. La compilation
terminée ne vaut pas qualification mémoire. Gate3 reste ouverte et non
qualifiée ; aucune adoption Primary ni publication.

### Mise à jour 321 — Allocations matériaux : correction Source027 isolée, contrôles CPU actifs

L'inspection Source026 établit des publications inutiles dans les chemins
de synchronisation : insertions des matériaux réfraction village/terrain à
chaque frame, réécriture du drapeau optical refraction_ready, couleur et
émission des objets RPG réassignées même inchangées. La préparation Bevy
recrée les bind groups lors des modifications de matériaux. Cela établit
une source de travail et d'allocations répétées, pas la cause exclusive des
20 Go RAM/16 Go VRAM signalés par le propriétaire.

Source027 est une copie isolée : 2 552 pins, sept fichiers de delta depuis
Source026, dont deux fichiers de tests. Les matériaux captures comparent
toute leur base et extension avant insertion. Les handles manquants sont
recréés, les éditions véritables, niveau d'eau, données LOD/textures et
retrait des sources restent transmis. Les flags optical et les surfaces
RPG ne demandent un emprunt mutable que pour un changement de valeur.
Pas de modification shader, dépendance, rendu ni de fork Bevy ; aucune
adoption Primary. Les tests régressions vérifient les événements Assets
réels sur cent répétitions puis les changements et la récupération.

Tentative027-001 : strict exit101, comparaison de float du flag GPU.
Tentative027-002 : strict exit101, point-virgule dans le test. Les fichiers
concernés sont archivés avant réparation ; aucun lint supprimé. La
tentative027-003 passe format et strict moteur all-targets/all-features.
Sa session52752 poursuit les tests moteur puis strict WASM moteur/Studio.
Les nouveaux tests ne sont pas annoncés PASS avant leur exécution.

Le contrôle mémoire durant compilation montre 21,57 Gio physiques libres
et 12,33 Gio virtuels libres. Aucun nouveau Studio/replay graphique lancé.
La revue finale machine est préparée, pas acquise. Le propriétaire est
interrogé sur la fenêtre fermée afin d'attribuer ses valeurs aux bonnes
traces. Gate3 ouverte/non qualifiée ; nouveaux replays lourds suspendus
pendant le diagnostic ; aucune publication.

### Mise à jour 322 — Source027 machine/GPU ciblés PASS ; candidat029 en construction

La session52752 termine à exit0. Les cinq contrôles de la tentative027-003
passent : format, strict moteur natif all-targets/all-features, 713 tests
moteur / 0 FAIL / 13 ignorés, strict WASM moteur puis Studio. Les trois
tests ajoutés passent sur la vraie image archivée : cent synchronisations
sans événements matériaux superflus puis mises à jour couleur/combustion,
base village, niveau d'eau, données LOD/texture, récupération du capture
manquant et retrait des sources. Les sept tests existants clocks/receivers
passent également. Revue des logs, hashes, 2 552 pins, sept deltas et
identité des dépendances/shaders effectuée ; rendu graphique non acquis.

Les trois tests GPU des buffers résidents passent à nouveau sur la vraie
image moteur Source027. La fermeture indépendante CIM confirme zéro image
ou driver en vie. Le garde n'a aucune mesure et n'a pas observé l'image
brève : aucun pic, plateau RAM/VRAM ou rendu complet n'est qualifié. Les
preuves portent sur les uploads ciblés, pas sur les matériaux en scène.
La revue GPU a initialement refusé un chemin relatif incorrect après son
déplacement dans son dossier ; le script est archivé avant correction du
répertoire parent puis revue PASS, sans rejouer les tests.

Le checkpoint privé 53a53276f3f09eab0c55afb9521dee7b7962d60a est propre,
parent ccbb6bdf4942c1b78c1daacce60e24628c6d46ee, sept deltas. La
session96526 termine à exit0, sans remote public. La session72861 construit
le frontend puis le natif candidat029 à partir de ce checkpoint, avec
3 000 imports et onze fichiers de kit inchangés. La revue du candidat et
les scripts de diagnostic029 sont préparés, pas exécutés. Le diagnostic
conserve les limites privées6/dédiées4/partagées2/headroom8 Gio/300s,
observer v3 et contenu auteur inchangés. Aucun nouveau replay graphique
lourd lancé ; attente du candidat corrigé avant qualification protégée.

L'observation propriétaire 20 Go RAM/16 Go VRAM reste sans attribution
instrumentée précise ; la fenêtre fermée n'a pas encore été identifiée.
Les causes matériaux répétées sont corrigées et validées au niveau des
événements CPU, sans annoncer qu'elles expliquent toute la saturation.
Clustering et autres ressources restent à mesurer. Aucun remplacement
Primary ni publication. Tous les critères publics RPG/ressources,
sauvegarde/reprise, fermeture, feu/poussée, validation humaine et CI
conservent leurs exigences. Gate3 reste ouverte et non qualifiée.

### Mise à jour 323 — Build029 toujours actif ; fixture et navigateur revérifiés

La session72861 reste vivante et n'est pas relancée. Le journal frontend
a progressé de la compilation moteur à celle du Studio. Le relevé CIM
identifie le seul driver26536 et ses descendants : environ6,03 Gio privés
au relevé, 25,05 Gio physiques et 11,97 Gio virtuels libres. C'est une
mesure ponctuelle du build, pas un pic ni une qualification runtime.

Le navigateur figé154.0.4258.53 et l'objet auteur existant correspondent
toujours à leurs SHA-256. Le contenu réel est en révision42 : inventaire
joueur0 et vitalité gardien500. L'ancien config de chargement contient des
métadonnées40/24/100 inutilisées par le flux loading, qui ne prouvent pas
les ressources. La recette029-fixture-v2 est préparée et compilée en
syntaxe ; après la revue du build, elle dérivera ces métadonnées depuis
l'objet immuable et épinglera le nouveau config. Aucune édition cachée du
contenu auteur, aucun lancement ou résultat de diagnostic029 acquis.
La recette initiale029 et sa preuve restent conservées.

Source027 machine/GPU ciblés restent PASS ; matériau en scène, plateau
mémoire et totalité des critères Gate3 restent non qualifiés. Aucun
changement Primary ni publication. Prochaine dépendance : vraie fin de
session72861, revue candidate029, préparation v2 et self-test du driver,
puis diagnostic sous les protections existantes.

### Mise à jour 324 — Candidat029 PASS build et diagnostic borné ; croissance résiduelle

La session72861 termine à exit0. Revue candidat029 : parité des 2 552
sources, frontend/natif/worker, 3 000 imports et onze fichiers de kit PASS.
La préparation diagnostic029-fixture-v2 dérive réellement son metadata
depuis la fixture immuable : revision42, inventaire joueur0, gardien500.
Self-test du driver exact PASS. Préflight : aucun Studio concurrent et
18,67 Gio de marge selon le compteur OS utilisé par le garde.

La session59434 termine à exit0. Le flux public ouvre Tidewater, lance
Jouer et observe le chargement/idle pendant150s. 68 mesures mémoire,
32 observations navigateur ; pic privé5 648 756 736 octets (~5,26 Gio),
dédié2 295 296 000 (~2,14 Gio), partagé172 179 456 (~0,16 Gio).
Aucun arrêt de protection ni dépassement des limites dans les relevés.
Fermeture normale et vérification CIM indépendante : tous les processus
observés sont absents. Sources, helper, config et fixture restent stables.
Observerv3 : un device observé, zéro erreur GPU non capturée observée,
zéro erreur d'instrumentation ; aucune affirmation sur les erreurs avant
installation ni sur device.lost.

Le diagnostic borné passe sa revue. Il ne qualifie pas la mémoire de
toute la boucle RPG. Dans la fenêtre CPU-ready57,857s, les augmentations
restent122 875 904 octets privés (~117 Mio) et113 778 688 dédiés (~109 Mio).
La comparaison à diagnostic027 confirme même fixture, navigateur,
harness, driver, observer et garde ; l'ancienne fenêtre57,342s augmentait
804 999 168 privés et701 513 728 dédiés avant arrêt de protection. Les
durées et fins diffèrent ; pas de causalité exclusive ni plateau acquis.

Les buffers compute résidents observés n'ont aucune création supplémentaire
dans la dernière fenêtre navigateur CPU-ready55,645s. Les huit labels
présents totalisent chacun une création ; le canal atmosphère observateur
reste inconnu/absent de l'observation, jamais traité comme zéro. Les trois
buffers partagés poissons/vent/receivers restent chacun à une création.
Le compteur total37787 est cumulatif, pas un nombre de buffers résidents.

L'audit029 conserve les stacks limitées64shapes/4stacks/2048caractères,
avec16440 créations hors shapes retenues. Il identifie encore les
familles OpticalMaterial, compute environnement live, bindings/uploads
clusters Bevy et autres matériaux. Ces appels observés ne prouvent pas
une fuite exclusive. La croissance résiduelle doit être traitée avant
d'annoncer une stabilité longue ; aucun test public ressources ni gain
mémoire global acquis par le seul diagnostic. Source027 toujours isolée,
pas de changement Primary, pas de publication. Gate3 ouverte/non qualifiée.

### Mise à jour 325 — Source028 : buffers environnement résidents, qualification en cours

La croissance résiduelle de diagnostic029 conduit à traiter les allocations
du compute environnement live dans une nouvelle copie isolée Source028.
Les 2 552 fichiers Source027 sont vérifiés avant copie ; Source028 possède
2 554 fichiers épinglés et quatre différences : compute, déclaration du
module et deux nouveaux fichiers de buffers/tests. Aucun shader ni manifeste
de dépendances ne change. Aucun changement appliqué au Primary.

Quatre buffers UNIFORM|COPY_DST de tailles exactes720/64/80/32 et un
buffer COPY_SRC|COPY_DST de32 octets pour la lumière sont conservés par
génération/device. L'ABI exacte du programme environnement est conservée.
Un changement de génération ou device recrée le cache et le contrôleur.
La lumière est validée avant les uploads ; les commandes restent dans le
RenderContext existant et le commit du contrôleur suit leur acceptation.
Le calcul consomme un seul input cohérent par invocation RenderGraph.

Le test GPU ajouté vise douze uploads/soumissions successifs sans attente
intermédiaire, lecture shader des720/64/80/32 octets et copie des32 octets
de lumière dans un historique ; identités stables et lecture exacte requises.
Ce test GPU est ignoré par défaut et n'a PAS encore été exécuté. Le contrôle
de son shader et un test de packing de bits flottants sont aussi ajoutés.

Première tentative machine028-001 : format PASS, Clippy exit101 pour
chunks_exact_mut constant. Rapport et source avant réparation conservés.
Correction conforme as_chunks_mut, aucune suppression de lint.
Tentative028-002 démarrée, handle47319 : format PASS, qualification native,
tests moteur et WASM à poursuivre sur ce même handle. Aucune preuve de
stabilité mémoire runtime n'est acquise et aucun nouveau replay Tidewater
n'est lancé. Le signal propriétaire20 Go RAM/16 Go VRAM libérés reste
sans attribution instrumentée exclusive. Gate3 ouverte/non qualifiée.

### Mise à jour 326 — Source028 : correction du test pour wgpu29, troisième tentative

Le handle47319 est terminal exit1 : tentative028-002, format PASS puis
Clippy exit101. Le nouveau test employait device.pop_error_scope, absent
de wgpu29.0.4 ; sa validation est corrigée avec le ErrorScopeGuard retourné
par push_error_scope et guard.pop. Le montage des ressources est extrait
dans une fonction dédiée. Aucun lint désactivé ; rapport d'échec et fichier
avant correction archivés. Les quatre seuls fichiers modifiés restent
dans l'environnement live, avec shaders et dépendances inchangés.

Tentative028-003 démarrée sur handle48532, à poursuivre sans relancer le
même driver. Le test GPU explicite et le replay Tidewater restent non
exécutés ; aucun plateau mémoire, aucune qualification finale acquis.
Source isolée, aucune adoption Primary ni publication. Gate3 ouverte.

### Mise à jour 327 — Source028 moteur715 et GPU4 PASS ; candidat030 en construction

La session48532 termine exit0. Revue de ses cinq commandes : format,
Clippy natif all-targets/all-features strict, tests moteur715 PASS/0FAIL/
14ignorés, Clippy WASM moteur et WASM Studio strict PASS. Les2 554 pins
et quatre différences sont revérifiés. Tous les shaders et dépendances
de Source027 restent identiques. Exécutable réellement testé conservé
hors cache avec SHA2565adb051efc7fe8e1f0524912ba009e9d1d9e4cae415dcf3f9c6e465bb2fbc2d6.

Quatre cas GPU explicitement exécutés depuis cette image, chacun listé
exactement avant exécution, chacun1PASS/0FAIL. Le nouveau cas environnement
lit les720/64/80/32 octets de paramètres par shader et les32 octets de
lumière par copie, sur12 soumissions sans attente intermédiaire ; historique
11136 octets exact et identités stables. Les trois cas préexistants de
UniformBatch/cloud/atmosphère passent aussi sur l'image Source028.
Garde inchangé45s/privé2Gio/dédié1Gio/partagé1Gio/marge8Gio, sans arrêt
de protection. Un seul échantillon et candidate observé : aucun pic ou
plateau mémoire qualifié. Fermeture CIM indépendante image/driver PASS.

La revue statique épingle les fichiers réellement installés de Bevy0.19.1,
la déclaration Forge before(camera_driver), le RenderContext et la phase
Submit du pipeline : upload cohérent puis encodage/acceptation avant la
soumission du graphe, avant l'invocation suivante. Portée distincte de la
lecture GPU ; aucune promesse de plusieurs uploads avant un seul submit.

La session88636 termine exit0. Checkpoint privé propre Source028
2b48b1f706b420b9c4be7e990c91bfc19fced816, parent Source027
53a53276f3f09eab0c55afb9521dee7b7962d60a ; quatre fichiers de delta,
aucun remote public. Scripts candidat030 finalisés après ces preuves.
Construction frontend puis natif démarrée sur handle14797 ; poursuivre
ce même handle. Build et parité restent à revoir ; aucun nouveau replay
Tidewater lancé. Stabilité globale, ressources publiques et autres critères
Gate3 toujours à qualifier. Aucune adoption Primary ni publication.

### Mise à jour 328 — Scripts diagnostic030 préparés ; frontend030 toujours en compilation

Handle14797 revérifié actif sur la construction frontend ; aucune relance
du builder. Le log atteint la compilation du moteur WASM. Les scripts du
diagnostic030 sont préparés et compilés syntaxiquement, sans exécution.
Ils exigent la revue appariée du candidat030 et la preuve des quatre cas
GPU Source028. Le champ du reviewer devient four_native_gpu_cases_pass ;
le passage des paramètres environnement et lumière est exigé explicitement.
La version précédente du reviewer est conservée avant cette correction.

La recette fixture-v2 conserve la dérivation depuis l'objet auteur immuable
revision42/player_capacity0/guardian_vitality500 ; ce metadata du futur
config030 reste à produire et à vérifier lors de la préparation effective.
Ni modification cachée de la fixture ni preuve runtime par syntaxe seule.
Même driver public chargement/idle, même observerv3, même harness et mêmes
seuils privé6Gio/dédié4Gio/partagé2Gio/marge8Gio/durée300s.
Le calcul de fenêtre CPU-ready testé est réutilisé ; cinq labels des buffers
environnement s'ajoutent aux compteurs. Une baseline absente reste inconnue.
Les compteurs cumulés ne sont pas assimilés à des buffers résidents.

Aucun nouveau diagnostic lancé : attendre build/parité030 puis self-test,
préflight et lancement borné. Source028 et tests715/GPU4 restent acquis
dans leur portée ; la stabilité longue mémoire et toute Gate3 restent
non qualifiées. Aucun changement Primary ni publication.

### Mise à jour 329 — Candidat030 parité PASS ; diagnostic mémoire030 en cours

Handle14797 termine exit0 : frontend puis natif construits, sources et
assets stables. Revue candidat030 PASS :2 554 sources, frontend/natif/
worker,3 000 imports et11 fichiers de kit. Aucun changement Primary.
La recette030-fixture-v2 produit le config depuis la fixture immuable :
revision42, capacité joueur0, vitalité gardien500. Self-test du driver exact
PASS. Préflight : aucun Studio concurrent et17 Gio de marge OS du garde.

Un seul diagnostic public chargement/idle030 est lancé sur handle19176,
avec copie isolée de la fixture et garde inchangé privé6Gio/dédié4Gio/
partagé2Gio/marge8Gio/durée300s. Les premières observations sont encore
en chargement ; aucun pic final, fermeture ou plateau acquis. Les cinq
labels environnement résidents sont visibles chacun à une création dans
les observations initiales ; compteur cumulatif, pas une résidence.

L'audit statique optique épingle les APIs Bevy installées et Source028.
ShaderBuffer réutilise le buffer pour taille/usage/label identiques avec
COPY_DST. Les uniforms optiques720/binding7 et64/binding18 restent dérivés,
leurs updates font encore des borrows matériels suivis. Un resize océan
remplace Image sous le même handle : supprimer les events du matériau
sans rebinding des vues ne serait donc pas une correction suffisante.

Piste conditionnelle non implémentée : buffers uniformes d'assets et layout
UNIFORM explicite via AsBindGroup. Préserver toutes les autres interfaces,
shaders et changements réels ; vérifier events, bytes GPU, clonage, resize,
réallocation et device recovery. Ne pas alimenter un layout STORAGE avec
un buffer UNIFORM. Les borrows non suivis ne sont corrects que pour les
miroirs CPU dont la valeur GPU est publiée séparément. Attendre le résultat
du diagnostic030 avant de décider sur la croissance mémoire effective.

Gate3 et stabilité mémoire globale restent non qualifiées. Aucune adoption
Primary ni publication ; périmètre RPG/authoring/qualifications inchangé.

### Mise à jour 330 — Diagnostic030 FAIL fermé : croissance et enfant WebView2 après fermeture

Session19176 terminal exit1. Driver public exit0, chargement/idle complété,
fenêtre annoncée Closed=true/Forced=false ; garde exit1, arrêt de protection
owned_descendant_outlived_driver. Aucun seuil mémoire/durée ne déclenche
dans les71 relevés. Pic privé5 692 280 832 octets (~5,30 Gio), dédié
2 228 252 672 (~2,08 Gio), partagé172 179 456 (~0,16 Gio).
Cette séquence n'est PAS un PASS de fermeture normale ni de qualification.

Le garde conserve puis nettoie l'identité du racine WebView2 pid9348,
créé2026-10-06T04:04:01.164096+02:00, parent22408. Le dernier relevé
montre encore quatre descendants WebView2 et3 998 502 912 octets privés
(~3,72 Gio) après disparition du candidat natif. Le compteur dédié a
déjà chuté à42 799 104 octets dans ce dernier relevé. La vérification CIM
indépendante retrouve ensuite zéro identité observée vivante. Les anciennes
valeurs propriétaire20Go/16Go restent sans attribution exclusive.

Revue d'échec dédiée épingle les vrais logs/mesures/identités. La fenêtre
CPU-ready réutilise exactement le corps d'analyse et la preuve de test
du reviewer027 :27 mesures sur59,174s, hausse198 680 576 octets privés
(~189,48 Mio) et176 693 248 dédiés (~168,51 Mio). Pas de plateau acquis.
Les observations navigateur actives couvrent55,549s : zéro création
supplémentaire pour les cinq buffers environnement et tous les autres
labels résidents présents. Le canal atmosphère observateur absent reste
inconnu. Aucun gain global ni causalité exclusive déduit de ces seules
fenêtres, de durées/chronologies/calculs différents du diagnostic029.

Observerv3 : un device observé, zéro erreur non capturée et zéro erreur
d'instrumentation dans la portée enregistrée. Ces compteurs ne prouvent
pas toute la fidélité visuelle ni l'absence d'erreurs avant installation.
Source, fixture, helpers et candidat conservés. Aucun nouveau replay lancé.

Suite : analyser/corriger les publications du matériau optique avec les
contraintes de layout et de rebinding documentées, et la terminaison
WebView2 après fermeture du natif. Ne pas augmenter les seuils du garde
pour transformer cet échec en réussite. Aucun test public ressources,
aucune adoption Primary ni publication. Gate3 ouverte/non qualifiée.

### Mise à jour 331 — Source029 : retour de boucle native pour cleanup, contrôle en cours

Diagnostic030 FAIL fermé reste inchangé. La fermeture native est traitée
dans une nouvelle copie isolée Source029 :2 554 pins, seul main.rs change.
Le Builder construit App puis appelle run_return ; main rend ExitCode au
lieu de la boucle divergente Builder::run. Les handlers de confirmation,
fonctions de contrôle dirty/discard et destruction confirmée des fenêtres
restent identiques ; aucun timeout/seuil de garde augmenté, aucun unsafe
ou dépendance ajouté, aucun rendu modifié.

Les APIs installées sont épinglées : Tauri2.11.5, runtime-wry2.11.4,
Wry0.55.1 et Tao0.35.3 Windows. Tao.run appelle sa boucle retournante puis
std::process::exit ; le chemin retournant Wry permet le drop des owners
après la boucle. Le destructeur Wry WebView2 appelle controller.Close.
Ces sources étayent la correction proposée ; elles ne prouvent pas encore
la cause exclusive du descendant030 ni sa fermeture sur nouveau candidat.
Les exit codes byte-range sont conservés ; un code hors plage rapporte
failure1. Aucun command Studio existant n'expose de code de sortie arbitraire.

Handle49353 actif : format PASS, Clippy Studio natif all-targets/all-features
strict en cours, puis tests du binaire Studio. Le contexte d'assets est le
frontend030 vérifié, passé via TAURI_CONFIG sans modification du fichier
de config ou des assets. Les sources moteur/WASM restent identiques à
Source028 ; les715 tests et GPU4 parent ne sont pas réexécutés par cette
qualification native ciblée. Aucun nouveau replay/Studio GPU lancé.

Reviewer dédié préparé pour logs/2554 pins/unique delta/image native
réellement exécutée et tests de fermeture existants ; la preuve runtime
WebView et la croissance du matériau optique restent à traiter. Aucune
adoption Primary ni publication. Gate3 ouverte/non qualifiée.

### Mise à jour 332 — Source029 contrôle natif strict et13 tests PASS ; fermeture WebView non qualifiée

Handle49353 termine exit0. Trois commandes complètes PASS : format,
Clippy Studio natif all-targets/all-features strict et tests du binaire
forge-studio13PASS/0FAIL/0ignoré. Le reviewer vérifie logs/exit codes,
2 554 pins et seule différence main.rs, ainsi que l'image native réellement
exécutée conservée hors cache. Les dix tests du main original passent et
les fonctions après finish_window_close sont textuellement identiques.

La première lecture du reviewer refusait les noms des tests gate0, car son
expression n'acceptait pas les chiffres dans les identifiants Rust. Ancien
reviewer conservé avec son hash antérieur ; expression corrigée, revue
seule rejouée PASS. Aucun résultat machine remplacé ni test supprimé.

Portée : entrée native run_return/ExitCode et politique de confirmation
existante. Les715 tests moteur parent Source028 ne sont pas réexécutés ici,
ses sources moteur/WASM restant identiques. Les tests natifs sans fenêtre
ne prouvent ni la libération de WebView2 ni la cause exclusive d'orphelin030.
Cette fermeture doit encore être mesurée sur un candidat apparié ; aucun
nouveau runtime lancé dans cette étape.

Pour le prochain changement optique, l'ordre des events exige de publier
explicitement l'invalidation matérielle après les events d'images et avant
l'extraction du rendu. Une seule invalidation par update de buffers remettrait
les allocations récurrentes ; rester sur publication séparée des uniforms
et rebindings des textures réellement remplacées. AsBindGroup doit conserver
les layouts UNIFORM exacts et les autres interfaces. Cette correction
optique n'est pas encore implémentée ; elle ne reçoit aucun PASS par plan.

Diagnostic030 FAIL/croissance198 680 576 privés et176 693 248 dédiés reste
inchangé. Gate3 ouverte/non qualifiée. Aucun changement Primary ni publication.

### Mise à jour 333 — Source030 : uniforms optiques résidents et invalidation d'images, contrôles actifs

Copie isolée Source030 depuis Source029 contrôlée :2 556 pins, trois
différences optiques versus Source029. La correction native run_return
est retenue à l'identique ; shaders et dépendances ne changent pas.
OpticalMaterial délègue ses bindings statiques à OpticalBindings. Les
handles ShaderBuffer des bindings7/18 résolvent les ressources ; le layout
effectif est explicitement UNIFORM720/64 avec mêmes visibilités. Ne pas
assimiler les annotations de résolution storage du helper au layout final.

Les étapes de stage allouent un couple de buffers d'assets privé de tailles
fixes, labels fixes et usageUNIFORM|COPY_DST|COPY_SRC. Les updates frame et
atmosphère calculent leurs valeurs localement, valident les deux backings
avant toute écriture et publient seulement les données changées. Les miroirs
CPU seuls sont modifiés sans event matériau ; les autres champs et edits
restent suivis. Les publications gardent les matrices, clocks, immersion
et éclairage d'origine ; aucun effet supprimé ni shader retouché.

Un listener après AssetEventSystems transmet un event matériau lorsque
l'une des21 dépendances image est ajoutée/modifiée/retirée/chargée ; une
image sans lien reste sans effet. Cet event est disponible avant extraction
du rendu. Les updates des buffers uniforms ne provoquent pas cette
invalidation ; le pipeline GpuShaderBuffer doit réutiliser son backing.
Clonage d'extraction conserve les handles de la scène ; un nouveau stage
alloue une paire distincte. Aucune stabilité GPU annoncée sans lecture réelle.

Trois tests ajoutés au pipeline Assets réel :100 publications de bits
frame/atmosphère sans event matériau et idle silencieux ; remplacement
de texture et vrai edit toujours transmis, image sans lien ignorée ; refus
atomique d'atmosphère invalide/backing absent et paires de stage distinctes.
Ces tests sont préparés mais pas encore PASS. Une lecture GPU des backings
et du layout optique effectif reste requise, puis le runtime complet.

Tentative machine030-002 : format PASS, strict exit101 (import inutilisé,
semicolon unit, closure redondante, expression booléenne, bloc720 passé
par valeur). Rapports et trois sources avant correction conservés.
Réparations sans suppression de lint, bloc720 désormais emprunté.
Tentative030-003 démarrée sur handle60202 ; poursuivre ce handle pour
format/natif moteur all-targets/all-features/tests moteur/WASM moteur/Studio.
Aucun nouveau replay lancé, pas de gain mémoire global acquis. Fermeture
WebView Source029 reste à mesurer. Gate3 ouverte, aucune adoption Primary
ni publication ; périmètre RPG/authoring final inchangé.

### Mise à jour 334 — Fixture optique corrigée, troisième tentative machine active

Handle60202 termine exit1 : tentative030-003 format PASS, strict exit101.
Le générateur de fixture avait ajouté les deux champs de backing à chaque
itération de la liste des champs, créant des initialisateurs dupliqués dans
le test. Ce défaut appartient à la fixture, pas à une preuve GPU.
Fixture avant correction et générateur initial archivés ; indentation du
générateur corrigée, duplications retirées. Un seul fichier de test change
versus030-003 ; les deux fichiers produit restent identiques. Aucun test
retiré, aucune suppression de lint, rapports d'échec conservés.

Tentative030-004 démarrée sur handle26842 pour les mêmes cinq contrôles.
Les2 556 pins sont revérifiés ; reprendre ce handle, sans relancer le
driver. Les tests Assets du nouveau matériau, la lecture GPU et la fidélité
du rendu restent non qualifiés. Source029 native strict/13 tests conserve
sa portée et son runtime WebView doit toujours être mesuré. Aucun replay
lourd lancé, aucun changement Primary ni publication. Gate3 ouverte.

### Mise à jour 335 — Source030 machine PASS ; saturation mémoire toujours non résolue

Handle26842 termine exit0. Les cinq contrôles passent : format,
Clippy moteur natif tous targets/features strict, moteur 718 PASS / 0 FAIL /
14 ignored, Clippy WASM moteur et Studio strict. Revue racine vérifie les
logs, 2 556 pins, les trois deltas optiques, le maintien byte-identique du
correctif natif Source029, shaders/dépendances inchangés et l'image de tests
réellement exécutée, archivée et hachée. Les trois nouveaux tests Assets
passent, dont publications exactes sans invalidation du matériau à chaque
frame, invalidation des images/éditions et refus atomique des entrées invalides.

L'utilisateur signale 20 Go RAM et 16 Go VRAM libérés après fermeture.
Cette observation est conservée sans attribution instrumentale exclusive.
Loading030 reste FAIL : croissance et descendant WebView après fermeture.
Aucun nouveau replay Tidewater. Restent la qualification GPU des buffers
optiques et de leur layout effectif, fermeture native complète et stabilité
mémoire sur le candidat intégrant Source029/030. Ces contrôles machine ne
prouvent ni plateau mémoire ni fidélité du rendu. Gate3 ouverte, aucune
adoption Primary ni publication ; poursuite solo.

### Mise à jour 336 — Test GPU optique préparé ; contrôle machine actif

Source031 conserve le produit Source030 byte-identique et ajoute uniquement
un module de tests GPU et son inclusion : 2 557 pins. Le test utilise le
GpuShaderBuffer::prepare_asset de Bevy, vérifie les entrées UNIFORM effectives
7/18 du matériau (720/64 octets), compare les BufferIds sur douze soumissions
et prévoit une relecture shader de 9 408 octets, valeurs entières et flottantes
mixtes incluses. Le cas sans précédent prépare aussi une nouvelle identité ;
ce cas ne constitue pas une simulation de perte de device.

Première tentative83653 terminal exit1 : format PASS, compilation du test
refusée pour Result de SystemState et comparaison directe des wrappers
Buffer. Source avant réparation archivée, accès Result et comparaisons
BufferId corrigés sans modification produit ni suppression de contrôle.
Tentative031-002 active handle38350 pour les cinq contrôles machine.
Le runner des cinq cas GPU est préparé, non exécuté, avec le même garde
45 s / 2 GiB privé / 1 GiB dédié / 1 GiB partagé / 8 GiB headroom.
Aucun chargement Tidewater. Qualification du bind group complet, perte de
device, fermeture native et stabilité mémoire scène restent ouvertes.
Poursuite solo, Primary inchangé, aucune publication. Gate3 ouverte.

### Mise à jour 337 — Source031 strict natif PASS, compilation tests active

Handle38350 terminal exit1 : les erreurs d'API sont corrigées mais le
contrôle strict refuse la longueur du test et une closure redondante.
Test avant correction archivé, création du probe GPU extraite dans un
helper et référence Buffer::id utilisée. Toutes assertions conservées,
aucune suppression de lint, produit Source030 byte-identique.
Tentative031-003 handle73010 active : format et strict natif tous
targets/features PASS ; compilation moteur/tests en cours. Les contrôles
WASM et les cinq cas GPU doivent encore terminer/être exécutés.
Garde GPU inchangé, aucun replay Tidewater, aucune qualification mémoire
scène/fermeture native/device perdu. Gate3 ouverte ; solo, aucune publication.

### Mise à jour 338 — Source031 GPU5 PASS, candidat031 en construction

Handle73010 terminal exit0 : format, strict moteur natif tous targets/features,
moteur719 PASS / 0 FAIL / 15 ignored et strict WASM moteur/Studio PASS.
Image réellement exécutée archivée ; sources et logs revérifiés par la revue.
Handle74893 terminal exit0 : les cinq cas GPU explicitement listés passent,
y compris les buffers optiques préparés par GpuShaderBuffer::prepare_asset.
Deux identités conservées, layout effectif UNIFORM7/18 (720/64 octets),
douze soumissions et relecture exacte des9 408 octets. Préparation sans
précédent produit une nouvelle identité, sans prétendre à une perte de device.
Le pilote attend un échantillon OS/headroom réel avant GPU ; seuils du garde
inchangés. Aucun trip, deux échantillons, candidate_seen=true. Pics mémoire
non qualifiés. Vérification CIM indépendante : image et pilote absents.

Le produit Source030 est byte-identique à Source031 hors tests. Checkpoint
privé Source030 terminé handle39121 exit0, head050540d8165d1253ce4567f0952a57923aafcadf,
parent2b48b1f706b420b9c4be7e990c91bfc19fced816 ; 2 556 pins, quatre deltas
(main native Source029 et trois optiques), clean, aucun remote configuré.
Le main est identique à l'image native Source029 strict/13tests qualifiée.
Construction frontend puis native candidat031 active handle39829, un job,
assets/imports3000/kit11 vérifiés, aucun chargement de scène.

Restent le bind group optique complet, fidélité rendu, perte de device,
fermeture native et stabilité RAM/VRAM scène sur le candidat actuel. L'incident
20 Go RAM/16 Go VRAM observé par l'utilisateur reste sans attribution
exclusive. Gate3 ouverte ; progression solo, Primary inchangé, aucune publication.

### Mise à jour 339 — Recettes de fermeture shell031 préparées ; build actif

Le même handle39829 est revérifié vivant ; frontend candidat031 en compilation,
aucun redémarrage de build. Reviewer source/assets/frontend/native préparé pour
le candidat031, sans exécution ni preuve de parité anticipée.
Recettes du probe de fermeture shell031 écrites et syntaxe PowerShell PASS,
copies et SHA archivés. Deux profils WebView et copies de fixture neufs,
attente du premier échantillon OS/headroom avant lancement, fermeture publique
par le helper gelé et exigence de deux sorties natives0 sans force.
Le vérificateur indépendant couvrira toutes les identités observées dans les
échantillons, plutôt que le seul dernier échantillon. Préparation finale reste
conditionnée au reviewer du candidat réellement construit.

Le futur probe est borné180 s / 2 GiB privé / 1 GiB dédié / 1 GiB partagé /
8 GiB headroom. Aucune action d'ouverture de zone prévue ; les limites du
replay scène ne sont ni augmentées ni qualifiées par ce probe de shell.
Aucun runtime native/shell ni nouveau chargement Tidewater exécuté à cette
étape. Fermeture après gameplay, stabilité RAM/VRAM scène, bind group complet
et fidélité rendu restent ouverts. Gate3 ouverte ; solo, Primary inchangé,
aucune publication.

### Mise à jour 340 — Candidat031 build/parité PASS ; deux fermetures shell PASS

Handle39829 terminal exit0 : frontend et native candidat031 construits.
Reviewer vérifie le checkpoint050540d8165d1253ce4567f0952a57923aafcadf,
2 556 sources, assets/imports3000/kit11, parité native/WASM/worker et empreintes
réelles. Produit optique Source030 et main natif Source029 réunis, sans adoption
Primary ni publication. La parité est une preuve de build, pas de rendu accepté.

Probe shell031 préparé après cette revue, SelfTest helpers PASS.
Préflight live : pas de Studio, commit headroom12.86 GiB. Handle14360 terminal
exit0 : deux profils WebView et copies de fixture neufs, aucun appel d'ouverture
de zone. Deux fermetures publiques par le helper gelé, sorties natives0/0,
Closed=true, Forced=false, sans GracefulCloseFailure. Garde inchangé, premier
échantillon réel requis avant native, 16 échantillons dont10 GPU, aucun trip.
Maxima échantillonnés : privé453 574 656 octets (~432.56 MiB), GPU dédié
97 333 248 (~92.82 MiB), partagé12 288 000 (~11.72 MiB). Ces valeurs ne
qualifient pas les pics de Tidewater.

Vérification CIM indépendante couvre toutes les identités de tous les
échantillons :16 identités observées absentes, candidat absent, fermeture
normale sans force. Revue racine PASS pour deux cycles du shell uniquement.
Le correctif natif n'est pas encore qualifié après gameplay/chargement lourd.
Loading030 FAIL reste conservé, l'observation utilisateur20 Go/16 Go reste
sans attribution exclusive. Stabilité RAM/VRAM de Tidewater, bind group
optique complet, fidélité du rendu, ressources RPG et autres critères Gate3
restent ouverts. Aucun nouveau chargement de Tidewater à cette étape.
Poursuite solo, aucune publication. Gate3 ouverte.

### Mise à jour 341 — Loading031 protégé préparé, non exécuté

Après GPU5 et deux fermetures shell PASS, les recettes Loading031 sont
préparées sur le candidat031 réellement haché/revu. Même parcours public
chargement/Jouer/observation150 s/fermeture, même instrument v3 et helper
gelé, mêmes seuils scène300 s / 6 GiB privé / 4 GiB dédié / 2 GiB partagé /
8 GiB headroom. Ajout de l'attente du premier échantillon OS/headroom avant
native ; aucune augmentation de limites, aucun effet désactivé.
Métadonnées recalculées depuis l'objet immuable : revision42, inventaire
joueur0, vitalité gardien500. Fixture source inchangée. SelfTest import des
helpers PASS ; aucune scène lancée, aucun started-proof/launch-proof.
Reviewer préparé avec les mêmes calculs CPU-ready déjà testés et deux
labels optiques résidents ajoutés ; compteurs cumulés distingués de la résidence.
Le prochain diagnostic devra mesurer la croissance réelle et la fermeture
WebView après la scène ; les passes shell/GPU ne les anticipent pas.
Gate3 ouverte ; pas de runtime actif, solo, Primary inchangé, aucune publication.

### Mise à jour 342 — Loading031 interrompu par marge système ; nettoyage fermé

Préflight live : commit headroom13.14 GiB, pas de Studio détecté.
Métadonnée instrument_version héritée2 corrigée3 avant lancement ; preuve
initiale archivée et générateur corrigé. L'instrument v3 est byte-identique.
Handle47524 terminal exit1 : diagnostic FAILED_CLOSED après environ32 s,
garde MEMORY_PROTECTION_ABORT uniquement system_commit_headroom_floor.
Seuils privé6/dédié4/partagé2 GiB et headroom8 conservés. Douze échantillons,
six observations navigateur ; maximum privé4 146 462 720 octets (~3.86 GiB),
dédié694 464 512 (~0.65 GiB), partagé93 536 256 (~89.20 MiB).
Dernier headroom8 457 482 240 octets (<8 GiB), contre13 886 332 928 au premier
échantillon du garde. La simulation reste prête=false, reconstruction terrain
et premier retour GPU en attente. Un device observé, zéro erreur GPU non
capturée et zéro erreur instrumentation ; buffers optiques pas encore
observés (null, pas zéro). Aucun intervalle CPU-ready ni gain comparatif acquis.

Le garde force la protection : Stop.Closed=true après l'arrêt ne constitue
pas une fermeture publique normale. Le tableau actions_performed du driver
hérité décrit son parcours prévu, pas une preuve de discard après ce trip.
Closure indépendante des huit identités observées sur tous les échantillons,
candidat et pilote absents ; aucune identité restante. Revue échec PASS pour
l'intégrité des traces et le nettoyage, sans qualification de mémoire/rendu.

Pas de relance dans les mêmes conditions. Admission suivante impose une
réserve initiale16 GiB de commit headroom, plancher du garde8 GiB inchangé.
Contrôle d'admission readonly réalisé, insuffisant : replay différé ; détection
Studio couvre désormais les noms avec tiret et underscore. Aucun processus
tiers arrêté. Contrôles sans chargement scène restent possibles, sans réduire
le périmètre de Gate3. Fermeture pleine scène et stabilité RAM/VRAM ouvertes.
Solo, Primary inchangé, aucune publication. Gate3 ouverte.

### Mise à jour 343 — Source032 test du bind group complet préparé, machine active

Revalidation live : commit headroom11.05 GiB, physique libre16.07 GiB,
pas de Studio (noms tiret/underscore couverts). Pas de nouveau replay scène.
Source032 copie les pins Source031 et ajoute uniquement un module de test
et son inclusion :2 558 pins, produit Source030/candidat031 inchangé.
Le nouveau cas GPU prépare les ressources par les chemins Bevy réels,
crée toutes les entrées du bind group optique et relit les uniformes via
le groupe complet. Quatre petites images couvrent les dimensions D2/D2Array,
formats couleur/profondeur et sampler comparison ; textures optionnelles
utilisent le fallback Bevy. Un remplacement d'image sous handle stable,
RetryNextUpdate si absente et reconstruction des ressources sur un second
device natif sont prévus. Pas de qualification de rendu ni d'événement
device-lost anticipée ; buffers auxiliaires de fixture, aucune scène chargée.

Le générateur avait attendu les deux chemins dans un ordre opposé au tri ;
assertion réparée et finalisation des mêmes fichiers, sans modifier le produit.
Machine handle36459 active : format/strict natif tous targets/features/tests
moteur ; trois contrôles neufs, WASM hérité Source031 dont le produit reste
byte-identique hors tests cfg(test). Ne pas qualifier avant logs terminaux.
Runner six cas GPU préparé avec mêmes45 s / 2 GiB privé / 1 GiB dédié /
1 GiB partagé / 8 GiB headroom et premier échantillon requis, non exécuté.
Fermeture pleine scène et mémoire Tidewater restent ouvertes. Solo, aucun
Primary modifié ni publication. Gate3 ouverte.

### Mise à jour 344 — Source032 GPU6 PASS ; Loading031 tentative002 active

Handle36459 terminal exit0 : format, strict natif tous targets/features et
moteur719 PASS / 0 FAIL / 16 ignored. Produit inchangé, WASM hérité Source031,
image réellement exécutée archivée/revue. Handle49119 terminal exit0 : six
cas GPU PASS, dont nouveau bind group optique complet avec images typées
D2/D2Array couleur/profondeur et sampler comparison. Missing image refuse
RetryNextUpdate ; remplacement/resize sous handle stable donne une nouvelle
vue et un nouveau groupe valide. Deux devices natifs, douze soumissions chacun,
relecture exacte18 816 octets ; CPU assets retenus, ressources render recréées.
Cela ne simule pas l'événement device-lost ni le rendu du shader de production.
Garde petits tests inchangé, image et pilote absents selon CIM indépendant.

Préflight live après tests : headroom21.50 GiB. Les conditions externes ont
changé depuis Loading031/001, conservé en échec ; pas de relance aveugle.
Tentative Loading031/002 préparée et SelfTest helpers PASS. Même candidat,
fixture42/inventaire0/gardien500, instrumentv3, flow public et seuils scène.
Admission16 GiB exigée par runner avant driver et par premier échantillon du
garde avant native. Handle26050 actif ; observations initiales sans trip.
Le writer d'admission a ajouté un suffixe littéral backslash-n au JSON :
preuve brute préservée, dérivée normalisée archivée ici. La décision runtime
utilisait le dictionnaire mesuré avant écriture ; aucun seuil changé ni source
de runtime modifiée pendant le test. Corriger le writer après sa terminaison.

Attendre ce même handle, ne pas relancer. Croissance CPU-ready, fermeture
WebView après scène, fidélité rendu et mémoire complète toujours non qualifiées.
Gate3 ouverte ; solo, Primary inchangé, aucune publication.

### Mise à jour 345 — Loading031/002 normal-close PASS ; croissance mémoire persistante

Handle26050 terminal exit0, 71 échantillons/32 observations, aucun trip.
Simulation CPU-ready observée, fermeture publique normale et garde sans force,
candidat/pilote/toutes les identités observées absents selon CIM indépendant.
Reviewer exécuté depuis la racine evidence pour respecter ses chemins relatifs
imbriqués ; copie préparée dans le dossier runtime conservée non exécutée.
Vérificateur toutes-identités corrigé pour dériver normal_scenario_closure des
rapports garde/pilote réels, versions précédentes archivées ; revue normale
et absence des processus confirmées. C'est une preuve de fermeture sur ce
diagnostic unique, pas une validation du parcours P02 complet.

Maxima échantillonnés : privé5 453 910 016 octets (~5.08 GiB), dédié
1 984 122 880 (~1.85 GiB), partagé163 790 848 (~156.20 MiB).
CPU-ready57.086 s/26 échantillons : privé+647 499 776 (~617.50 MiB),
dédié+155 721 728 (~148.51 MiB). Toujours pas de plateau ; stabilité mémoire
NON QUALIFIÉE malgré bounded normal-close PASS. Un device, zéro erreur GPU
non capturée/instrumentation. Buffers optiques frame/atmosphère : chacun une
création finale, delta0 sur la fenêtre55.585 s, comme les autres résidents
présents ; observer atmosphère absent reste null. Pas d'attribution exclusive
ni comparaison causale à un ancien run de cadence/durée différentes.

Formes cumulées :262 144 octets usage136 mapped=true (3372 créations),
12 288 usage136 false (3372),48 usage396 true (3372),122 880 usage136
true/false (3371 chacune), plus uniformes80/64 fréquents. Histogramme limité
à64 formes,4565 créations en overflow : pas d'inventaire exhaustif ni de
résidence. Les callsites doivent être rattachés au code avant nouveau correctif.
Le générateur futur d'admission est corrigé avec chr(10) ; runner mesuré et
preuve brute inchangés, dérivée normalisée conservée dans progress344.

Mémoire/rendu complets, qualification device-lost et exigences RPG/Studio
restent ouverts. Aucun runtime actif, aucune répétition lourde sans nouvelle
raison. Prochain travail : audit des allocations restantes, sans réduire le
périmètre ni désactiver effets. Solo, Primary inchangé, aucune publication.
Gate3 ouverte.

### Mise à jour 346 — callsites Loading031 identifiés ; correctif ciel Source033 préparé

Audit033 rapproche les piles WASM et les sources installées Bevy 0.19.1.
Les cinq formes majeures proviennent de ViewClusterBindings::write_buffers et
upload_view_gpu_clustering_buffers. prepare_clusters_for_gpu_clustering crée
de nouveaux ViewClusterBindings et ViewGpuClusteringBuffers par vue/préparation.
Total des cinq formes :1 754 003 520 octets demandés cumulés ; ni résidence ni
mémoire physique. Histogramme64 formes/4565 créations en overflow. Aucun fork,
aucune attribution exclusive aux20Go RAM/16Go VRAM libérés observés par l'owner.

Petits uniformes :ColorMaterial/FishMaterial/SkyMaterial/FishStandMaterial et
Input::encode des nuages. Piles échantillonnées non exhaustives. Le buffer
atmosphère Input::encode est déjà dans un cache : répétition n'établit pas
l'allocation inconditionnelle ; réinitialisations/instances à mesurer. Les
préparations AsBindGroup peuvent allouer avant RetryNextUpdate ; taux de refus
vs succès non mesuré. Audit avec ancres/lignes/hashes, pas un verdict causal.

Deux chemins Studio solaire/nuages accèdent à SkyMaterial via iter_mut avant
comparaison. Source033 remplace ces accès par compare/get_mut seulement si un
champ consommé ou cloud_view change. Périmètre solaire par transmittance retenu,
valeurs auteur lune/étoiles retenues ; absence de nouveau traced_view conserve
la texture courante. Les vrais changements et alternances de textures restent
publiés. Aucun shader ni dépendance modifié, aucun effet supprimé.

Copie isolée2559pins/4deltas vsSource032 :sky.rs,nouveau sky/updates.rs,
authoring_imported_terrain.rs et solar.rs. Format préparatoire PASS ; un test
de régression préparé (100 mises à jour idle, vrais changements/générations),
pas encore compilé ni exécuté. Recette native engine/WASM engine+Studio prête,
non lancée. Fix natif029 et résidents optiques030 retenus byte-identiques.
Primary inchangé, aucune adoption/publication, aucun replay lourd.

Source033 ne prouve pas le plateau mémoire et ne résout pas les allocations
Bevy. Suite : validations pertinentes puis poursuite de l'attribution et replay
borné uniquement sous admission mémoire. Tous les critères RPG/Studio et CI
restent requis. Solo, Gate3 ouverte — non qualifiée.

### Mise à jour 347 — garde CPU vérifiée ; trace nuages exclue des créations répétées

Garde033 séparée pour builds/tests CPU, dérivée de la garde006 : acquisition
des handles/processus descendants, seuil privé6GiB et headroom8GiB conservés.
Admission initiale16GiB et attente du premier échantillon puis de l'identité
du Python surveillé avant travail. GPU reste null/non qualifié dans ce mode,
sans refus artificiel des compteurs après30s. La garde des scènes est inchangée.
SelfTest sur incident mesuré PASS. Fixture32MiB/35s, handle75850 terminal0 :
17 échantillons,33.235s observées avec enfant, pic privé41 431 040 octets,
driver0/garde0, aucun trip ni force, fermeture normale.

Contrôle indépendant initial PID-only refusé :3216 avait été réutilisé par un
rustc externe (06:20), différent du Python surveillé (06:16). Preuve initiale
archivée. Vérificateur réparé avec dates de création des enfants et identité
explicite du pilote ; toutes identités surveillées absentes. Aucun processus
externe modifié. Le PASS garde est CPU seul, pas une preuve runtime/Gate.

Audit labels033/002 sur les mêmes32 observations Loading031 :paired cloud
trace présent28 fois, toujours created=1/64octets. Sa pile partage la forme64
avec d'autres matériaux ; ce producteur n'explique pas les créations répétées.
La piste cache trace du premier audit est donc écartée sur ce run, rapport
initial conservé. Instrumentation WeakMap/WeakSet et piles bornées vérifiées ;
absence d'influence totale non prouvée. ExplicitDestroyed ne compte pas la
collecte JS ni la libération différée : zéro destroy n'est pas résidence prouvée.
Champ booléen ambigu de la preuve brute clarifié dans root-proof dérivée,
preuve brute inchangée. Attribution20Go/16Go toujours non prouvée.

Préflight machine033 consigné :CPU_MACHINE_DEFERRED_BUSY_OR_HEADROOM,
compilations externes actives. Aucun driver de compilation ni scene lancé,
Source033 reste formaté/préparé, compilation et tests non exécutés.
Suite : relire l'admission vivante, exécuter les cinq contrôles sous garde
CPU lorsque la machine le permet, puis poursuivre l'attribution/rendu et
la boucle RPG complète. Solo, Primary inchangé, aucune publication ; Gate3 ouverte.

### Mise à jour 348 — Source033720/strict natif/WASM PASS sous garde CPU

Première tentative handle37038 terminal1 :Clippy refuse la comparaison directe
f32 stars_time_night.z. Pilote101/garde0,19 échantillons,pic privé2 391 126 016,
aucun trip ; source/logs initiaux conservés. Nouvelle copie033/002 :comparison
to_bits pour publier tout changement exact du paramètre GPU, sans tolérance
qui pourrait masquer un changement. Une seule correction vs033/001.

Garde CPU033/002 byte-identique à la garde033/001 testée ; preuve smoke parent
réutilisée. Admission00216GiB initial, cache distinct,un job ; travaux externes
préservés, floor8GiB et maximum privé6GiB inchangés. Politique initiale « aucun
compilateur externe » assouplie pour ce seul job CPU protégé, sans assouplir
la garde mémoire ni la règle d'admission des scènes. Pas de scène/GPU explicite.

Handle21569 terminal0 :format/strict engine all-targets all-features/tests
engine/strict WASM engine/strict WASM Studio tous exécutés PASS.
720 passés/0 échec/16 ignorés ; test sky idle100 mises à jour et changements
solaires/textures/générations PASS. Image native réellement mesurée archivée,
SHA a9ed44e5b0f59913bb9b04764dab0811bcf094c6f7c67076deea1a9a6e949190.
Source2559pins stable,4deltas vs032. Fix natif029, optiques résidentes030,
shaders et dépendances retenus. Reviewer racine exécuté PASS.

Garde0/pilote0,229 échantillons,523.282s avec enfant observé, pic privé
4 734 849 024 octets (~4.41GiB), aucun trip/force, GPU non mesuré en mode
CPU. Vérification CIM indépendante :toutes les identités observées absentes,
dates de création utilisées ; chemin image du pilote normalisé avant comparaison.
Ces maxima concernent les contrôles, pas Tidewater en jeu.

Audit source wgpu29.0.4 verrouillé :WebBuffer::drop ne fait pas d'appel explicite
destroy ; destroy distinct appelle inner.destroy. Les champs et références JS
se libèrent néanmoins normalement. Zéro explicitDestroyed dans l'observateur
ne prouve pas une fuite ou résidence ; timing GC/retrait pilote non mesuré.
Créations Bevy répétées restent piste de pression mémoire, sans cause exclusive.

Source033 compilé/testé, pas encore adopté dans Primary ni reconstruit en
candidat Studio. Candidate031 reste l'ancienne paire Source030. Aucun replay
Tidewater relancé, stabilité mémoire toujours NON QUALIFIÉE. Suite :préparer
une paire Studio reproductible, poursuivre l'attribution/réutilisation des
allocations restantes et mesurer sous garde, puis satisfaire toute la boucle
RPG/Studio, les retours édition/fermetures, CI exacte et revue humaine requise.
Solo, aucune publication, Gate3 ouverte.

### Mise à jour 349 — checkpoint privé033 ; candidate032 build protégé actif

Source033/002 checkpoint propre2559 fichiers,head
9331c44de4d7f22d0ea92b6bf3a9babc55e83da7,parent030
050540d8165d1253ce4567f0952a57923aafcadf. Sept deltas vs030 :quatre fichiers
de correction ciel et trois fichiers de tests optiques031/032. Aucun remote
public, aucune publication/adoption Primary ; pins machine033 byte-identiques.

Builders appariés frontend/natif préparés avec2559pins, checkpoint exact,
3000 imports et11 éléments de kit immuables hérités. Parse PowerShell PASS,
admission CPU16GiB PASS. Garde CPU byte-identique à celle testée, privé6GiB,
headroom8GiB,total1200s ; scènes et garde GPU inchangées. Le pilote attend les
échantillons gardés avant la charge. Pas d'exécution Tidewater/GPU explicite.

Handle42145 réellement actif, identité pilote revalidée CIM. Étape frontend
en cours ; native doit suivre. Dernier contrôle mémoire partagé dans le turn :
privé1 408 032 768 (~1.31GiB),headroom18 922 737 664,sans trip ; observation
ponctuelle,pas un maximum final. Les outputs032 frontend/candidate sont de
nouveaux dossiers,031 reste conservé. Aucun succès de build/parité/runtime
annoncé avant terminaison et revue. Prochaine étape :reprendre le même handle,
vérifier la fermeture de toutes les identités, source/assets/binaires appariés,
puis préparer un diagnostic mémoire borné avec motif nouveau. Allocation Bevy,
stabilité mémoire et toute la boucle RPG/Studio restent ouvertes. Solo,Gate3 ouverte.

### Mise à jour 350 — build032/001 protection privée ; essai CGU16 sous même garde

Handle42145 terminal1. Garde MEMORY_PROTECTION_ABORT, motif unique
owned_private_bytes_limit ; pilote4294967295/garde1,48 échantillons,
104.879s avec enfant, pic privé6 455 062 528 octets (~6.01GiB).
Arrêt de protection pendant la compilation frontend forge_engine, pas une
erreur de validation métier. Aucun natif ni scène lancé. Les outputs partiels,
logs et RUNNING intentionnel du runner interrompu restent conservés ; l'état
terminal autoritaire vient de la garde. Toutes les identités observées absentes
selon CIM indépendant. Ce n'est pas une fermeture normale. Pins2559 et
checkpoint9331c44 propre revalidés, Primary et processus externes préservés.

Profil release source :codegen-units=1,opt3,LTO thin. Seconde préparation002
applique uniquement au build frontend forge_engine le CLI Cargo
--config profile.release.package.forge_engine.codegen-units=16.
Opt3 et thin LTO retenus, native debug/worker inchangés ; code/shaders/assets
et profil Cargo source inchangés. L'override est hashé dans la preuve ; il doit
être conservé pour toute reproduction et sa performance reste à qualifier.
Références primaires :Cargo Book profiles#codegen-units et config#profile.
Pas de promesse de performance équivalente ni amélioration mémoire avant mesure.

Parse/admission002 PASS, garde privée6GiB/headroom8GiB/initial16GiB inchangée,
byte-identique à celle testée. Handle31994 actif, pilote revalidé CIM. Ligne du
rustc descendant effectivement capturée :forge_engine WebAssembly CGU16 opt3.
Frontend002 en cours ; nouveau candidate032/002 doit suivre. Dernière mesure
communiquée :privé4 325 281 792 (~4.03GiB),headroom15 827 644 416,
sans trip ; pas un maximum terminal. Aucun replay Tidewater.

Suite :suivre le même handle, classifier son résultat et vérifier fermeture,
configuration réellement appliquée, parité source/assets/binaires avant usage.
Le candidat expérimental ne remplace pas la qualification performance ni la CI
du candidat final. Allocation Bevy/résidence mémoire et toute la boucle RPG/Studio
restent requis. Solo, aucune adoption/publication, Gate3 ouverte.


### Mise à jour 351 — incident RAM/VRAM ; charges propres arrêtées

Le propriétaire signale environ 20 Go de RAM et 16 Go de VRAM libérés après
arrêt du test. Observation humaine conservée : unités précises, processus
concernés et mesure avant/après ne sont pas établis par nos journaux.
Le replay031/002 archivé mesurait au pic 5 453 910 016 octets privés et
1 984 122 880 octets GPU dédiés. Après CPU-ready, +647 499 776 octets privés
et +155 721 728 octets GPU dédiés en57.086s : aucune preuve de plateau.
Son périmètre borné ne suffit pas à attribuer les quantités libérées signalées.
Les créations cumulées de buffers ne prouvent pas leur résidence ni une fuite.

Seconde construction032/002 terminale : garde1, pilote4294967295,
262 échantillons/594.047s, pic privé6 485 458 944 octets, arrêt de protection.
Troisième construction032/003 utilisait CGU16 pour engine et Studio, opt3.
Arrêt explicite de notre arbre après signalement de l'incident : pilote4294967295,
garde0,88 échantillons/196.714s,pic privé4 064 387 072 octets. Aucun natif
ni scène lancé. Ce n'est ni un succès de build ni une fermeture normale.
CIM indépendant :six identités PID+date observées toutes absentes.
Le zéro de forced_protection_stop signifie que la garde n'a pas déclenché
l'arrêt ; user-resource-incident-stop.json archive l'arrêt externe réel.
Les handles31994 et67864 ne sont plus actifs. Logs/outputs partiels conservés.

Correction de portée de la mise à jour350 :LTO thin est déclaré dans le
profil source. La commande Studio de la seconde tentative n'affiche aucun
drapeau LTO explicite. Le LTO effectif n'est pas qualifié ; ne pas transformer
la déclaration du manifest en preuve de configuration réellement appliquée.

Preuve current-campaign-progress-351/resource-incident-proof.json avec hashes.
Aucun nouveau replay graphique avant attribution de l'incident et revue du
périmètre de surveillance. Diagnostic statique autorisé poursuivi sans charge
graphique. Source033 reste qualifiée pour ses contrôles CPU, mais candidat032,
stabilité mémoire, boucle RPG/Studio complète, CI et acceptation restent ouverts.
Gate3 OUVERTE — NON QUALIFIÉE. Travail solo, aucune publication.


### Mise à jour 352 — incident initial retrouvé ; hausse RAM répartie par processus

Revue indépendante des captures brutes de l'incident021 initial, correspondant
au signalement archivé20Go RAM/16Go VRAM. Tous les hashes evidence_pins vérifiés.
Chaîne renderer37472 → browser6212 → candidat15900 confirmée. Renderer privé
27 050 508 288 octets (25.19GiB), RAM physique disponible récupérée
28 621 205 504 octets (26.66GiB), calculée sur os-before/os-after-stop.
La consommation massive RAM possède donc une preuve machine historique ;
la seule comparaison031 décrite351 était insuffisante pour l'attribution.
Ne pas présenter cet incident021 comme une nouvelle exécution033 du6octobre.
VRAM avant arrêt non archivée :les16Go libérés restent observation propriétaire.

Replay031/002 :les six rôles WebView2 ont chacun des échantillons avec identité
PID+date, y compris gpu-process (69) et renderer (69). Inclusion RAM confirmée.
Les compteurs GPU bruts par PID/adaptateur ne sont pas archivés, seulement
l'agrégat ; leur exhaustivité individuelle n'est donc pas reconstruisible.

Partition indépendante de la même fenêtre CPU-ready tardive57.086s/26samples :
Studio natif +455 860 224 octets (~434.74MiB), processus GPU mémoire privée
+155 467 776 (~148.27MiB), renderer +36 909 056 (~35.20MiB), autres rôles
faibles deltas négatifs. Identités constantes aux bornes, somme exacte
+647 499 776 octets. Mémoire privée du processus GPU distincte de VRAM dédiée.
La croissance principale RAM de031 exige d'auditer aussi Studio natif et ses
copies/caches/IPC, sans attribuer toute la hausse aux allocations WebGPU.
021 et031 sont des candidats distincts ; aucune preuve de cause commune exclusive.

Preuve memory-process-scope-root-audit-352/proof.json ; scripts de revue
exécutés sur archives sans charge graphique. Prochaine investigation :chemins
Studio natifs actifs durant chargement/suivi et durées de vie des allocations.
Aucun replay ni adoption/publication. Gate3 ouverte et mémoire non qualifiée.


### Mise à jour 353 — audit statique des allocations natives au chargement

Huit chemins source030 réellement construits pour031 comparés aux pins puis
àSource033 :byte-identiques. Audit native-loading-static-audit-353/proof.json.
read_terrain_source reconstruit/clone ActiveSession.document puis calcule son
hash àchaque chunk256KiB avant le contrôle du cache raster. Frontend attend
chaque réponse IPC. Piste de travail/allocations répétées réelle, mais volume
ni durée de vie mesurés ; aucune attribution exclusive aux435MiB natifs.
Correction éventuelle devra conserver autorité du document effectif, refus
des données périmées et retouches. Caches terrain/végétation àune entrée,
ce qui ne borne pas leurs octets ni les allocations temporaires.

Sources Tauri2.11.5/tauri-utils2.9.3 locales copiées/hashées :branche
EmbeddedAssets compression alloue/décompresse un Vec temporaire par get.
Feature effective àétablir depuis build exact. Aucun cache décodé persistant
dans cette fonction ; response_cache proxy est cfg(dev,mobile), pas preuve
d'un cache Windows. Durée de vie des réponses et heap vivant non mesurés.
3029 fichiers web /497 101 915 octets inventoriés ; taille fichier différente
de résidence RAM/VRAM. Aucun changement source produit, replay ni publication.
Gate3 et stabilité mémoire restent non qualifiées.


### Mise à jour 354 — correction isolée de l'identité de lecture raster034

Source034 dérivée des2559 pins033 vérifiés, quatre fichiers modifiés :host,
lecture raster, entrée workspace, régression transport. Ajout d'une seule
identité dérivée (hash document/blob, taille, autorité) réutilisée entre chunks.
Toute entrée execute authoring/workspace l'invalide avant traitement ; le
prochain chunk revalide le document effectif. Cache des octets inchangé, aucune
seconde représentation canonique, aucun critère ni contenu Tidewater réduit.

Tentative001 :fmtPASS,Clippy101 items_after_statements dans le nouveau test.
Pilote1/garde0, neuf samples/16.358s,pic privé640 593 920 octets ; toutes les
15 identités observées fermées par vérification indépendante. Logs conservés.
Tentative002 :import corrigé dans nouvelle copie,fmt et Clippy strict project
all-targets/all-features PASS. Contrôle tests forge_project encore actif handle85288,
source002 gelée, pilote et enfants revalidés CIM. Pas de verdict tests anticipé.

Relecture du nouveau test :la retouche d'une zone gérée exige Preview avant
Apply. Copie003 préparée avec ce parcours, sans modifier la source002 en cours.
Elle n'est pas exécutée. Même garde CPU privée6GiB/headroom8GiB/initial16GiB,
durée1200s ; compteurs GPU inconnus explicitement null, pas GPU zéro.

La régression exige refus de l'ancien hash après renommage avec blob inchangé,
acceptation du nouveau hash et octets exacts, plus invalidation workspace.
Contrôles machine finaux, WASM, candidatures et effet réel mémoire restent à
qualifier. Aucune adoption Primary/push/scène graphique. Gate3 ouverte.


### Mise à jour 355 — CPU interrompu/refusé ; couverture partielle GPU renforcée

Handle85288 terminal1 :arrêt externe de notre arbre pendant compilation avant
exécution des tests, pour corriger le parcours Preview/Apply connu invalide du
nouveau test. Aucun échec de comportement produit ni testsPASS revendiqué.
Clippy002 PASS conservé sur son scope exact. Pilote4294967295/garde0,
125samples/282.638s,pic privé933 289 984 octets. Toutes70 identités observées
fermées CIM. external-cancellation-proof.json précise le motif ; sorties préservées.

Source003 test corrigé gelé. Nouveau contrôle avec les deux transports ciblés,
suite complète project toujours requise, refusé par admission initiale :marge
14 459 564 032 octets (~13.47GiB) <16GiB. Pilote1/garde0,un sample,aucun
enfant candidat, aucun compilateur/test lancé. Pilote fermé indépendamment.
Nouvelle capture système :marge14 272 266 240 octets,RAM physique disponible
28 186 464 256 octets. Ces métriques diffèrent ; ce n'est pas un manque de
28GiB RAM libre. Travaux externes/apps propriétaires préservés, seuils inchangés.
Avant nouvelle tentative CPU, revalider marge initiale et utiliser une nouvelle
préparation d'exécution ; ne pas relancer les runners déjà terminés.

Garde GPU006 :une ligne de compteur correspondant àun PID owned quelconque
pouvait masquer l'absence de compteur du WebView gpu-process. Risque statique
de couverture partielle établi, pas preuve qu'il a eu lieu sur031 ou causé021.
Nouvelle Watch-Gate3OwnedMemory-007.ps1 conserve seuils/identités/nettoyage,
archive lignesGPU brutes et owned parPID/adaptateur, PIDGPU attendus/reçus/manquants,
et déclenche après grâce sur gpu-process attendu sans compteur même si un
renderer possède déjà un compteur. Parse et ancien incident ownership PASS,
cinq fixtures synthétiques PASS (complet, étranger exclu,partiel,absent,
GPUattendu étranger refusé). Preuve gpu-counter-coverage-guard-007/test-proof.json.
Ce n'est pas une mesureGPU ni une qualification d'exécution complète de la garde.

Aucune charge de cette conversation en cours. ValidationCPU finale034,
WASM,candidat final,replay protégé,stabilité,CI,boucleRPG et Studio restent ouverts.
Pas de rendu graphique/adoption/push. Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 356 — compression effective du candidat031 vérifiée

Pas de nouvelle tentativeCPU :marge système actuelle13 026 152 448 octets
(~12.13GiB), en dessous16GiB. Aucun handle actif de cette conversation.

Audit candidate031-tauri-feature-audit-356/proof.json exécuté en lecture seule.
SHA du binaire deps/forge_studio.exe identique au candidat0312693ecd1.
App dep-info unique contenant Source030 ET le web candidate031 ; métadonnées
et dep-info copiées avant remplacement éventuel du cache. Dépendance u64
1882568400804223171 correspond aux octets fingerprint Tauri bf89b0ba4c011a02
en little-endian ; dépendance tauri_utils chaînée de la même façon, correspondance
unique dans chaque cas. Features effectives compression/custom-protocol côté
Tauri, compression/brotli côté tauri_utils. Cette association utilise les
métadonnées Cargo et dep-info en plus des octets exécutables, pas seulement
les defaults déclarés du manifest ou une proximité de dates.

La branche de décompression EmbeddedAssets étudiée353 est donc pertinente
pour031 :Vec décodé temporaire par get puis corps de réponse desktop. Nombre
de requêtes, durées de vie du heap et contribution aux435MiB natifs non mesurés.
Compression activée ne signifie pas fuite ni cause exclusive ; aucune option
de build ni scène n'est simplifiée pour obtenir un résultat favorable.

La correction034 attend toujours ses tests corrigés puis la suite complète,
WASM et candidat apparié. Mémoire, bouclesRPG/Studio, CI et acceptation restent
ouverts. Pas de rendu graphique, adoption ni publication. Gate3 non qualifiée.


### Mise à jour 357 — textures converties ; créations tardives stables

Marge actuelle commit10 876 571 648 octets (~10.13GiB), <16GiB initial :pas
de tentative de lancementCPU. Inventaire statique limité en mémoire, sans
décodage de pixels ni rendu. 3000 fichiers import/337 497 930 octets hashés.
Pas de PNG/JPEG/WEBP autonome trouvé par signature ; deuxGLB contiennent un
périmètre images embarquées non inspecté. Ne pas conclure absence globale
d'images encodées àpartir de ces signatures seules.

Huit textures terrain/plantes RGBA8 connues :88 429 872 octets (~84.33MiB),
dimensions/mips/formule et pins package vérifiés. Modules source030/034
byte-identiques. Copie terrain linear_detail explicite :1 398 100 octets de
payload supplémentaires (~1.33MiB). Format déjà brut, pas expansion PNG/JPEG
sur ces huit fichiers. Nombre de copies vivantes, autres textures brutes,
GLB, cibles générées/rendu, géométrie et surcoût pilote restent hors compte.
Octets payload ne sont pas résidenceGPU, seuil d'acceptation ni budget global.

Revue du même observateur031 sur fenêtre tardiveCPU-ready12obs/55.585s :
145 appels createTexture cumulés,27 labels, aucun nouvel appel dans la
fenêtre ; trois tailles de création inconnues. Ce compteur ne mesure ni
résidence ni GC ni mémoire transitoire dans la fenêtre. La hausseGPU mesurée
et les11 219 nouvelles créations de buffers ne sont pas expliquées par une
recréation de textures observée dans cette fenêtre. Orienter suite vers
buffers/queues/staging/rétention sans cause exclusive annoncée ni extrapoler021.
Preuve candidate031-texture-payload-audit-357/root-proof.json et archives hashées.

Inventaire agents live :root seul, aucun sous-agent actif. Pas de charge
graphique/compilation active dans cette conversation ni arrêt de tâches externes.
Tests034,candidat final,replay borné puis toute Gate3 restent àqualifier.
Gate3 ouverte ; aucun changement produit adopté ni publication.


### Mise à jour 358 — consommation anormale ; sécurité du moniteur

Le propriétaire confirme environ20Go RAM et16Go VRAM libérés. Ces chiffres
restent une observation utilisateur, sans mesure instrumentée VRAM avant arrêt.
La surconsommation reste un blocage de qualification ; aucune cause exclusive
ni correction de résidence GPU démontrée. Aucun lancement graphique ici.

CPU034004 :fmt/Clippy PASS mais transport en échec avec artefact du cache
partagé attribué insuffisamment au source. CPU034005 utilise un nouveau target
privé exclusif :fmt et Clippy strict PASS ; tests transports terrain/végétation
2PASS/0FAIL, sources2559pins inchangés. Suite complète et WASM restent ouverts.
Ces résultats bruts ne qualifient pas la protection mémoire ni toute Gate3.

Le moniteur005 incluait AMDInstallManager PID3196 créé le4octobre, sous un
parentPID réutilisé le6octobre. Ancien algorithme fondé sur PIDparent sans
chronologie :59échantillons faux. Arrêt individuel vérifié du seul moniteur,
avant son nettoyage. Revue automatique avait refusé un arrêt groupé ; cet
arrêt groupé n'a pas été exécuté. La charge CPU a achevé ses contrôles.
Capture indépendante :aucune identité de notre charge encore active ; AMD
étranger préservé. Run classé MONITOR_OWNERSHIP_SAFETY_ABORT, pas gardePASS.

Nouvelles gardes locales008graphique/006CPU :parent antérieur à enfant,
ancrage PID+création du seed retenu, contrôle image/création/commande avant
arrêt individuel via handle ; suppression Kill(true). Parsing, six cas
synthétiques par garde et rejeu des59échantillons fautifs PASS. Exécution
réelle des nouvelles gardes non qualifiée :ne pas relancer de scène avant
ce contrôle. Sources originales et échantillons conservés.
Preuve owned-process-guard-repair-008/root-proof.json.

Prochaine étape :qualifier les protections réparées avec une petite charge,
puis poursuivre le diagnostic buffers/queues/staging et le candidat apparié.
La map Tidewater et le périmètre RPG/Studio restent requis. Gate3 ouverte,
aucun changement produit adopté et aucune publication.


### Mise à jour 359 — garde CPU validée ; appelants buffers ciblés

Garde CPU007 issue de réparation009 :réutilisation d'un PID retenu traité,
identité driver initiale conservée pour nettoyage, reçus des arrêts individuels.
L'archive incident021 n'a pas CreationDate :ancien selftest inadapté a échoué,
puis test corrigé refuse cette archive comme preuve d'ownership et conserve
seulement le test des seuils sur IDs historiques connus. Gardes originales
préservées. Parsing, six cas synthétiques et59échantillons faux PASS.

Deux charges réelles CPU isolées, aucun moteur/compilation/GPU :normal32MiB
pendant35s,17échantillons, driver0/monitor0, fermeture normale ; charge parent
32MiB et enfant16MiB, durée garde14s,8échantillons, arrêt protecteur attendu.
Deux arrêts individuels vérifiés image/commande/création via handle, aucun
Kill(true). Capture indépendante :aucun processus des tests encore actif,
AMD3196 étranger toujours présent. Protection CPU sur petite charge qualifiée,
pas essai réel de dépassement de1GiB ni validation graphique/compteursGPU.
Preuve owned-process-guard-validation-009/root-proof.json.

Revue du même chargement031, fenêtre tardive55.585s/12observations :
11219 créations buffers,898086128octets demandés (~856.48MiB), zéro destroy
explicite observé. Ce dernier compteur ne mesure pas GC ni résidenceGPU.
9493 créations sans label/898003280octets ; métadonnées clustering1726fois,
82848octets. Sept shapes connues couvrent10356créations/898058512octets ;
863créations hors table plafonnée/27616octets par différence comptable.
Les cinq grandes shapes sont créées1726fois chacune ; leurs piles échantillonnées
dans la fenêtre pointent ViewClusterBindings::write_buffers et
upload_view_gpu_clustering_buffers. Source locale bevy_pbr0.19.1 archivée/hashée :
préparation recrée ViewClusterBindings et ViewGpuClusteringBuffers par vue.
Cette attribution des demandes n'établit ni durée de vie ni fuite, et ne prouve
pas la cause exclusive de la croissance RAM native/GPU ou de l'incident021.
Aucun fork Bevy ni changement des effets/scène adopté.
Preuve candidate031-buffer-window-audit-359/root-proof.json.

Suite complète projet034 et WASM restent requis, puis candidat apparié et
replay mémoire borné après validation de la garde graphique. Boucle complète
RPG/Studio Tidewater, CI exacte, distribution et acceptation demeurent ouvertes.
Root seul ; aucun processus de test actif, aucune publication, Gate3 ouverte.


### Mise à jour 360 — suite complète forge_project034 native PASS

Source034003 inchangé,2559pins vérifiés avant et après. Cargo test
-p forge_project --all-targets --all-features --locked --offline -j1,
cache privé du même source que le contrôle ciblé005, mêmes Rustflags debuginfo0.
Résultat223PASS/0FAIL/1ignored,48groupes ; le test ignoré est le replay local
opt-in via FORGE_DELTA_SOURCE, qui n'a pas été exécuté dans cette suite.
Ce verdict couvre tous les targets forge_project, pas les doctests, tout le
workspace, un build WASM, ni le replay Studio graphique.

Garde CPU réparée007 :driver0/monitor0,153échantillons, aucune limite atteinte,
aucun arrêt forcé ; source stable. Capture indépendante des identités observées
confirme fermeture de notre charge et AMD étranger préservé. Les limites restent
1.5GiB privés/8GiB réserve commit/1200s, admission10GiB ; celles du rendu graphique
n'ont pas changé. Preuve terrain-identity-full-project-cpu-034-006/root-proof.json
avec pics, durée, hashes des sources, logs et reçus. Aucune scène lancée.

Correction du cache d'identité terrain034 :fmt, Clippy strict, transports ciblés
et suite native complète du projet désormais qualifiés localement. Contrôle
WASM de ce même source, candidat apparié, mémoire graphique et intégration
produit restent à faire. La cible wasm32-unknown-unknown est installée,
ce constat n'est pas un buildPASS. Toute Gate3 reste ouverte ; root seul,
aucun processus de test actif, aucune adoption produit ni publication.


### Mise à jour 361 — WASM refusé avant charge ; intégration préparée

Tentative WASM034007 terminale :driver1/monitor0, un échantillon,
9352552448octets de marge (~8.71GiB), <10GiB requis à l'admission CPU.
Aucun Python candidat, compilateur, cargo check ni scène lancé ; logs WASM
absents et driver fermé par capture indépendante. Ce n'est pas un échec
du code ni un checkPASS. Source gelé et seuils conservés, preuve
terrain-identity-wasm-cpu-034-007/root-proof.json. Nouvelle tentative exige
une nouvelle sortie et une admission effective, pas réutilisation de ce run.

Garde graphique009 :sept régressions synthétiques parsing/couvertureGPU
multi-adapter, exclusion étrangère, renderer ne masquant pas GPU-process
manquant, absence des compteurs, identité étrangère attendue refusée et seuils
numériques PASS. Aucun compteurGPU réel ni exécution graphique qualifiée.
Preuve gpu-coverage-validation-361/proof.json. Le compilateur observé40688,
créé08:07:17 locale, travaille tidewater_plant_probe dans .build/cargo-target,
hors cache et arbre de notre charge. Sa consommation privée observée8.68GiB
contribue à la pression actuelle ; aucune attribution exclusive, aucun arrêt.

Audit d'intégration034 sans mutation produit :exactement quatre fichiers
changés depuis Source0339331c44d ; les quatre Primary correspondent byte pour
byte à cette base. Copies base/candidat/Primary et patches archivés.
Comparaison151fichiers projet/core/gameplay + manifests :sept différences,
les quatre corrections, Cargo.toml/Cargo.lock et test gameplay first_loop.
La preuve native du source034 ne qualifie donc pas automatiquement Primary.
Autres changements propriétaire et build actif préservés ; pas de transfert
de patch pendant cette observation. Root Primary vérifié en lecture seule à
6b001dbed0fdf1c002d43cf507c60eece1119130 ; métadonnées Git nécessitent le contexte
non restreint, le checkout existe bien. Preuve terrain034-primary-integration-audit-361/root-proof.json.

Le dossier propriétaire ADR0021 courant est Rapier, déjà présenté et en attente
de réponse ; il ne concerne pas Bevy. Aucun nouveau fork ni adoption effectué.
Suite :WASM du même source à marge suffisante, garde graphique réelle, candidat
apparié et replays mémoire/RPG/Studio, intégration/requalification, CI exacte,
distribution et acceptation complète. Gate3 demeure ouverte et non qualifiée.
Root seul, pas de charge de cette conversation encore active, aucune publication.


### Mise à jour 362 — WASM projet034 PASS ; garde GPU réelle bornée

Marge revenue à18409193472octets (~17.15GiB) ; nouvel essai WASM034008,
sortie fraîche avec même source2559pins et mêmes limites CPU. Cargo check
-p forge_project --lib --all-features --target wasm32-unknown-unknown
--locked --offline -j1 PASS ; source stable, driver0/monitor0,31échantillons,
aucun arrêt forcé. Revue indépendante des identités fermées. Ce contrôle
ne compile pas tout le frontend et n'exécute pas les tests dans un runtime WASM.
Preuve terrain-identity-wasm-cpu-034-008/root-proof.json. Source034 dispose
maintenant de fmt/Clippy,223tests natifs projet et checkWASM bibliothèque.

Essai réel de garde graphique009 sans fenêtre ni scène :D3D11 matériel,
un buffer16MiB seulement. ABI vérifiée dans les headers Windows SDK installés.
Normal35s,17échantillons, compteurs GPU de notre PID effectivement présents,
fermeture normale et Release COM explicite. Première tentative d'arrêt borné
refusée avant allocation pour marge14858985472octets <16GiB. Sortie retry001
neuve après marge18947825664octets (~17.65GiB) :buffer16MiB réellement créé,
arrêt protecteur durée14s attendu,7échantillons, un arrêt individuel vérifié
image/commande/création via handle. Driver4294967295/monitor1 sont attendus
pour cet essai forcé, sans erreur de garde. Pas de test réel de dépassement
VRAM4GiB/RAM6GiB. Les seuils numériques de production n'ont pas changé.

Capture indépendante :aucune identité de nos charges encore active, aucun
compteur résiduel pour leurs deux PID GPU ; AMD étranger préservé. PID33676
réutilisé par un travail ImportWork F: reste intact, sa création/commande
diffèrent de notre ancien run. Garde qualifiée sur petite charge GPU D3D11,
pas encore sur l'arbre WebView/Tidewater ou pour la mémoire du produit.
Preuve small-gpu-guard-validation-362/root-proof.json et reçus retry001.

Inventaire DXGI lecture seule :RX7800XT LUID0:116FB,16963133440octets de
capacité dédiée annoncée ; iGPU AMD et adaptateur logiciel distincts. Snapshot
global Win32 GPUAdapterMemory sur RX :3925606400dédiés/4157874176partagés.
Ne pas déduire une saturation physique actuelle de la somme des processus :
ressources partagées entre processus peuvent y être comptées plusieurs fois,
comme documenté par Microsoft (https://devblogs.microsoft.com/directx/gpus-in-the-task-manager/).
Capacité moins compteur n'est pas un budget WDDM du processus ni garantie
d'allocation. Ces chiffres actuels ne reconstruisent pas les16Go libérés de
l'incident021. Preuve gpu-adapter-scope-audit-362/root-proof.json.

Suite :checkpoint du source qualifié et candidat natif/WASM apparié,
replay Tidewater avec garde réparée/couverture WebView et compteurs adaptateur,
diagnostic mémoire, toute boucle RPG/Studio, intégration/requalification,
CI exacte, distribution et acceptation complète. Root seul ; aucun processus
de cette conversation encore actif, aucune adoption produit ni publication.
Gate3 ouverte et non qualifiée.


### Mise à jour 363 — checkpoint034 ; build apparié réellement en cours

Checkpoint privé Source034 bff49996d74b54504657332eff0a834340ad1026, parent
9331c44de4d7f22d0ea92b6bf3a9babc55e83da7,2559inputs exacts et propres,
exactement quatre deltas du cache d'identité terrain/test. Fetch local du
parent, aucun remote public, aucun transfert au checkout principal ni push.
Preuve terrain-identity-checkpoint-034-001/checkpoint-proof.json.

Build frontend puis natif du même checkpoint, sorties034001 fraîches, imports
3000 et kit11 hashés conservés. Nouveau cache privé vide à la préparation,
.gate3-candidate034-private-target-001, NTFS compressé pour les nouveaux fichiers.
Il n'emprunte aucun artefact de l'ancien cache partagé, ce qui évite l'ambiguïté
de provenance rencontrée sur les transports034. Disque disponible55.71GiB
initial :admission disque40GiB, garde de réserve16GiB ajoutée et frontières
testées. Aucun cache ancien nettoyé, aucun fichier de preuve supprimé.

Garde CPU réparée007 étendue d'une lecture DriveInfo et d'un seuil disque :
plafond6GiB privés et réserve système8GiB inchangés, admission16GiB.
Durée totale bornée3600s pour le build frais, distincte des1200s précédents.
Parsing des quatre scripts et trois frontières disque PASS. Frontend garde
les overrides codegen16 engine/Studio et opt3 du précédent essai ; fichiers
profil/shaders inchangés. LTO déclaré thin n'est pas sa mesure effective,
performance/équivalence restent non qualifiées. Native debug et worker sans
ce nouvel override frontend. Sourceengine byte-identique au source033 qualifié,
preuve project034 native223 et checkWASM propres, sans transfert de verdict
à l'ensemble du candidat tant que les deux builds ne sont pas revus.

Session41220 réellement active, pilote13120 créé08:53:03.630998locale,
image/commande vérifiées indépendamment. Python candidat et rustc en activité,
phase frontend, plusieurs dépendances compilées dans le cache neuf. Snapshot
08:55:12.7721696locale :1056366592octets privés (~0.98GiB),16081293312octets
de marge commit (~14.98GiB),59060506624octets libres disque (~55GiB), zéro trip.
CLI wasm-bindgen0.2.127 correspond à la crate compilée0.2.127.
Preuve integrated-terrain034-studio-build-preparation-001/root-start-proof.json.
Ce snapshot et un fichier RUNNING ne remplacent pas un terminal ; reprendre
le même handle41220 et réinspecter son état, ne pas relancer par timeout.

Aucune scène lancée et aucun résultat de mémoire/render du produit annoncé.
Suite :terminal du build apparié, revue des artifacts et profils réels,
replay Tidewater protégé/couverture WebView/compteurs adaptateur, diagnostic
mémoire, toute boucle RPG/Studio, intégration, CI exacte, distribution et
acceptation humaine. Root seul ; Gate3 ouverte et non qualifiée.


### Mise à jour 364 — arrêt de protection ; aucun replay Tidewater relancé

Le propriétaire signale 20Go de RAM et 16Go de VRAM libérés à la fermeture
du test. Observation conservée, sans la convertir en mesure GPU avant/après
ni en preuve d'une cause exclusive. Le diagnostic de consommation reste ouvert.

La session41220 a terminé en erreur de garde CPU, «Process executable identity
changed», après127 échantillons ; aucun dépassement mémoire dans le dernier
échantillon. Arrêts individuels par handles vérifiés archivés. Une vérification
CIM indépendante ne retrouve aucun des six processus du build. Le terminal
de garde listait des survivants temporaires, il ne constitue pas la preuve
de leur présence après cette vérification. Frontend et natif non achevés,
aucune scène lancée, aucun PASS de protection ou de build attribué à cet essai.

Preuve current-campaign-progress-364/root-proof.json. Prochaine étape :
diagnostiquer et valider l'erreur d'identité de la garde avant de relancer
le build puis un replay mémoire borné de Tidewater. Les compteurs de processus
GPU et d'adaptateur doivent rester explicitement distingués. Aucun effacement,
aucune publication ; root seul, Gate3 ouverte et non qualifiée.


### Mise à jour 365 — correction de course d'identité ; reprise CPU Source034

Le contrôle d'identité peut lire MainModule après la fin naturelle du processus.
Version privée owned-process-identity-race-365 : le handle OS retenu confirme
la sortie avant de retirer l'identité ; toute divergence sur un processus
encore vivant reste une erreur fermée, avec PID/images désormais détaillés.
Les identités temporelles, l'ascendance chronologique et les arrêts individuels
restent obligatoires. Aucun Kill(true), aucune adoption d'un processus étranger.
Le message générique de l'essai364 ne permet pas d'établir sa cause exacte.

Neuf cas synthétiques PASS, dont sortie pendant lecture, image/création
divergentes et image inconnue encore vivante. Test léger réel :100 descendants
courts créés, terminaison normale pilote/moniteur0 sans arrêt forcé ; neuf
identités échantillonnées entre les deux tests, sans prétendre observer les100.
Second test réel : durée14s provoque l'arrêt individuel des deux petits Python
détenus, failure=null et motif durée uniquement. Vérification CIM indépendante :
aucune identité échantillonnée restante. Preuve owned-process-identity-race-365/
root-proof.json ; portée CPU légère, pas qualification WebView/GPU ni build complet.

Reprise réelle session73974, préparation/build034002, même checkpoint privé
bff49996d74b54504657332eff0a834340ad1026 et mêmes2559 sources/imports/kit.
Cache privé034001 partiellement compilé réutilisé uniquement pour ce même
source/profil ; il n'est pas déclaré vide à cette reprise. Aucun cargo/rustc
du build arrêté ne restait actif au contrôle préalable. Sorties frontend/natif
002 fraîches, limites6GiB privés/8GiB réserve commit/16GiB disque/3600s conservées,
admission16GiB. Snapshot indépendant et échantillon réel dans terrain034-studio-
build-cpu-protection-002/live-snapshot-365.json ; compilation frontend en cours.
Reprendre le handle73974, ne pas confondre son démarrage avec un PASS.

Aucune scène Tidewater relancée, aucune consommation GPU runtime qualifiée,
aucun transfert au checkout principal, aucune publication. Après terminal,
revoir artifacts/profils puis garde graphique réparée et replay mémoire borné,
avant la boucle RPG/authoring complète et les critères CI/distribution/humains.
Root seul ; Gate3 ouverte et non qualifiée.


### Mise à jour 366 — garde graphique corrigée et compteurs adaptateur réels

La garde graphique reprend la même identité tolérant une sortie confirmée
que la garde CPU365 ; neuf cas synthétiques/parse PASS. Ajout du relevé brut
GPUAdapterMemory par LUID à chaque échantillon. Les sommes GPUProcessMemory
détenues restent des compteurs par processus pouvant compter plusieurs fois
des ressources partagées, et les métriques adaptateur couvrent toutes les
applications. Portées explicitement enregistrées, aucune somme entre ces séries,
aucun plafond physique VRAM déduit ni budget WDDM prétendu.

Validation réelle légère small-gpu-guard-validation-366/root-proof.json :
un buffer matériel16MiB par essai, aucune fenêtre ni scène. Normal :17samples,
pilote/moniteur0, libération COM explicite. Protection :7samples, durée14s,
failure=null, un arrêt individuel vérifié du Python détenu. Compteurs bruts
adaptateur présents dans chaque sample. Vérification CIM/GPU indépendante :
aucune identité de test restante et aucun compteur GPU associé ; AMD étranger
préservé. Pas de test réel du seuil4GiB et pas de qualification WebView/Tidewater.
Les résultats de charge légère ne prouvent pas la disparition du problème mémoire.

Protocole candidate034-memory-replay-protocol-draft.md actualisé au checkpoint
bff49996, overridesCGU16 engine/Studio et correctif identité terrain, même carte,
effets et révision42/inventaire0/gardien500. Périmètre complet ressources,
refus persistants, RPG/environnement, retouches Studio et deux fermetures conservé.
Protocole non exécuté ; attendre la revue du build apparié session73974, puis
mesure mémoire bornée/coverage WebView avant les parcours complets.
Root seul ; aucune publication ; Gate3 ouverte et non qualifiée.


### Mise à jour 367 — second terminal ; course de lecture du module reproduite

La session73974 est réellement terminée en MEMORY_GUARD_ERROR, pas en attente :
93 échantillons, «Live process identity mismatch: pid=22752 creationMatch=True
expectedImage= actualImage=». Chemins CIM/module vides alors que la création
correspondait au handle. Arrêts individuels vérifiés archivés ; vérification CIM
indépendante par PID/date de création :aucune identité du build encore présente.
Le PID1308 réutilisé par un rustc externe F:/OpenRPG_ImportWork a été distingué
de l'ancien pwsh1308 ; aucun arrêt du travail externe. Frontend/natif
inachevés ; aucun PASS du build complet ni de sa protection attribué à cet essai.

Probe réel retenu-process-image-probe-367, API officielle
QueryFullProcessImageNameW via SafeProcessHandle, DWORDflags0 et bufferUTF16 :
100 petits Python, tous terminés naturellement, image native exacte100/100,
MainModule vide96/100. Preuve retained-process-image-probe-367/root-proof.json
et records.json. Une course de disponibilité du module au démarrage est donc
reproduite, sans preuve que tous les échecs antérieurs aient cette seule cause.
Source API Microsoft :
https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew

Suite concrète : intégrer cette lecture du handle retenu à la garde, conserver
les contrôles kernelCreation/ascendance/image attendue et cleanup individuel,
traiter explicitement l'image CIM absente sans adopter une autre identité,
puis tests légers réels avant nouvelle reprise. Le probe n'est pas une garde
intégrée qualifiée. Le PASS GPU léger366 reste limité à cette version et cette
charge ; il ne qualifie ni WebView ni la scène entière. Protocole Tidewater034
non exécuté, diagnostic mémoire produit toujours ouvert.
Root seul ; aucune publication ; Gate3 ouverte et non qualifiée.


### Mise à jour 368 — identité native intégrée ; build Source034 repris

Garde privée owned-process-native-image-368 : QueryFullProcessImageNameW via
SafeProcessHandle remplace MainModule. KernelCreation/CIM/PID restent comparés,
ascendance chronologique conservée. L'image CIM absente est complétée uniquement
sur copie locale par le handle retenu ; commande absente :reprise bornée3x100ms,
création inchangée, puis refus fermé. Divergence vivante ou lecture native
impossible restent fatales. Cleanup par handle individuel, sans Kill(true).
14cas ciblés et parsing des deux gardes PASS ; image manquante, PID/création
réutilisés, image différente et commande inconnue couverts.

Tests CPU légers réels :100 descendants courts, normal pilote/moniteur0,
durée14s arrête les deux Python détenus sans erreur de garde. Tests GPU légers
small-gpu-native-image-368 :un buffer matériel16MiB, normal17samples/libération
COM explicite, protection7samples/un arrêt individuel à la durée. Compteurs
GPUProcessMemory et GPUAdapterMemory bruts conservés dans leurs portées propres.
Clôture indépendante PID/création et compteurs GPU :aucune identité de test
restante. Preuves root-proof.json dans les deux dossiers. Ce PASS est limité
aux petites charges ; build complet/WebView/Tidewater encore non qualifiés.

Build apparié034003 réellement repris, session65981, checkpoint privé
bff49996d74b54504657332eff0a834340ad1026. Même source2559pins, imports3000/kit11,
mêmes profils CGU16 frontend engine/Studio, cache privé034001 du même source
et profil uniquement ; outputs003 fraîches. Aucun conflit de source/cache au
contrôle préalable. Limites6GiB privés, réserve commit8GiB, disque16GiB,
durée3600s et admission16GiB inchangées. Snapshot indépendant pilote11776
créé09:21:46.765007locale ; frontend en compilation, dernier sample09:25:22,
privés1213173760 (~1.13GiB), marge18580770816 (~17.30GiB), zéro alerte.
Preuve integrated-terrain034-studio-build-preparation-003/root-start-proof.json.
Reprendre le même handle65981 ; démarrage/snapshot ne sont pas un PASS terminal.

Protocole candidate034-memory-replay-native368-protocol-draft.md préparé au
candidat034003 et à la garde368. Carte/effets/révision42 et toute boucle
RPG/authoring, environnement, refus persistants, Save/Resume et fermetures
conservés. Aucun replay exécuté, problème mémoire produit non résolu par ces
tests de garde. Root seul, aucune publication ; Gate3 ouverte et non qualifiée.


### Mise à jour 369 — harness privé de fermeture vérifiée ; replay préparé

Copie privée integrated-public-runtime-harness-034-safe-close-001 des416fichiers
épinglés du harness021003, parent intégralement inchangé. Un seul fichier
hérité modifié :Invoke-Gate3GameplayPlaytestSmoke.ps1 ; ajout bibliothèque
Gate3OwnedProcessIdentity.ps1 issue de la garde native368. SafeProcessHandle
acquis au lancement, identité conservée dans Run ; fallbacks de démarrage et
de fermeture passent par la vérification création/image/commande et un arrêt
individuel. Le wrapper refuse aussi un PID stocké différent du handle.
Le WM_CLOSE/dialog/discard et ses limites restent inchangés, arrêt forcé toujours
un échec. Aucun arrêt global ni PID-only dans ces deux fallbacks corrigés.

SelfTest final Playtest PASS. Huit cas sur petits processus Windows réels,
interfaces UI simulées :sortie naturelle sans forced, mauvais PID/création/
image/commande ou identité absente refusés en préservant le processus vivant,
échec de fermeture publique provoque un seul arrêt vérifié et reste forced,
échec de démarrage ferme seulement son petit processus détenu. Deux reçus
d'arrêt exacts, contrôle CIM indépendant PID/date :aucune identité arrêtée
restante. Preuve safe-replay-harness-preparation-369/root-proof.json et manifeste
final gelé. Ce test ne joue pas Studio et ne qualifie pas une fermeture UI réelle
ni tous les autres scripts historiques du harness.

Préparation integrated-memory-loading-diagnostic-034-001 :template034003,
driver/observateur031 byte-identiques pour comparaison, garde graphique368 et
harness369. Aucun hash natif inventé, config.json absent et aucun runner actif :
gel nécessaire après fin/revue des artifacts appariés. Fixtures et WebView
hérités restent à revérifier au gel. Même observation150s, plafond6GiB privés,
GPU détenu4GiB/2GiB, commit8GiB, admission16GiB/durée300s. Les compteurs
adaptateur globaux et processus gardent leurs portées distinctes. CPU prête
ne prouve pas le rendu prêt, et ce diagnostic ne remplace aucun parcours RPG.

Session65981 revalidée vivante sur le même pilote11776, compilation frontend
progressant jusqu'aux crates Forge/Leptos/Rapier, sans erreur de garde. Les
snapshots datés ne remplacent pas le terminal du build. Reprendre ce même
handle. Aucune scène lancée, consommation Tidewater non résolue/qualifiée ;
toute boucle RPG/Studio, environnement, CI exacte, distribution et conditions
humaines restent au périmètre. Root seul, aucune publication, Gate3 ouverte.


### Mise à jour 370 — frontend/worker034003 revus ; compilation native en cours

Frontend034003 réellement terminé exit0 et sources/assets stables. Revue
frontend034003-root-review-370/root-proof.json :checkpoint privé bff49996,
2559sources avant/après/current identiques, Git propre, imports3000hashés,
magic WASM et hashes frontend/worker/manifest contrôlés. FrontendWASM
a3bbc2cb83d4f21129ca4846d6c50e3130ef93265c59608e74c4ccfe94f77f52 ; worker
f5537e2e8039394663d1ba37889b4d0f7a8b502b863b43d2b0e07964d3e5547e.
Ce PASS porte sur le build frontend/worker, pas sur le runtime/navigation.

Capture indépendante d'un rustc Studio détenu PID11592/date vérifiée :CGU16
et opt3 réellement observés. Aucun flag LTO identifié dans cette commande
capturée ; ni effet LTO ni équivalence de performance qualifiés. La revue
complète appariée est préparée dans integrated-studio-candidate-root-review-
034003/review.py, syntaxe valide, exige terminal succès et clôture indépendante
avant tout PASS. Elle vérifiera sources, trois exécutables, imports et kit11,
avec scopes project223/checkWASM et engine720hérité byte-identique séparés.
Reviewer préparé ne vaut pas exécution de revue native.

Même session65981, pilote11776/date conservée, réellement active, passée au
build natif. Cargo/Tauri et rustc détenus observés ; native.log peut rester vide
car Build-StudioCandidate redirige vers candidate034003/build.log. Ce silence
n'est pas un terminal. Snapshot live-snapshot-370.json dans la garde034003,
aucune alerte au relevé. Reprendre ce même handle, ne pas relancer par timeout.

Snapshot OS/PerfMemory en lecture seule system-memory-metric-snapshot-370.json :
RAM physique disponible ~30.18GiB, marge de commit réelle ~18.40GiB ; FreeVirtual
~18.40GiB concorde à la cadence près. Ces métriques physiques et de commit
restent distinctes ; ce relevé pendant compilation ne qualifie pas la mémoire
Tidewater. Aucun replay lancé, natif non terminé, plein périmètre Gate3 conservé.
Root seul, aucune publication ; Gate3 ouverte et non qualifiée.


### Mise à jour 371 — candidat034003 apparié revu ; croissance mémoire persistante

Build session65981 terminé exit0, garde CPU1136échantillons sans arrêt forcé,
clôture indépendante vérifiée. Revue réellement exécutée dans
integrated-studio-candidate-root-review-034003/proof.json : sources2559,
frontend/natif, imports3000 et kit11 concordants. PASS build uniquement.

Replay Tidewater034001 réellement exécuté sur copie isolée, session22317
terminée : pilote exit0, garde exit1 MEMORY_PROTECTION_ABORT pour
owned_descendant_outlived_driver. Application fermée publiquement sans force ;
seul le WebView crashpad-handler survivant a nécessité un arrêt individuel
par identité et handle vérifiés. Clôture indépendante des11identités PASS,
aucun survivant du candidat ou du navigateur du test.

68échantillons OS et32observations navigateur : pic privé5520130048octets
(5.14GiB), compteurs GPU dédiés2030587904octets. Après signal CPU prêt,
croissance privée1205882880octets sur environ106s, sans déplacement ;
croissance des compteurs dédiés423116800octets. Aucun PID GPU attendu absent
des compteurs. Sommes GPU processus potentiellement doublonnées entre
processus : elles ne mesurent pas la résidence VRAM physique exclusive.
Ni plateau mémoire ni cause exclusive prouvés. Le signal CPU prêt ne
qualifie pas le rendu. Source/candidat/helper inchangés pendant le replay.

root-memory-review.json et independent-identity-closure.json sont conservés
dans integrated-memory-loading-diagnostic-034-001. Aucun replay lourd relancé
à ce stade. Diagnostic mémoire à poursuivre avant les parcours longs.
Le plein périmètre RPG/Studio, environnement, CI exacte, distribution et
validation humaine reste requis. Root seul, aucune publication ; Gate3 ouverte.


### Mise à jour 375 — attribution processus/buffers et diagnostic transport revu

Audit372 du replay034001 : après CPU prêt, 15257buffers sans label créés
sur96.126s pour1443256720octets demandés ; cinq formes majeures pointent
ViewClusterBindings::write_buffers et upload_view_gpu_clustering_buffers.
Ces octets cumulés et zéro destroy explicite ne prouvent pas une résidence
GPU ou une fuite. Répartition native/GPU/renderer archivée par PID/date/role.

Observateur373 borné à64clés nommées et overflow par type, tests10000appels
PASS. Replay034002 réellement tenté, arrêté avant ouverture de la zone car
invoke Tauri est non configurable. Pilote1/garde0,2samples, aucun arrêt forcé,
clôture indépendante8identités PASS. Ce cas manquait au test simulé initial.

Observateur374 respecte les fonctions protégées ; source Tauri2.11.5 archivée
confirme IPC Windows via fetch, avec fallback postMessage hors de sa couverture.
Replay034003 réellement exécuté :69samples OS/31observations navigateur,
pic privé5539028992octets, dédié processus2093568000octets. Garde0 sans alerte,
fermeture publique normale sans force, clôture indépendante10identités PASS.
Pilote1 conservé : préparation Python avait lu le pilote UTF8 comme CP1252,
altérant l'accent du regex de disponibilité. Les données UTF8 archivées
montrent22observations CPU prêtes, première08:27:54.4641031UTC ; cette revue
ne réécrit pas le verdict de la commande et ne qualifie pas le rendu.

Revue375 : après CPU prêt, croissance privée720113664octets ; GPU-process
+579014656, renderer+146292736, natif-151552. Contrairement au replay précédent,
le natif reste stable sur cette fenêtre. Appels fetch natifs191→406, pending
maximum3, callbacks natifs maximum8 puis0 : pas d'accumulation terminale observée.
Ces URL ne distinguent pas les variantes StudioCommand et completion fetch
ne prouve pas consommation de la réponse. Pics isolate V8 : used51638824,
embedder152524760, backing22244009octets ; portées distinctes de la VRAM et
du privé total, pics non simultanés à ne pas additionner.

Préparation375 force UTF8 explicite, vérifie le regex original byte roundtrip
et son accord avec22observations ; observateur immutable-test10000appels PASS.
Diagnostic034004 gelé après revue du même candidat/source et SelfTest PASS,
mais NON LANCÉ. Aucun GC forcé, corps de requête lu ou réponse copiée.
Plafonds inchangés6GiB privés,4/2GiB GPU processus, commit8GiB/admission16GiB,
durée300s. Aucun changement de qualité ou fork Bevy adopté. La piste du cycle
de vie des buffers est prioritaire ; causes exclusives et stabilité non prouvées.
Plein périmètre RPG/Studio, environnement, CI, distribution et validation humaine
conservé ; root seul, aucune publication, Gate3 ouverte et non qualifiée.


### Mise à jour 376 — contrôle clustering partiel et incident mémoire propriétaire

Le propriétaire rapporte 20Go de RAM et16Go de VRAM libérés à l'arrêt du test.
Cette observation est conservée avec sa portée : aucune conversion Go/GiB
supposée, aucun rattachement rétrospectif à une identité de processus mesurée.
Les replays courts034 ne reproduisent pas cette amplitude et ne l'invalident
pas. La pression mémoire et sa cause doivent être traitées avant les parcours
longs ; ni fuite exclusive ni stabilité ne sont établies.

Source035 privée dérivée du checkpoint034 bff49996d74b54504657332eff0a834340ad1026 :
2560sources épinglées, deux fichiers modifiés et un module moteur ajouté pour
un contrôle CPU/public GlobalClusterSettings. Le défaut automatic reste intact.
Le backend est observé dans RenderSchedule, avec compteur de frames ; le
paramètre demandé ne constitue pas une preuve du backend ou du rendu.
Le code Bevy CPU comme GPU recrée les bindings : le contrôle CPU n'est donc
ni une correction démontrée ni une solution adoptée.

Qualification CPU035 réellement lancée, puis interrompue : fmt PASS et Clippy
moteur all-targets/all-features avec -D warnings PASS. Les tests moteur ont
seulement commencé leur compilation ; aucun test exécuté, checks WASM non
exécutés. Garde160samples, pic privé détenu2689630208octets ; plancher système
commit7306960896octets, sous8GiB. Arrêt de protection limité aux processus
détenus et vérifiés ; clôture indépendante41identités PASS. Le verdict reste
FAILED_CLOSED, qualification CPU complète NON PASS. Aucun plafond relevé.

Comparaison automatic/CPU035 préparée uniquement : mêmes source/binaire,
authoring et effets requis, backend réellement observé et garde6/4/2GiB.
Templates non gelés faute de candidat035 qualifié ; aucun build frontend/natif
035 ni replay lancé. Les quatre nouveaux tests écrits ne sont pas comptés PASS.

Proposition036 archivée : réutilisation des buffers par entité de vue dans
bevy_pbr0.19.1/src/cluster/gpu.rs, reset logique et traitement des vues inactives.
Préimage/postimage/patch hashés et syntaxe formatée ; aucune compilation,
application ou mesure. Aucun fork créé et aucune dépendance modifiée.
AGENTS.md, « Interdictions avant validation explicite », liste « fork de Bevy » :
autorisation propriétaire explicitement demandée, réponse encore attendue.
L'ADR0009 acceptée concerne Rerecast et ne couvre pas ce fork de rendu.
Tests multi-vue/capacité/readback/retrait/reset et checks natifs/WASM, puis
comparaison Tidewater du rendu, performances et mémoire restent obligatoires.

La Gate3 conserve tout son périmètre RPG/Studio, environnement, CI exacte,
distribution et validation humaine. Modifications propriétaire préservées,
root seul, aucune publication ; Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 377 — reprise CPU protégée interrompue et pression externe observée

Reprise035002 réellement lancée après marge commit remontée à environ19GiB et
vérification sans propriétaire actif du cache ciblé. Source2560hashes identique,
anciens fmt/Clippy PASS conservés ; seules les trois étapes inexécutées demandées.
Session43437 terminal exit1 : garde MEMORY_PROTECTION_ABORT,77samples,
pic privé détenu4744830976octets ; au déclenchement privé743223296octets et
commit7960162304octets, sous8GiB. Les tests n'ont pas commencé et les deux
checks WASM n'ont pas été exécutés. Aucun PASS inventé, aucun plafond relevé.
Clôture indépendante4identités PASS, aucun arrêt de processus par le reviewer.

Snapshot09:25:05UTC : marge commit757723136octets. Autre candidat actif dans
.placeables-smoke-tidewater-scanned-v4-20261006-v2, identité41992/date archivée,
parentage navigateur42712 → GPU36828/renderer42276 vérifié. Privé GPU-process
16377499648octets, renderer2842611712octets. Ce privé n'est pas de la VRAM
physique mesurée. Ces identités ne sont pas celles de la qualification ; elles
sont conservées, sans arrêt ni relance de notre campagne. Ce relevé montre une
pression simultanée, pas une cause exclusive ni l'explication complète de
l'incident propriétaire. Aucun test graphique lancé par root dans cette reprise.

Revue statique376 du patch036 : UninitBufferVec.clear réinitialise la longueur,
pas les contenus GPU ; reserve conserve l'allocation si capacité suffisante.
La suppression des composants ne prouve pas la libération immédiate de toutes
les références/readbacks. La garde inactive du système GPU ne couvre pas à elle
seule les transitions CPU/GPU. Couverture des écritures shader, transitions,
multi-vue et récupération du device doivent donc être vérifiées avant adoption.
Sources/extraits/patch hashés ; aucune application, compilation ou qualification
du fork. Autorisation explicite AGENTS pour « fork de Bevy » toujours attendue.

Périmètre complet RPG/Studio, environnement, CI exacte, distribution et jugement
humain inchangé. Modifications propriétaire préservées, root seul, aucune
publication ; Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 378 — sources upstream comparées et contrôle035 figé en privé

Sources officielles effectivement acquises09:29UTC : gpu.rs v0.19.1,
v0.20.0-rc.1 et main. Le fichier0.19.1 est byte-identique à l'upstream local
épinglé, SHA0db47d27ee58a25206221d318a9bb0b070471dc433a1f8d1c7fc966de9783839.
Les trois fonctions prepare_clusters_for_gpu_clustering inspectées construisent
ViewClusterBindings et ViewGpuClusteringBuffers dans leur boucle de vues.
Snapshots/URL/ETag/hash archivés dans bevy-upstream-buffer-lifecycle-review-378.
Cette inspection ne qualifie pas un upgrade comme correction et ne prétend
pas couvrir tous les issues/PR/branches ni toutes les causes mémoire.
Aucune dépendance mise à jour, aucun fork créé, aucun test lourd relancé.

Source035 figée dans le checkpoint privé48df1b266a6ab54b1b0da918d6fecef72a72e1ab,
parent034 bff49996d74b54504657332eff0a834340ad1026. Delta arbre exactement
authoring_imported_terrain.rs, forge_engine/lib.rs et nouveau module
qualification_cluster_assignment.rs ;2560bytes hashes avant/après inchangés.
Qualification CPU toujours incomplète : deux interruptions de compilation,
nouveaux tests non exécutés, checks WASM non exécutés. Ni candidat035 construit
ni contrôle automatic/CPU rejoué ; templates restent des brouillons non gelés.

Premier statut privé affichait1320modifications après restauration des fins
de ligne du clone, alors que diff contenu était vide hors delta intentionnel.
Après le checkpoint, réapplication des règles Git à l'index privé uniquement :
aucun delta d'arbre/index, aucun byte source changé, statut effectivement vide.
Preuves initiale et revue propre conservées séparément ; aucun reset ni
renormalisation des fichiers du dépôt principal. Le checkpoint n'est pas un
PASS de tests ou une adoption du mode CPU comme correctif mémoire.

Autorisation explicite du fork Bevy proposé toujours attendue selon AGENTS.
Plein périmètre RPG/Studio, environnement, CI, distribution et humain maintenu.
Root seul, aucune publication ; Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 380 — fork Bevy autorisé, réalisé en privé ; validation interrompue

Accord humain reçu le 2026-10-06 : « on peux creer un fork oui ».
Le blocage d'autorisation du fork mentionné en378/379 est levé.
Source036 dérive du checkpoint035 48df1b266a6ab54b1b0da918d6fecef72a72e1ab.
Le package bevy_pbr0.19.1 est vendorié avec attribution et licences originales,
puis raccordé dans cette copie par patch.crates-io. Un seul fichier upstream
modifié : cluster/gpu.rs ; shaders et autres sources upstream conservés.
Le lock final remplace uniquement l'origine registry de ce package par le
chemin local : autres versions/entrées identiques ; metadata locked offline PASS.
Le résultat initial trop large de cargo update est archivé, sans adoption.

Buffers conservés par vue ; longueurs, compteurs et données logiques remis
à zéro chaque frame ; invalidation lors d'un changement de device. Nettoyage
des vues inactives ou passées au CPU, avec conservation des bindings CPU actifs.
Sept cas de régression CPU/ECS sont écrits mais n'ont pas été exécutés.
ADR0022 et FORGE-FORK.md présents dans la copie privée ;2716 fichiers épinglés,
manifest SHA420243309e2de321c34b32816b30ddc1af673c66749b853db94a754904c0e5f3.
Cette implémentation ne démontre pas encore une baisse de mémoire résidente.

Tentative complète036001 : admission mémoire16GiB refusée avant tout compilateur.
Tentative de vérification de types036002 : formats workspace/fork PASS ;
native-strict interrompu par la limite privée2GiB, pic mesuré2297548800bytes.
46 échantillons ; aucun test unitaire, check WASM ou replay graphique exécuté.
Protection déclenchée, résultat FAILED_CLOSED conservé. Revue indépendante
ultérieure : IDENTITY_CLOSURE_PASS,20 identités suivies, aucun survivant exact.
Le budget2GiB concerne uniquement cette tentative de types, pas une limite
de qualification du jeu ni une preuve de régression mémoire du fork.

Reprise : nouvelle tentative CPU complète sous le garde existant6GiB privé,
admission16GiB, plancher commit8GiB et disque16GiB, après vérification de marge.
Puis tests du fork et moteur, strict natif/WASM, candidat apparié et comparaison
Tidewater bornée du rendu, performances, mémoire et fermeture publique.
Ne pas écraser les tentatives interrompues ni modifier leurs pins/logs.
Le disque disponible ne remplace pas la marge mémoire système.

Aucune adoption de code/Cargo dans le dépôt principal, aucun push, aucun
sous-agent. Périmètre RPG/Studio complet, environnement, CI du candidat exact,
distribution et validation humaine toujours requis. Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 381 — types du fork vérifiés ; raccordement Clippy corrigé, reprise037

Tentative ciblée036003 : commande cargo clippy -p bevy_pbr --lib --tests
terminée0, protection et fermeture indépendante PASS13identités. Les artifacts
montrent la compilation de la librairie et du code de tests en métadonnées.
Deux avertissements subsistent malgré -Dwarnings : private_interfaces sur
ForgeGpuClusteringBufferDevice et unused_qualifications dans le test.
Le package étant exclu du workspace, ce succès n'est PAS un verdict Clippy
strict. Aucun test unitaire exécuté, aucun replay GPU/Tidewater ; preuve brute
conservée avec interprétation corrigée dans current-campaign-progress-381.

Source037 créée à partir des2716fichiers épinglés036, sans écraser036.
Visibilité du type ajustée à pub(crate), qualification du type simplifiée.
Fork inclus dans les membres workspace afin de devenir une cible Clippy ;
opt-level3 préservé explicitement pour ne pas changer son optimisation dev.
Cette inclusion exige la résolution des dépendances optionnelles upstream :
10packages ajoutés, aucun retiré, versions/checksums existants conservés.
Arêtes optionnelles ajoutées dans bevy_pbr, bevy_render et profiling auditées ;
autres entrées existantes identiques. Ce nouveau lock n'est pas byte-identique
au lock036. Résolution offline initialement impossible faute lz4_flex ;
première tentative réseau confinée Schannel en échec, retry avec permissions
réseau normales terminé. Sources/diagnostics/résolution conservés séparément.

Nouvelle tentative037001 format+Clippy sous limite privée2GiB et admission12GiB :
refus avant Python/compilateur, marge commit9217400832bytes (~8.58GiB).
Aucun check de037 réellement exécuté ; fermeture indépendante PASS.
Manifest037 SHA5201b5f3cac46daea9641477d5e380228fd3175724b67e19cbf001113c1f707f.
Le code est figé mais non qualifié, la création est une copie source isolée,
pas une publication ni une adoption dans le dépôt principal.

Prochaine reprise : nouvelle tentative protégée sur037, formatage et Clippy
effectif du fork, puis suite native/WASM et tests complets. Garder les budgets
et seuils existants, attendre la marge requise plutôt que lancer Tidewater
sous pression mémoire. La qualification du fork ne remplace pas la boucle
RPG authorée depuis Studio, R3-A/R3-B/R3-C, P01 à P05, CI, distribution et humain.
Root seul ; aucun code principal modifié ou publié. Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 382 — blocage mémoire revalidé sur trois tours de reprise

Dernier état mémoire acquis : marge commit5869428736bytes,
toujours sous l'admission16GiB de la qualification complète. Les preuves380,
381 et382 montrent le même blocage, malgré les progrès source/typechecks.
Deux compilations extérieures à nos contrôles restent actives : exemple
tidewater_plant_probe dans .build/cargo-target et frontend Studio WASM dans
.build/studio-live-environment-20261003-v1/target. Identités/commandes archivées.
Aucun processus étranger arrêté ; aucun nouveau test ou rendu lancé.
Les sessions de qualification036/037 antérieures sont terminales avec fermeture
indépendante PASS ; il ne s'agit pas d'un timeout observateur à relancer.

2716fichiers Source037 revérifiés, pins inchangés. Suite complète préparée dans
bevy-cluster-fork-cpu-037-002, PAS lancée : format Forge plus fichiers fork
modifiés séparément, strict fork/native/WASM, tests fork et moteur complets.
Budgets6GiB privé/admission16GiB/plancher commit8GiB/disque16GiB conservés.
Reprise conditionnée à une nouvelle marge système suffisante, puis qualification
du candidat apparié et parcours Tidewater sur tout le périmètre. Le statut de
l'objectif peut être bloqué pour cette condition externe ; Gate3 reste active
dans le roadmap, OUVERTE — NON QUALIFIÉE, sans réduction de ses critères.
Audit explicite dans gate3-memory-blocked-audit-382/audit.json.
Root seul ; aucune adoption principale, publication ou gate clôturée.


### Mise à jour 383 — disque, RAM physique et engagement mémoire distingués

Le propriétaire signale110Go libres sur disque ; ce signal est compatible
avec l'état actuel. Vérification indépendante à17:33Paris le2026-10-06 :
disque107.71GiB libres, RAM physique22.33GiB
disponibles, mais marge d'engagement système2.13GiB.
GetPerformanceInfo et GlobalMemoryStatusEx natifs, compteur Windows et
Win32_OperatingSystem.FreeVirtualMemory utilisé par le garde concordent à
moins de256MiB sur leurs acquisitions successives. L'admission ne confond
donc pas l'espace disque ou la RAM physique disponible avec la capacité
restante de Windows à engager de la mémoire. Mesures brutes archivées.

Aucune augmentation de seuil, modification du fichier d'échange ou arrêt
de processus ; pas de compilation ni rendu lancé sous le plancher8GiB.
Source0372716fichiers revérifiés inchangés ; suite complète037002 toujours
préparée et non exécutée, admission16GiB requise. Objectif repris pour cet
audit, compteur de blocage frais1tour ; pas de clôture ou redéfinition de gate.
Preuve memory-admission-independent-383/review-proof.json.
Gate3 reste OUVERTE — NON QUALIFIÉE sur tout son périmètre.


### Mise à jour 384 — Clippy effectif du fork PASS ; suite complète en cours

Marge système remontée : contrôle ciblé037003 effectivement admis et terminé.
Formats des deux fichiers fork modifiés PASS ; cargo clippy -p bevy_pbr
--lib --tests --locked --offline -j1 -- -Dwarnings PASS, sans diagnostic.
Le package est maintenant membre du workspace ; code de librairie et de
tests vérifié, aucune exécution de tests dans ce contrôle ciblé. Garde normal,
aucun dépassement/arrêt forcé ; fermeture indépendante IDENTITY_CLOSURE_PASS.
2716pins Source037 revérifiés, aucun byte de source modifié pendant contrôle.

Suite complète037002 admise après17.58GiB de marge observée, mais interrompue
au premier format : option cargo fmt --exclude non prise en charge, exit2.
Aucun test ni compilateur Rust commencé dans cette tentative. Erreur conservée
avec fermeture indépendante PASS, puis invocation corrigée sans modifier
sources/seuils : cargo fmt --package liste explicite des10packages Forge
--check, fichiers fork modifiés contrôlés séparément par rustfmt.

Suite complète037004 effectivement démarrée sous limites6GiB privé,
admission16GiB/plancher8GiB/disque16GiB, sans rendu. Native/fork strict, tests
fork et moteur, puis WASM moteur/Studio requis ; verdict complet en attente.
Conserver et suivre la même session82053/identité driver tant qu'elle est live ;
ne pas relancer sur un timeout d'observation. Preuves de démarrage et revue
dans bevy-cluster-fork-cpu-037-004 et current-campaign-progress-384.
Ce PASS Clippy ciblé n'est pas une qualification mémoire ou Tidewater.
Plein objectif RPG/Studio, R3-A/B/C et P01-P05, CI/distribution/humain conservé.
Root seul, aucun code principal adopté ou publié ; Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 385 — sept régressions CPU/ECS du fork effectivement PASS

Suite037004 : après18m06 de compilation de l'exécutable du fork, les7tests
forge_buffer_reuse_tests ont effectivement été exécutés :7PASS,0FAIL,
0ignored ;2tests upstream hors filtre. Longueurs/compteurs/données remises à
zéro, croissance/réduction de scratchpad, indépendance logique de vues,
retrait des ressources de vue inactive/CPU, maintien de vue GPU active et
préservation des composants non marqués testés en CPU/ECS, sans device GPU.
Le binaire mesuré est copié hors cache ; SHA
48e7a4fd751f13906aa49682a129f9a18dfae18d6d51a69b31626c6ed7b48cb8.
Log et executable-proof vérifiés ;2716pins Source037 toujours inchangés.

La suite moteur puis WASM restent en cours/à suivre dans la même session82053.
Ce PASS ne prouve ni identité d'allocation GPU conservée, pixels/performance,
croissance mémoire bornée, readback/dépassement ni récupération d'un device.
Aucun nouveau candidat rendu ou replay Tidewater lancé.

Workflow rust-gate.yml inspecté : clippy et tests --workspace --all-targets
--locked, sans activation globale de toutes les features expérimentales Bevy.
Même portée préparée dans bevy-cluster-fork-workspace-cpu-037-005, avec offline
et un seul job ; PAS lancée, à exécuter séquentiellement après037004.
Aucune commande CI modifiée ni CI distante/publication déclenchée.
Les résultats ciblés du moteur/fork ne remplacent pas ce scope complet.

Pour construire le candidat apparié, conserver une révision source propre et
les mêmes pins pour frontend/native ; la copie037 n'est pas encore un checkpoint
Git privé. Ne pas modifier son état pendant la session CPU active. Qualification
du fork et objectif Gate3 sur le parcours RPG authoré depuis Studio restent
distincts : R3-A/B/C, P01-P05, CI/distribution/humain toujours requis.
Root seul ; aucun code principal adopté ou publié. Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 386 — moteur 724 PASS et WASM moteur PASS ; correction Studio isolée

La suite037004 est terminale : sept régressions du fork PASS, puis garde
MEMORY_GUARD_ERROR sur un processus disparu entre inventaire et GetProcessById.
Ce n'est pas un dépassement mémoire. Fermeture indépendante PASS ; une identité
étrangère réutilisant un PID est conservée. Le garde386 corrige uniquement cette
course : absence réelle du processus acceptée, erreurs de droits et erreurs
inattendues toujours bloquantes. Six sorties réelles de petits processus et
contrôles de refus PASS ; seuils et vérification des identités conservés.

Tentative037006 refusée à l'admission avant compilation. Replay moteur037008
interrompu au plafond privé2GiB ; aucun verdict global de tests dans ce replay.
La reprise037009 sous budget6GiB/admission16GiB a réellement exécuté la suite :
724 PASS,0FAIL,16ignored,0filtered,33.57s. Exécutable mesuré archivé hors cache,
hash vérifié par executable-proof. Clippy WASM moteur strict PASS. Studio WASM
strict échoue sur un unique clippy::unnested_or_patterns dans le sélecteur de
backend. Garde normal et fermeture indépendante PASS pour cette reprise.

Source038 corrige le motif ; son contrôle rustfmt échoue avant Clippy car la
branche doit tenir sur une ligne. Échec et fermeture PASS conservés. Source039
corrige ce format ; les2716pins sont vérifiés, seul authoring_imported_terrain.rs
diffère de038. Moteur, fork, lock et shaders restent byte-identiques. Format
Studio039 PASS ; Clippy WASM Studio en cours dans la session46403. Ne pas déduire
un résultat global039 des tests037. Workspace natif complet, portée CI réelle
--workspace --all-targets sans --all-features, préparé039007 et non lancé.

Ces preuves CPU ne qualifient pas la réutilisation d'allocations GPU, le device,
les pixels ni la stabilité RAM/VRAM sur Tidewater. Candidat frontend/native
apparié, parcours RPG/Studio R3-A/B/C et P01-P05, CI/distribution et acceptation
humaine restent requis. Aucun processus étranger arrêté, aucune adoption
principale ou publication. Root seul ; Gate3 OUVERTE — NON QUALIFIÉE.
Preuves et hashes dans current-campaign-progress-386/root-review.json.


### Mise à jour 387 — Studio WASM strict PASS ; workspace complet en cours

Le premier contrôle039001 a effectivement passé format et Clippy WASM Studio,
mais son garde a échoué sur l'identité du pilote lors de sa fin. Pas de dépassement
ni arrêt forcé ; fermeture indépendante PASS. Ce verdict observateur n'est pas
transformé en qualification. Garde387 corrigé : une identité CIM incompatible
ne peut être traitée comme terminée que si le handle Windows déjà conservé du
pilote confirme HasExited. Toute discordance vivante ou sans handle reste fatale.
Bloc de validation de production testé sur les deux gardes : six sorties réelles
chacun, identités vivantes incorrectes et absence de handle refusées ; seuils PASS.

Rejeu039002 réel, mêmes2716pins : format Studio PASS, Clippy WASM Studio strict
PASS, garde MONITORED_DRIVER_TERMINAL sans dépassement/arrêt forcé, fermeture
indépendante PASS. Aucun diagnostic Clippy dans le log. La suite039007 est ensuite
réellement démarrée avec le garde387 : clippy puis tests --workspace --all-targets
--locked --offline -j1, portée native complète du workflow. Session62427 à suivre
jusqu'au terminal ; verdict workspace en attente. Les copies037/038 et leurs
échecs restent conservés ; Source039 n'est pas modifiée pendant la suite.

Qualification mémoire et rendu Tidewater encore absente pour le fork ; candidat
frontend/native apparié et boucle RPG/Studio complète restent à réaliser.
R3-A/B/C, P01-P05, CI réelle du candidat, distribution et acceptation humaine
gardent leur portée. Root seul ; aucune adoption principale ou publication.
Gate3 OUVERTE — NON QUALIFIÉE. Preuve current-campaign-progress-387/root-review.json.


### Mise à jour 388 — Clippy du workspace complet PASS ; tests complets actifs

Source039, suite039007 : cargo clippy --workspace --all-targets --locked
--offline -j1 -- -Dwarnings effectivement terminé exit0, aucun compiler-message
dans le log JSON archivé et hash vérifié. Il inclut les cibles natives du workspace,
dont le fork Bevy membre. Suite cargo test --workspace --all-targets en compilation
dans la même session62427 ; résultat des tests complets encore en attente.
Échantillon mémoire CPU sans dépassement archivé dans root-review388 ; il ne
qualifie pas la stabilité mémoire d'un rendu ni la VRAM.

Checkpoint Git privé039 préparé, NON exécuté : nécessite le PASS des deux étapes
workspace, du garde et de la fermeture indépendante ; stage uniquement les2716
pins dans la copie isolée, sans toucher Git principal. Préparation du build
frontend/native039 apparié faite, NON exécutée : même checkpoint propre et pins
avant/après, imports gouvernés3000 et kit11 vérifiés, cache privé039 distinct,
garde387 et seuils inchangés. Les overrides frontend historiques de compilation
restent explicites ; aucune équivalence de performance n'est revendiquée.

Puis qualification réelle du fork et de la boucle RPG/Studio Tidewater requise,
avec contrôles R3-A/B/C et P01-P05, CI/distribution et acceptation humaine.
Aucun rendu039, adoption principale, publication ou passage de gate.
Root seul ; Gate3 OUVERTE — NON QUALIFIÉE.
Preuve current-campaign-progress-388/root-review.json.


### Mise à jour 389 — tests complets en compilation ; format CI exact à qualifier

Session62427 confirmée live par son handle : après Bevy et Rapier, moteur,
Studio, CLI puis Runtime compilent pour cargo test --workspace --all-targets.
Aucun verdict global de tests039 disponible. Clippy complet039 PASS conservé.
Le garde de cette suite reste6GiB privé/admission16GiB/plancher système8GiB ;
ne pas relancer la compilation sur un timeout d'observation.

La commande exacte du workflow cargo fmt --all -- --check doit aussi couvrir
le fork désormais membre. Tentative039001 refusée avant commande : marge sous
16GiB. Contrôle uniquement de formatage039002, sous budget des petits contrôles
2GiB privé/admission12GiB/plancher8GiB, également refusé avant commande : marge
10.74GiB. Ni format PASS ni échec de format revendiqué. Deux fermetures
indépendantes PASS, aucun arrêt forcé. Aucun seuil de compilation complète ou
GPU réduit. Reprise de format039003 préparée, NON lancée ; attendre l'admission
réelle, de préférence après la suite active. Source039 inchangée pendant tests.

Préparations latest003 de checkpoint et build apparié, NON exécutées : imposent
PASS du workspace, de son garde/fermeture, puis du format CI exact039003 avant
création du Git privé. Les préparations388 restent vérifiables à leurs hashes.
Pas de checkpoint ou nouveau candidat039 créé. Essais comparatifs GPU automatique
et CPU039 préparés en draft, NON gelés/lancés : même binaire et révision42,
backend réellement observé au RenderSchedule avec30frames et deux observations
minimum. Fixture42 sert au chargement/mémoire ; elle ne prouve pas la quête
complète. Garde3876GiB privé/4GiB GPU dédié/2GiB partagé et admission16GiB conservé.

La boucle RPG/Studio complète sur Tidewater et la qualification RAM/VRAM restent
à réaliser avec R3-A/B/C et P01-P05, CI réelle, distribution et revue humaine.
Root seul ; aucune adoption principale ou publication. Gate3 OUVERTE — NON
QUALIFIÉE. Preuve current-campaign-progress-389/root-review.json.


### Mise à jour 390 — 1786 tests PASS ; format CI corrigé et candidat041 en construction

Suite039007 terminale PASS : Clippy natif --workspace --all-targets strict,
puis tests même portée,1786PASS,0FAIL,29ignored,0filtered sur163groupes. Neuf
tests Bevy dont sept régressions du fork. Garde normal sans dépassement/arrêt
forcé et fermeture indépendante PASS.96exécutables de groupes non vides
archivés hors cache et vérifiés par hash ; logs et2716pins Source039 stables.
Ce sont des résultats CPU locaux Source039, pas des tests GPU ou une CI distante.

Le format exact039003 échoue réellement sur le style upstream du fork, membre
du workspace. Le rustfmt.toml du commit Bevy b56fc29d3016e641754765244b5ba3f9cc504671
est acquis par HTTPS vérifié : use_field_init_shorthand=true, newline_style=Unix,
style_edition=2021 ; SHA9158cc77bf53cccac4f0cf6850bc0344e86aba527c93efbff193b17e0e8f3a6d.
Le langage reste édition2024 ; aucune règle de CI désactivée. Source040 ajoute
seulement ce fichier ; format restant échoué sur une assertion du test du fork.
Source041 normalise uniquement les espaces de cette assertion : tokens exacts
conservés ; les2715autres fichiers de039 restent byte-identiques, dont toute la
production, lock, shaders et workflow. Config supplémentaire porte le total2717.
cargo fmt --all -- --check041 PASS avec garde et fermeture indépendants PASS.
Échecs039/040 conservés ; aucune modification de leurs sources pendant mesures.

Checkpoint privé041 propre créé : c5fc41a3de79136d5d72097247ed8115e91c4746,
2717pins physiques inchangés par Git. Résultats CPU039 référencés avec différence
bornée exacte de style ; aucun test directement exécuté sur041 revendiqué.
Frontend puis native041 réellement lancés depuis ce même checkpoint et mêmes
inputs gouvernés3000imports/11kit, cache privé041, garde6GiB privé/admission16GiB/
plancher8GiB/disque16GiB. Session4451 live, frontend en compilation, verdict
apparié encore en attente. Pas de rendu ou replay041 à cet instant.

Qualification RAM/VRAM du fork et parcours RPG/Studio complet Tidewater restent
requis ; R3-A/B/C et P01-P05, CI réelle du commit candidat, distribution et revue
humaine conservent leur portée. La partie complète n'est pas remplacée par ces
tests ou la future comparaison de chargement. Root seul ; aucune adoption
principale/publication. Gate3 OUVERTE — NON QUALIFIÉE.
Preuve current-campaign-progress-390/root-review.json.


### Mise à jour 391 — construction active et comparaison Tidewater préparée

La session4451 reste active : frontend041 en compilation, sans verdict terminal.
Le relevé du garde archivé dans current-campaign-progress-391/root-review.json
ne signale pas de dépassement ; son espace disque libre dépasse largement16GiB.
Le manque de place annoncé précédemment n'est pas confirmé par ce relevé.
La mémoire privée de compilation ne qualifie pas la RAM/VRAM du runtime.

Le helper natif exige une branche nommée Rust. La correction du nom de la
branche privée est préparée, mais ne sera exécutée qu'après la fin effective
du premier essai, son échec explicite sur cette condition et sa fermeture
indépendante. Même commit et mêmes2717sources exigés ; aucun changement de
branche pendant la construction active. Le frontend validé sera réutilisé
dans un essai natif séparé, avec preuves distinctes et échec initial conservé.
Reprise native et revue source/frontend/native/assets préparées, non exécutées.

Comparaison Tidewater041 préparée, non figée ni lancée : même candidat et même
fixture de révision42, automatique puis CPU. Les backends réellement observés
depuis le RenderSchedule doivent être respectivement gpu et cpu sur au moins
30frames, avec deux observations. Limites conservées : privé6GiB, GPU dédié
par compteurs de processus4GiB, partagé2GiB, admission commit16GiB et plancher8GiB.
Ces compteurs ne constituent pas une mesure physique exclusive de la VRAM.
Fermetures normales et vérification indépendante des identités exigées.

Cette fixture de chargement n'est pas une aventure complète. R3-A/B/C et P01-P05,
la preuve Studio du parcours, la qualification mémoire, la CI et la revue humaine
restent requises. Root seul, sans adoption principale ni publication.
Gate3 OUVERTE — NON QUALIFIÉE.


### Mise à jour 392 — essai041 arrêté pendant wasm-bindgen, fermeture vérifiée

La session4451 est maintenant terminale en échec. Rust termine le frontend
release en43m35s, puis wasm-bindgen atteint environ5,25GB de mémoire privée.
Le garde arrête l'essai quand la marge commit système tombe à7,50GB, sous son
plancher8GiB. Ce n'est pas un PASS frontend : bindings, worker et packaging
ne sont pas qualifiés ; le natif n'a pas été lancé.1118relevés et l'arrêt forcé
restent dans les preuves. La fermeture indépendante ultérieure est PASS,
sans survivant des identités de cet essai ; aucun processus étranger arrêté.

Après cette terminaison seulement, la branche de la copie privée041 est
renommée Rust : même commit c5fc41a3de79136d5d72097247ed8115e91c4746,
mêmes2717fichiers et arbre propre vérifiés. Aucun Git principal modifié.
La reprise native seule préparée002 ne convient plus, faute de frontend PASS ;
elle et sa revue041002 restent non exécutées et sont remplacées par une reprise
appariée dans bevy-cluster-candidate041-build-preparation-002 : frontend002 puis
candidat003, même cache privé compilé, mêmes sources/profils et limites.
Les outputs partiels frontend001 et l'échec initial sont conservés.

La mesure après fermeture distingue la RAM physique disponible, environ32,74GB,
de la marge commit Windows, environ11,56GB. Cette dernière ne satisfait pas
l'admission16GiB ; la reprise n'est donc pas lancée. Une compilation Forge
dans relocated-work/Rust-target est distincte de notre cache041 et préservée.
Root seul ; aucun sous-agent actif. Pas de diminution des protections mémoire.

La comparaison GPU/CPU devra être figée sur le prochain candidat effectivement
validé. Aucun replay041 ni résolution du problème RAM/VRAM revendiqué.
R3-A/B/C, P01-P05 et leurs preuves Studio complètes restent requis.
Gate3 OUVERTE — NON QUALIFIÉE, sans adoption principale ni publication.
Preuve current-campaign-progress-392/root-review.json.


### Mise à jour 393 — fixture restaurée et reprise041 effectivement lancée

Le préflight détecte que la fixture021 de révision42 a été transférée vers une
archive par la tâche de stockage concurrente. Restauration dans un nouveau
dossier privé sur E:, sans changer l'archive ni la jonction originale : SHA-256
de l'archive complète et210membres vérifiés. Objet courant, révision42 et acteurs
restent identiques. Configurations automatique/CPU041002 reliées à cette copie.
Les2717sources,3000imports,11fichiers du kit, garde387, harness et WebView passent
le contrôle d'intégrité et de disponibilité. Aucun replay ou PASS mémoire déduit.

La marge commit remonte à environ18,5GB, au-dessus de l'admission16GiB.
La reprise appariée041002 est réellement lancée, session78414 active, depuis
le cache privé compilé. Mêmes sources, profils et protections. Frontend002 puis
candidat003 ; la revue041003 et la comparaison041002 sont préparées, non exécutées.
Le dernier relevé conservé dans la preuve ne signale pas de dépassement.
L'arrêt forcé initial et sa fermeture restent consignés ; aucun essai remplacé.

Qualification du candidat, comparaison RAM/VRAM Tidewater et parcours Studio
complet R3-A/B/C et P01-P05 restent requis. Gate3 OUVERTE — NON QUALIFIÉE.
Root seul, sans adoption principale ni publication.
Preuve current-campaign-progress-393/root-review.json.


### Mise à jour 394 — candidat041003 vérifié ; premier replay refusé avant lancement

La construction appariée041002 est terminale PASS : frontend002 et worker,
puis native003, mêmes sources et assets stables.509relevés, garde normal sans
déclenchement ou arrêt forcé ; fermeture indépendante PASS, aucun survivant.
Revue complète041003 PASS :2717sources,HEAD c5fc41a3de79136d5d72097247ed8115e91c4746,
frontend/native/worker/PE,3000imports et11fichiers du kit concordants.
Ces preuves qualifient la construction et les entrées, pas le rendu ou la mémoire
Tidewater. Tests CPU Source039 réutilisés selon le delta de style exact déjà consigné.

Comparaison automatique/CPU041002 effectivement figée sur ce même candidat et
la fixture restaurée de révision42. Selftest d'import des helpers PASS. Le numéro
de candidat attendu par le gel restait041002 : corrigé vers la revue réelle041003
avant gel et lancement ; aucun contrôle supprimé. Les deux gels passent.

Le premier lancement automatique est refusé à l'admission, avant le driver,
avec marge commit environ16,38GB, inférieure aux16GiB requis, et zéro Studio
existant. Aucun processus de jeu ou charge GPU lancé. Preuve et source-before
de ce refus conservés dans automatic/prelaunch-refusal-001. La mesure suivante
reste sous l'admission ; protections inchangées. CPU pas encore lancé.

Les mesures RAM/VRAM, le parcours Studio complet R3-A/B/C et P01-P05, la CI et
la revue humaine restent requis. Gate3 OUVERTE — NON QUALIFIÉE.
Root seul, sans adoption principale ni publication.
Preuve current-campaign-progress-394/root-review.json.


### Mise à jour 396 — blocage d'admission GPU vérifié sur trois tours

Les tours394,395 et396 rencontrent le même blocage : marge commit Windows
inférieure à16GiB au préflight, malgré de la RAM physique disponible. Deux
relances sont refusées avant tout driver ; leurs preuves/source-before sont
archivés séparément. Mesure actuelle :16,729GB libres virtuels et16,754GB de
marge compteur, tous deux sous17,180GB requis. Aucun Studio concurrent.

Candidat041003 construit et vérifié, processus de construction fermés ; entrées
automatique/CPU figées et stables. Aucun driver ou jeu lancé, aucun job de jeu
ou build de cette tâche encore actif. Préparations indépendantes terminées.
Le contrôle ne sera pas abaissé ; les tâches et applications étrangères restent
préservées. Une marge commit stable au-dessus16GiB est nécessaire pour reprendre.

Audit gate3-memory-blocked-audit-396/audit.json : trois tours consécutifs,
impasse de qualification nécessitant un changement externe. Objectif incomplet :
mesures RAM/VRAM, parcours Studio complet R3-A/B/C et P01-P05, preuves CI et
revue humaine restent requis. Gate3 OUVERTE — NON QUALIFIÉE.
Sans adoption principale ni publication ; aucun PASS runtime déduit du build.


### Reprise du 7 octobre 2026 — contrôle mémoire 001

Contrôle CIM frais : marge commit WMI 14.83 Gio ; marge compteur 14.80 Gio ; seuil inchangé 16 Gio. Admission refusée. Aucun lancement natif. Les pins des deux préparations et l’objet restauré restent vérifiés ; candidat présent, sans nouveau hash du binaire.

Premier contrôle de la nouvelle reprise, distinct de l’audit historique 396. Preuve locale resume-20261007-001/proof.json. Aucun seuil abaissé, aucune qualification runtime ou mémoire. Gate 3 OUVERTE — NON QUALIFIÉE.


### Reprise du 7 octobre 2026 — contrôle mémoire 002

Contrôle CIM frais : marge commit WMI 14.85 Gio ; marge compteur 14.83 Gio ; seuil inchangé 16 Gio. Admission refusée. Aucun lancement natif. Les pins des deux préparations et l’objet restauré restent vérifiés ; candidat présent, sans nouveau hash du binaire.

Contrôle 2 de la nouvelle reprise, distinct de l’audit historique 396. Preuve locale resume-20261007-002/proof.json. Aucun seuil abaissé, aucune qualification runtime ou mémoire. Gate 3 OUVERTE — NON QUALIFIÉE.


### Reprise du 7 octobre 2026 — contrôle mémoire 003

Contrôle CIM frais : marge commit WMI 14.02 Gio ; marge compteur 14.01 Gio ; seuil inchangé 16 Gio. Admission refusée. Aucun lancement natif. Les pins des deux préparations et l’objet restauré restent vérifiés ; candidat présent, sans nouveau hash du binaire.

Contrôle 3 de la nouvelle reprise, distinct de l’audit historique 396. Preuve locale resume-20261007-003/proof.json. Aucun seuil abaissé, aucune qualification runtime ou mémoire. Gate 3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — suppression des seuils de mémoire disponible demandée par le propriétaire

Admission à 16 Gio et arrêt sur marge commit sous 8 Gio supprimés pour la nouvelle paire cluster-assignment-replay-preparation-041-003. Les mesures restent enregistrées. Plafonds de consommation du test conservés : RAM privée 6 Gio, GPU dédié 4 Gio, GPU partagé 2 Gio ; durée 300 secondes et vérifications d’identité/fermeture inchangées. Les anciennes preuves et préparations restent intactes.

Test rouge observé sur le garde précédent ; tests ciblés verts pour les deux lanceurs et syntaxe PowerShell PASS. Aucun essai natif lancé par cette modification. La régression complète des identités de processus n’a pas été rejouée : son code est inchangé. Preuve available-memory-gate-removal-20261007/proof.json. Gate 3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — correction du seuil supplémentaire dans le driver

L’essai automatique041003 a échoué avant ouverture du jeu : un contrôle supplémentaire de marge commit à16 Gio subsistait dans Invoke-ClusterComparison.ps1. Driver terminal, aucun candidat observé, garde sans déclenchement, fermeture indépendante PASS. Les preuves restent conservées.

Nouvelle paire041004 : contrôle supplémentaire supprimé, synchronisation avec le premier échantillon et refus sur déclenchement des plafonds de consommation conservés. Tests étendus rouges sur041003 puis verts pour les deux lanceurs041004. Les anciennes affirmations de suppression complète pour041003 sont corrigées par ce résultat. Aucun PASS runtime ou Gate3 déduit.


### 7 octobre 2026 — essais réels appariés041004

Automatique : commande et revue PASS, backend GPU effectivement observé, simulation prête, fermeture normale et indépendante PASS. 66 relevés ; pics RAM privée 4,71 Gio, compteur GPU dédié 1,46 Gio, partagé 0,15 Gio. Un chargement/repos borné ne qualifie ni aventure complète, ni rendu humain, ni cause exclusive de l’incident mémoire.

CPU : backend CPU et simulation prête observés, puis échec du garde sur couverture des compteurs GPU. Pics RAM privée 5,10 Gio, compteur dédié 1,74 Gio, partagé 0,15 Gio ; aucun plafond de consommation dépassé. À l’arrêt, un nouveau processus GPU5652 âgé de 2.025 secondes ne dispose pas encore de compteur, tandis que3944 reste rapporté. Le délai actuel de30 secondes est lié au premier candidat plutôt qu’à cette nouvelle identité GPU. Le motif de création du nouveau processus reste inconnu. Arrêt protégé, fermeture indépendante PASS, aucun PASS CPU ou comparaison déduit.

Preuve cluster-assignment-replay-preparation-041-004/paired-review.json. Suite : corriger et tester le délai de couverture par identité GPU, conserver une détection bornée des compteurs manquants, puis rejouer CPU. Tous les seuils de mémoire disponible restent supprimés. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — délai de publication des compteurs GPU par identité

Paire041005 préparée : délai de30 secondes appliqué à la date de création de chaque processus GPU appartenant au test. Compteur encore absent après30 secondes refusé ; identités inconnues, dupliquées ou futures refusées. Une absence totale de processus GPU après le délai de démarrage reste refusée. Mesures manquantes conservées et explicitement classées en attente/matures dans les relevés.

Tests rouges sur le garde précédent, verts sur le nouveau garde ; cas observé5652, frontière30 secondes, compteur mature manquant et identités invalides couverts. Tests de plafonds RAM/VRAM et absence de seuil de mémoire disponible verts ; syntaxe PowerShell PASS. Code de fermeture/identité inchangé ; sa régression complète n’est pas rejouée. Preuve gpu-publication-grace-20261007/proof.json. Aucun PASS runtime déduit ; Gate3 ouverte.


### 7 octobre 2026 — résultat CPU041005 et correction de fin de compteurs

CPU041005 : parcours public complet, simulation prête, backend CPU, fermeture normale et indépendante PASS ; driver0. Le garde finit en échec sur couverture GPU au dernier relevé de fermeture (aucun candidat natif, compteur17256 disparu), sans arrêt forcé et sans plafond de consommation dépassé. 67 relevés ; pics privée5,08 Gio, compteur dédié1,74 Gio, partagé0,15 Gio. Aucun PASS global déduit.

Paire041006 préparée : les compteurs absents d’un processus ne sont plus exigés si son handle noyau retenu prouve sa fin. Un processus encore vivant reste soumis au délai et au contrôle de couverture ; absence de handle ou erreur de lecture refusée. Tests ciblés rouges/verts, vraie sortie naturelle d’un helper, délai par identité et plafonds de consommation PASS. Identité/fermeture inchangées. Preuve gpu-terminal-counter-20261007/proof.json. Rejeu CPU requis ; Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — comparaison appariée041006 PASS au chargement/repos

Même candidat, fixture restaurée42, helpers, observateurs et garde ; seuls mode/backend/output diffèrent. Automatique : backend GPU, simulation prête,66 relevés, pics privée4,73 Gio, compteur GPU dédié1,47 Gio, partagé0,15 Gio. CPU : backend CPU, simulation prête,67 relevés, pics privée5,11 Gio, compteur dédié1,75 Gio, partagé0,15 Gio. Deux commandes, revues et fermetures indépendantes PASS ; aucun arrêt forcé ni déclenchement du garde.

Les seuils de mémoire disponible restent supprimés à la demande du propriétaire ; plafonds de consommation privée6/dédié4/partagé2 Gio conservés. Corrections du délai par création GPU et de la fin par handle retenu testées et exercées par ces rejeux. Anciennes tentatives en échec conservées.

Preuve cluster-assignment-replay-preparation-041-006/paired-root-review.json. Un seul essai de chargement/repos par mode : ni cache froid identique, ni stabilité longue, ni cause exclusive de l’incident20Go RAM/16Go VRAM, ni performance/rendu humain qualifiés. Compteurs par processus susceptibles de double comptage ; adapter global incluant les autres applications. Cette fixture négative ne prouve pas une aventure complète.

Suite : parcours Studio complet R3-A/B/C et P01–P05 sur candidat courant, interactions feu/eau/poussée et conséquences sauvegardées, décision ADR0021/intégration principale, CI réelle au commit candidat, contraintes de distribution et revue humaine. Aucune publication implicite. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — checkpoint d’aventure revalidé dans Studio sur041

Checkpoint historique d’aventure restauré avec SHA global archive et238 fichiers vérifiés ; révision40,22 entités, inventaire joueur24, gardien100. Distinct de la fixture négative42 utilisée pour les mesures mémoire. Archive et jonction d’origine préservées ; seul le chemin de la fixture est adapté dans une copie du ledger parent, avec provenance explicite.

Sur candidat041003 actuel : lecture publique des22 entités, noms/positions/statistiques/dialogue et propriétés physiques vérifiés indépendamment ; Save Studio acquitté ; archive et head complets inchangés, sources préservées, fermeture normale et indépendante PASS. Garde sans déclenchement, six relevés. Aucune nouvelle création ou partie jouée, aucun résultat runtime historique hérité, aucune acceptation visuelle déduite.

Preuve public-checkpoint-revalidation-preparation-041-001/root-review.json. Fixture revalidée disponible pour les scénarios environnementaux et la quête complète. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — premier rejeu de rive041 et pilotage des grands déplacements

Rejeu ShorePush041001 en échec sur délai global360 secondes du trajet : passage de rive traversé, arrivée vers le point29, mais budget restant2 secondes insuffisant pour la sous-étape suivante. Ni poussée ni extinction validées. 178 relevés ; pics privée5,68 Gio, compteur GPU dédié2,19 Gio ; garde normal sans déclenchement, fermeture indépendante PASS. 65 fichiers de source préservés et sauvegardes cohérentes ; preuves public-shore-preparation-041-002/root-review.json et route-analysis.json.

Nouvelle préparation041003 : les grands déplacements utilisent la vitesse effectivement mesurée pour viser la moitié de la distance restante, sans dépasser600ms et sans passer sous la cadence observée. Trajet, budget360 secondes, tolérances et trois observations fraîches stables au sol inchangés ; contrôle précis de contact et fermeture inchangés. Test rouge puis tests verts du feedback et autotests des helpers. Rejeu encore requis, aucun PASS runtime déduit. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — obstacle physique après la passerelle et contournement041004

Le rejeu ShorePush041002 termine en échec sur la sous-étape28/2 (30 secondes), avant poussée et extinction. 143 relevés, garde sans déclenchement ni arrêt forcé ; pics privée5,74 Gio et compteur GPU dédié2,31 Gio. Fermeture indépendante PASS, 65 fichiers source préservés, sauvegardes cohérentes. La limite de RAM disponible reste supprimée (seuil null, admission et garde sans seuil disponible). Preuve : public-shore-preparation-041-003/root-review.json.

Le joueur reste vers X52,24/Z−62,18 malgré les pulses acquittés. Un contact observé à X52,35/Z−61,60 correspond spatialement au casier beach-prop16 du descripteur exact du candidat (ceci ne prouve pas son identité ECS). Le segment nominal croise les enveloppes conservatrices des casiers15/16 et de la caisse18. Préparation041004 : contournement par X49/Z−65,21 puis X49/Z−59, retour X50/Z−55. Aucun croisement avec les boîtes/cylindres beach-props, après majoration XZ de0,91m (rayon joueur0,30 + tolérance0,60 + offset0,01). Cette vérification ne couvre pas toute la géométrie ni la praticabilité runtime.

Seul le trajet du harnais change ; carte, collisions, candidat, commandes publiques, cadence, contact précis, critères de sauvegarde/reprise et délai360 secondes sont inchangés. Autotests et analyse syntaxique PASS. Rejeu lancé, qualification en attente de résultat et revue indépendante. Preuve : public-shore-preparation-041-004/harness-proof.json. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — récupération publique du focus041005

Rejeu ShorePush041003 terminé en échec avant la validation du contournement : perte du focus clavier du canvas pendant le déplacement. Origine de cette perte non établie. 39 relevés, sans déclenchement mémoire ni arrêt forcé ; pics privée4,70 Gio et compteur GPU dédié1,60 Gio. Fermeture indépendante PASS ; 65 fichiers source préservés et sauvegardes cohérentes. Aucun PASS poussée/extinction. Preuve : public-shore-preparation-041-004/root-review.json.

Préparation041005 : récupération par relâchement public WASD, clic public existant sur le canvas et nouvelle observation prête/au sol/entrée active ; la stabilité est réinitialisée. Délai existant conservé, état invalide ou délai expiré refusé. Tests rouge ancien helper puis vert nouveau helper : ordre des actions, refus de chacun des quatre états invalides, aucune action après expiration ; régression cadence et autotests PASS. Carte, contenu, collisions, contournement et critères inchangés. Rejeu lancé ; son résultat reste à qualifier. Preuve : public-shore-preparation-041-005/harness-proof.json. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — fin du trajet proche, délai expiré puis plafond privé041005

Rejeu ShorePush041004 arrêté : le trajet atteint la dernière portion (waypoint32/3, X42,80/Z−35,57, distance restante2,22m), après50 arrivées intermédiaires. Le contournement des casiers est franchi. Le délai global360 secondes ne laisse que3 secondes à la dernière sous-étape ; son échec apparaît dans le transcript avant l’arrêt mémoire. 1924 entrées de journal, dont162 pulses et1046 observations de centrage. Aucun événement de récupération du focus n’a été requis pendant ce rejeu.

Pendant la collecte de l’échec, le garde arrête les processus à6,12 Gio de privée cumulée ; compteur GPU dédié2,73 Gio. Le pic privé est porté surtout par les deux processus WebView (environ3,30 et1,75 Gio), le natif autour1,01 Gio. La cause précise de cette hausse n’est pas établie : ni le journal PowerShell ni le moteur GPU ne sont désignés comme cause exclusive. Seuil de RAM disponible toujours désactivé. Fermeture indépendante PASS après protection ; sources65 fichiers, harnais et entrées figées inchangés. Le ledger final manque, aucune preuve poussée/extinction/sauvegarde-reprise PASS. Preuve : public-shore-preparation-041-005/abort-root-review.json.

Le pilote attend trois observations stables entre les corrections grossières, en plus des trois requises à chaque arrivée. Prochaine correction : une observation publique fraîche et immobile peut autoriser la correction suivante ; les trois observations stables d’arrivée, la tolérance, le trajet et le délai restent requis. Le scénario ForestThrow est préparé séparément (public-forest-throw-preparation-041-001/preparation-proof.json), NON EXÉCUTÉ, sans héritage de PASS. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — correction grossière depuis une observation fraîche041006

Préparation041006 et lancement réel : après un premier pulse, une observation publique fraîche, prête, focalisée, au sol et immobile permet la correction grossière suivante hors tolérance. La première correction garde son attente initiale ; l’arrivée garde trois observations fraîches stables dans0,60m. Rejet des ticks dupliqués, contact précis, trajet, délai360 secondes et garde6/4/2 Gio inchangés ; aucun seuil de RAM disponible. Test comportemental rouge ancien contrôleur puis vert nouveau : second pulse à la première observation après relâchement, arrivée seulement après les trois observations stables d’origine. Régressions cadence/focus et autotests PASS. Preuve : public-shore-preparation-041-006/harness-proof.json. Rejeu actif, résultat à vérifier, aucun PASS runtime déduit. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — trajet de rive terminé041006, mémoire GPU-processus encore ouverte

Rejeu ShorePush041005 : parcours de rive terminé en282.79 secondes, dans le délai360 secondes. 51 arrivées vérifiées dans0,60m, avec les trois observations publiques fraîches stables au sol requises. Sauvegardes before/push-before nouvelles, acquittées, cohérentes et vérifiées par SHA ; le garde intervient ensuite, avant validation de poussée/extinction. Preuve bornée : public-shore-preparation-041-006/route-save-root-review.json.

Arrêt de protection privée à6,05 Gio ; le processus GPU WebView PID28648 compte3,28 Gio privés, le second WebView1,73 Gio, le natif0,97 Gio. Aucun seuil de RAM disponible. Fermeture indépendante PASS, sources65 fichiers et harnais préservés. Ledger final et état public d’échec absents après arrêt : aucune qualification du flux ni de la mémoire. Cause de croissance encore à diagnostiquer ; ne pas attribuer les bytes privés GPU-processus à de la VRAM physique exclusive. Preuve : public-shore-preparation-041-006/abort-root-review.json.

Lecteur complémentaire de reçus préparé : push/ignite/extinguish nouveaux, joueur/objet exacts, chronologie entre snapshots et mêmes reçus après Resume ; tests synthétiques négatifs PASS, NON EXÉCUTÉ sur un flux runtime complet. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — observation des allocations pendant le parcours041

Diagnostic préparé et lancé sur le même candidat041003 et le même checkpoint public revision40 : observateur WebGPU repris du contrôle apparié006, installé avant ouverture de zone ; captures toutes les cinq secondes lors des lectures publiques. Il mesure créations API cumulées, estimations de tailles, destructions explicites et fences de soumission ; ce ne sont pas des mesures de mémoire résidente ni une preuve de libération par GC. Aucun handle GPU conservé par l’observateur (WeakMap pour métadonnées). Deux appels CDP supplémentaires à chaque capture ; ce coût peut influencer la durée du parcours.

Carte, collision, fonctionnalités GPU, cadence de correction, tolérances, délai360 secondes et garde6/4/2 Gio inchangés. Aucun seuil de RAM disponible. Régressions déplacement/focus/cadence, autotests et syntaxe PASS. Preuve : shore-allocation-diagnostic-preparation-041-001/harness-proof.json. Le backend réellement observé, les allocations et l’issue du rejeu restent à examiner ; aucune cause mémoire ni qualification déduite de la préparation. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — diagnostic allocations du parcours041 terminé

Diagnostic réel terminé par garde privée à6,13 Gio, avant qualification des interactions ; fermeture indépendante PASS, 65 fichiers source et tous les pins préservés. 57 observations, dont56 prêtes, backend GPU réellement observé ; aucune erreur GPU non capturée, observateur ou fence. Depuis la première observation prête, aucune recréation des buffers labellisés de vertices/indices du clustering ; ceci ne prouve pas tous les cycles de vie GPU.

43138 créations de buffers sans label (estimations cumulées336432504 octets), et grossissements de slabs de meshes (environ83 Mio cumulés). Parmi les tailles suivies, les uniforms80/176 octets recréés ont des piles de matériaux animés FishMaterial/FishStandMaterial et autres ; leurs bytes nominaux n’expliquent pas seuls le pic. Le suivi des64 premières formes manque17550 créations supplémentaires : élargir la couverture des tailles/piles avant conclusion causale. Le dernier fence demandé9675 est accompli sur9686 soumissions ; une file GPU entièrement bloquée n’est pas observée. GC ou mémoire résidente des ressources non mesurés. Preuve : shore-allocation-diagnostic-preparation-041-001/root-review.json. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — couverture complète des tailles de buffers, diagnostic041002

Observateur v4 testé sur les tailles tardives, comptages, forwarding des arguments/retours/erreurs et destruction explicite idempotente ; déplacements/focus/cadence inchangés et régressions PASS. Totaux de chaque observation réconciliés entre classes, labels et compteur global. Jusqu’à56 classes fixes, quatre échantillons récents par classe ; le dossier de préparation annonçait55, corrigé ici pour l’arrondi IEEE de log2(MAX_SAFE_INTEGER). Aucune rétention forte de ressource par cette observation. Rendu, carte, gameplay, délais et garde6/4/2 Gio inchangés ; aucun seuil de RAM disponible.

Essai réel terminal : code 1, garde MONITORED_DRIVER_TERMINAL, 25 observations, 24 prêtes en run1. Fermeture indépendante PASS ; 65 fichiers source et pins préservés. Pic privé 5.18 Gio ; créations nominales de buffers depuis prêt 268792640 octets. Ces octets cumulés ne sont pas une mesure de résidence. Les tailles/piles sont dans shore-allocation-diagnostic-preparation-041-002/root-review.json ; aucune cause exclusive, correction mémoire ou qualification de boucle RPG n’en est déduite. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — appui contradictoire pendant le diagnostic041002

Échec réel au waypoint5/step1 : délai local30 secondes et tolérance0,6 m conservés. Après une impulsion publique S200 ms relâchée, 138 lectures attendent le retour d’appui sans envoyer de touche. Trois lectures espacées confirment Rapier grounded=true, body grounded=false ; vitesse verticale −2,943 puis−141,263 puis−280,075 m/s, ancre restant vers42,2/3,9/−107,3. Aucun plafond mémoire déclenché, fermeture normale puis indépendante PASS. Le diagnostic ne couvre donc pas la durée du parcours complet et son pic5,19 Gio ne démontre aucune amélioration mémoire.

Le chemin walking_support exige le témoin ray/capsule en plus de l’appui KCC ; reproduire cette divergence avec les témoins réels avant correction. Les positions ECS observées des contacts ne valent pas points de contact. Preuve : shore-allocation-diagnostic-preparation-041-002/support-stall-root-review.json. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — appui de capsule au bord des planches, source042

Le contrôle d’entrée200 ms à60 images/s passe sans correction : il ne reproduit pas l’incident. La pose publique relâchée42,38227/3,952249/−107,268234 reproduit ensuite le défaut aux deux cadences (un ou cinq pas physiques par image) : contacts source praticables, rayon central sur terrain plus bas, appui source refusé et vitesse verticale jusqu’à−294 m/s. L’amorçage initial annulait la vitesse : ces deux cas rouges commencent au repos ; ils ne prouvent pas une trajectoire complète identique à la scène. La première tentative était une erreur de typage du test, conservée et distincte de ces échecs physiques.

Correction dans une copie isolée de041 : court balayage de la sphère inférieure de la capsule lorsque le rayon manque l’appui. Pente admissible, tolérance6 cm, appui KCC et rejet de vitesse ascendante conservés ; terrain/profondeur d’eau inchangés. Distance d’impact strictement positive et bornée, exclusion du joueur et des sensors ; tests de sol éloigné, pénétration et pente impraticable. Les quatre contrôles de pose finale (repos/vitesse observée après synchronisation, deux cadences) passent ; tous les 35 tests natifs Tidewater ciblés passent. Les exécutables mesurés rouge/vert sont archivés et hashés. Aucun autre fichier de production ni fork/lockfile modifié.

Preuve : boardwalk-released-support-preparation-042-001/root-review.json. Clippy strict, suite moteur complète, construction WASM/Studio et rejeu public restent requis. Aucune correction de consommation mémoire ou qualification de boucle RPG déduite des tests natifs. Travail primaire préservé, aucune publication. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — validation native complète du moteur042

Sur les mêmes sources figées et le même exécutable natif archivé de la correction d’appui : formatage workspace PASS ; suite lib moteur complète731 PASS /0 FAIL /16 ignorés /0 filtrés ; Clippy natif moteur lib+tests avec−D warnings PASS. Vérification indépendante des logs, exécutable et2717 fichiers source inchangés. Clippy workspace/all-targets et WASM ne sont pas déduits de ce contrôle de sous-système.

Preuve : lower-capsule-support-native-validation-042-001/root-review.json. La correction reste isolée ; construction du candidat Studio/WASM et rejeu public de la même aventure Tidewater encore requis, avec garde de consommation6/4/2 Gio et sans seuil de RAM disponible. Ni cause mémoire ni réparation mémoire démontrée. Gate3 OUVERTE — NON QUALIFIÉE.

### 7 octobre 2026 — checkpoint042 et reconstruction Studio

Checkpoint local isolé042 propre, HEAD `4ea116d00f1d792132f9fa5f6ac8d308d52cf24b` : seuls `tidewater_player.rs` et ses tests changent depuis041 ; les2717 fichiers correspondent aux pins de la validation native. Les réglages locaux de fins de ligne et la réinclusion du Cargo.lock upstream déjà suivi corrigent uniquement la construction de l’index du clone ; aucun fichier testé ni lockfile n’a changé. Preuve : lower-support-source042-checkpoint-001/proof.json.

Le garde CPU dérivé et ses modèles de lancement retirent aussi les seuils disponibles16 Gio à l’admission et8 Gio pendant la compilation. Tests de politique PASS avec disponibilité0/−1/1/8/16 Gio, protections de consommation et disque conservées, syntaxe PASS. Les scripts et résultats historiques exécutés restent archivés. Preuve : cpu-available-memory-gate-removal-20261007/proof.json.

Reconstruction WASM/navigation-worker puis Studio native lancée dans lower-support-candidate042-build-preparation-001, mêmes profils/features et mêmes3000 imports/11 fichiers kit ; les259 entrées acquises sont vérifiées sur leurs copies conservées. Aucun minimum de RAM disponible ne bloque cet essai. Compilation et rejeu public restent à examiner ; aucun PASS de construction, de mémoire ou de parcours déduit du lancement. Dépôt primaire préservé, aucune publication. Gate3 OUVERTE — NON QUALIFIÉE.


### 7 octobre 2026 — candidat042 construit et aventure relue dans Studio

WASM/frontend, worker navigation et paquet natif construits avec les mêmes profils/features ; revue indépendante PASS sur2717 sources,3000 imports,11 fichiers kit et259 entrées acquises conservées. Les sources restent propres et byte-identiques aux pins042 validés. Exécutable dc1428fa, WASM6b061f99, worker aa3072e6 ; hashes complets dans integrated-studio-candidate-root-review-042001/proof.json. Fermeture indépendante de la compilation PASS, aucun arrêt de protection, aucun seuil de RAM disponible. Avis Cargo de compatibilité future concernant proc-macro-error2 conservé dans le log ; aucune compatibilité avec une future version de Rust ni équivalence de performance déduite.

La première admission ShorePush042 refuse le ledger public041 avant ouverture du candidat : identité du candidat parent différente. Aucun natif lancé, aucun trip mémoire, entrées inchangées, fermeture indépendante PASS. Preuve shore-lower-support-preparation-042-001/preflight-root-review.json. Le refus reste archivé ; le contrôle d’identité n’est pas retiré.

Revalidation réelle depuis le checkpoint public041 : ouverture des22 entités dans Studio042, valeurs publiques vérifiées, Save acquitté, head et objet complets inchangés à révision40.65 fichiers source préservés, fermeture normale et indépendante PASS. Capture UI inspectée ; elle ne qualifie pas le rendu3D. Aucun PASS de parcours ou de nouvelle création hérité. Preuve public-checkpoint-revalidation-preparation-042-001/root-review.json.

Nouveau rejeu ShorePush préparé et lancé dans shore-lower-support-preparation-042-002, avec ce ledger public042 concordant. Harnais, parcours, tolérances, délais et graphismes inchangés ; garde de consommation6/4/2 Gio conservée, sans minimum de RAM disponible. Son issue reste à examiner. Correction isolée, aucune application primaire ni publication. Gate3 OUVERTE — NON QUALIFIÉE.

## 7 octobre 2026 — deux traversées complètes, consommation encore non qualifiée

Le candidat042 traverse le parcours côtier complet deux fois : 52 arrivées en 260,35 s, puis 51 en 261,12 s. Les 33 points du parcours et leurs sous-étapes sont vérifiés avec appui, ticks frais et tolérance inchangés. Le blocage d’appui observé sur le candidat041 au point 5 ne se reproduit pas dans ces essais. Cela ne qualifie pas encore la boucle RPG complète.

Les essais sont interrompus par le plafond de consommation privée des seuls processus d’essai (6,13 et 6,11 Gio), sans seuil de RAM système disponible. La poussée, le feu, l’extinction et la reprise restent non qualifiés. Les deux fermetures indépendantes des identités de processus passent ; les mentions de descendants encore vivants au terminal du garde restent conservées dans ses rapports.

Le diagnostic v5 conserve les allocations tardives : buffers de stockage sans label, environ 3,37 Mo, usage 136. Les anciens échantillons de démarrage ne permettent donc pas d’attribuer cette croissance aux slabs de maillage. Le relevé s’arrête au wrapper RenderDevice::create_buffer ; l’observateur v6, testé par un cas rouge/vert, conserve temporairement 24 frames et restaure immédiatement Error.stackTraceLimit. Son coût supplémentaire reste non mesuré. Un nouveau replay est lancé pour identifier le système appelant ; aucune correction du rendu ni qualification mémoire n’est encore acquise.

Preuves locales : `.gate3-evidence/20261002/shore-lower-support-preparation-042-002/root-review.json`, `shore-large-buffer-diagnostic-preparation-042-001/root-review.json`, `large-buffer-deep-stack-preparation-042-001/proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.

## 7 octobre 2026 — appelant GPU identifié, correction de capacité isolée

Le diagnostic v6 est terminé. La consommation privée atteint 6,06 Gio et le garde arrête ses propres processus ; la fermeture indépendante passe. Le journal compte 51 arrivées mais aucun acquittement de fin de route : cette tentative ne constitue pas une troisième traversée complète validée. La boucle poussée/extinction/reprise reste non qualifiée.

Les quatre derniers échantillons de gros buffers sans label (3 376 032 à 3 376 384 octets, usage 136) pointent vers `write_batched_instance_buffers::<MeshPipeline>`, closure de préparation des buffers par phase. La source appelle `data_buffer.write_buffer(render_device)` sur `UninitBufferVec<MeshUniform>` ; la réservation upstream croît exactement avec la longueur logique. Les deux fichiers Bevy_render inspectés sont identiques à l’archive locale dont le checksum correspond au Cargo.lock. Cela identifie ces allocations, sans prouver une cause exclusive de toute la consommation.

Source043 est une copie isolée de source042. Le fork bevy_pbr existant réserve une capacité géométrique bornée par les limites du device avant l’écriture upstream. Les longueurs logiques, phases, shaders, assets et réglages sont conservés. Trois fichiers changent dans le fork, dont sa notice ; aucun nouveau fork ni changement de dépendance. Cinq tests autonomes de politique passent (croissance progressive, réutilisation après réduction, limites et overflow). Le cas rouge utilise un contrefactuel de la réservation exacte, pas une reproduction GPU. Les tests compilés dans bevy_pbr sont lancés ; aucune réutilisation GPU, amélioration mémoire ou parité de rendu n’est encore qualifiée.

Preuves : `.gate3-evidence/20261002/shore-deep-buffer-diagnostic-preparation-042-001/late-buffer-caller-review.json`, `phase-instance-capacity-preparation-043-001/proof.json`, `phase-instance-capacity-native-validation-043-001/preparation-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; le candidat principal et ses modifications existantes sont préservés, sans publication.

### Contrôle GPU ciblé préparé pour la correction source043

Un test matériel natif DX12 est préparé séparément, sans modifier source043 pendant sa compilation. Il prévoit un contre-exemple upstream réel sur 20 petites croissances, puis la réutilisation des identités de buffers dans le système adapté sur 250 variations, deux phases indépendantes, une réduction à zéro et un dépassement de capacité. L’adaptateur CPU est refusé. Seule la syntaxe et le formatage sont vérifiés à ce stade : le test GPU n’est ni compilé ni exécuté. La revue statique confirme l’ordre avant l’écriture upstream et le comptage des work items du shader ; elle ne prouve ni rendu, ni réutilisation matérielle, ni effet mémoire. Preuves : `.gate3-evidence/20261002/phase-instance-gpu-test-preparation-043-001/proof.json` et `static-review.json`.

### Résultat source043 et reprise de compilation source044

La compilation du package source043 échoue avant exécution des tests : le passage direct de rustfmt ajoute deux virgules aux setters de la macro upstream `impl_atomic_pod!`, refusées par sa grammaire. Le garde termine normalement, sans seuil de RAM disponible ni arrêt de protection ; la fermeture indépendante passe. Les cinq tests autonomes restent acquis à leur portée, mais aucun PASS natif de package source043 n’est déclaré.

Source044 conserve cet essai et restaure la syntaxe upstream dans une nouvelle copie. Une annotation `rustfmt::skip` protège uniquement cette macro DSL des virgules invalides ; les contrôles de compilation et de tests ne sont pas modifiés. La configuration de formatage explicite du fork est utilisée. Le test matériel préparé est intégré sous cfg(test) natif et reste ignoré par défaut. Les nouveaux tests de package sont lancés ; aucun test GPU ni nouveau candidat Studio n’est encore qualifié. Preuves : `.gate3-evidence/20261002/phase-instance-capacity-native-validation-043-001/launch-proof.json` et `phase-instance-capacity-native-validation-044-001/preparation-proof.json`.

### Source044 : cinq tests Bevy et réutilisation matérielle DX12 vérifiés

La copie réparée source044 compile. Les cinq tests de capacité passent réellement dans bevy_pbr (zéro échec, zéro ignoré, dix filtrés). L’exécutable exécuté est archivé et hashé ; la fermeture indépendante passe. Le test matériel ciblé passe ensuite sur AMD Radeon RX 7800 XT, backend DX12, GPU discret : vingt petites hausses produisent vingt allocations upstream, tandis que le système corrigé conserve une même identité de buffer sur 250 variations. Deux phases gardent leurs buffers indépendants ; réduction, longueur nulle et dépassement de capacité sont vérifiés sans altérer les longueurs logiques.

Le garde termine normalement et la fermeture indépendante passe. Le test dure 1,06 s ; ses deux échantillons incluent une identité terminale mais pas une mesure complète de sa durée de vie. Le pic mémoire reste expressément non qualifié. Ce PASS fonctionnel natif ne prouve ni libération physique, ni pixels WebGPU, ni performance, ni consommation de Tidewater. Aucune cause exclusive de la hausse mémoire n’est déclarée.

La validation source044 de formatage, Clippy strict bevy_pbr, suite complète forge_engine et Clippy strict moteur est lancée sous protection de consommation, sans seuil de RAM disponible. Les suites et le candidat Studio restent à qualifier. Preuves locales : `.gate3-evidence/20261002/phase-instance-capacity-native-validation-044-001/root-review.json`, `phase-instance-native-gpu-validation-044-001/root-review.json`, `phase-capacity-engine-validation-044-001/preparation-proof.json`. La Gate3 conserve tout son périmètre et reste OUVERTE — NON QUALIFIÉE ; aucune publication ni adoption sur le candidat principal.

## 7 octobre 2026 — source045 validée au natif, reconstruction Studio

Le contrôle strict source044 a relevé six diagnostics : conversion u64 redondante dans le système, annotations/imports/qualification et conversion dans le test matériel. Source045 les corrige dans une nouvelle copie sans changer assertions, limites, shaders ni longueurs logiques. L’échec source044 et sa fermeture vérifiée sont conservés.

Sur source045 : formatage workspace PASS ; cinq tests de capacité PASS ; Clippy bevy_pbr lib/tests strict PASS ; suite forge_engine lib complète 731 PASS, zéro échec, 16 ignorés, zéro filtré (119,35 s) ; Clippy forge_engine lib/tests strict PASS. Les exécutables réellement exécutés sont archivés et hashés ; la fermeture indépendante passe. Ces contrôles ne sont pas une qualification de tous les targets du workspace.

Le test matériel est rejoué sur l’archive exacte source045 : un PASS DX12 sur AMD Radeon RX 7800 XT (0,52 s), comparaison vingt allocations upstream contre une allocation adaptée pour 250 variations, deux phases indépendantes, réduction/vide/franchissement de capacité vérifiés. Sa courte durée ne qualifie pas le pic mémoire, le rendu WebGPU ou la scène complète. La fermeture indépendante passe.

Un checkpoint privé propre `e2f47858138c0845677d382f206bd60573190c96` contient les 2719 fichiers épinglés. Quatre fichiers du fork diffèrent du checkpoint042 ; les octets source restent identiques avant/après le checkpoint. Aucun Git principal modifié ni publication. La reconstruction Studio/WASM/native est lancée en conservant assets, profils, features, réglages et budgets de consommation. Aucun seuil de RAM disponible. Le replay Tidewater et toute la qualification Gate3 restent à faire.

Preuves locales : `.gate3-evidence/20261002/phase-capacity-engine-validation-045-001/root-review.json`, `phase-instance-native-gpu-validation-045-001/root-review.json`, `phase-capacity-source045-checkpoint-001/proof.json`, `phase-capacity-candidate045-build-preparation-001/preparation-proof.json`. Gate3 reste OUVERTE — NON QUALIFIÉE.


### Reconstruction frontend045 terminée, replay comparatif préparé

La compilation WASM et celle du worker de navigation source045 passent ; elles restent des preuves BUILD_ONLY. Le build natif est lancé sur le même checkpoint. Le lecteur de revue du candidat est corrigé avant utilisation : il vérifie les quatre fichiers changés depuis source042 et nomme le candidat045, au lieu des anciennes références042. Les scripts de préparation de la revalidation publique et du replay de rive passent leur contrôle de syntaxe, sans exécution de scène ni PASS runtime.

Le replay comparatif prévoit de réutiliser intégralement le harness v6 déjà exécuté : mêmes 33 points, contrôles, délais, assets, réglages graphiques, observateur et plafonds de consommation. Il exige au préalable la revue du candidat045 et une nouvelle ouverture/sauvegarde publique de son aventure ; aucun résultat runtime précédent ne sera hérité. Les seuils de RAM disponible restent supprimés. Preuves : `.gate3-evidence/20261002/source-snapshot-001-current.json`, champ `source045_replay_preparation`, et `tidewater-integrated-primary-045-frontend-001/frontend-build-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.


### Candidat045 construit et revu, revalidation publique lancée

Le build frontend, worker et natif source045 termine avec succès ; la fermeture indépendante des identités passe. La revue byte à byte vérifie 2 719 fichiers source, 3 000 imports, 11 fichiers du kit et 259 entrées conservées, ainsi que les binaires embarqués et le checkpoint privé propre. SHA-256 natif : `22e24fdc527f3b5f6055757cafba9e6b3f1626e8cf203c67379dec0105504626`. La preuve reste BUILD_ONLY : ni rendu, ni mémoire du parcours, ni aventure complète ne sont qualifiés.

La revalidation publique des 22 entités et de la sauvegarde est lancée avec ce candidat dans une nouvelle copie isolée, à partir du checkpoint réellement sauvegardé par Studio042. Le résultat reste à observer. Preuve build : `.gate3-evidence/20261002/integrated-studio-candidate-root-review-045001/proof.json` ; préparation publique : `public-checkpoint-revalidation-preparation-045-001/freeze-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; le principal est préservé, sans publication Git.


### Aventure045 relue et sauvegardée publiquement, parcours comparatif en cours

La revalidation publique045 et sa fermeture indépendante passent. La revue contrôle les 22 entités par les formulaires publics, la révision40, les 65 fichiers source préservés et le head sauvegardé identique. La capture après sauvegarde est inspectée : le viewport est vide ; cette preuve qualifie uniquement la lecture/sauvegarde, pas la scène 3D ni l’aventure jouée. Preuve : `.gate3-evidence/20261002/public-checkpoint-revalidation-preparation-045-001/root-review.json`.

Le parcours ShorePush045 est lancé sur ce checkpoint revalidé avec le harness v6 inchangé. Les résultats de route, poussée, feu, extinction, sauvegarde/reprise et consommation restent à mesurer. Préparation et identité du lancement : `shore-phase-capacity-preparation-045-001/freeze-proof.json` et `started-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.


### Parcours045 complet, consommation encore hors plafond

La route045 passe : 33 points, 51 arrivées et 261,52 secondes, avec appui, ticks frais et tolérance inchangés. Le garde arrête ensuite les seuls processus du test à 6,09 Gio de mémoire privée ; le compteur dédié GPU atteint 2,70 Gio. Aucun seuil de RAM disponible n’est appliqué. La fermeture indépendante passe ; les mentions de descendants au terminal restent conservées. La poussée, le feu, l’extinction et la reprise ne sont pas qualifiés.

La fenêtre finale des observations (12:34:01,596 à 12:34:22,906 UTC) ne présente aucune nouvelle création de texture. Elle compte 16 514 nouveaux buffers sans label pour seulement 2 313 800 octets nominaux, plus 37 readback buffers. De nombreux petits buffers sont mappedAtCreation ; leurs piles v6 atteignent DeviceExt::create_buffer_init mais pas le système appelant. Ces compteurs ne prouvent ni résidence réelle ni cause exclusive de la hausse RAM/VRAM. Le diagnostic v7 élargit seulement les piles des petits buffers déjà échantillonnés, avec bornes et forwarding testés rouge/vert ; son coût supplémentaire reste non mesuré. Preuves : `shore-phase-capacity-preparation-045-001/root-review.json`, `late-small-buffer-review.json`, `small-buffer-deep-stack-preparation-045-001/proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.


### Diagnostic045 des appelants des petits buffers lancé

Un replay045 est lancé avec le seul observateur v7 substitué dans une nouvelle copie du harness. Le candidat, les 33 points, les contrôles, délais, assets, réglages graphiques et plafonds sont identiques. L’échantillonnage des petits buffers reste périodique ; les piles échantillonnées atteignent désormais 24 frames/8 192 caractères, avec quatre échantillons par classe. Aucun PASS de gameplay ou mémoire n’est hérité ; le coût de diagnostic supplémentaire reste non mesuré. Préparation : `.gate3-evidence/20261002/shore-small-buffer-caller-preparation-045-001/harness-proof.json` et `started-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.


### Appelants des petits buffers045 identifiés, correction complète encore nécessaire

Le replay diagnostique v7 termine également après une route complète : 33 points, 51 arrivées, 262,23 secondes. Le garde arrête les seuls processus de l’essai à 6,02 Gio privés, avant la poussée ; la fermeture indépendante passe. Aucun seuil de RAM disponible n’a été appliqué et aucune boucle poussée/extinction/reprise n’est qualifiée.

Les piles échantillonnées des petits buffers remontent à FishMaterial et SkyMaterial, dans AsBindGroup::unprepared_bind_group, via RenderDevice::create_buffer_with_data et DeviceExt::create_buffer_init. Cela établit leurs appelants, pas la cause exclusive de la consommation ni les mutations CPU déclenchantes. Les sources inspectées disposent déjà d’horloges/récepteurs partagés pour les poissons et de comparaisons avant mutation ; supprimer arbitrairement leurs mises à jour ou réduire la qualité ne constituerait pas une correction qualifiée. Il reste à suivre les changements réellement nécessaires et conserver les ressources GPU avec leurs valeurs exactes. Preuves : `shore-small-buffer-caller-preparation-045-001/root-review.json`, `small-buffer-caller-terminal-review.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.


### Source046 : uniformes OwnedData par slot, validation native lancée

Source046 est une nouvelle copie isolée de source045, avec 2 720 fichiers et six fichiers changés, dont un nouveau test GPU. FishMaterial et SkyMaterial optent dans le chemin OwnedData upstream : mêmes champs, types ShaderType à un champ, numéros de bindings et WGSL. Le fork PBR conserve les uniform buffers de chaque binding dans le même slot non-bindless, met à jour leurs octets par RenderQueue et invalide la réutilisation si taille, layout ou device diffèrent. Free retire tout l’état du slot ; aucun pool global ni nouveau fork. Les bind groups restent recréés et bindless garde son cycle upstream.

Deux tests d’octets uniformes couvrent les paramètres poisson mixtes float/u32, le padding et les tableaux du ciel ; un test de cycle de slot et un test matériel ignoré par défaut couvrent réutilisation, permutations des bindings, isolation, taille, layout, déchargement et device. Les tests GPU visent les identités et les octets de préparation CPU, pas un readback des valeurs shader ni des pixels. Le format workspace passe ; les tests complets du package PBR, du moteur et leurs Clippy stricts sont lancés. Aucun PASS natif/GPU/scène mémoire n’est encore déclaré. Preuve : `.gate3-evidence/20261002/material-uniform-engine-validation-046-001/preparation-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; le principal est préservé, sans publication Git.


### Source047 : PBR complet et Clippy strict passent, moteur en contrôle

La première compilation046 échoue dans deux assertions nouvelles : MaterialBindingId ne fournit pas PartialEq. Le résultat et la fermeture indépendante sont conservés, sans PASS natif046. Source047 ne change que ces assertions : les mêmes identités sont comparées par leurs champs group/slot. Aucun changement du code de production046.

Le package PBR047 passe 15 tests, zéro échec et deux tests matériels ignorés, puis Clippy lib/tests strict. Le binaire réellement exécuté est archivé avec SHA-256 ; les 2 720 fichiers source sont vérifiés. Les tests du moteur complet et leur Clippy restent à contrôler. Aucune exécution des deux cas GPU ni amélioration mémoire/rendu de scène n’est déclarée. Preuves : `.gate3-evidence/20261002/material-uniform-engine-validation-046-001/command-proof.json`, `material-uniform-engine-validation-047-001/pbr-progress-review.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; le principal est préservé, sans publication Git.


### Source048 : correction du type des octets dans les tests ABI

Le contrôle047 est terminé en échec de compilation des deux nouveaux tests moteur (E0282/E0283 : Vec::new ambigu entre les deux buffers encase). Ses 15 tests PBR et son Clippy PBR PASS restent acquis dans leur portée ; aucun test moteur047 ni Clippy moteur047 PASS. La fermeture indépendante des processus passe. Aucun arrêt de protection mémoire : available_memory_threshold_enabled=false, trip_reasons vide.

Source048 précise uniquement Vec::<u8>::new dans ces deux tests, sans changer le code de production047. Les 2 720 fichiers sont épinglés ; la validation native complète est lancée sous les mêmes plafonds propres aux processus, sans seuil de RAM disponible. Les résultats moteur, GPU matériel et mémoire de scène restent à contrôler. Preuves : `.gate3-evidence/20261002/material-uniform-engine-validation-047-001/command-proof.json`, `material-uniform-engine-validation-047-001/independent-identity-closure.json`, `material-uniform-engine-validation-048-001/preparation-proof.json`, `material-uniform-engine-validation-048-001/started-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucun changement principal ni publication.


### Source049 : identité de queue pour invalider les uniformes entre instances

La validation native048 passe et est relue : format, package PBR 15 PASS/2 ignorés, moteur 733 PASS/16 ignorés, Clippy strict PBR et moteur lib/tests. Les deux tests ABI passent ; binaires mesurés archivés. Le test matériel048 échoue sur Radeon RX 7800 XT / DX12 : son assertion supposait différents les identifiants wgpu de deux périphériques créés par des instances distinctes, mais ceux-ci sont égaux. Il s’arrête avant la vérification finale d’invalidation. Aucun PASS GPU048, aucune qualification mémoire ou pixels. La fermeture indépendante passe, sans arrêt de protection.

Source049 ajoute l’identité Arc de RenderQueue à la comparaison de périphérique, conservée par slot et libérée avec lui. Les clones partagent cette identité ; une nouvelle queue invalide même en cas de collision des identifiants wgpu. Le remplacement du wrapper d’une même queue peut invalider de manière conservatrice. Le test conserve le changement réel de périphérique et compare désormais les identités de queue. Le principal, les champs canoniques, les shaders et les réglages restent préservés. Validation native049 lancée, résultats et replay à contrôler. Le preparation-review.json049 explicite les métadonnées applicables et supersède les anciens champs hérités du créateur sans modifier l’artefact exécuté.

Preuves : `.gate3-evidence/20261002/material-uniform-engine-validation-048-001/root-review.json`, `material-uniform-native-gpu-validation-048-001/gpu-test.log`, `material-uniform-native-gpu-validation-048-001/independent-identity-closure.json`, `material-uniform-engine-validation-049-001/preparation-review.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication.


### Source049 : PBR passe, binaire archivé, moteur en compilation

La correction d’identité périphérique/queue049 passe les 15 tests PBR (zéro échec, deux cas matériels ignorés) et Clippy PBR lib/tests strict. Le binaire réellement exécuté est archivé avec SHA-256 ; les 2 720 fichiers source et la revue des métadonnées sont vérifiés. Le moteur complet est encore en compilation ; son résultat, Clippy moteur, le cas DX12 et le replay Tidewater restent à contrôler. Preuve : `.gate3-evidence/20261002/material-uniform-engine-validation-049-001/pbr-progress-review.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE, sans publication.


### Source049 : validation native et réutilisation DX12 des uniformes

Le contrôle049 est terminé et relu : format workspace, package PBR (15 PASS, 2 ignorés), moteur lib (733 PASS, 16 ignorés), Clippy lib/tests PBR et moteur strict passent. Les deux tests ABI ajoutés passent ; ils couvrent les octets et le padding de leurs types précis, sans qualifier tous les pixels. Les binaires réellement exécutés sont archivés et les 2 720 fichiers source vérifiés.

Le cas GPU explicite passe sur AMD Radeon RX 7800 XT / DX12 : 20 allocations de contrôle, 250 mises à jour corrigées, deux bindings et deux matériaux indépendants. Les identités des buffers et les octets CPU envoyés sont vérifiés, avec changement d’ordre, taille, layout, déchargement et périphérique. Le test ne lit pas les valeurs depuis le GPU et ne qualifie ni les pixels ni le pic mémoire sur toute la durée de la scène. Fermetures indépendantes PASS ; aucun seuil minimal de RAM disponible. Preuves : `.gate3-evidence/20261002/material-uniform-engine-validation-049-001/root-review.json`, `material-uniform-native-gpu-validation-049-001/root-review.json`. La reconstruction du candidat Studio apparié et le replay Tidewater restent nécessaires. Gate 3 reste OUVERTE — NON QUALIFIÉE, sans modification principale ni publication.


### Candidat049 : checkpoint privé propre et reconstruction Studio lancée

Le checkpoint privé049 2d59fce613dfd5e3b1ffbceca43ffb3a5190f308 conserve les 2 720 fichiers source validés, sur le parent045, avec six fichiers modifiés et aucun changement de source produit par le checkpoint. La reconstruction appariée du frontend WASM, du worker de navigation et de Studio natif est lancée ; les 3 000 imports, 11 fichiers du kit et 259 entrées acquises conservées restent soumis aux contrôles de parité. Aucun changement ni publication du checkout principal.

Preuves : `.gate3-evidence/20261002/material-uniform-source049-checkpoint-001/proof.json`, `material-uniform-candidate049-build-preparation-001/build-start-record.json`. Le build n’est pas encore terminé/relu ; chargement/sauvegarde public et replay côtier049 non exécutés. Le PASS DX12 fonctionnel ne qualifie pas les pics mémoire ou les pixels de Tidewater. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Candidat049 : frontend, worker et Studio natif appariés et relus

Le build049 et sa fermeture indépendante passent. La revue vérifie les 2 720 fichiers source, les 3 000 imports, 11 fichiers du kit et 259 entrées acquises conservées, le checkpoint privé propre et les binaires frontend/worker/natif embarqués. SHA-256 natif : `5d0f9b3e1e753e41469d2e289770553d480de011b91934f856212e638bc920ee`. Preuve : `.gate3-evidence/20261002/integrated-studio-candidate-root-review-049001/proof.json`.

Cette preuve reste BUILD_ONLY : aucun rendu, pic mémoire du parcours ou résultat d’aventure complète n’est déduit du build. La revalidation publique du checkpoint puis le replay Tidewater restent à exécuter et contrôler. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucun changement principal ni publication Git.


### Aventure049 : lecture et sauvegarde publiques du checkpoint

La revalidation publique049 et sa fermeture indépendante passent. La revue vérifie les 22 entités par les formulaires publics, la révision40, les 65 fichiers source préservés, le head sauvegardé identique et l’accusé public de sauvegarde. La capture après sauvegarde est inspectée : les formulaires sont visibles mais le viewport est vide. Preuve : `.gate3-evidence/20261002/public-checkpoint-revalidation-preparation-049-001/root-review.json`.

Cette étape qualifie uniquement la lecture/sauvegarde. Elle ne qualifie ni la scène 3D, ni la poussée/feu/eau, ni le parcours complet ou ses pics mémoire. Le replay Tidewater reste à exécuter et contrôler ; aucune acceptation visuelle humaine. Gate 3 reste OUVERTE — NON QUALIFIÉE, sans publication.


### Parcours049 : lancement public avec le harness V7 inchangé

Le replay ShorePush049 est lancé sur le checkpoint049 publiquement revalidé. Le harness et l’observateur V7 sont réutilisés byte à byte, avec le même parcours, les mêmes assets, réglages, contrôles, délais et plafonds propres aux processus. Aucun seuil minimal de RAM disponible. Preuves : `.gate3-evidence/20261002/shore-material-uniform-preparation-049-001/freeze-proof.json`, `harness-proof.json`, `start-record.json`.

Le parcours, la poussée, le feu/eau, les sauvegardes/reprises et le gain mémoire restent à observer puis relire ; aucun résultat045 n’est hérité. Gate 3 reste OUVERTE — NON QUALIFIÉE, sans publication.


### Parcours049 : poussée, extinction et reprise passent sous les mêmes plafonds

Le replay côtier049 et sa fermeture indépendante passent. La revue vérifie 33 points de route, 51 arrivées, 267.22 secondes, les ticks frais/appuis/tolérances, la poussée causale, les nouveaux reçus joueur d’allumage/extinction, les sauvegardes publiques cohérentes et les mêmes reçus après reprise dans un nouveau processus. Les 65 fichiers source et le candidat/harness restent préservés. Aucun arrêt de protection.

La comparaison V7 vérifie le même harness, driver, objet sauvegardé, parcours, réglages, assets, contrôles, délais et plafonds. Pic privé observé : 4.70 Gio, contre 6.02 Gio sur045 ; compteur GPU dédié par processus : 1.39 Gio, contre 2.65 Gio. Le contrôle045 a été arrêté après la route, tandis que049 termine poussée/extinction/reprise : durées et portées terminales différentes. Les compteurs ne prouvent pas résidence physique, cause exclusive, pixels, performance ou mémoire d’une aventure complète/longue session.

Preuves : `.gate3-evidence/20261002/shore-material-uniform-preparation-049-001/root-review.json`, `action-receipts-assessment.json`, `same-v7-memory-comparison.json`. La suite conserve tout R3-A/B/C et P01–P05, intégration propriétaire, CI exacte, distribution et revue humaine. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucun changement principal ni publication.


### Parcours049 : lancer en forêt lancé sur le checkpoint public courant

Le contrôle ForestThrow049 est lancé après la fermeture du parcours côtier, dans une nouvelle copie isolée du checkpoint publiquement revalidé. Le candidat049 et le harness V7 sont conservés ; aucun seuil minimal de RAM disponible. Traversée des neuf points de forêt, lancer, contacts/dégâts et sauvegarde/reprise restent à observer et relire ; aucun PASS côtier n’est transféré à ce scénario. Preuves : `.gate3-evidence/20261002/public-forest-throw-preparation-049-001/freeze-proof.json`, `start-record.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication.


### Forêt049-001 : interactions causales validées, fermeture non qualifiée

Le lancer en forêt termine ses neuf points (60 arrivées, 93.61 secondes), ses huit contrôles causaux, les dégâts et les sauvegardes/reprises. Le garde-fou constate toutefois des descendants WebView encore actifs après la sortie du pilote et les arrête : résultat global FAILED_CLOSED, sans qualification. Aucun plafond mémoire dépassé ; pics observés de 4.81 Gio privés et 1.47 Gio au compteur GPU dédié. La fermeture indépendante passe. Preuve : `.gate3-evidence/20261002/public-forest-throw-preparation-049-001/root-review.json`.

Le pilote du prochain essai049-002 attendra au plus dix secondes la fermeture des identités exactes observées, sans arrêt de processus, tandis que le garde-fou existant continue ses contrôles. Les tests réels de sortie retardée et de délai expiré passent ; la préservation d’un PID avec une autre date de création est simulée, sans réutilisation OS observée. Preuve : `.gate3-evidence/20261002/owned-replay-closure-drain-20261007/regression-001/proof.json`. Harness, candidat, assets, parcours et plafonds restent identiques ; aucun seuil minimal de RAM disponible. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Forêt049-002 : replay avec attente de fermeture lancé

Le second replay ForestThrow049 est lancé, avec dix secondes au plus de drainage des identités observées avant sortie du pilote. Le garde-fou GPU et ses plafonds restent inchangés. Aucun résultat causal ou mémoire n’est transféré de049-001 ; la fermeture normale et les interactions doivent être observées à nouveau. Preuves : `.gate3-evidence/20261002/public-forest-throw-preparation-049-002/freeze-proof.json`, `start-record.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Forêt049-002 : faux arrêt sur le délai de publication GPU du nouveau processus

Le replay049-002 atteint le lancer puis le démarrage de la reprise, mais le garde-fou utilise encore l’horodatage du premier candidat pour refuser l’absence de compteurs du nouveau Studio. Au refus, celui-ci existe depuis 0.921 seconde seulement. Résultat FAILED_CLOSED conservé ; fermeture indépendante PASS, aucune qualification causale ni de drainage. Les plafonds privés/GPU ne sont pas dépassés. Preuve : `.gate3-evidence/20261002/public-forest-throw-preparation-049-002/terminal-review.json`.

Le garde-fou dérivé pour049-003 applique le délai existant de trente secondes à la date de création de chaque candidat vivant vérifié. Le test reproduit le faux arrêt historique, accepte le démarrage frais, refuse un candidat mature non mesuré et vérifie les trois plafonds ainsi que l’absence de seuil de RAM disponible. Identités et nettoyage sont inchangés. Preuve : `.gate3-evidence/20261002/gpu-candidate-epoch-counter-20261007/regression-proof.json`. Aucun résultat de replay003 n’est encore acquis ; Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Forêt049-003 : replay avec attente de fermeture lancé

Le troisième replay ForestThrow049 est lancé, avec dix secondes au plus de drainage des identités observées avant sortie du pilote. Le garde-fou GPU utilise le délai corrigé par date de création du candidat ; ses plafonds restent inchangés. Aucun résultat causal ou mémoire n’est transféré des essais précédents ; la fermeture normale et les interactions doivent être observées à nouveau. Preuves : `.gate3-evidence/20261002/public-forest-throw-preparation-049-003/freeze-proof.json`, `start-record.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Forêt049-003 : fermeture et lancer/reprise passent, impact non reproduit

Le troisième essai termine la route (60 arrivées, 94.35 secondes) et la sauvegarde/reprise sans arrêt forcé. Le drainage et la fermeture indépendante passent ; les six contrôles de flux, sources, provenance, sauvegardes, lancer et persistance du lancer sont vrais. Les deux contrôles d’impact sur le gardien restent faux : aucun PASS du premier essai n’est transféré. Preuve : `.gate3-evidence/20261002/public-forest-throw-preparation-049-003/root-review.json`.

La dernière observation de l’approche montrait une distance de 0.018 m, mais encore une vitesse horizontale de 0.093 m/s. Le clic intervient ensuite à une autre position ; cela motive un contrôle stabilisé sans établir une cause physique exclusive. La boucle complète EnvironmentQuest utilisera le même point de contact et la même tolérance de 0.025 m, avec trois échantillons frais immobiles avant le clic et un échec explicite si le centrage n’aboutit pas. Un seul fichier du harness dérivé change ; moteur, contenu, observateur V7, parcours et plafonds restent identiques. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Boucle RPG049 : quête environnementale complète lancée

EnvironmentQuest049 est lancé sur une nouvelle copie isolée du checkpoint public. Les interactions publiques doivent accepter la quête, traverser la forêt, neutraliser le gardien par un impact avant tout combat, récupérer le Sceau, revenir au village, terminer la quête, sauvegarder et reprendre dans un autre processus. Le lancer attend une approche stabilisée ; son point et sa tolérance restent identiques. Les événements d’impact, les dégâts, la rupture/chute, les états persistants et les deux trajets seront relus. Preuves : `.gate3-evidence/20261002/public-environment-quest-preparation-049-001/freeze-proof.json`, `start-record.json`. Aucun résultat de cette boucle n’est acquis à ce stade ; Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Boucle RPG049-001 : les douze contrôles causaux passent, contrôle final de fermeture en échec

La revue de la boucle complète vérifie les routes aller (59 arrivées, 93.62 secondes) et retour (58 arrivées, 79.61 secondes), une approche stabilisée avant lancer, les impacts et dégâts, rupture/chute, gardien neutralisé avant combat, Sceau récupéré puis consommé, quête terminée et sauvegarde/reprise dans un nouveau processus. Les douze contrôles causaux requis sont vrais, les sources restent intactes, la fermeture indépendante passe et aucun arrêt forcé ou plafond dépassé n’est observé (pics privés 5.66 Gio, compteur GPU dédié 1.47 Gio). Preuve : `.gate3-evidence/20261002/public-environment-quest-preparation-049-001/root-review.json`.

La commande globale reste FAILED_CLOSED : le drainage refuse un ancien échantillon dont le chemin d’un processus WebView terminé est absent. Le helper dérivé consulte d’abord le handle noyau et sa date de création ; une identité vivante sans chemin reste refusée, un PID d’une autre date est préservé. Les cinq tests passent, y compris les deux cas d’image absente. Preuve : `.gate3-evidence/20261002/owned-replay-closure-drain-image-exit-20261007/regression-001/proof.json`. Un nouveau replay complet049-002 est préparé avec ce seul changement de fermeture, sans transfert de PASS. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Boucle RPG049-002 : quête environnementale complète lancée

EnvironmentQuest049-002 est lancé sur une nouvelle copie isolée du checkpoint public. Les interactions publiques doivent accepter la quête, traverser la forêt, neutraliser le gardien par un impact avant tout combat, récupérer le Sceau, revenir au village, terminer la quête, sauvegarder et reprendre dans un autre processus. Le lancer attend une approche stabilisée ; son point et sa tolérance restent identiques. Les événements d’impact, les dégâts, la rupture/chute, les états persistants et les deux trajets seront relus. Preuves : `.gate3-evidence/20261002/public-environment-quest-preparation-049-002/freeze-proof.json`, `start-record.json`. Aucun résultat de cette boucle n’est acquis à ce stade ; Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Boucle RPG049-002 : erreur du garde-fou sur un compteur GPU sans handle conservé

Le second replay est arrêté pendant la route par MEMORY_GUARD_ERROR : « Missing GPU counter has no retained owned handle ». Le garde-fou arrête les processus exacts possédés ; la fermeture indépendante passe, sources/candidat/harness restent préservés, aucun plafond privé/GPU n’est dépassé. Aucun résultat de boucle complète n’est acquis. Preuve : `.gate3-evidence/20261002/public-environment-quest-preparation-049-002/terminal-review.json`.

Le garde-fou dérivé003 n’accepte ce cas que si le noyau prouve le processus absent ou terminé ; un processus vivant sans handle conservé reste refusé, sans acquérir de propriété depuis le PID. Les tests réels de workers terminés/vivants passent, sans rôle GPU réel revendiqué. Le délai par date de création et les trois plafonds repassent également leurs tests. Preuves : `.gate3-evidence/20261002/gpu-missing-handle-terminal-counter-20261007/kernel-regression-proof.json`, `regression-proof.json`. Le prochain replay conserve candidat, harness stabilisé, parcours, helper de fermeture et plafonds. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Boucle RPG049-003 : quête environnementale complète lancée

EnvironmentQuest049-003 est lancé sur une nouvelle copie isolée du checkpoint public. Les interactions publiques doivent accepter la quête, traverser la forêt, neutraliser le gardien par un impact avant tout combat, récupérer le Sceau, revenir au village, terminer la quête, sauvegarder et reprendre dans un autre processus. Le lancer attend une approche stabilisée ; son point et sa tolérance restent identiques. Les événements d’impact, les dégâts, la rupture/chute, les états persistants et les deux trajets seront relus. Preuves : `.gate3-evidence/20261002/public-environment-quest-preparation-049-003/freeze-proof.json`, `start-record.json`. Aucun résultat de cette boucle n’est acquis à ce stade ; Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Boucle RPG049-003 : quête par impact, butin et reprise passent avec fermeture normale

Le troisième replay complet passe sa commande, son drainage, la fermeture indépendante et les douze contrôles causaux relus. La route aller comprend 59 arrivées en 91.43 secondes ; le retour, 57 arrivées en 74.44 secondes, toutes avec appui observé. Le lancer stabilisé produit des impacts/dégâts et une rupture/chute ; le gardien est neutralisé par le décor avant combat. Le Sceau est récupéré puis consommé lors de la quête terminée ; ces conséquences et les champs causaux des objets sont vérifiés après reprise dans un nouveau processus. Les 65 fichiers source sont préservés et aucun arrêt de protection n’est effectué.

Pics observés : 4.94 Gio privés et 1.46 Gio au compteur GPU dédié par processus, sous les plafonds inchangés. Ces mesures ne qualifient pas une longue session, la résidence physique, les pixels ou une cause mémoire exclusive. Les comparaisons de persistance portent sur released/destroyed/initiator, sans comparaison de pose/vitesse/in_motion. Preuves : `.gate3-evidence/20261002/public-environment-quest-preparation-049-003/root-review.json`, `terminal-record.json`.

Ce résultat qualifie cette boucle publique par impact, pas l’ensemble R3-A/B/C et P01–P05. La seconde approche feu/danger, la navigation, les retouches Studio/P02, les diagnostics/P03, l’isolation/P04, les tests complets du candidat, décisions d’intégration, CI exacte, distribution et acceptation visuelle/humaine gardent leurs preuves propres. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.


### P04 actuel049 : isolation des parties de test lancée

Le contrôle public R3-P04 est lancé après fermeture indépendante de la boucle de quête003. Il doit observer un état choisi et sa révision, déplacer/sauvegarder une simulation isolée, puis repartir du même état dans un second processus sans reprendre cette mutation ; documents authorés et sauvegardes source doivent rester intacts. Les entrées acquises sont vérifiées, le candidat049 et les plafonds sont conservés, sans seuil de RAM disponible. Preuves : `.gate3-evidence/20261002/public-replay-isolation-preparation-049-001/freeze-proof.json`, `start-record.json`. Aucun PASS d’isolation antérieur n’est hérité ; Gate 3 reste OUVERTE — NON QUALIFIÉE.


### P04 actuel049 : état choisi, révision et isolation passent

La commande P04, le drainage, la fermeture indépendante et la revue passent. Les deux processus repartent du même état choisi (40, -115), à la révision publique40. Le premier déplace le joueur et sauvegarde ; le second lance une nouvelle partie depuis cet état choisi. Archives/objets authorés, documents source et sauvegardes source restent byte à byte identiques ; le store de sauvegarde de la copie est vérifié séparément. Les deux Studio ferment normalement, sans arrêt de protection. Preuves : `.gate3-evidence/20261002/public-replay-isolation-preparation-049-001/root-review.json`, `terminal-record.json`.

Cette preuve porte sur l’isolation et la remise au même départ, sans revendiquer une physique bit-identique ni transformer le second nouveau départ en reprise de sauvegarde. Elle reste distincte de P02, navigation, CI, distribution et revue humaine. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication.


### P03 actuel049 : diagnostic de quête invalide lancé

Après fermeture indépendante du P04, le contrôle P03 est lancé sur une nouvelle copie du checkpoint public. Il doit refuser la suppression du Sceau lié à la quête, expliquer objet/quête/quantité et garder la cible sélectionnée pour correction, sans committer un état invalide ni une nouvelle révision. Preuves : `.gate3-evidence/20261002/public-invalid-quest-preparation-049-001/freeze-proof.json`, `start-record.json`. Aucun résultat antérieur n’est hérité ; Gate 3 reste OUVERTE — NON QUALIFIÉE.


### P03 actuel049 : diagnostic explicite et cible de correction passent

La commande, le drainage, la fermeture indépendante et la revue P03 passent. La suppression du Sceau requis est refusée ; le diagnostic identifie quête, objet et quantité. Le Sceau reste sélectionné dans le contrôle éditable, Appliquer est désactivé et aucune nouvelle révision, commande invalide ou mutation source n’est committée. La capture est inspectée : message et sélection sont visibles ; le viewport reste vide avec un message de chargement du relief importé. Ce diagnostic n’est donc pas une preuve de rendu 3D final, de correction effectivement committée ou d’acceptation visuelle humaine. Preuves : `.gate3-evidence/20261002/public-invalid-quest-preparation-049-001/root-review.json`, `terminal-record.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE, sans publication.


### Seconde approche049 : quête par feu et réaction au danger lancée

Après fermeture indépendante du diagnostic P03, EnvironmentFireQuest049 est lancé dans une nouvelle copie du checkpoint public. Il doit accepter la quête, traverser, allumer la balise depuis une position contrôlée, conserver les reçus publics de chaleur/dégâts et de réaction au danger, neutraliser le gardien avant combat, quitter la chaleur, récupérer le Sceau, terminer la quête puis sauvegarder/reprendre dans un nouveau processus. Les preuves d’exposition/retreat et les trajets seront relus ; aucun PASS du scénario par impact n’est transféré. Preuves : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-049-001/freeze-proof.json`, `start-record.json`. Candidat, harness, assets et plafonds restent conservés, sans seuil de RAM disponible. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu049-001 : approche précise en échec avant allumage

Le premier replay par feu atteint la forêt mais consomme le délai global de120 secondes sur les premières approches précises (première arrivée après87.70 secondes, trois arrivées enregistrées). Des mouvements dépassent alternativement le petit point cible ; la dernière étape n’a plus que quatre secondes. Aucun allumage n’est observé, aucun plafond dépassé ou arrêt forcé ; fermeture indépendante PASS. Preuve : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-049-001/terminal-review.json`.

Le replay002 préparé remplace uniquement les approches précises de la balise et du butin par les impulsions stabilisées déjà utilisées. Même polyline, points, tolérances0.10/0.15 m et délais120 secondes ; chaque arrivée revalide l’ancre publique fraîche, appui et disponibilité d’entrée avant son reçu. Un seul fichier du harness dérivé change ; le moteur, candidat, assets, observateur, garde-fou et plafonds sont conservés. Aucun résultat de feu/dégâts/réaction IA/quête n’est encore qualifié. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Seconde approche049-002 : quête par feu et réaction au danger lancée

Après fermeture indépendante du diagnostic P03, EnvironmentFireQuest049-002 est lancé dans une nouvelle copie du checkpoint public. Il doit accepter la quête, traverser, allumer la balise depuis une position contrôlée, conserver les reçus publics de chaleur/dégâts et de réaction au danger, neutraliser le gardien avant combat, quitter la chaleur, récupérer le Sceau, terminer la quête puis sauvegarder/reprendre dans un nouveau processus. Les preuves d’exposition/retreat et les trajets seront relus ; aucun PASS du scénario par impact n’est transféré. Preuves : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-049-002/freeze-proof.json`, `start-record.json`. Candidat, assets et plafonds restent conservés ; le harness utilise les approches précises stabilisées, sans seuil de RAM disponible. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu049-002 : premier sous-point non stabilisé dans le délai

Le replay échoue avant allumage sur le premier sous-point précis : dernière distance publique 0.091160 m, vitesse horizontale 0.000006761 m/s, mais une seule observation stable sur les trois requises à la fin du délai local de30 secondes. Aucun reçu d’arrivée ni qualification de feu. Les tolérances et le délai global de120 secondes restent inchangés. Aucun arrêt forcé ni dépassement des plafonds ; fermeture indépendante PASS. Pic privé5302427648 octets, compteur GPU dédié1604653056 octets. Preuve : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-049-002/terminal-review.json`.

Après cette fermeture, le parcours public GameplayRetouchParent049-001 est lancé dans une copie isolée : intérieur, dialogue, combat ordinaire, inventaire, achèvement et sauvegarde/reprise. Il doit produire un véritable ledger de gameplay relu avant de servir aux retouches Studio P02 ; les résultats des scénarios physiques ne sont pas transférés. Preuve de départ : `.gate3-evidence/20261002/public-gameplay-retouch-parent-preparation-049-001/start-record.json`. Aucun seuil de RAM disponible ; plafonds de consommation conservés. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### GameplayRetouchParent049-001 : refus des paramètres avant lancement natif

Le driver a omis `VisitInteriorBeforeSave`, préalable explicite de `VisitInteriorBeforeQuest`. Le harness refuse avant lancement du candidat : aucun gameplay ni PASS acquis, aucun arrêt forcé ; fermeture indépendante PASS. L’essai002 ajoute seulement cette option au driver, conserve harness/candidat/assets/plafonds/critères et vérifie que le checkpoint public contient la salle authorée. Preuve : `.gate3-evidence/20261002/public-gameplay-retouch-parent-preparation-049-001/terminal-review.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### GameplayRetouchParent049-002 lancé avec les prérequis de visite

Le nouveau driver demande les deux options de visite intérieure et le parcours complet dialogue/combat/inventaire/achèvement/Save/Resume. Une capture du seuil a été inspectée : le viewport rend le terrain, la végétation et les bâtiments ; cette observation ponctuelle ne vaut ni revue de partie complète ni acceptation visuelle propriétaire. Le lecteur indépendant est préparé pendant le run pour vérifier les sources, routes, cinq reçus de dégâts par contact de20, cycle de porte, butin consommé, quête terminée et reprise ; il n’est pas présenté comme épinglé avant lancement. Aucun PASS n’est encore acquis. Preuve : `.gate3-evidence/20261002/public-gameplay-retouch-parent-preparation-049-002/start-record.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### GameplayRetouchParent049-002 : parcours intérieur/combat/Save/Resume relu PASS

Le candidat049 termine les deux processus publics sans arrêt forcé ni déclenchement du garde-fou. Les deux identités natives distinctes sont vérifiées dans les échantillons. Le lecteur indépendant vérifie la source conservée, les routes effectivement parcourues, le cycle ouvert/fermé de la porte intérieure, cinq commandes publiques de combat correspondant chacune à un reçu sauvegardé de20 dégâts par contact, le Gardien mort, le butin récupéré/consommé et la quête Completed. La reprise publique confirme la quête achevée, l’absence du Sceau et la direction initiale ; le fichier sauvegardé est relu et son SHA vérifié. Le hash métier de l’état n’est pas recalculé indépendamment. Preuves : `.gate3-evidence/20261002/public-gameplay-retouch-parent-preparation-049-002/root-review.json`, `terminal-record.json`. Cette preuve devient parent admissible pour les retouches P02 ; elle ne qualifie pas ces retouches, le NAV complet, la CI, P05 ou toute la Gate.

Après fermeture indépendante, Feu049-003 est lancé : mêmes quatre points d’approche, tolérances0.10/0.15 m, délai global120 secondes et limite locale30 secondes ; appel à l’algorithme existant de corrections proportionnelles sur les seuls segments précis. Candidat, assets, plafonds et observateur conservés. Preuve : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-049-003/start-record.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu049-003 : approche et allumage acquis, retrait en échec

L’approche précise termine en77.949 s avec huit arrivées aux bornes originales0.10 m et un allumage public positif. La sauvegarde d’exposition est conservée, mais le retrait échoue : première touche acquittée2351.3724 ms après début du clic, contre un budget inchangé1000 ms, et joueur vaincu à l’arrivée. La chronologie distingue acquittement Ignite356.928 ms, déclenchement Save1239.883 ms, acquittement Save1470.265 ms, garde de capture2183.110 ms et retrait4709.407 ms. Ces observations imposent un diagnostic des échanges publics pendant l’exposition ; ni délai élargi ni baisse de dégâts ne sont appliqués. Sources/candidat/harness stables, aucun arrêt forcé ni plafond dépassé, fermeture indépendante PASS. Preuve : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-049-003/terminal-review.json`. Les résultats causaux partiels ne qualifient pas la quête complète par feu.

Après fermeture, P02 source049 est lancé sur une copie de la vraie sauvegarde du parcours intérieur/combat : retouche Studio de réplique et porte, Save, refus de reprise ordinaire puis reprise compatible et nouveau processus. Preuve : `.gate3-evidence/20261002/public-compatible-retouch-preparation-049-001/start-record.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### P02 source049-001 : retouches enregistrées, passage intérieur en échec

Les commandes Studio sauvegardent la réplique modifiée et déplacent la porte de0.05 m, avec identité et liens préservés. Le passage intérieur bloque ensuite àX46.97/Z-148.31, environ0.92 m à côté du centre retouchéX46.05 : douze secondes sans progression horizontale. Aucun PASS P02 ni nouveau processus de reprise validé. Fermeture indépendante PASS ; aucun arrêt forcé ni dépassement de consommation. Preuve : `.gate3-evidence/20261002/public-compatible-retouch-preparation-049-001/terminal-review.json`. Le replay002 préparé ajoute le centrage latéral public à0.10 m par l’algorithme proportionnel existant, puis relit une observation fraîche ; les cibles/tolérances/délais du passage original, retouches, assets et candidat restent conservés. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### P02 source049-002 : effets relus, fermeture native en échec

La relecture indépendante vérifie exactement deux différences des corrections : texte de dialogue et X de porte(+0.05 m). Tout autre contenu et lien reste identique ; la sauvegarde réelle sur la source initiale contient quête completed, Sceau consommé et porte ouverte. La reprise compatible retrouve la nouvelle réplique et l’état attendu dans deux processus natifs distincts ; le centrage permet effectivement la traversée intérieure. Pourtant le premier native a été arrêté de force : son motif détaillé de fermeture gracieuse n’est pas consigné dans ce ledger. La fermeture indépendante finale PASS ne transforme pas cet arrêt forcé en succès. P02 reste NON QUALIFIÉ. Preuve : `.gate3-evidence/20261002/public-compatible-retouch-preparation-049-002/root-review.json`.

Le replay003 préparé ferme d’abord la zone via les commandes publiques déjà utilisées dans le gameplay réussi, puis garde la fermeture WM_CLOSE/discard et son délai10 secondes. Il conserve deux reçus détaillés de fermeture. Ce changement teste l’hypothèse d’un nettoyage de zone chargée ; aucune cause exclusive n’est annoncée. Après fermeture de002, NAV049-001 est lancé sur le checkpoint authoré actuel : NoPath porte fermée, patrouille/seam, Open, sortie physique, tile revision, stabilité et Save. Aucun PASS historique transféré. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### NAV049-001 : signature du helper historique incompatible avec le harness actuel

Le script NAV historique appelle deux fois `Move-PlaytestSettledPulses` avec une option proportionnelle que le helper actuel049 expose sur `Move-PlaytestResumeSettledPulses`. Le runtime refuse ce paramètre ; aucun NAV qualifié. Le garde-fou arrête ensuite des descendants restés après le driver (owned_descendant_outlived_driver) ; la fermeture indépendante confirme toutes les identités terminées. Preuve : `.gate3-evidence/20261002/public-navigation-preparation-049-001/terminal-review.json`. NAV002 préparé corrige seulement ces deux appels, sans modifier les helpers, points, tolérances, délais, observateur ou lecteurs.

P02 source049-003 est lancé après fermeture NAV001. Il garde les retouches et le centrage de002, ferme publiquement la zone avant la fermeture native et enregistrera ses deux reçus de fermeture. Le SelfTest du script retouche passe ; aucun PASS runtime du nouvel essai n’est encore acquis. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### P02 source049-003 : deux sorties sans force, délai natif de dix secondes dépassé

Les effets des retouches et les deux reprises sont relus indépendamment : deux différences exactes de corrections, liens conservés, quête completed/Sceau consommé/porte ouverte dans la sauvegarde, réplique retouchée dans deux processus et traversée intérieure réussie. La fermeture publique de zone est effectuée avant WM_CLOSE. Les deux processus terminent sans force, mais le reçu du premier conserve `Owned Studio process did not exit within 10 seconds after public discard.` ; le second reçu est propre. Le premier termine pendant la fermeture de la connexion CDP après ce délai. Cette chronologie est une piste de protocole de fermeture, pas une cause exclusive démontrée. Le lecteur PASS strict n’est pas exécuté contre cette condition déjà négative ; un lecteur d’échec distinct archive les observations sans les qualifier. Fermeture indépendante PASS ; aucun garde-fou mémoire déclenché. Preuves : `.gate3-evidence/20261002/public-compatible-retouch-preparation-049-003/root-review.json`, `terminal-record.json`. P02 reste NON QUALIFIÉ.

NAV source049-002 est lancé après cette fermeture : appels proportionnels compatibles avec les helpers actuels, critères/observateur inchangés. Les autotests du lecteur, reçus de Save et signature du helper passent ; aucun résultat runtime acquis au départ. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### NAV source049-002 : observations et sauvegarde indépendamment rejouées PASS

Le candidat049 termine le replay public sans arrêt forcé ni déclenchement du garde-fou, puis le drain et la fermeture indépendante passent. Les 68 fichiers de samples sont vérifiés par SHA ; le lecteur archivé revalide tous les profils/ticks/sources et recalcule huit rapports : NoPath stable du Messager porte fermée, déplacement réel du Patrouilleur, passage de seam avec appui/KCC, sortie physique du Messager à travers le seuil après Open, arrivée Reached stable, révisions Open et Close, reçus de porte dans la sauvegarde cohérente. Les archives authorées et leurs copies sont byte-vérifiées inchangées ; la sauvegarde est relue par SHA. Preuves : `.gate3-evidence/20261002/public-navigation-preparation-049-002/root-review.json`, `observation-replay.json`, `terminal-record.json`. Cette qualification porte sur les cas authorés patrouille/salle/porte, sans prétendre couvrir toute la map ou tous les comportements PNJ. Aucun résultat historique n’est hérité. Gate 3 reste OUVERTE — NON QUALIFIÉE : quête complète par feu, fermeture P02, contrôles workspace/candidat exact, décisions restantes, CI réelle et revue humaine restent distincts.


### P02 source049-004 : ordre de fermeture testé, replay natif en attente

Après acquittement public de « Quitter sans enregistrer », la connexion CDP est libérée avant l’attente native. Sa fermeture consomme le même budget total de dix secondes. Trois workers Python réels et connexions WebSocket locales prouvent l’échec de l’ancien ordre, la fermeture normale du nouvel ordre et le refus avec arrêt du worker détenu lorsqu’il reste vivant au-delà du délai. Les acquittements UI/fenêtre de ce contrôle sont simulés : aucune qualification du Studio natif ni de P02 n’en découle. Les essais de collecte défectueux sont conservés (nom de cas écrasé par SelfTest, puis sortie void PowerShell parasite) ; le contrôle corrigé passe avec exactement trois cas. Preuves : `.gate3-evidence/20261002/public-compatible-retouch-preparation-049-004/native-close-order-regression.json` et `close-order-regression-attestation.json`.

Le replay natif n’a pas démarré : l’admission constate un autre Studio actif depuis `F:\OpenRPG_ImportWork\studio-native-library-candidate-v1`. Ce processus est préservé. La demande de fermeture normale a été présentée au propriétaire ; la préparation reste figée. Aucun seuil de RAM disponible n’a été réintroduit ; les plafonds de consommation restent actifs. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu source049-004 : activation redondante isolée, qualification native non exécutée

Le helper de focus reçoit une option réservée au retrait d’exposition qui évite cinq échanges d’activation de fenêtre lorsque le document est déjà visible et au premier plan. Le chemin par défaut, le cooldown complet de1500 ms mesuré depuis Escape, l’observation fraîche de focus/visibilité/point exposé, la capture effective, le prédicat d’entrée et la borne de trois tentatives sont conservés. Huit contrôles sur le vrai corps de fonction, avec transport/DOM simulés, passent : baseline historique et défaut inchangé, opt-in déjà au premier plan, activation maintenue si non focalisé ou caché, perte fraîche de focus avant clic, refus avec cooldown et reprise, refus persistant borné. L’assertion initiale de cooldown exactement1500 ms a été corrigée pour vérifier délai demandé plus temps déjà écoulé depuis le timestamp de release ; l’essai original est conservé.

La syntaxe des trois scripts et le SelfTest du helper physique passent. L’entrée de scénario n’expose pas de paramètre SelfTest : sa tentative d’appel a été rejetée avant exécution native, puis remplacée par analyse syntaxique et SelfTest du helper prévu. Preuves : `.gate3-evidence/20261002/public-fire-latency-helper-preparation-049-004/regression-proof.json` et `public-environment-fire-quest-preparation-049-004/helper-selftest-proof.json`. Seulement deux scripts du harness changent ; le candidat, les assets, les trajectoires, les dégâts, Save, les lecteurs causaux et le budget de première touche1000 ms restent identiques. L’amélioration de latence n’est pas mesurée et le respect de ce budget n’est pas prédit. Aucun Studio/GPU lancé pour cette préparation ; le replay attend la fermeture normale et le contrôle indépendant de P02-004. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Audit complet049 : erreur de compilation de test, correction050 isolée

Le contrôle complet source049 passe le formatage puis échoue sous Clippy --workspace --all-targets avec deux diagnostics E0283 pour une même comparaison de bytes dans le nouveau test matériel GPU : type de sortie de bytemuck::cast_slice ambigu avec les features réunies. Les tests complets n’ont pas été exécutés. Sources préservées, aucun arrêt/plafond mémoire déclenché, fermeture indépendante PASS. Le PASS PBR/engine limité antérieur ne couvrait pas cette union de features. Preuves : `.gate3-evidence/20261002/full-workspace-native-validation-049-001/machine-proof.json`, `launch-proof.json`, `independent-identity-closure.json`.

Un snapshot privé050 rend seulement cette comparaison explicite avec cast_slice::<u32,u8>. Les2720 fichiers de travail sont vérifiés par SHA : seul le fichier de test diffère ; les bytes de production restent identiques. Le clone initial n’avait pas conservé les attributs Git locaux de préservation des bytes. La copie restaure ces attributs, réapplique les bytes source pinés et renormalise l’index ; les tentatives intermédiaires sont conservées dans l’historique privé. Le diff final Git depuis049 contient seulement le fichier de test et l’arbre050 est propre. Checkpoint local9439996e035ee719d14c705959e0637826cf153f ; branche principale et publication intactes. Les mêmes contrôles complets sont relancés sous plafond RAM6 Gio, sans seuil de RAM disponible. Preuve : `.gate3-evidence/20261002/full-workspace-native-validation-050-001/start-record.json`. Aucun PASS050 ni héritage automatique des replays049 n’est déclaré.

Audit de portée : `.gate3-evidence/20261002/source049-closure-audit-001/review.md` et `proof.json` recensent R3-A/B/C et P01–P05. Leur observation de tests049 en cours est remplacée par cet échec et la relance050. La quête feu complète, fermeture P02, CI exacte, décision d’intégration et revue humaine restent ouvertes. Le lock courant contient glib0.18.5, explicitement bloquant pour la distribution dans la politique ; l’[avis RustSec RUSTSEC-2024-0429](https://rustsec.org/advisories/RUSTSEC-2024-0429.html) indique une correction à partir de0.20.0. Aucun waiver ni résolution revendiqué. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu049-005 : entrées publiques ordonnées et cooldown avant exposition

La préparation005 envoie les trois mêmes événements CDP trusted Input.dispatchMouseEvent (mouvement, pression, relâchement) dans leur ordre original puis attend tous les acquittements exacts, dans un budget de transport borné. Huit cas avec vrais workers détenus et WebSockets locaux passent : acquittements ordonnés, inversés, événement asynchrone, réponse fragmentée, erreur CDP, réponse sans result, acquittement release absent et timeout. Les coordonnées non finies sont refusées avant transport. La réception des trois événements avant toute réponse est attestée par les fixtures ; aucun navigateur/Studio/GPU n’est lancé. La réussite protocolaire ne qualifie pas l’action native ni le retrait thermique.

Huit contrôles supplémentaires de helpers avec DOM/transport simulés passent : Escape historique conservé, option idempotente seulement si la capture ET l’entrée sont déjà relâchées selon lecture fraîche, Escape et attente maintenus si l’un reste actif, refus de relâchement conservé ; le cooldown complet1500 ms est payé avant Ignite, selon le timestamp connu ou complet si inconnu. État et cible Ignite sont résolus fraîchement après ce délai ; le premier-key budget reste1000 ms depuis le début réel du clic Ignite. La quête, Save, approche, dégâts, loot, Resume et lecteurs causaux restent inchangés. Le focus générique conserve son défaut ; ces options sont réservées au retrait d’exposition. La fermeture reprend le helper dont les trois régressions de budget dix secondes sont déjà passées.

Syntaxe et SelfTest physique du harness005 passent ; candidat049, assets et plafonds restent figés. Aucun héritage du snapshot050 corrigé pour les tests ni résultat runtime005. Preuves : `.gate3-evidence/20261002/public-fire-mouse-pipeline-helper-preparation-049-005/proof.json`, `input-helper-regression.json` et `public-environment-fire-quest-preparation-049-005/prepared-record.json`. Le replay attend le Studio externe et P02-004 avec fermeture indépendante. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### glib candidat050 : graphe Linux précisé, proposition non appliquée

Les graphes cargo tree --workspace --all-features --locked, cibles Windows et Linux, sont relus avec exit0 et SHA des logs. glib0.18.5 est absent du graphe Windows, présent sur Linux via GTK0.18.2/Tauri2.11.5/forge_studio. Lock et politique inchangés : blocage de distribution conservé. Première tentative offline en échec faute de bitvec1.1.1, puis acquisition des dépendances verrouillées pour lecture, sans compilation. Preuve : `.gate3-evidence/20261002/source050-glib-target-graph-002/root-review.json`.

La crate actuelle contient le pointeur immutable concerné. Un diff de deux lignes conforme à la [correction upstream gtk-rs#1343](https://github.com/gtk-rs/gtk-rs-core/pull/1343) est préparé dans `glib-variant-iterator-proposed.patch`, sans appliquer de fork, patch Cargo ou changement de politique. Aucun test GLib natif/Linux n’est revendiqué ; l’adoption requiert provenance, tests optimisés de l’itérateur et validation complète GTK/Tauri/Linux et gouvernance. Dossier : `source050-glib-target-graph-002/decision-preparation.md`. Aucun waiver ni qualification de distribution. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Candidat050 : reconstruction correspondante préparée, tests en attente

Les wrappers frontend/WASM-worker et natif du candidat049 sont dérivés pour le snapshot050 dont seule une assertion de test change. Les profils, overrides de compilation, assets3000/kit11/acquisitions259 et plafonds restent identiques. Les quatre scripts PowerShell sont analysés sans erreur ; le driver Python exige un checkpoint propre et une vraie revue PASS des contrôles complets source050, avec leurs SHA. Ces SHA sont volontairement absents de la préparation tant que les tests ne sont pas terminés/revus. Aucun checkpoint de qualification ni build050 n’est lancé ou annoncé comme PASS.

Préparation : `.gate3-evidence/20261002/material-uniform-candidate050-build-preparation-001/preparation-proof.json` et `syntax-proof.json`. Le finaliseur ne peut s’exécuter qu’après résultats complets et fermeture indépendante. Un seul travail CPU lourd est conservé ; la compilation des tests050 est encore active. Les preuves runtime049 gardent leur portée et ne sont pas transférées automatiquement à050. Le Studio externe et les décisions/CI/revue humaine restent à traiter. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### CI distante actuelle : ancien head publié en échec, candidat050 non qualifié

Consultation gh en lecture seule des cinq derniers runs et des jobs du plus récent : [37125741497](https://github.com/Karenax/OpenRPG_Forge/actions/runs/37125741497) au head6b001dbed0fdf1c002d43cf507c60eece1119130 conclut failure. Windows et Linux échouent sur Clippy ; leurs tests et builds suivants sont skipped. WASM échoue au bundle Studio. Supply-chain passe selon sa politique. Les runners ont réellement exécuté des étapes : aucun blocage de facturation n’est déduit de cet état. Ce run ne porte pas sur le snapshot050 9439996e035ee719d14c705959e0637826cf153f, qui n’apparaît pas dans les cinq derniers runs consultés. Aucune CI exacte050 PASS, aucun workflow dispatch ni publication. Preuve : `.gate3-evidence/20261002/source050-remote-ci-state-002/root-review.json`, avec réponses brutes et SHA. Les contrôles locaux050 en cours ne remplacent pas la CI distante au commit candidat. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Source050 : validation complÃ¨te native du workspace

Le snapshot exact du snapshot050 (test uniquement) passe cargo fmt --all --check, Clippy --workspace --all-targets -D warnings, puis cargo test --workspace --all-targets, locked/offline avec compilation et tests Ã  un thread. Relecture indÃ©pendante : 1801 tests passent, 31 ignorÃ©s, zÃ©ro Ã©chec et zÃ©ro filtrÃ©, 163 groupes ; 163 exÃ©cutables rÃ©fÃ©rencÃ©s sont archivÃ©s et leurs SHA vÃ©rifiÃ©s. Les2720 entrÃ©es source sont prÃ©servÃ©es, les trois commandes et logs relus, consommation plafonnÃ©e6 Gio sans seuil de RAM disponible, aucun arrÃªt de protection, fermeture des identitÃ©s PASS. Preuve : `.gate3-evidence/20261002/full-workspace-native-validation-050-001/root-review.json`. Ce rÃ©sultat couvre les features natives par dÃ©faut ; il ne remplace ni CI Linux/WASM rÃ©elle, ni parcours Tidewater, ni revue humaine. Gate 3 reste OUVERTE â€” NON QUALIFIÃ‰E.


### Candidat050 : construction appariée démarrée après validation complète

La validation native050 et sa fermeture indépendante passent. Le premier lecteur de résultats a refusé la comparaison textuelle des chemins mixtes `/` et `\`; le lecteur002 normalise chaque chemin avec Path puis conserve la comparaison ordonnée exacte des 163 exécutables, leurs tailles et SHA. Aucun test ni critère n’a été réduit. Le build apparié050 a démarré à 2026-10-07T18:50:18.943705+00:00 sous plafond privé6 Gio sans seuil de RAM disponible. Aucun résultat de build ou replay050 n’est encore acquis. Preuve de démarrage : `.gate3-evidence/20261002/material-uniform-candidate050-build-preparation-001/started-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Candidat050 : interruption du build001 et reprise isolée002

Le handle001 a disparu après une erreur technique du helper d’exécution. Aucun verdict terminal ni PASS n’est déduit. Toutes les identités échantillonnées sont absentes au contrôle CIM exact PID/date ; un PID réutilisé et les compilations étrangères sont préservés. Cause exacte de l’interruption non prouvée. Les logs et sorties partielles001 restent conservés. L’essai002 est démarré à 2026-10-07T19:03:14.518266+00:00 dans de nouveaux dossiers frontend/candidat avec mêmes builders, sources2720, assets, profils, cache privé et plafond6 Gio, sans seuil de RAM disponible. Preuves : `material-uniform-candidate050-build-preparation-001/interruption-review.json` et `material-uniform-candidate050-build-preparation-002/started-proof.json` sous `.gate3-evidence/20261002`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Candidat050002 : build apparié relu, revalidation publique démarrée

Le build frontend/worker/natif et sa fermeture indépendante passent. Revue des2720 fichiers source,3000 imports,11 entrées kit et259 entrées acquises, checkpoint privé propre et composants embarqués. Exécutable natif SHA256 `78bdc433673a5c8263c4db59e3f217dd3d2053886446f4e231335820888430d1`. Preuve : `.gate3-evidence/20261002/integrated-studio-candidate-root-review-050002/proof.json`. La revalidation publique Load/Save a démarré sur une copie isolée du checkpoint049 ; aucun résultat runtime049 n’est promu au candidat050. BUILD_ONLY ne prouve ni rendu, ni parcours, ni revue humaine. Aucun changement principal ou publication. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Candidat050 : checkpoint public Load/Save relu

Le parcours public050 passe : 22 formulaires auteur relus, Save acquitté, tête complète identique, révision40,65 fichiers source préservés, fermeture native normale et identités absentes. Preuve : `.gate3-evidence/20261002/public-checkpoint-revalidation-preparation-050-001/root-review.json`. Aucun parcours de jeu, reprise de sauvegarde gameplay, création auteur, rendu accepté ou résultat049 n’est promu par ce contrôle. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication.


### Source050 : parcours public intérieur/combat/quête/Save/Resume démarré

Le scénario complet049-002 est rejoué sur le nouveau candidat050002 et une copie du checkpoint publiquement revalidé050. Même harness, options et critères intérieur avant quête, contact réel au combat, inventaire, quête terminée, Save et nouveau processus Resume avec direction caméra initiale. Aucun PASS049 hérité. Plafonds privés6 Gio, GPU dédié4 Gio et partagé2 Gio, sans seuil de RAM disponible. Preuve de démarrage : `.gate3-evidence/20261002/public-gameplay-retouch-parent-preparation-050-001/started-proof.json`. Aucun verdict runtime050 ou clôture de Gate déduit du démarrage. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Source050 : parcours RPG intérieur/combat/quête/Save/Resume relu PASS

Les12 contrôles indépendants passent : intérieur avant quête, reçus porte ouverts/fermés,5 attaques publiques correspondant à5 reçus contact20 dégâts, gardien vaincu, Sceau récupéré puis consommé, quête terminée sauvegardée et reprise dans un second processus avec caméra initiale correcte. Sources65 préservées, plafonds respectés, fermeture normale et identités absentes. Preuve : `.gate3-evidence/20261002/public-gameplay-retouch-parent-preparation-050-001/root-review.json`. Ce parcours devient le parent effectif des retouches compatibles050 ; aucun résultat049 promu, navigation/environnement/retouches/revue humaine encore distincts. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Source050 : retouches compatibles publiques démarrées

Retouche de la réplique et déplacement de porte, Apply/Save, reprise compatible du Save RPG050 et vérification des effets publics dans deux sessions natives. Même harness de fermeture CDP avant attente native, budget total10 secondes inchangé, régression positive/négative déjà acquise sur helper byte-identique. Aucun résultat natif de retouche049 hérité. Preuve de démarrage : `.gate3-evidence/20261002/public-compatible-retouch-preparation-050-001/started-proof.json`. Les retouches ne sont pas encore qualifiées ; Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Source050 : R3-P02 retouches compatibles relues PASS, feu complet démarré

Les13 contrôles P02 passent : exactement réplique et positionX porte retouchées, autres corrections/liens préservés, Save RPG complet retrouvé, Resume ordinaire refusé et deux reprises compatibles réalisées, réplique retouchée publiquement visible, porte franchie réellement, deux identités natives distinctes fermées normalement dans le budget original10 secondes. Source préservée, aucun arrêt forcé ni plafond déclenché. Preuve : `.gate3-evidence/20261002/public-compatible-retouch-preparation-050-001/root-review.json`. Le scénario feu050 est ensuite démarré sur copie du checkpoint050 revalidé, même helper testé de commandes souris acquittées, cooldown complet1500ms avant ignition et premier recul1000ms toujours exigé. Aucun verdict feu ni clôture Gate acquis. Preuve de démarrage : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-050-001/started-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu source050-001 : approche précise refusée avant ignition

Le scénario échoue sur le point17.05/-267.95 avant ignition. Des observations entrent dans la tolérance0.10m, mais l’approche ne satisfait pas son arrêt stabilisé dans le budget ; ne pas confondre position instantanée et arrivée acquise. Aucun résultat de recul/feu/quête qualifié. Sources/candidat/helpers préservés, aucun plafond déclenché, aucun arrêt forcé ; fermeture indépendante PASS. Preuve : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-050-001/terminal-review.json`. Les logs, journal, observations GPU et Save d’échec restent conservés. NAV050 est préparé avec critères inchangés, sans lancement ni héritage du PASS049. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu050-001 : correction du diagnostic, préparation002 à tolérance0.04m inchangée

La lecture de l’appel échoué corrige l’interprétation précédente : sa tolérance réelle est0.04m et son budget12 secondes ; minimum observé0.0715m, zéro observation dedans. Il accepte une distance instantanée et la stabilisation suivante n’a jamais été atteinte. La route précédente utilise séparément0.10m. Rapport précédent conservé, correction : `public-environment-fire-quest-preparation-050-001/terminal-review-corrected.json`. L’essai002 remplace seulement cet appel final par le helper existant d’impulsions relâchées proportionnelles avec mêmes coordonnées,0.04m et12 secondes, trois observations fraîches stabilisées exigées. Aucun délai de recul, heat, loot, quête ou Resume relâché. Pas de runtime002 lancé ; fermeture NAV050 préalable requise. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### NAV050 relue PASS ; feu050-002 démarré après fermeture

Navigation050 : fermeture indépendante PASS, observations archivées et Save rejoués par les lecteurs, puis revue racine PASS. Cas authorés : NoPath stable porte fermée, mouvement réel de patrouille, KCC/seam, sortie physique après Open, Reached stable, révisions distinctes Open/Close et reçus sauvegardés. Sources et archives préservées. Preuve : `.gate3-evidence/20261002/public-navigation-preparation-050-001/root-review.json`. Toute la map/tous les comportements PNJ et jugement humain restent non qualifiés. Le feu050-002 démarre ensuite avec seul appel final changé vers le helper existant relâché/proportionnel ; syntaxe PS du script modifié PASS, mêmes coordonnées,0.04m et12 secondes, recul1000ms et cooldown1500ms inchangés. Aucun résultat feu acquis. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Feu050-002 : approche stabilisée acquise, recul tardif et trajet refusés

L’arrivée finale est acquise à0.03158m pour0.04m, avec le helper relâché et budget12s. Ignition réelle effectuée ; délai jusqu’au premier recul1451.6893ms pour1000ms et trajet radial non atteint en12s. Le driver échoue ; son ledger ne signale pas de force, mais le garde-fou arrête ensuite des descendants restés après driver (owned_descendant_outlived_driver), donc aucune fermeture normale globale ni PASS. Aucun plafond mémoire indiqué. Fermeture indépendante finale PASS, sources/candidat/helpers préservés. Chronologie et Save d’exposition conservés : `.gate3-evidence/20261002/public-environment-fire-quest-preparation-050-002/terminal-review.json`. La boucle complète d’impact050 est préparée avec mêmes critères et0.025m au contact, après fermeture du feu. Gate 3 reste OUVERTE — NON QUALIFIÉE.


### Source050 : boucle complète par impact démarrée

Scénario complet EnvironmentQuest sur candidat050 et checkpoint public050, même harness049-003 conservé byte-identique. Contact de lancement0.025m et trois échantillons frais stabilisés, impact/événements causaux avant combat, piège/destructible, loot, quête, Save et nouveau processus Resume sont exigés par le lecteur. Le feu050002 est indépendamment fermé avant lancement. Aucun résultat049 hérité et aucun PASS050 acquis par le démarrage. Preuve : `.gate3-evidence/20261002/public-environment-quest-preparation-050-001/started-proof.json`. Gate 3 reste OUVERTE — NON QUALIFIÉE.
### Source050 : boucle complète par impact relue PASS

Le scénario natif termine avec code0 ; fermeture indépendante PASS puis revue causale racine PASS. Les12 contrôles exigés prouvent lancement réel, dégâts par impact et neutralisation du gardien avant combat, piège/destructible, butin, quête terminée, Save cohérent et reprise dans un nouveau processus. Contact stabilisé à0.025m, sources65 préservées, aucun arrêt forcé ni plafond mémoire déclenché. Preuve : `.gate3-evidence/20261002/public-environment-quest-preparation-050-001/root-review.json`. Le feu, la poussée/rive/extinction, les autres critères et la validation humaine restent distincts. Gate 3 reste OUVERTE — NON QUALIFIÉE ; aucune publication Git.
