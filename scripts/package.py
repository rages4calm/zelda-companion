"""Package fresh build output using explicitly audited clean runtime dependencies."""
from pathlib import Path
import argparse, hashlib, json, shutil, subprocess, zipfile
from audit_public import audit_source, audit_zip

ROOT=Path(__file__).resolve().parent.parent

def package(runtime,version):
    runtime=runtime.resolve()
    assert not (runtime/'UserData').exists() and not (runtime/'Game/zelda3_assets.dat').exists(), 'Use an uninitialized clean release template'
    manifest=json.loads((runtime/'SHA256-manifest.json').read_text())
    assert all(hashlib.sha256((runtime/name).read_bytes()).hexdigest()==digest for name,digest in manifest.items()),'Changed runtime template'
    audit_source(ROOT)
    source_names=subprocess.check_output(['git','ls-files','-z'],cwd=ROOT).decode().split('\0')
    dest=ROOT/'.build/packages'/('ZeldaCompanion-'+version+'-win-x64')
    dest.mkdir(parents=True,exist_ok=False)
    def copy(src,target):target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,target)
    # Template copies are selected by its audited manifest, never by directory walking.
    for name in manifest:
        if name.startswith(('Game/','Tools/','Licenses/')):copy(runtime/name,dest/name)
    for folder,target in [(ROOT/'.build/Companion',dest),(ROOT/'.build/Relay',dest/'Relay')]:
        assert folder.is_dir(),'Run scripts/Build.ps1 first'
        for file in folder.iterdir():
            if file.is_file() and file.suffix!='.pdb':copy(file,target/file.name)
    for name in ['zelda3.exe','online.exe','SDL2.dll']:
        rebuilt=ROOT/'.build/Companion/Game'/name
        if rebuilt.exists():copy(rebuilt,dest/'Game'/name)
    for name in ['README.md','USER-GUIDE.md','RANDOMIZER.md','CREDITS.md','LEGAL.md','CHANGELOG.md','BUILDING.md','Validation.txt','START HERE.html','START HERE.txt','UPSTREAMS.txt','LICENSE']:
        copy(ROOT/name,dest/name)
    for name in source_names:
        if name:copy(ROOT/name,dest/'Source'/name)
    for name in ['online.png','randomizer.png']:copy(ROOT/'docs/images'/name,dest/'docs/images'/name)
    hashes={p.relative_to(dest).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in dest.rglob('*') if p.is_file()}
    (dest/'SHA256-manifest.json').write_text(json.dumps(hashes,indent=2))
    archive=dest.parent/(dest.name+'.zip')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for file in sorted(dest.rglob('*')):
            if file.is_file():z.write(file,dest.name+'/'+file.relative_to(dest).as_posix())
    audit_zip(archive)
    digest=hashlib.sha256(archive.read_bytes()).hexdigest()
    (archive.parent/(archive.name+'.sha256')).write_text(digest+'  '+archive.name+'\n')
    print(archive)

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--runtime-root',type=Path,required=True)
    parser.add_argument('--version',default='0.3.1')
    args=parser.parse_args()
    assert args.version and all(c.isalnum() or c in '.-' for c in args.version),'Invalid version'
    package(args.runtime_root,args.version)
