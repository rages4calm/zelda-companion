"""Two short native MSU starts in isolated profiles; never use personal saves."""
from pathlib import Path
import subprocess, ctypes, time, json, shutil, re, hashlib
from ctypes import wintypes
root=Path(__file__).resolve().parent.parent
stage=root/'online-project/validation/msu-game'
installed=root/'Zelda Companion/Game'
helper=root/'online-project/validation/music-editor/Diagnostics.Windows.dll'
data=root/'online-project/validation/music-editor/UserData'
stage.mkdir(parents=True,exist_ok=True)
user=ctypes.windll.user32
user.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
user.PostMessageW.argtypes=[wintypes.HWND,wintypes.UINT,wintypes.WPARAM,wintypes.LPARAM]
ENUM=ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
protected={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in installed.rglob('*') if p.is_file() and ('saves' in p.parts or p.name in ['zelda3.ini','zelda3_assets.dat'])}
results=[]
for exe in ['zelda3.exe','online.exe']:
    folder=stage/exe.removesuffix('.exe');folder.mkdir(exist_ok=True)
    for name in [exe,'SDL2.dll','zelda3_assets.dat','zelda3.ini']:shutil.copy2(installed/name,folder/name)
    ini=folder/'zelda3.ini';text=ini.read_text(encoding='utf-8')
    for key,value in [('Fullscreen','0'),('OutputMethod','SDL'),('Shader',''),('EnableAudio','1')]:
        text=re.sub(r'^'+key+r'\s*=.*$',key+' = '+value,text,flags=re.M)
    ini.write_text(text,encoding='utf-8')
    prepare=subprocess.run(['dotnet',str(helper),'--prepare-msu',str(data),str(folder)],capture_output=True,text=True,check=True)
    handles=[]
    p=subprocess.Popen([str(folder/exe)],cwd=folder,stdout=subprocess.PIPE,stderr=subprocess.PIPE,creationflags=subprocess.CREATE_NO_WINDOW)
    @ENUM
    def find(hwnd,_):
        pid=wintypes.DWORD();user.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
        if pid.value==p.pid and user.IsWindowVisible(hwnd):handles.append(hwnd)
        return True
    try:
        for _ in range(80):
            assert p.poll() is None,'Game closed during startup'
            user.EnumWindows(find,0)
            if handles:break
            time.sleep(.1)
        assert handles,'Game did not create its own window'
        time.sleep(9)
        assert p.poll() is None,'Game did not stay running'
        user.PostMessageW(handles[0],0x10,0,0)
        out,err=p.communicate(timeout=12)
    finally:
        if p.poll() is None:p.terminate();p.wait(timeout=5)
    log=out.decode('utf-8','replace')+err.decode('utf-8','replace')
    (folder/'native-output.txt').write_text(log,encoding='utf-8')
    assert p.returncode==0,p.returncode
    assert 'Loading MSU ' in log,log
    assert not any(s in log for s in ['Unable to read MSU','MSU read/decode error','Warning: MSU','Unable to open audio']),log
    result=dict(engine=exe,exit_code=p.returncode,msu_files=re.findall(r'Loading MSU (.+)',log),audio_enabled=True,isolated_saves=True,seconds=9)
    results.append(result);print(json.dumps(result),flush=True)
for name,digest in protected.items():assert hashlib.sha256(Path(name).read_bytes()).hexdigest()==digest,name
(stage/'native-game-results.json').write_text(json.dumps(dict(checks=results,installed_game_files_unchanged=len(protected)),indent=2),encoding='utf-8')
