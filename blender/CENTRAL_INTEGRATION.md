# Central station — issue 117

Generated with Blender 5.2.2, twelve FBX assets in `Assets/_Project/Art/Models/Central`, editable scene `blender/sources/Central.blend`. Blender Z is vertical; export converts to Unity Y. Rest positions and roots are at the origin; placement belongs to scene/prefab assembly.

| Model | Moving parts | Blender animation axis |
| --- | --- | --- |
| CentralSteeringWheel | Wheel | Y |
| CentralDepthTrimConsole | DepthWheel, TrimWheel, DepthNeedle, TrimNeedle | Y |
| EngineTelegraphFivePosition | CommandHandle | Y, five stops -70/-35/0/35/70 degrees |
| CentralChartTable | Drawer | Y translation |
| ChartPaperBlank | DrawablePaper | UV0 drawing surface |
| GreasePencil | Pencil | Whole portable root |
| ManualOK114OpenBinder | LeftCover, RightCover, TurnableLeaf00–29 | Y hinges |
| CentralInstrumentConsole | Cabinet | Mount_Gauge00–13 accept reusable animated gauge modules |
| PneumaticArrivalStation | ArrivalHatch | Z hinge |
| PneumaticCapsule | Cap | Y hinge |
| RolledOrderPaper | Roll | Whole portable root |
| ManualOK114LoosePage | DrawablePage | Whole portable root, UV0 |

The verified FBX point mapping is Blender (x,y,z) to Unity (-x,z,-y). Blender Y rotations map to Unity Z with the same sign, Blender Z rotations to Unity Y with sign reversal. Always validate the imported prefab's local rotation during wiring. No animation clips are embedded; separate pivots support interaction-driven motion.

The manual is 8 kg as required by the GDD. Other portable masses are documented prototype assumptions in `central_manifest.json`. Every paper mesh has normalized UV0. Thirty double-page PNG layouts are art templates only; final procedure text and illustrations remain to author from approved game rules. Each page samples one half of a spread. The default FBX material remains vertex colored. Assign a page shader/material for final artwork and use the loose-page mesh for torn-out page interaction.

The pneumatic capsule is hollow and has a hinge lid. Parent RolledOrderPaper at Mount_RolledOrder. Station mount Mount_Capsule is the arrival socket; Mount_AirInlet and Mount_Whistle support pipe and audio integration. The instrument console provides fixed faces for visual completeness and mounting sockets for replacing them with the animated instrument kit.

Checked: every mesh has finite vertices, nonzero triangle area and Col vertex colors. Preview images include manual leaf turning, arrival hatch opening and capsule lid opening. Unity import validation, colliders, gameplay wiring, page materials and final procedure artwork are separate integration tasks. The periscope is P2 in the GDD and excluded from this P1 kit.

Unity entry points: `SousTension.EditorTools.CentralAssetValidation.Run` checks all twelve imported FBX, triangles, color shader, exact pivot/socket positions, widths in metres, UV0 and manual leaf count. `CentralAssetValidation.BuildPrefabs` creates `CentralChartStation` and `CentralMultiConsole` assembled prefabs. These editor methods must be run by the shared Unity verification pass; their existence does not imply a successful Unity check.
