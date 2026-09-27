# Water optical comparison profiles

`clear-medium.preview.json` is a Forge-original synthetic RGB medium, not measured
water data or a qualified default. Coefficients are inverse metres in linear
light, not sRGB colors. It is the same input exercised in the native medium/contact
proofs and can be passed to `forge_runtime --water-medium-input` with the required
bounded fixed-capture options. Studio's explicit `clear-medium-preview` route
embeds this file into the build and applies it only to its loaded water copy.
No canonical map or saved authoring document opts in automatically.
