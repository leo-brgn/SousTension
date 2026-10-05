"""Damage and fluid meshes only. Runtime VFX shaders/particles remain separate."""
import os,sys,math
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from lib import *
from kit_tools import run

def plane(p,sx,sy,col,grid=1,vertical=False):
 before=p._snapshot();v=[]
 for j in range(grid+1):
  row=[]
  for i in range(grid+1):
   x=(i/grid-.5)*sx;y=(j/grid-.5)*sy
   row.append(p.bm.verts.new((x,0,y) if vertical else (x,y,0)))
  v.append(row)
 for j in range(grid):
  for i in range(grid):p.bm.faces.new((v[j][i],v[j][i+1],v[j+1][i+1],v[j+1][i]))
 p._finish_prim(before,Matrix.Identity(4),col,0)

def build_all():
 out=[]
 for label,size in [('Small',.02),('Medium',.05),('Large',.10)]:
  a=Asset('LeakAnchor_'+label);p=a.part('RupturedSeal');p.disc_ring((0,0,0),size*.45,size,.012,C['rust_d'],axis='Y',segs=12);a.mount('Mount_Jet',(0,-.009,0),(math.pi/2,0,0));out.append(a)
 a=Asset('PoppedRivet');p=a.part('Body');p.cyl((0,0,.025),.012,.05,C['steel_d'],segs=10);p.sphere((0,0,.05),(.028,.028,.015),C['steel'],u=12,v=8);out.append(a)
 for i in range(3):
  a=Asset('BentHullPlate_'+str(i+1));p=a.part('Body');plane(p,.5+i*.15,.40+i*.1,C['steel_d'],grid=8)
  for v in p.bm.verts:v.co.z=.045*(i+1)*math.sin(v.co.x*9)*math.cos(v.co.y*7)
  a.mount('Mount_DamageCentre',(0,0,0));out.append(a)
 for name,sx,sy in [('BilgeWaterTile_2m',2,2),('BilgeWaterCompartment_4x6',4,6),('BilgeWaterCompartment_6x8',6,8)]:
  a=Asset(name);p=a.part('WaterSurface');plane(p,sx,sy,C['glass'],grid=16);a.mount('Mount_WaterLevel',(0,0,0));out.append(a)
 for name,sx,sy,vertical,col in [('SteamCard',1,1.5,True,C['white']),('CondensationDecal',1,1,True,C['glass']),('FrostDecal',1,1,True,C['white']),('ContaminationPuddle',1.2,.8,False,C['green_lamp']),('CherenkovGlowCard',.4,.4,True,C['glass'])]:
  a=Asset(name);p=a.part('EffectSurface');plane(p,sx,sy,col,grid=4,vertical=vertical);a.mount('Mount_Effect',(0,0,0));out.append(a)
 a=Asset('SparkAnchor');p=a.part('ExposedContact');p.box((0,0,0),(.08,.025,.06),C['bakelite'],bevel=.004)
 for x in (-.022,.022):p.cyl((x,-.028,0),.008,.03,C['brass'],axis='Y',segs=8)
 a.mount('Mount_Sparks',(0,-.045,0),(math.pi/2,0,0));out.append(a)
 return out
if __name__=='__main__':run('Damage',build_all,['Support meshes only: steam, water, frost, condensation, contamination and Cherenkov cards require runtime transparent/emissive VFX shaders.','Water surfaces contain regular 16x16 quad grids for compartment water level and vertex displacement.','Effect surfaces include normalized UVMap; anchors use local -Y outward emission.'])
