# Tidewater — hauteur FFT au point terminal sous l'eau

Référence : `dgreenheck/tidewater@1438b1abfcaee3267092b75573014f4d9b4a983c`,
`src/ocean/WaterQuery.js`. Là où aucun pixel de la surface déplacée n'est
visible, le repli du shader sous-marin réévalue maintenant la hauteur au point
terminal du rayon. Il reprend les quatre cascades FFT, leur atténuation selon
la profondeur, les vagues de rivage et deux itérations inverses horizontales.
Il réutilise le groupe de ressources du `WaterMaterial` Bevy ; aucun second
device GPU ni champ de vagues dupliqué n'est introduit.

Le masque de la vraie surface visible reste prioritaire. Une garde locale de
lentille utilise la normale de la requête GPU de la même frame pour écarter
les auto-intersections proches quand la caméra est dans l'air. La composition
du shader vérifie la présence de cette garde ; un test moteur couvre cette
régression. Le test `tidewater_underwater` passe à 8/8.

Build natif PASS. Le parcours de nage
`E:/OpenRPG_Forge/.tidewater-integration/native-underwater-water-speed-150-001`
passe à l'échelle d'eau `1,5×`, avec pipeline sous-marin prêt, 3 809 ticks et
trois paires d'images FHD à état figé. Float `ACTIVE`/`BYPASSED` est identique
pixel par pixel ; Dive et Neutral changent respectivement 73,741 % et 100 %
des pixels RGB. La capture Dive montre une ligne d'eau mixte et le fond marin
sans teinte sous-marine ajoutée à la partie vue depuis l'air.

Ce parcours valide l'exécution et le comportement A/B, pas la parité de chaque
rayon avec l'oracle navigateur. La cible RG séparée, les autres matériaux
transparents, les caustiques, la chaîne finale et WebGPU restent ouverts.
