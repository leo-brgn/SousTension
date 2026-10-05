# Couverture géométrique V1 — tous les assets de l’annexe 1.0

387 FBX ; 109 lignes de besoins couvertes. Ce pointage couvre les supports 3D P1/P2/P3, pas la finition artistique et fonctionnelle.

| Besoin GDD | Priorité | Modèles V1 |
|---|---|---|
| Section de coque intérieure droite (module 2 m) | P1 | `Structure/HullModule_2m` |
| Section de coque courbe proue | P2 | `Fixtures/HullInteriorCurvedBow` |
| Section de coque courbe poupe | P2 | `Fixtures/HullInteriorCurvedStern` |
| Cloison pleine inter-compartiments | P1 | `Structure/Bulkhead_Solid` |
| Sas manuel de cloison 🔧 | P1 | `Structure/Bulkhead_Hatch`, `Structure/HatchDoor` |
| Plancher caillebotis (module) | P1 | `Structure/FloorGrating_1m` |
| Fond de cale sous caillebotis | P1 | `Structure/BilgeFloor_2m` |
| Échelle verticale + trémie | P2 | `Fixtures/ConningLadderHatch` |
| Kiosque intérieur (habitacle périscope) | P2 | `Fixtures/ConningTowerInterior` |
| Coque extérieure complète classe Molosse | P2 | `World/MolossHull_LOD0_Wear0`, `World/MolossHull_LOD0_Wear1`, `World/MolossHull_LOD0_Wear2`, `World/MolossHull_LOD1_Wear0`, `World/MolossHull_LOD1_Wear1`, `World/MolossHull_LOD1_Wear2`, `World/MolossHull_LOD2_Wear0`, `World/MolossHull_LOD2_Wear1`, `World/MolossHull_LOD2_Wear2` |
| Barres de plongée, safran, hélice (ext.) 🔧 | P2 | `World/MolossDivingPlane`, `World/MolossRudder`, `World/MolossPropeller` |
| Kiosque extérieur, antennes, périscope sorti | P2 | `World/MolossConningTower`, `World/MolossAntenna`, `World/MolossPeriscopeHead` |
| Variantes d'usure de coque ext. (3 niveaux) | P3 | `World/MolossHull_LOD0_Wear0`, `World/MolossHull_LOD0_Wear1`, `World/MolossHull_LOD0_Wear2` |
| Tuyauterie modulaire (droite, coude, T, vanne inline) | P1 | `Structure/Pipe_Straight_1m`, `Structure/Pipe_Elbow`, `Structure/Pipe_Tee`, `Structure/Pipe_ValveInline` |
| Chemin de câbles + boîtiers de jonction (kit) | P2 | `Fixtures/CableTrayStraight`, `Fixtures/CableJunctionBox` |
| Plaques gravées / panneaux réglementaires (kit, ~15 formes) | P2 | `Signage/Plate_Torpedoes`, `Signage/Plate_Central`, `Signage/Plate_Radio`, `Signage/Plate_Reactor`, `Signage/Plate_Machines`, `Signage/Plate_Living`, `Signage/Plate_Primary`, `Signage/Plate_Bilge`, `Signage/Plate_Electric`, `Signage/Plate_Interphone`, `Signage/Plate_Manual`, `Signage/Plate_Pneumatic`, `Signage/Plate_Battery`, `Signage/Plate_Scram`, `Signage/Plate_Registry` |
| Tube lance-torpilles (x4 visibles) 🔧 | P2 | `Torpedoes/CargoTorpedoTube` |
| Râtelier de stockage cargo 🔧 | P2 | `Torpedoes/CargoRack` |
| Sangles d'arrimage 🔧 | P2 | `Torpedoes/CargoTieDownStrap` |
| Palan/rail de manutention plafond 🔧 | P3 | `Fixtures/CargoCeilingHoistRail`, `Fixtures/CargoCeilingHoist` |
| Torpille d'exercice inerte (déco) 📦 | P3 | `Fixtures/InertTrainingTorpedo` |
| Barre de direction (volant) 🔧 | P1 | `Central/CentralSteeringWheel` |
| Pupitre de barre : profondeur/assiette (2 volants + indicateurs) 🔧 | P1 | `Central/CentralDepthTrimConsole` |
| Télégraphe machine 🔧 | P1 | `Central/EngineTelegraphFivePosition` |
| Périscope 🔧 | P2 | `Fixtures/PeriscopeInterior`, `Fixtures/PeriscopeExternalHead` |
| Table à cartes + carte papier 🔧 | P1 | `Central/CentralChartTable`, `Central/ChartPaperBlank` |
| Crayon gras 📦 | P1 | `Central/GreasePencil` |
| Compas de relèvement, règle Cras, chronomètre 📦 | P2 | `Fixtures/BearingCompass`, `Fixtures/CrasNavigationRule`, `Fixtures/NavigationStopwatch` |
| Le Manuel OK-114 📦🔧 | P1 | `Central/ManualOK114OpenBinder`, `Central/ManualOK114LoosePage` |
| Pupitre central multi-instruments | P1 | `Central/CentralInstrumentConsole` |
| Tube pneumatique du Commandant 🔧 | P1 | `Central/PneumaticArrivalStation` |
| Capsule pneumatique 📦 | P1 | `Central/PneumaticCapsule`, `Central/RolledOrderPaper` |
| Horloge de bord | P2 | `Fixtures/ShipClock` |
| Tableau à craie + craie 🔧📦 | P2 | `Fixtures/Chalkboard`, `Fixtures/ChalkStick` |
| Portrait officiel du Commandant (cadre) | P2 | `Fixtures/CommandantPortraitFrame` |
| Fauteuil du chef de quart | P2 | `Fixtures/WatchOfficerChair` |
| Console sonar à écran circulaire 🔧 | P2 | `Radio/SonarConsole` |
| Casque d'écoute sonar 📦🔧 | P2 | `Radio/SonarHeadphones`, `Radio/HeadphoneCableCoiled` |
| Hydrophone à manivelle 🔧 | P2 | `Radio/HydrophoneCrankStation` |
| Enregistreur à bandes magnétiques 🔧 | P2 | `Radio/MagneticTapeRecorder` |
| Bobine de bande vierge / enregistrée 📦 | P2 | `Radio/TapeReelBlank`, `Radio/TapeReelRecorded` |
| Poste radio VLF + antenne déployable 🔧 | P2 | `Radio/VLFRadioStation`, `Radio/VLFAntennaDeployable` |
| Machine à chiffrer (à clavier mécanique) 🔧 | P2 | `Radio/MechanicalCipherMachine` |
| Feuillets de messages / bloc de chiffrement 📦 | P2 | `Radio/CipherMessagePad` |
| Baie électronique à lampes (déco animée) | P3 | `Fixtures/VacuumValveElectronicsRack` |
| L'Émetteur du Signal (acte III) 🔧 | P3 | `Cargo/SignalEmitterFrame`, `Cargo/SignalEmitterPower`, `Cargo/SignalEmitterCoil`, `Cargo/SignalEmitterAerial`, `Cargo/SignalEmitterControls`, `Cargo/SignalEmitterAssembled` |
| Tableau de commande RK-1 « Petit Soleil » 🔧 | P1 | `Reactor/RK1_Console`, `Reactor/Breaker`, `Instruments/SelectorRotary`, `Instruments/GaugeRound_M` |
| Levier SCRAM 🔧 | P1 | `Reactor/ScramLever` |
| Colonne des barres de contrôle (mécanisme apparent) 🔧 | P2 | `Fixtures/ControlRodDriveColumn` |
| Hublot de visée du cœur | P2 | `Fixtures/CoreViewingPorthole` |
| Circuit primaire : pompes (x2) 🔧 | P1 | `Primary/PrimaryPump`, `Primary/PrimaryPump_Broken` |
| Vannes principales primaire (x4, volants) 🔧 | P1 | `Primary/PrimaryValve` |
| Échangeur / générateur de vapeur | P2 | `Fixtures/SteamGenerator` |
| Panneau dosimétrie mural + alarme | P2 | `Fixtures/WallDosimetryAlarm` |
| Rideau/portique de zone contrôlée | P3 | `Fixtures/ControlledZoneStripCurtain` |
| Bidon de bore d'urgence 📦 | P3 | `Fixtures/EmergencyBoronCan` |
| Turbine + réducteur | P2 | `Machines/TurbineReducer` |
| Tableau électrique principal 🔧 | P1 | `Machines/ElectricalPanel`, `Machines/MachineBreaker` |
| Batteries de secours (banc) 🔧 | P2 | `Machines/BatteryBank` |
| Pompe de cale (x2) 🔧 | P1 | `Machines/BilgePump` |
| Ligne d'arbre + presse-étoupe 🔧 | P2 | `Fixtures/PropellerShaftLine`, `Fixtures/ShaftStuffingBox` |
| Compresseur d'air / cartouches CO₂ 🔧📦 | P2 | `Fixtures/AirCompressor`, `Fixtures/CO2CartridgeNew`, `Fixtures/CO2CartridgeUsed` |
| Établi + panneau d'outils | P2 | `Fixtures/MachineWorkbench`, `Fixtures/WorkshopToolboard` |
| Interphone (combiné mural) 🔧 | P1 | `Machines/Interphone` |
| Ventilateur de gaine 🔧 | P3 | `Fixtures/DuctVentilator` |
| Sas de plongée (chambre + 2 portes + volants) 🔧 | P2 | `Living/DivingAirlockChamber` |
| Scaphandre sur son support 🔧 | P2 | `Living/DivingSuitOnRack` |
| Casque de scaphandre 📦 | P2 | `Living/DivingHelmet` |
| Ombilical + touret 🔧 | P2 | `Living/UmbilicalReel` |
| Douche de décontamination 🔧 | P2 | `Living/DeconShowerCabin` |
| Lance à eau de décon 📦🔧 | P2 | `Living/DeconWaterLance` |
| Couchettes superposées (x2 modules) | P2 | `Living/BunkBed_Double` |
| Cambuse : cuisinière + marmite de soupe 🔧 | P2 | `Living/GalleyStove`, `Living/SoupPot` |
| Vaisselle en fer émaillé (kit 6 pièces) 📦 | P2 | `Living/EnamelBowl`, `Living/EnamelMug`, `Living/EnamelPlate`, `Living/EnamelPotLid`, `Living/EnamelSaucepan`, `Living/EnamelSpoon` |
| Table + banquettes | P2 | `Living/MessTable`, `Living/MessBench` |
| Gramophone + disque de l'hymne 🔧📦 | P3 | `Fixtures/MessGramophone`, `Fixtures/AnthemGramophoneRecord` |
| Casiers personnels (x4) 🔧 | P3 | `Living/PersonalLocker` |
| Samovar 🔧 | P3 | `Fixtures/ElectricSamovar` |
| WC marin + sa procédure murale 🔧 | P3 | `Fixtures/MarineToiletSevenValve`, `Fixtures/MarineToiletProcedureBlank` |
| Porte du carré des officiers (fermée) | P2 | `Living/OfficersQuartersDoor` |
| Caisses réglementaires (S/M/L) 📦 | Vivres, pièces, munitions d'exercice | `Environment/CargoCrate_S`, `Environment/CargoCrate_M`, `Environment/CargoCrate_L` |
| Fûts 📦 | Carburant, saumure, « NE PAS OUVRIR » | `Cargo/FuelDrum`, `Cargo/BrineDrum`, `Cargo/SealedDrum` |
| Combustible nucléaire (château de transport) 📦 | Très lourd, crépite, à 2 joueurs | `Cargo/NuclearFuelTransportCask` |
| Pièces détachées 📦 | Pompe neuve, cartouches, fusibles, joints, roulement | `Cargo/SpareFuseBox`, `Cargo/SpareGasket`, `Cargo/SpareBearing`, `Cargo/SpareImpeller`, `Cargo/SpareMotor`, `Cargo/SparePipeSection`, `Fixtures/CO2CartridgeNew`, `Fixtures/CO2CartridgeUsed`, `Primary/PrimaryPump` |
| Objets d'épave à valeur 📦 | Coffre, cloche de bateau, hélice de bronze, instruments anciens | `Cargo/SalvageChest`, `Cargo/SalvageShipBell`, `Cargo/SalvageBronzePropeller`, `Cargo/AntiqueSextant` |
| Objets anachroniques (indices narratifs) 📦 | Jet-ski cassé, conteneur moderne, canard gonflable, panneau solaire | `Cargo/BrokenJetSki`, `Cargo/ModernContainer`, `Cargo/InflatableDuck`, `Cargo/SolarPanel` |
| Objets bruyants à haute valeur 📦 | Balise active, boîte à musique bloquée | `Cargo/ActiveBeacon`, `Cargo/JammedMusicBox` |
| Reliques kraviques (acte II) 📦 | Radeau de 1983, journal de bord, médailles | `Cargo/LifeRaft1983`, `Cargo/OldLogbook`, `Cargo/KravicMedalCase` |
| Les 5 pièces de l'Émetteur 📦 | Voir §4 | `Cargo/SignalEmitterFrame`, `Cargo/SignalEmitterPower`, `Cargo/SignalEmitterCoil`, `Cargo/SignalEmitterAerial`, `Cargo/SignalEmitterControls` |
| La base du fjord : ponton, grue 🔧, atelier, bureau des tampons, tableau d'affichage 🔧, baraquements, phare | P2 | `World/FjordWorkshop`, `World/FjordStampOffice`, `World/FjordBarracks`, `World/FjordStorageShed`, `World/FjordPontoon_4m`, `World/FjordGantryCrane`, `World/FjordNoticeBoard`, `World/FjordLighthouse`, `World/FjordMooringBollard`, `World/FjordGangway`, `World/FjordDockLadder`, `World/FjordFuelTank`, `World/FjordCargoPallet`, `World/FjordDockFence_2m`, `World/FjordWorkshopBench`, `World/FjordDockPowerPedestal` |
| Terrain sous-marin : kit rochers (8), tombants, sable, forêt de kelp (cartes), cheminée hydrothermale | P2 | `World/UnderwaterRock_00`, `World/UnderwaterRock_01`, `World/UnderwaterRock_02`, `World/UnderwaterRock_03`, `World/UnderwaterRock_04`, `World/UnderwaterRock_05`, `World/UnderwaterRock_06`, `World/UnderwaterRock_07`, `World/UnderwaterCliff`, `World/UnderwaterSandTile_8m`, `World/UnderwaterKelpCluster`, `World/HydrothermalVent` |
| Épaves kraviques : cargo, chalutier, sous-marin jumeau (grand frisson garanti) | P2–P3 | `World/WreckCargo`, `World/WreckTrawler`, `World/WreckTwinSub` |
| Dépôt militaire sous-marin (structure + sas) | P2 | `World/UnderwaterMilitaryDepot`, `World/DepotAirlock` |
| Filets de pêche industriels 🔧 | P2 | `World/IndustrialFishingNet` |
| Trafic civil de surface (coques vues du dessous) : ferry, porte-conteneurs, voilier, jet-ski, pédalo | P3 | `World/CivilFerry`, `World/CivilContainerShip`, `World/CivilSailboat`, `World/CivilJetSki`, `World/CivilPedalBoat` |
| Forces de l'Entente : frégate (surface + carène), avion de patrouille, bouée sonar 🔧, grenade d'exercice | P3 | `World/EntenteFrigate_LOD0`, `World/EntenteFrigate_LOD1`, `World/EntentePatrolPlane`, `World/SonarBuoy`, `World/ExerciseGrenade` |
| Parc éolien offshore, plateforme, bouées de chenal 🔧, iceberg (3), régate de fin (10 voiliers + foule low-poly) | P3 | `World/OffshoreWindTurbine`, `World/OffshorePlatform`, `World/ChannelBuoy`, `World/Iceberg_0`, `World/Iceberg_1`, `World/Iceberg_2`, `World/RaceSpectatorLowPoly`, `World/RaceSailboat_01`, `World/RaceSailboat_02`, `World/RaceSailboat_03`, `World/RaceSailboat_04`, `World/RaceSailboat_05`, `World/RaceSailboat_06`, `World/RaceSailboat_07`, `World/RaceSailboat_08`, `World/RaceSailboat_09`, `World/RaceSailboat_10` |
| Matelot de base (corps unique, pataud) | P1 | `Crew/SailorBase`, `Crew/FirstPersonArms` |
| Têtes (x6) + moustaches (x12) + coiffures (x8) | P2–P3 | `CrewVariants/Head_01`, `CrewVariants/Head_02`, `CrewVariants/Head_03`, `CrewVariants/Head_04`, `CrewVariants/Head_05`, `CrewVariants/Head_06`, `CrewVariants/Moustache_01`, `CrewVariants/Moustache_02`, `CrewVariants/Moustache_03`, `CrewVariants/Moustache_04`, `CrewVariants/Moustache_05`, `CrewVariants/Moustache_06`, `CrewVariants/Moustache_07`, `CrewVariants/Moustache_08`, `CrewVariants/Moustache_09`, `CrewVariants/Moustache_10`, `CrewVariants/Moustache_11`, `CrewVariants/Moustache_12`, `CrewVariants/Hair_01`, `CrewVariants/Hair_02`, `CrewVariants/Hair_03`, `CrewVariants/Hair_04`, `CrewVariants/Hair_05`, `CrewVariants/Hair_06`, `CrewVariants/Hair_07`, `CrewVariants/Hair_08` |
| Uniformes : tenue de bord (P1), vareuse, tenue de sortie, tablier de cuisine, pyjama réglementaire | P2–P3 | `Crew/SailorBase`, `CrewVariants/Vareuse`, `CrewVariants/DressUniform`, `CrewVariants/CookApron`, `CrewVariants/RegulationPyjamas` |
| Casquettes/bonnets (x10) + cosmétiques divers (x50 en 1.0 : la plupart = petits props) | P3 | `CrewVariants/PeakedCap`, `CrewVariants/WatchBeanie`, `CrewVariants/WoolBonnet`, `CrewVariants/ServiceBeret`, `CrewVariants/CookCap`, `CrewVariants/SideCap`, `CrewVariants/EarflapCap`, `CrewVariants/FlatCap`, `CrewVariants/DeckHardhat`, `CrewVariants/WinterHood`, `CrewVariants/RoundBadge`, `CrewVariants/SquareBadge`, `CrewVariants/AnchorBadge`, `CrewVariants/ChevronBadge`, `CrewVariants/WingBadge`, `CrewVariants/MedalSingle`, `CrewVariants/MedalPair`, `CrewVariants/RibbonBar`, `CrewVariants/ServiceStars`, `CrewVariants/Epaulette`, `CrewVariants/DeckBelt`, `CrewVariants/UtilityBelt`, `CrewVariants/Suspenders`, `CrewVariants/Scarf`, `CrewVariants/Neckerchief`, `CrewVariants/BowTie`, `CrewVariants/Tie`, `CrewVariants/GloveLeft`, `CrewVariants/GloveRight`, `CrewVariants/MittensPair`, `CrewVariants/SpectaclesRound`, `CrewVariants/SpectaclesSquare`, `CrewVariants/Monocle`, `CrewVariants/Goggles`, `CrewVariants/EarDefenders`, `CrewVariants/Whistle`, `CrewVariants/PocketWatch`, `CrewVariants/PocketNotebook`, `CrewVariants/PenBundle`, `CrewVariants/PencilClip`, `CrewVariants/KeyRing`, `CrewVariants/ToolPouch`, `CrewVariants/Canteen`, `CrewVariants/Satchel`, `CrewVariants/ShoulderBag`, `CrewVariants/Armband`, `CrewVariants/WristCuff`, `CrewVariants/CompassPendant`, `CrewVariants/ClothPatch`, `CrewVariants/NamePlateBlank` |
| Scaphandre porté (variante complète du personnage) | P2 | `CrewVariants/DivingSuit` |
| Version « contaminé » (surcouche shader + accessoires) | P2 | `CrewVariants/ContaminatedSailor` |
| État évanoui (pose ragdoll contrôlée + traînage) | P2 | `Crew/SailorBase`, `CrewVariants/WakeUpSalts` |
| Le Commandant Varga | P3 | `CrewVariants/CommanderVarga` |
| Kit transversal Instruments | P1/P2 | `Instruments/ButtonGuarded`, `Instruments/CounterRollers`, `Instruments/Crank`, `Instruments/GaugeRound_L`, `Instruments/GaugeRound_M`, `Instruments/GaugeRound_S`, `Instruments/GaugeVertical`, `Instruments/LampDome`, `Instruments/LeverSwitch`, `Instruments/RatchetWheel`, `Instruments/SelectorRotary`, `Instruments/VUMeter`, `Instruments/ValveWheel_L`, `Instruments/ValveWheel_M`, `Instruments/ValveWheel_S` |
| Kit transversal Tools | P1/P2 | `Tools/AdjustableWrench`, `Tools/Blowtorch`, `Tools/Bucket`, `Tools/Crowbar`, `Tools/Extinguisher`, `Tools/Flashlight`, `Tools/Form_Incident`, `Tools/Form_K90B`, `Tools/Form_Maintenance`, `Tools/Form_Radiation`, `Tools/Form_Requisition`, `Tools/Hammer`, `Tools/Headlamp`, `Tools/HullPatch`, `Tools/InkPad`, `Tools/InkStamp`, `Tools/ManualLoosePage`, `Tools/MissionOrder`, `Tools/Mop`, `Tools/PatrolNote`, `Tools/RolledOrder`, `Tools/Screwdriver`, `Tools/WoodWedge`, `Tools/WristDosimeter` |
| Kit transversal Damage | P1/P2 | `Damage/BentHullPlate_1`, `Damage/BentHullPlate_2`, `Damage/BentHullPlate_3`, `Damage/BilgeWaterCompartment_4x6`, `Damage/BilgeWaterCompartment_6x8`, `Damage/BilgeWaterTile_2m`, `Damage/CherenkovGlowCard`, `Damage/CondensationDecal`, `Damage/ContaminationPuddle`, `Damage/FrostDecal`, `Damage/LeakAnchor_Large`, `Damage/LeakAnchor_Medium`, `Damage/LeakAnchor_Small`, `Damage/PoppedRivet`, `Damage/SparkAnchor`, `Damage/SteamCard` |
| Kit transversal Lighting | P1/P2 | `Lighting/CompartmentCeilingLight`, `Lighting/EmergencyLamp` |

## Limites explicites

- 30 manual spreads,8 propaganda illustrations,portrait/photo and localization remain artwork placeholders
- Generic clips are prototypes; no game IK, cloth/cable/fluid/vehicle simulation or ragdoll
- crowd is instanced; scenery shells and partial wreck interiors require level assembly and playtests
- 12 upgrades are not specified as named3Dassets in the annex; no speculative upgrade designs added
