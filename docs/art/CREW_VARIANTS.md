# Bibliothèque personnages V1 — géométrie

Générateur : `blender/kit_crew_variants.py`. Source éditable : `blender/sources/CrewVariants.blend`. Exports : `Assets/_Project/Art/Models/CrewVariants/`. Aperçus : `scratch_out/preview/CrewVariants/`. Le lot Crew d'origine est conservé.

## Comptage exact

- 6 têtes, avec volumes faciaux/nez/proportions distincts.
- 12 moustaches, 8 coiffures.
- 10 couvre-chefs : casquette à visière, bonnet de quart, bonnet à pompon, béret, toque, calot, chapka, casquette plate, casque de pont, capuche d'hiver.
- **50 cosmétiques au total = ces 10 couvre-chefs + 40 petits accessoires** : insignes/médailles, ceintures, bretelles, foulards/cravate, gants, lunettes, protections auditives, petits objets de poche, sacoches et brassards. Les têtes/moustaches/coiffures sont comptées séparément.
- 4 uniformes complets : vareuse, tenue de sortie, tablier de cuisine et pyjama réglementaire.
- 3 personnages complets supplémentaires : scaphandre porté, matelot contaminé, Commandant Varga.
- 1 flacon de sels de réveil transportable.

Total : **84 FBX**. Le manifeste `crew_variants_manifest.json` liste chaque export, ses triangles, sa famille et son montage. Les variantes présentent des différences géométriques ; les recolorations seules ne sont pas comptées comme nouveaux accessoires.

## Squelette et montage

Les 7 personnages complets reprennent les **27 os et poses de repos de SailorBase**, avec skinning et 4 clips prototypes Idle/Walk/ValveTurn/Faint. Les accessoires de costume sont pondérés sur les os existants. Ils n'ajoutent aucun os. Importer comme rig **Generic**, créer l'Avatar depuis le modèle et conserver la hiérarchie. Le postprocesseur Crew couvre désormais ces sept noms dans `Models/CrewVariants/`, avec `importAnimation=true` et `animationType=Generic`. Le build V1 crée leurs controllers et prefabs et valide les avatars/meshes/clips dans Unity. Les 77 addons restent statiques et utilisent le matériau couleur de sommets partagé.

Les addons ont une origine à leur point de montage et un empty `Mount_Attachment`. Le manifeste indique `skeletonBone`, `anchorRestPositionBlender`, `offsetBlenderRigAxes` et `boneLocalOffsetBlender`. Les mesures sont en mètres, axes Blender Z haut. Pour une pose de repos, la position dans l'espace du rig est **anchorRestPositionBlender + offsetBlenderRigAxes**. Le champ boneLocalOffsetBlender est ce décalage exprimé dans les axes du bone Blender ; éviter de le recopier tel quel dans un bone Unity dont la conversion FBX change les axes. Pour Unity, convertir la position de repos en Y haut puis calculer la position locale avec `bone.InverseTransformPoint(rig.TransformPoint(restPositionUnity))`. Calculer la rotation locale de la même manière depuis la rotation du rig convertie, plutôt que d'inventer un Euler compensateur.

Les têtes/moustaches/cheveux/couvre-chefs sont des meshes statiques à attacher à Head. Les personnages complets contiennent toujours la tête/casquette/moustache de base. Pour un système de personnalisation, retirer/masquer les faces pondérées sur Head du mesh de base avant d'ajouter une nouvelle tête ; sinon les volumes se superposent. Les accessoires de tenue portés sont inclus dans les personnages complets. Les masses des petits props sont des hypothèses de prototype à équilibrer, pas un système physique intégré.

## Validation et limites

Le générateur vérifie géométrie finie, couleur de sommets, UV et triangles d'aire positive. `blender/validate_crew_variants.py` réimporte chaque FBX et vérifie les mêmes propriétés, l'unicité géométrique par famille, les 27 os/poids normalisés et les 4 clips, avec 5 échantillons de pose par clip. Rapport : `crew_variants_validation.json`.

Les UV sont des projections de support, pas un dépliage artistique fini. Les insignes/plaques sont sans texte ; les inscriptions finales demandent les contenus approuvés et localisés. Les gants se montent comme addons, leur adaptation aux doigts et leur éventuel skinning relèvent de l'intégration. Le scaphandre fournit casque/grille/hublot/harnais et appareil porté ; la transparence/refraction du verre, l'ombilical simulé et les transitions d'habillage ne sont pas implémentés. La variante contaminée fournit salissures et accessoire lisible ; la surcouche shader animée/contamination gameplay reste à réaliser. Varga conserve les gestes prototypes du matelot ; sa mise en scène finale et son ombre sous la porte demandent une intégration.

Les clips ne fournissent pas d'IK sur objets, ragdoll contrôlé, traînage, animation de douche ni pipeline de déblocage cosmétique. Les proportions et l'ergonomie doivent être validées dans une scène Unity. La livraison couvre des assets 3D V1 géométriques, pas la finition de tous les systèmes décrits au GDD.
