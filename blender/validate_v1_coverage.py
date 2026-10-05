"""Trace each release-1.0 annex table row to actual exported V1 geometry.
Run with Python or Blender --background --python; stdlib only.
This checks geometry coverage, never painted artwork or gameplay completion.
"""
import os, re, json
ROOT=os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MODELS=os.path.join(ROOT,'Assets','_Project','Art','Models')
def kit(folder,*names):return [folder+'/'+n for n in names]
S=lambda *n:kit('Structure',*n)
F=lambda *n:kit('Fixtures',*n)
W=lambda *n:kit('World',*n)
T=lambda *n:kit('Torpedoes',*n)
C=lambda *n:kit('Central',*n)
R=lambda *n:kit('Radio',*n)
M=lambda *n:kit('Machines',*n)
L=lambda *n:kit('Living',*n)
G=lambda *n:kit('Cargo',*n)
V=lambda *n:kit('CrewVariants',*n)
rules=[
 ('Section de coque intérieure droite',S('HullModule_2m')),
 ('Section de coque courbe proue',F('HullInteriorCurvedBow')),
 ('Section de coque courbe poupe',F('HullInteriorCurvedStern')),
 ('Cloison pleine',S('Bulkhead_Solid')),
 ('Sas manuel de cloison',S('Bulkhead_Hatch','HatchDoor')),
 ('Plancher caillebotis',S('FloorGrating_1m')),
 ('Fond de cale',S('BilgeFloor_2m')),
 ('Échelle verticale',F('ConningLadderHatch')),
 ('Kiosque intérieur',F('ConningTowerInterior')),
 ('Coque extérieure complète',W(*['MolossHull_LOD%d_Wear%d'%(l,w) for l in range(3) for w in range(3)])),
 ('Barres de plongée',W('MolossDivingPlane','MolossRudder','MolossPropeller')),
 ('Kiosque extérieur',W('MolossConningTower','MolossAntenna','MolossPeriscopeHead')),
 ("Variantes d'usure",W('MolossHull_LOD0_Wear0','MolossHull_LOD0_Wear1','MolossHull_LOD0_Wear2')),
 ('Tuyauterie modulaire',S('Pipe_Straight_1m','Pipe_Elbow','Pipe_Tee','Pipe_ValveInline')),
 ('Chemin de câbles',F('CableTrayStraight','CableJunctionBox')),
 ('Plaques gravées',kit('Signage',*['Plate_'+n for n in ['Torpedoes','Central','Radio','Reactor','Machines','Living','Primary','Bilge','Electric','Interphone','Manual','Pneumatic','Battery','Scram','Registry']])),
 ('Tube lance-torpilles',T('CargoTorpedoTube')),
 ('Râtelier de stockage',T('CargoRack')),
 ("Sangles d'arrimage",T('CargoTieDownStrap')),
 ('Palan/rail',F('CargoCeilingHoistRail','CargoCeilingHoist')),
 ("Torpille d'exercice",F('InertTrainingTorpedo')),
 ('Barre de direction',C('CentralSteeringWheel')),
 ('Pupitre de barre',C('CentralDepthTrimConsole')),
 ('Télégraphe machine',C('EngineTelegraphFivePosition')),
 ('Périscope',F('PeriscopeInterior','PeriscopeExternalHead')),
 ('Table à cartes',C('CentralChartTable','ChartPaperBlank')),
 ('Crayon gras',C('GreasePencil')),
 ('Compas de relèvement',F('BearingCompass','CrasNavigationRule','NavigationStopwatch')),
 ('Le Manuel OK-114',C('ManualOK114OpenBinder','ManualOK114LoosePage')),
 ('Pupitre central',C('CentralInstrumentConsole')),
 ('Tube pneumatique',C('PneumaticArrivalStation')),
 ('Capsule pneumatique',C('PneumaticCapsule','RolledOrderPaper')),
 ('Horloge de bord',F('ShipClock')),
 ('Tableau à craie',F('Chalkboard','ChalkStick')),
 ('Portrait officiel',F('CommandantPortraitFrame')),
 ('Fauteuil du chef',F('WatchOfficerChair')),
 ('Console sonar',R('SonarConsole')),
 ("Casque d'écoute",R('SonarHeadphones','HeadphoneCableCoiled')),
 ('Hydrophone',R('HydrophoneCrankStation')),
 ('Enregistreur',R('MagneticTapeRecorder')),
 ('Bobine de bande',R('TapeReelBlank','TapeReelRecorded')),
 ('Poste radio VLF',R('VLFRadioStation','VLFAntennaDeployable')),
 ('Machine à chiffrer',R('MechanicalCipherMachine')),
 ('Feuillets de messages',R('CipherMessagePad')),
 ('Baie électronique',F('VacuumValveElectronicsRack')),
 ("L'Émetteur du Signal",G('SignalEmitterFrame','SignalEmitterPower','SignalEmitterCoil','SignalEmitterAerial','SignalEmitterControls','SignalEmitterAssembled')),
 ('Tableau de commande RK-1',kit('Reactor','RK1_Console','Breaker')+kit('Instruments','SelectorRotary','GaugeRound_M')),
 ('Levier SCRAM',kit('Reactor','ScramLever')),
 ('Colonne des barres',F('ControlRodDriveColumn')),
 ('Hublot de visée',F('CoreViewingPorthole')),
 ('Circuit primaire',kit('Primary','PrimaryPump','PrimaryPump_Broken')),
 ('Vannes principales',kit('Primary','PrimaryValve')),
 ('Échangeur',F('SteamGenerator')),
 ('Panneau dosimétrie',F('WallDosimetryAlarm')),
 ('Rideau/portique',F('ControlledZoneStripCurtain')),
 ('Bidon de bore',F('EmergencyBoronCan')),
 ('Turbine',M('TurbineReducer')),
 ('Tableau électrique',M('ElectricalPanel','MachineBreaker')),
 ('Batteries de secours',M('BatteryBank')),
 ('Pompe de cale',M('BilgePump')),
 ("Ligne d'arbre",F('PropellerShaftLine','ShaftStuffingBox')),
 ("Compresseur d'air",F('AirCompressor','CO2CartridgeNew','CO2CartridgeUsed')),
 ('Établi',F('MachineWorkbench','WorkshopToolboard')),
 ('Interphone',M('Interphone')),
 ('Ventilateur de gaine',F('DuctVentilator')),
 ('Sas de plongée',L('DivingAirlockChamber')),
 ('Scaphandre sur',L('DivingSuitOnRack')),
 ('Casque de scaphandre',L('DivingHelmet')),
 ('Ombilical',L('UmbilicalReel')),
 ('Douche de décontamination',L('DeconShowerCabin')),
 ('Lance à eau',L('DeconWaterLance')),
 ('Couchettes superposées',L('BunkBed_Double')),
 ('Cambuse',L('GalleyStove','SoupPot')),
 ('Vaisselle',L(*[os.path.splitext(p)[0] for p in sorted(os.listdir(os.path.join(MODELS,'Living'))) if p.startswith('Enamel') and p.endswith('.fbx')])),
 ('Table + banquettes',L('MessTable','MessBench')),
 ('Gramophone',F('MessGramophone','AnthemGramophoneRecord')),
 ('Casiers personnels',L('PersonalLocker')),
 ('Samovar',F('ElectricSamovar')),
 ('WC marin',F('MarineToiletSevenValve','MarineToiletProcedureBlank')),
 ('Porte du carré',L('OfficersQuartersDoor')),
 ('Caisses réglementaires',kit('Environment','CargoCrate_S','CargoCrate_M','CargoCrate_L')),
 ('Fûts',G('FuelDrum','BrineDrum','SealedDrum')),
 ('Combustible nucléaire',G('NuclearFuelTransportCask')),
 ('Pièces détachées',G('SpareFuseBox','SpareGasket','SpareBearing','SpareImpeller','SpareMotor','SparePipeSection')+F('CO2CartridgeNew','CO2CartridgeUsed')+kit('Primary','PrimaryPump')),
 ("Objets d'épave",G('SalvageChest','SalvageShipBell','SalvageBronzePropeller','AntiqueSextant')),
 ('Objets anachroniques',G('BrokenJetSki','ModernContainer','InflatableDuck','SolarPanel')),
 ('Objets bruyants',G('ActiveBeacon','JammedMusicBox')),
 ('Reliques kraviques',G('LifeRaft1983','OldLogbook','KravicMedalCase')),
 ("Les 5 pièces",G('SignalEmitterFrame','SignalEmitterPower','SignalEmitterCoil','SignalEmitterAerial','SignalEmitterControls')),
 ('La base du fjord',W(*['Fjord'+n for n in ['Workshop','StampOffice','Barracks','StorageShed','Pontoon_4m','GantryCrane','NoticeBoard','Lighthouse','MooringBollard','Gangway','DockLadder','FuelTank','CargoPallet','DockFence_2m','WorkshopBench','DockPowerPedestal']])),
 ('Terrain sous-marin',W(*['UnderwaterRock_%02d'%i for i in range(8)])+W('UnderwaterCliff','UnderwaterSandTile_8m','UnderwaterKelpCluster','HydrothermalVent')),
 ('Épaves kraviques',W('WreckCargo','WreckTrawler','WreckTwinSub')),
 ('Dépôt militaire',W('UnderwaterMilitaryDepot','DepotAirlock')),
 ('Filets de pêche',W('IndustrialFishingNet')),
 ('Trafic civil',W('CivilFerry','CivilContainerShip','CivilSailboat','CivilJetSki','CivilPedalBoat')),
 ("Forces de l'Entente",W('EntenteFrigate_LOD0','EntenteFrigate_LOD1','EntentePatrolPlane','SonarBuoy','ExerciseGrenade')),
 ('Parc éolien',W('OffshoreWindTurbine','OffshorePlatform','ChannelBuoy','Iceberg_0','Iceberg_1','Iceberg_2','RaceSpectatorLowPoly')+W(*['RaceSailboat_%02d'%i for i in range(1,11)])),
 ('Matelot de base',kit('Crew','SailorBase','FirstPersonArms')),
 ('Têtes',V(*['Head_%02d'%i for i in range(1,7)])+V(*['Moustache_%02d'%i for i in range(1,13)])+V(*['Hair_%02d'%i for i in range(1,9)])),
 ('Uniformes',kit('Crew','SailorBase')+V('Vareuse','DressUniform','CookApron','RegulationPyjamas')),
 ('Casquettes/bonnets',[]),
 ('Scaphandre porté',V('DivingSuit')),
 ('Version « contaminé »',V('ContaminatedSailor')),
 ('État évanoui',kit('Crew','SailorBase')+V('WakeUpSalts')),
 ('Le Commandant Varga',V('CommanderVarga')),
]

