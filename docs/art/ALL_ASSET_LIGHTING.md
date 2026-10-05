# Scènes Unity, éclairage précalculé et captures

Le workflow `blender/build_showcase.ps1` crée les scènes de présentation de toute la bibliothèque, prépare les UV2 des modèles statiques, applique les réglages d'éclairage, lance les véritables bakes Unity, enregistre puis rouvre chaque scène pour contrôler ses lightmaps, et produit des captures natives URP. La scène `BoatEnvironment_Lit` présente l'environnement intérieur complet avec ses six compartiments.

Les résultats réellement obtenus sont enregistrés dans `ALL_ASSET_LIGHTING_BAKE.json`. Le manifeste `scratch_out/all-asset-scenes.json` donne la liste des scènes et des prefabs représentés. Les captures et leurs métadonnées figurent dans `scratch_out/preview/AllAssets/screenshots.json`. L'index HTML est généré avec `python tools/build_all_asset_index.py` seulement après la réussite des captures.

## Réglages d'éclairage

Trois LightingSettings partagés sont enregistrés dans `Assets/_Project/Art/Lighting` : Gallery, World et Interior. GI baked activée, GI temps réel désactivée, Progressive CPU, atlas de 1024 pixels maximum, padding de 4 pixels, compression et lightmaps non directionnelles. Résolutions respectives : 12, 8 et 16 texels/mètre avant application du scaleInLightmap de chaque renderer. Échantillons directs 32, indirects 128, environnement 64, deux rebonds. Occlusion ambiante indirecte de portée 0,35 m.

Les petites pièces ont une densité accrue ; les grandes structures World et les sols de galerie une densité réduite. Les UV2 sont générés par le ModelImporter pour les FBX statiques. Le workflow vérifie leur présence avant le bake. Les personnages skinnés et les surfaces d'effets restent dynamiques et utilisent des sondes d'éclairage. Aucun personnage n'est artificiellement rendu statique pour remplir une lightmap.

Le filtre de surfaces dynamiques cible les vrais supports d'eau de cale, vapeur, decals et glow cards. Il ne traite pas les rochers, falaises et bâtiments `Underwater*`, ni la lance de décontamination, comme des surfaces d'eau. Le contrôle visuel a permis de corriger cette exclusion et de recuire les scènes concernées. Les MeshRenderer dynamiques demandent explicitement ReceiveGI.LightProbes ; les solides statiques utilisent ReceiveGI.Lightmaps.

Les galeries utilisent deux lumières directionnelles baked : une lumière principale chaude de 1,4 et un remplissage frontal plus neutre de 1,2. Le contrôle visuel du premier bake a montré des surfaces vertes et des uniformes trop sombres ; le remplissage améliore leur lisibilité sans ajouter de lumière runtime. Dans le bateau, les six lampes ordinaires sont baked, chaudes, d'intensité 18 et de portée 6,5 m ; les lampes d'urgence restent éteintes dans l'état normal présenté. Les lentilles chaudes émettent avec un multiplicateur HDR de 5 ; les affichages et voyants appropriés reçoivent également des matériaux émissifs URP Lit avec participation au bake. Les autres surfaces conservent leurs matériaux et couleurs de sommets. Le shader VertexColorLit possède une passe META cohérente avec son albedo et son émission, lit les lightmaps et les sondes, et partage son CBUFFER entre ses passes locales pour le SRP Batcher.

Le profil `ShowcasePost` applique un bloom discret (intensité 0,20, seuil 1) et un tonemapping Neutral. Les caméras utilisent HDR et FXAA. Les captures conservent les lightmaps et le post-traitement des scènes enregistrées. La vue en coupe du bateau masque temporairement un côté et le toit et ajoute une lumière de présentation : cette variante de capture est explicitement identifiée dans le rapport et ne modifie pas la scène sauvegardée.

## Budget des lumières temps réel

Le profil PC URP est réglé sur une shadowmap principale de 1024 pixels, deux cascades, une distance d'ombre de 35 m, qualité d'ombre douce faible et deux lumières supplémentaires par objet. Les ombres des lumières supplémentaires sont désactivées et le SRP Batcher activé. Les scènes de présentation enregistrées utilisent des lumières baked ; la lampe de coupe est temporaire et sert seulement à la capture.

Ces paramètres réduisent les calculs d'éclairage temps réel et la taille des lightmaps. Ils ne remplacent pas une mesure de performances en gameplay sur le matériel cible. Le rapport indique les nombres réels de lightmaps, renderers lightmappés, sondes et lumières runtime pour chaque scène.

## Reproduction

Fermer les autres instances Unity utilisant ce projet, puis exécuter depuis PowerShell :

```powershell
./blender/build_showcase.ps1
```

L'index est généré automatiquement avec le Python fourni par Blender ; le paramètre `-Python` permet d'utiliser un autre interpréteur. `-BakeOnly` recuit les scènes déjà listées dans le manifeste. `-CaptureOnly` produit de nouvelles captures depuis les lightmaps sauvegardées. Le log Unity est `scratch_out/all-asset-lighting.log`. Les scènes générées, matériaux, LightingSettings et lightmaps restent éditables dans Unity.
