# R1-022/023 — isoler normales et réfraction sans reconstruire

9 septembre 2026, Rust. Gate 1 ouverte, aucun verdict artistique.

## Implémenté

Option native `--water-optics-isolation normals-off|refraction-off`. Valeur stricte,
unique ; candidat optique WorldSketch, caméra fixe, capture et sortie bornée requis.
Modification de la copie chargée uniquement, avant projection moteur et calcul
des hashes d'eau. Aucun fichier source écrit, aucune géométrie de carte modifiée.
L'événement `runtime_started.water_optics_isolation` identifie le mode ; absence
d'option = `none`. Aucun nouveau shader ni seconde représentation persistante.

Normales coupées : `surface.normal_strength=0`, déplacement géométrique conservé.
Réfraction coupée : profil de réfraction désactivé, décalage nul dans le candidat,
transmission et absorption conservées. Ce mode ne désactive pas la transparence.
Les profils complets et les contrôles du Studio ne sont pas implémentés par cet outil.

86 tests runtime PASS, zéro échec, Clippy runtime tous targets strict PASS,
format et build natif PASS. Premier Clippy : parseur 105/100 lignes refusé,
routage réutilisé sans désactiver le lint. Test de mutation limitée au seul champ
sélectionné et tests CLI de refus inclus. Aucune nouvelle compilation WASM requise
pour cette modification du lanceur natif.

## Trois captures du même exécutable

Racine locale `E:/OpenRPG_Forge/.gate1-evidence-runtime/r1-water-isolation-20260909/`.
`Capture.ps1 -Variant baseline|normals-off|refraction-off`. EXE SHA-256
`18C385A00AA2F77D5434CDC94B8A7F95CEDE6EE334F1AB0BAD41A1A7C207FE99`.
Vue water_close, temps figé, capture frame 300, arrêt frame 420, terrain/pont et
végétation procédurale de remplacement identiques, sans kit gouverné.

| Variante | PNG SHA-256 | PID terminé |
| --- | --- | --- |
| baseline | 8FC071F03D5DB63401EC9101448E035D65B814B43BE134FD30142FDCD5C92879 | 16940 |
| normals-off | F647FF68F104E10E1FF2A068275DC9052C3790FEDC6B054330AC939C19359813 | 31460 |
| refraction-off | 70E68AA78D6BDA68A817913E4FDF859EA7D5BF536FFEF833A29B806E9DA9BD8F | 35900 |

Chaque lancement : sortie 0, capture vérifiée, aucun ERROR ; mode JSON relu.
La première boucle externe a conclu à tort à un échec après baseline en testant
`$LASTEXITCODE` non renseigné par Start-Process. Le script contrôle lui-même le
code du PID ; baseline conservée, les deux autres essais seuls ont été lancés.

Inspection des trois PNG : désactiver les normales réduit nettement les motifs
de surface sous le pont ; supprimer le décalage de réfraction ne les supprime
pas. La frontière sombre reste dans les trois variantes : aucune preuve qu'il
s'agit d'une anomalie plutôt que d'une ombre reçue. Ne pas la masquer.

Conclusion bornée : cibler les normales de détail, après l'essai macro-relief
non concluant, au lieu d'une nouvelle variation de grandes vagues ou de réfraction.
Ce résultat ne prouve pas encore quelle fréquence ou quelle composante de normale
est responsable, ni la qualité du remplacement. Pas de gain performance revendiqué,
overlays hors benchmark. Pas de nouvelle revue humaine ni qualification de gate.

Après le build, seul le texte d'aide CLI a été complété ; le binaire archivé
ne contient pas encore cette mention d'aide, mais ses options sont exécutées.
