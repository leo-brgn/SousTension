# Matelot Crew — proposition complète issue du moodboard

Référence : `moodboard/v2_references/11_equipage_personnages.png`, principalement le deuxième marin en partant de la gauche. Révision du 5 octobre 2026.

## Résultat livré

Un matelot complet, avec casquette inclinée et insigne étoilé, visage, moustache recourbée avec mèches en relief, col rabattu, trois boutons, épaulettes, veste, pantalon, mains et bottes. Le corps et les manches ont été redessinés pour retrouver la silhouette en œuf de la référence. Les mains FP suivent le même dessin.

| Critère | Ancien modèle | Proposition actuelle |
|---|---|---|
| Proportions | Tête petite, jambes et bottes hautes | Tête élargie, jambes courtes, petites bottes ; hauteur totale environ 1,83 m |
| Silhouette | Torse et cuisses en volumes séparés | Ventre large et pantalon fusionné, sans boules de cuisses visibles |
| Bras | Bras écartés et épaules segmentées | Manches continues ; pose de repos avec mains près des hanches |
| Visage | Facettes et moustache en deux ovales | Surfaces lisses, nez arrondi, yeux ponctuels, sourcils et moustache recourbés |
| Uniforme | Col rectangulaire et casquette horizontale | Col à pointes arrondies, casquette inclinée, épaulettes brunes et boutons laiton |
| Matières | Aspect uniforme sur tout le personnage | Tissu mat, peau satinée, laiton métallique, bottes plus brillantes |

L'insigne étoilé rouge reprend volontairement la photo demandée. Il remplace la convention d'insigne entièrement laiton utilisée dans la première proposition.

## Fichiers

- `blender/sources/Crew.blend` : source modifiable avec rig, UV et matériau. Les mains FP sont masquées à l'ouverture pour présenter seulement le personnage complet.
- `blender/sources/CrewPresentation.blend` : personnage complet avec studio Cycles, éclairage et caméra de présentation.
- `Assets/_Project/Art/Models/Crew/SailorBase.fbx` : corps, 27 os, quatre clips prototypes.
- `Assets/_Project/Art/Models/Crew/FirstPersonArms.fbx` : mains et bras FP, 17 os, deux clips prototypes.
- `Assets/_Project/Art/Scenes/CrewReview.unity` : scène native de contrôle du prefab Unity.
- `scratch_out/preview/CrewRework/index.html` : moodboard, rendus Cycles et captures Unity.

Le modèle compte **25 672 sommets / 51 068 triangles** dans Blender, un mesh skinné et un matériau. Les mains FP comptent 11 504 triangles. Ces valeurs ne constituent pas un benchmark de performance mobile.

Le matériau `CrewSurface` stocke les couleurs en RGB sRGB et la famille de surface dans l'alpha : tissu 0, peau 0,25, laiton 0,5, cuir 0,75, cheveux 1. Le shader Unity `SousTension/CrewVertexLit` restitue les différences de rugosité et de métal avec un seul matériau. Le grain très fin du tissu dans Cycles est procédural ; il n'est pas exporté dans le FBX. Les UV sont fournis pour une éventuelle finition texturée.

## Validation

Le réimport FBX dans Blender vérifie les os, les poids normalisés, la présence des UV, les triangles non dégénérés, les codes de surfaces et cinq instants pour chacun des clips. Les captures Workbench montrent aussi les poses de valve, de prise et d'évanouissement. Les poses restent des prototypes à raccorder au gameplay, pas une locomotion finale ni un ragdoll.

Le contrôle Unity vérifie l'avatar Generic, les références du prefab, les bindposes, les UV, les couleurs, le shader dédié et les clips, puis produit deux captures natives. Rapports : `crew_validation.json`, `crew_manifest.json`, `scratch_out/crew-review-unity.log` et `scratch_out/preview/CrewRework/geometry.json`.

## Périmètre

Les noms d'os et les GUID des deux FBX sont conservés. Les positions de repos du rig ont été ajustées aux nouvelles proportions : les accessoires et animations externes conçus pour les anciennes proportions nécessitent une vérification de leur placement. Les sept personnages CrewVariants et les anciennes copies PBR ne sont pas inclus dans cette refonte ; leurs FBX existants restent autonomes, mais leurs générateurs/accessoires devront être adaptés avant une régénération fondée sur ce nouveau corps.

Le modèle suit la photo sans prétendre la reproduire au pixel près : la photo représente plusieurs marins et un éclairage de couloir, tandis que les vues de contrôle utilisent un studio. Le visage reste volontairement simple et le dos est interprété à partir de la vue frontale disponible.

## Reproduction

Génération : `blender/build.ps1 -Kit Crew -Preview`. Vérification : Blender background avec `blender/validate_crew.py` et le dossier FBX absolu après `--`. Présentation : `blender/preview_crew_review.py`. Unity batch : `-executeMethod SousTension.EditorTools.CrewReviewCapture.Run`.
