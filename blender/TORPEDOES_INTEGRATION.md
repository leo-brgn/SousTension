# Torpilles / cargo — supports de développement (#121)

Générateur : `blender/kit_torpedoes.py`, API `build_all()` (3 assets). Export Blender 5.2.2, FBX en mètres, couleurs `Col`, UVMap, géométrie finie et triangles > 1e-12 m². Source `blender/sources/Torpedoes.blend`. Les rendus dans `scratch_out/preview/Torpedoes` incluent une culasse ouverte à 108° ; le FBX et la source sont fermés au repos.

| Asset | Encombrement source approximatif | Usage |
|---|---|---|
| CargoTorpedoTube | 0,98 × 2,96 × 0,95 m | Tube réellement creux Ø intérieur 0,77 m ; fond fermé, deux appuis intérieurs. |
| CargoRack | 1,36 × 0,96 × 1,57 m | 3 étagères et 6 points d'arrimage. |
| CargoTieDownStrap | 0,08 × 1,25 × 0,07 m | Bande rigide de référence, cliquet à pivot ; simulation souple à développer. |

Les coordonnées du manifeste sont en Blender : X largeur, Y longueur, Z haut, devant vers -Y. Unity convertit l'axe vertical en Y. `BreechDoor` pivote autour de Z local Blender (Y Unity), `LockWheel` et `Dog0` à `Dog5` autour de Y Blender (Z Unity). Les loquets sont enfants de la porte et le volant conserve son pivot central. Test visuel ouvert : rotation Z = -108°. Déverrouiller les loquets avant d'ouvrir ; ceci reste une animation à programmer.

Pour quatre tubes dans un compartiment de 4 m de longueur et 6 m de large, placer les origines source (X,Y,Z) : (-2,0,0), (-2,0,1.02), (2,0,0), (2,0,1.02). Culasses côté -Y. Deux piles latérales laissent une coursive centrale d'environ 3 m. Réserver au moins 0,65 m devant les culasses pour leurs ouvertures. Le tube supérieur nécessite des supports muraux/scène supplémentaires ; ne pas interpréter cette disposition comme calcul structurel. L'ouverture intérieure est un casier à objets, pas un passage joueur.

`Mount_CargoInside` se trouve à (0,0,0.21), les extrémités à Y ±1.40. Utiliser `Mount_CargoShelf0..2` et `Mount_TieDown00..12` sur le râtelier, `Mount_EndA/B` sur la sangle. Le manifeste contient chaque position et rotation des mounts, les triangles et les axes d'animation.

Colliders : tubes statiques avec collider concave dédié pour préserver le trou ; ne pas mettre un collider convexe sur tout le tube, qui fermerait le casier. Culasse mobile : collider séparé sur `BreechDoor`. Colliders d'étagère séparés sur le râtelier. La sangle est un support visuel rigide et ses deux extrémités doivent être attachées au système de cargo. Pas de Rigidbody, interaction, simulation d'arrimage ni torpille décorative dans ce lot.