def main():
    with open(os.path.join(MODELS,'CrewVariants','crew_variants_manifest.json'),encoding='utf8') as f:crew=json.load(f)
    assert crew['coverage']['cosmetics_including_headwear']==50
    cosmetics=[a['name'] for a in crew['assets'] if a['category'] in ('cosmetic','headwear')]
    for i,(label,models) in enumerate(rules):
        if label=='Casquettes/bonnets':rules[i]=(label,V(*cosmetics))
    with open(os.path.join(ROOT,'GDD_Sous_Pression.md'),encoding='utf8') as f:body=f.read().split('Asset List v1.0',1)[1].split('## 12',1)[0]
    rows=[];unmatched=[];missing=[]
    for line in body.splitlines():
        if not line.startswith('|'):continue
        cells=[c.strip() for c in line.split('|')[1:-1]]
        if len(cells)<3 or cells[0] in ('Asset','Famille') or cells[0].startswith(':'):continue
        label=re.sub(r'[*\\]','',cells[0]).strip()
        match=next(((key,names) for key,names in rules if key in label),None)
        if not match:unmatched.append(label);continue
        for model in match[1]:
            if not os.path.isfile(os.path.join(MODELS,model+'.fbx')):missing.append(model)
        rows.append({'requirement':label,'priority':cells[1],'models':match[1],'status':'geometry_v1'})
    for folder in ('Instruments','Tools','Damage','Lighting'):
        names=[folder+'/'+os.path.splitext(p)[0] for p in sorted(os.listdir(os.path.join(MODELS,folder))) if p.endswith('.fbx')]
        assert names,folder
        rows.append({'requirement':'Kit transversal '+folder,'priority':'P1/P2','models':names,'status':'geometry_v1'})
    assert not unmatched,('Unmapped GDD rows',unmatched)
    assert not missing,('Missing FBX',missing)
    assert len(cosmetics)==50,('Cosmetic count',len(cosmetics))
    allfbx=[os.path.relpath(os.path.join(d,p),MODELS).replace('\\','/') for d,ds,ps in os.walk(MODELS) for p in ps if p.endswith('.fbx')]
    report={'scope':'GDD release1.0 annex, P1/P2/P3 geometry V1; not painted artwork, polished animation or gameplay',
        'fbx_count':len(allfbx),'coverage_rows':len(rows),'rows':rows,'unmapped':unmatched,'missing':missing,
        'limitations':['30 manual spreads,8 propaganda illustrations,portrait/photo and localization remain artwork placeholders',
        'Generic clips are prototypes; no game IK, cloth/cable/fluid/vehicle simulation or ragdoll',
        'crowd is instanced; scenery shells and partial wreck interiors require level assembly and playtests',
        '12 upgrades are not specified as named3Dassets in the annex; no speculative upgrade designs added']}
    out=os.path.join(ROOT,'docs','art');os.makedirs(out,exist_ok=True)
    with open(os.path.join(out,'V1_ASSET_COVERAGE.json'),'w',encoding='utf8') as f:json.dump(report,f,indent=2,ensure_ascii=False)
    text=['# Couverture géométrique V1 — tous les assets de l’annexe 1.0','',
        '%d FBX ; %d lignes de besoins couvertes. Ce pointage couvre les supports 3D P1/P2/P3, pas la finition artistique et fonctionnelle.'%(len(allfbx),len(rows)),
        '', '| Besoin GDD | Priorité | Modèles V1 |','|---|---|---|']
    for row in rows:text.append('| '+row['requirement']+' | '+row['priority']+' | '+', '.join('`'+n+'`' for n in row['models'])+' |')
    text+=['','## Limites explicites','']+['- '+x for x in report['limitations']]
    with open(os.path.join(out,'V1_ASSET_COVERAGE.md'),'w',encoding='utf8') as f:f.write('\n'.join(text)+'\n')
    print('V1_GEOMETRY_COVERAGE_PASS:',len(rows),'requirements,',len(allfbx),'FBX')

if __name__=='__main__':main()
