# Gate 7 — clôture locale du 18 novembre 2026

**Verdict propriétaire.** Gate 7 est clôturée sur les validations Windows déjà
acquises. Le propriétaire reporte le volet Linux et le rejeu du candidat 063
sur Windows propre. Les essais reportés conservent leur statut. La Gate 8
et la Gate 9 restent planifiées.

Le Studio exporte chaque jeu avec son propre launcher et le moteur commun.
Le Hub a été retiré au lot 048. Le candidat Windows 063 couvre le parcours
Studio → export du Grand Plateau → installation → reprise → désinstallation
sur le poste de développement. La maintenance a aussi vérifié le retour
063 → 062 → 063 avec les 33 fichiers auteur et exportés préservés.

## Critères et preuves retenues

| Critère | Périmètre retenu à la clôture |
| --- | --- |
| R7-01 Streaming | PASS local Windows au 015. Région de 2 km, trajet de 291 m et contrôles de résidence, identité et persistance. |
| R7-02 HLOD | PASS local Windows au 017. Aller-retour dans le jeu exporté, substitution au détail et réduction mesurée dans le cas dense. |
| R7-03 Sauvegardes multi-zones | PASS local Windows du 27 octobre. Transitions et reprise multi-zones, refus intermodules et staging interrompu. Le 063 reprend aussi la sauvegarde 061. |
| R7-04 Packaging runtime | PASS local Windows au 015. Le parcours joueur 060 confirme l’autonomie sur un Windows sans SDK ni Studio. Le 063 vérifie le contenu exact du kit dans son export. |
| R7-05 Profils graphiques | PASS local Windows au 015 pour les quatre profils et la persistance. Le 062 les observe et les rend au même point Tidewater. Le 063 apporte le comparatif de cadence en déplacement. |
| R7-06 Lanceur | PASS local Windows au 015. Le Grand Plateau 063 lance le jeu et reprend sa sauvegarde depuis son launcher configuré. |
| R7-07 Builds | Builds Release du Studio, du jeu et du CLI du 063 rapprochés du même inventaire. Volet Linux reporté. |
| R7-08 Licences | Relecture des paquets exacts du 063. Le Studio, le jeu et le module comptent 424, 422 et quatre chemins de licence. Le contenu original du Grand Plateau est sous CC0-1.0. Tidewater reste privé et le volet Linux est reporté. |
| R7-09 Installateur | Le 063 couvre la mise à jour et le retour arrière natifs du Studio ainsi que l’installation, la reprise et la désinstallation du jeu sur l’hôte. Le 060 couvre le cycle 059 → 060 → 059 et le parcours joueur sur Windows propre. Volet Linux reporté. |
| R7-10 Machine propre | Parcours auteur et joueur vérifiés pour le 060 sur Windows sans SDK avec WebView2 Fixed Version. Installation et ouverture du Studio 060 également vérifiées sous Evergreen sur Windows LTSC. Rejeu du 063 sur Windows propre et volet Linux reportés. |

Les verdicts datés, les cas de refus et la portée des candidats se trouvent
dans la [matrice d’acceptation](GATE7_PROGRESS_20261023.md#matrice-dacceptation).
La [passation du 063](../../HANDOFF.md#gate-7-windows-063) épingle le Setup,
les reçus d’export, de reprise, de maintenance et de licences. Le parcours
[Windows propre du 060](../../HANDOFF.md#gate-7-joueur-060-grand-plateau) et
le [contrôle Evergreen du 060](../../HANDOFF.md#gate-7-studio-060-evergreen-ltsc)
restent rattachés à leurs binaires.

## Cadence Tidewater

Le runtime livré dans le kit 063 correspond octet pour octet au runtime mesuré.
Sur le trajet de cinq repères à 1080p, le nouveau tri de navigation sur le
travailleur fait passer les intervalles d’affichage de plus de 33,33 ms de
trois à zéro, et le p99 de 27,78 à 20,83 ms. Tidewater 012 comptait un
intervalle de plus de 33,33 ms et un p99 de 27,78 ms. Le résultat porte sur
ce trajet et ces réglages. La parité générale de cadence et de rendu reste
à documenter. [Comparatif et reçu](../../HANDOFF.md#gate-7-tidewater-navigation-tri-worker).

Les preuves restent dans le catalogue central sous `gate7-monde-distribution`.
Les installateurs Windows 063 et 062 et les entrées du parcours propre sont
utiles à la reprise des contrôles reportés. La publication Git sauvegarde le
code et la documentation du lot. Elle ne publie pas les installateurs ni les
contenus privés.
