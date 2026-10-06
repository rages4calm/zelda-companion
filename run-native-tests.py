from pathlib import Path
import re, subprocess, hashlib, json, argparse

project=Path(__file__).resolve().parent
root=project.parent
parser=argparse.ArgumentParser(description='Focused Windows native co-op regression harness')
parser.add_argument('--tcc-root',type=Path,default=root/'zelda3/third_party/tcc')
parser.add_argument('--sdl-root',type=Path,default=root/'zelda3/third_party/SDL2-2.28.2')
parser.add_argument('--sdl-dll',type=Path,default=root/'zelda3/SDL2.dll')
parser.add_argument('--inspect',action='store_true')
args=parser.parse_args()
out=project/'validation/coop-native'
out.mkdir(parents=True,exist_ok=True)
source=(project/'engine/src/sprite.c').read_text()
start=source.index('void Sprite_MiniMoldorm_Recoil(')
brace=source.index('{',start)
depth=1;end=brace+1
while depth:
    depth+=(source[end]=='{')-(source[end]=='}');end+=1
fragment=source[start:end]
if args.inspect:
    print(fragment)
    print(sorted(set(re.findall(r'\b([A-Za-z_]\w*)\(',fragment))))
    raise SystemExit
stubs='''
void SpriteSfx_QueueSfx3WithPan(int k, uint8 sfx){}
void SpriteSfx_QueueSfx2WithPan(int k, uint8 sfx){}
void Sprite_ZeroVelocity_XY(int k){}
void ThrowableScenery_TransmuteToDebris(int k){}
void Sprite_Func3(int k){}
void Sprite_KillFriends(){}
'''
(out/'engine_recoil_fragment.h').write_text(stubs+'\n'+fragment+'\n')
tcc=args.tcc_root.resolve()/'tcc.exe'
sdl=args.sdl_root.resolve()
exe=out/'native-coop-tests.exe'
command=[str(tcc),'-o',str(exe),'-I'+str(project/'engine'),'-I'+str(out),'-I'+str(sdl/'include'),'-L'+str(sdl/'lib/x64'),'-lSDL2','-lws2_32','-lgdi32','-luser32',str(project/'Tests/native_tests.c')]
subprocess.run(command,check=True,cwd=project/'engine')
import shutil
shutil.copy2(args.sdl_dll,out/'SDL2.dll')
result=subprocess.run([str(exe)],capture_output=True,text=True)
(out/'results.txt').write_text(result.stdout+result.stderr)
print(result.stdout);print(result.stderr)
(out/'source-evidence.json').write_text(json.dumps({'engine_fragment_sha256':hashlib.sha256(fragment.encode()).hexdigest(),'function':'Sprite_MiniMoldorm_Recoil','exit':result.returncode,'visual_reload_and_unrelated_enemy_effects_stubbed':True},indent=2))
raise SystemExit(result.returncode)
