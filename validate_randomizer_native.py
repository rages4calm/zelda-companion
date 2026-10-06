"""Private native chest/receipt validation. Shipping engine and saves stay intact."""
from pathlib import Path
import shutil, subprocess, json, struct

project=Path(__file__).resolve().parent
root=project.parent
out=project/'validation/randomizer-native'
engine=out/'engine'
out.mkdir(parents=True,exist_ok=True)
shutil.copytree(project/'engine',engine,dirs_exist_ok=True,ignore=shutil.ignore_patterns('.git','saves','*.sfc','*.smc','*.exe','*.dll','zelda3_assets.dat'))
hook=r'''
// Private validation entry; never compiled into the released engine.
static void RandomizerNativeCheck(void) {
  int slots[320]={0},receipts=0;
  for(int i=0;i<kDungeonRoomChests_SIZE;i+=3){
    int room=(kDungeonRoomChests[i]|kDungeonRoomChests[i+1]<<8)&0x7fff;
    int slot=slots[room]++,position=-1;
    memset(g_ram,0,sizeof(g_ram));dungeon_room_index=room;link_bigkey=0xffff;
    dung_chest_locations[slot]=0x100;
    uint8 got=OpenChestForItem(0x58+slot,&position);
    if(got!=kDungeonRoomChests[i+2]||position<0)Die("Native chest lookup mismatch");
    printf("CHEST %d %d %d %u\n",i/3,room,slot,got);
    if(got==0x17||got==0x34||got==0x35||got==0x36||got==0x40||got==0x41||got==0x46){
      memset(g_ram,0,sizeof(g_ram));dungeon_room_index=room;link_bigkey=0xffff;
      dung_chest_locations[slot]=0x100;index_of_interacting_tile=0x58+slot;
      Link_PerformOpenChest();
      if(link_receiveitem_index!=got||item_receipt_method!=1)Die("Native receipt mismatch");
      receipts++;printf("RECEIPT %d %u\n",i/3,link_receiveitem_index);
    }
  }
  printf("RESULT %d %d\n",kDungeonRoomChests_SIZE/3,receipts);
}
'''
main=engine/'src/main.c'
source=main.read_text()
source=source.replace('#undef main','#include "dungeon.h"\n#include "player.h"\n'+hook+'\n#undef main',1)
source=source.replace('  ZeldaInitialize();','  ZeldaInitialize();\n  RandomizerNativeCheck();\n  return 0;',1)
main.write_text(source)
sdl=root/'zelda3/third_party/SDL2-2.28.2'
compiler=root/'zelda3/third_party/tcc/tcc.exe'
command=[str(compiler),'-o','randomizer-check.exe','-DCOMPILER_TCC=1','-DSTBI_NO_SIMD=1','-DHAVE_STDINT_H=1','-D_HAVE_STDINT_H=1','-DSYSTEM_VOLUME_MIXER_AVAILABLE=0','-I'+str(sdl/'include'),'-L'+str(sdl/'lib/x64'),'-lSDL2','-lws2_32','-lgdi32','-luser32','-I.']
command += [str(p.relative_to(engine)) for d in ['src','snes'] for p in (engine/d).glob('*.c')]
command += ['third_party/gl_core/gl_core_3_1.c','third_party/opus-1.3.1-stripped/opus_decoder_amalgam.c']
subprocess.run(command,cwd=engine,check=True)
shutil.copy2(root/'Zelda Companion/Game/SDL2.dll',engine/'SDL2.dll')
results=[]
for manifest in (project/'validation/randomizer-tests/UserData/Randomizer').glob('*/seed.json'):
    recipe=json.loads(manifest.read_text());game=manifest.parent/'Game'
    shutil.copy2(root/'Zelda Companion/Game/zelda3.ini',game/'zelda3.ini')
    process=subprocess.run([str(engine/'randomizer-check.exe'),'--config','zelda3.ini'],cwd=game,text=True,capture_output=True,timeout=15)
    (out/(recipe['Id']+'.log')).write_text(process.stdout+process.stderr)
    assert process.returncode==0,(recipe,process.stdout,process.stderr)
    raw=(game/'zelda3_assets.dat').read_bytes();count,keybytes=struct.unpack_from('<II',raw,80);offset=88+4*count+keybytes
    for i in range(9):
        offset=(offset+3)&~3;length=struct.unpack_from('<I',raw,88+4*i)[0]
        if i<8:offset+=length
    expected=[];slots={}
    for index in range(length//3):
        packed,item=struct.unpack_from('<HB',raw,offset+3*index);room=packed&0x7fff;slot=slots.get(room,0);slots[room]=slot+1;expected.append([index,room,slot,item])
    actual=[list(map(int,line.split()[1:])) for line in process.stdout.splitlines() if line.startswith('CHEST ')]
    assert actual==expected,'Actual native chest routine did not read all seed values exactly'
    receipts=[list(map(int,line.split()[1:])) for line in process.stdout.splitlines() if line.startswith('RECEIPT ')]
    assert receipts and all(expected[index][3]==item for index,item in receipts)
    results.append({'seed':recipe['Id'],'options':recipe['Options'],'native_chest_lookups':len(actual),'native_receipts':len(receipts),'exit':process.returncode})
(out/'native-results.json').write_text(json.dumps({'tests':results,'actual_native_chest_function':True,'actual_native_item_receipt_function':True,'private_instrumented_engine':True,'full_playthrough':False},indent=2))
print(json.dumps(results,indent=2))
