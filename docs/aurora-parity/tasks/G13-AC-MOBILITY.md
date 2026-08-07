# G13-AC — Mobilité avancée

Features : `ORF-GAME-005`, `ORF-GAME-006`, `ORF-GAME-007`.

Le profil gameplay configure apesanteur à six axes, inertie et propulseurs, véhicules à sièges
bornés et montures avec politique de combat. Le résolveur retourne une décision déterministe et
refuse les transitions ou sièges invalides.

Preuves : `G13AC_Mobility_ResolvesZeroGravityVehicleAndMountPolicies` et marqueurs
`G13AC_MOBILITY_PASS` Toolset/runtime.
