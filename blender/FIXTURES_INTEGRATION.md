# V1 interior fixtures — GDD annex 1–7

42 modular assets, `kit_fixtures.build_all()`, Blender 5.2.2, metre units, `Col` vertex colors. FBX/manifests: `Assets/_Project/Art/Models/Fixtures`. Editable source: `blender/sources/Fixtures.blend`. Previews: `scratch_out/preview/Fixtures` (42 normal and two deployed/open views). Manifest records all parts, local pivots and mount positions in Blender coordinates.

| GDD asset | Model(s) supplied |
|---|---|
| Interior curved bow/stern | HullInteriorCurvedBow, HullInteriorCurvedStern |
| Vertical ladder and hatch | ConningLadderHatch |
| Interior conning tower | ConningTowerInterior |
| Periscope, folded handles and external head | PeriscopeInterior, PeriscopeExternalHead |
| Cable tray and junction-box kit | CableTrayStraight, CableJunctionBox |
| Bearing compass, Cras rule, stopwatch | BearingCompass, CrasNavigationRule, NavigationStopwatch |
| Ship clock | ShipClock |
| Chalkboard and chalk | Chalkboard, ChalkStick |
| Official portrait frame | CommandantPortraitFrame (blank artwork UV0) |
| Watch officer chair | WatchOfficerChair |
| Vacuum-tube electronics rack | VacuumValveElectronicsRack |
| Control rod drive column | ControlRodDriveColumn |
| Core viewing porthole | CoreViewingPorthole |
| Steam generator | SteamGenerator |
| Wall dosimetry and alarm | WallDosimetryAlarm |
| Controlled-zone curtain | ControlledZoneStripCurtain |
| Emergency boron container | EmergencyBoronCan |
| Propeller shaft and stuffing box | PropellerShaftLine, ShaftStuffingBox |
| Air compressor and clean/used CO2 cartridges | AirCompressor, CO2CartridgeNew, CO2CartridgeUsed |
| Workbench and tool board | MachineWorkbench, WorkshopToolboard |
| Duct fan | DuctVentilator |
| Gramophone and anthem record | MessGramophone, AnthemGramophoneRecord |
| Samovar | ElectricSamovar |
| Marine toilet and wall procedure | MarineToiletSevenValve, MarineToiletProcedureBlank |
| Cargo ceiling hoist and rail | CargoCeilingHoistRail, CargoCeilingHoist |
| Inert training torpedo | InertTrainingTorpedo |
| Poster, period photo, paper label supports | PropagandaPosterBlank, PeriodPhotographBlank, PaperLabelBlank |

The straight hull remains in Structure; new end sections are 3m long with a 6m-wide compatible entrance profile, tapering to 42% width. They are interior surface meshes with structural ribs, not exterior hull LODs. The conning tower has a circular ladder opening and an intentional open rear access sector. The 2m tray has four cables and end sockets. Toolboard contains hooks and seven Mount_Tool sockets for existing tools; tools are not baked in.

Animation axes in Blender: periscope SlidingMast translates Z (0–.65m), RotatingEyepiece rotates Z, FoldHandleLeft/Right rotate Y, ElevationCrank rotates Y. The nested handles belong to the eyepiece and it belongs to the sliding mast. Periscope slap uses the same rotation hierarchy; no slap animation clip or gameplay collision is provided. ExternalHead mounts at the mast tip and must follow its travel. The hatch lid rotates X and its wheel is a child. Junction ServiceLid rotates Z. CompassRose rotates Z; clock HourHand/MinuteHand/SecondHand rotate Y. SwivelSeat rotates Z. Four ControlRod parts translate Z. Nine FlexibleStrip parts rotate X individually. PackingCompressionNut rotates Y, RotatingShaft rotates Y. Compressor Flywheel and FanRotor rotate Y. ViceMovingJaw translates Y. Gramophone Turntable rotates Z and Tonearm rotates Z. Samovar TapHandle rotates Z and BoilerLid translates Z. Toilet ProcedureValve00–06 rotate Y independently and SeatLid rotates X. Hoist moves along rail Y; ChainAndHook can translate Z for load travel. Torpedo TailPropeller rotates Y.

Blank UV0 surfaces exist for Chalkboard, CommandantPortraitFrame, MarineToiletProcedureBlank, PropagandaPosterBlank, PeriodPhotographBlank and PaperLabelBlank. Reuse the poster mesh with eight finalized visuals; no new propaganda content, historical photograph, portrait likeness or toilet procedure is invented. The seven-valve toilet is an art interpretation, not an engineering/history certification or validated operating procedure.

Emission/transparent materials need Unity assignment: CherenkovGlass, AlarmLens, VacuumTube_* and FlexibleStrip*. Their default exported material remains vertex colored. Vacuum tubes are separate meshes with glass envelope, socket and amber internal element for state-dependent heating presentation. The core window contains a thick glass disk and glow mount. Steam outlet/primary ports/leak socket, boron label/grip, shaft turbine/stuffing-box sockets, cartridge filter/grip sockets, controlled-zone portal and toilet inlet/outlet/procedure mounts are explicit.

Geometry includes finite vertices, nonzero triangle areas and Col colors. Representative inspection covered tapered hull, deployed periscope, open toilet/seven-valve plumbing, gramophone horn, tube rack, compressor, samovar and hoist. Portable mass, colliders, physics constraints, deformation, equipment behavior, actual fluid dynamics, audio, historical validation and final illustrated procedures are outside this geometry delivery. No Unity process, shared environment, common builder or build.ps1 was modified by this lot. Current FBX export follows shared lib.py nested-part handling.
