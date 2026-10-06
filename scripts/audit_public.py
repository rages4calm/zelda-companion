"""Check public source or a distributable without reading private player folders."""
from pathlib import Path, PurePosixPath
import argparse, hashlib, json, re, subprocess, zipfile

FORBIDDEN={'.sfc','.smc','.sav','.srm','.dat','.pak','.zspr','.pcm','.opuz','.msu','.bps','.ips'}
PRIVATE={'userdata','saves','logs','rom','roms','soundtracks','msu','validation-local','tools-local'}
SECRET_PATTERNS=[
    re.compile(rb'gh[pousr]_[A-Za-z0-9]{30,255}'),
    re.compile(rb'github_pat_[A-Za-z0-9_]{50,255}'),
    re.compile(rb'AKIA[0-9A-Z]{16}'),
    re.compile(rb'-----BEGIN (?:[A-Z ]+ )?PRIVATE KEY-----'),
    re.compile(rb'wss://[a-z0-9-]+\.trycloudflare\.com/room',re.I),
    re.compile(rb'[A-Z]:[\\/]+Users[\\/]',re.I),
]

def safe_path(name):
    path=PurePosixPath(name)
    assert not path.is_absolute() and '..' not in path.parts and ':' not in name and '\\' not in name,name
    parts=path.parts[1:] if path.parts[0]=='Source' else path.parts
    encoder_source=parts[:3]==('engine','other','msu') and path.suffix.lower()=='.py'
    assert not any(p.lower() in PRIVATE and not(p.lower()=='msu' and encoder_source) for p in path.parts),name
    assert path.suffix.lower() not in FORBIDDEN,name
    assert not name.lower().endswith(('.z3seed.json','.pem','.key')),name
    return path

def audit_source(root):
    root=root.resolve()
    if (root/'.git').exists():
        names=subprocess.check_output(['git','ls-files','-z'],cwd=root).decode().split('\0')
        names=[n for n in names if n]
        assert names,'Stage source before the tracked-file audit'
    else:
        names=[p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()
               and not any(x in {'.git','bin','obj','.build','__pycache__'} for x in p.relative_to(root).parts)]
    policy=json.loads((root/'distribution/source-resources.json').read_text())
    resources=policy['resources']
    for name in names:
        path=safe_path(name);file=root/name
        assert not file.is_symlink(),name
        assert path.suffix.lower() not in {'.exe','.dll','.pdb','.zip','.7z','.jpg','.jpeg','.gif','.webp'},name
        data=file.read_bytes()
        assert not data.startswith(b'Zelda3_v0'),name
        if path.suffix.lower() in {'.png','.ico','.bin'}:
            assert name in resources, 'Unreviewed binary resource: '+name
            assert hashlib.sha256(data).hexdigest()==resources[name]['sha256'],name
        else:
            for pattern in SECRET_PATTERNS:
                assert not pattern.search(data),'Potential credential/private path/live invitation in '+name
    print(json.dumps({'source_files':len(names),'roms':0,'generated_asset_packs':0,'saves':0,
                      'soundtrack_payloads':0,'unreviewed_binary_resources':0,'secret_pattern_matches':0}))

def audit_zip(archive_path):
    with zipfile.ZipFile(archive_path) as archive:
        names=archive.namelist();assert len(names)==len(set(names)),'Duplicate entries'
        prefixes={n.split('/')[0] for n in names};assert len(prefixes)==1
        prefix=next(iter(prefixes))+'/'
        manifest=json.loads(archive.read(prefix+'SHA256-manifest.json'))
        assert set(names)=={prefix+n for n in manifest}|{prefix+'SHA256-manifest.json'}
        for name in names:
            rel=name.removeprefix(prefix);path=safe_path(rel);data=archive.read(name)
            assert path.suffix.lower() not in {'.jpg','.jpeg','.gif','.webp'},rel
            if path.suffix.lower()=='.png':
                assert rel in {'Game/other/3x5_font.png','Source/engine/other/3x5_font.png',
                               'docs/images/online.png','docs/images/randomizer.png',
                               'Source/docs/images/online.png','Source/docs/images/randomizer.png'},rel
            assert not data.startswith(b'Zelda3_v0'),rel
            if rel in manifest:assert hashlib.sha256(data).hexdigest()==manifest[rel],rel
        digest=hashlib.sha256(archive_path.read_bytes()).hexdigest()
        print(json.dumps({'zip':archive_path.name,'sha256':digest,'manifest_files':len(manifest),
                          'roms':0,'generated_asset_packs':0,'saves':0,'soundtrack_payloads':0,
                          'game_sprite_image_files':0,'manifest_verified':True}))

if __name__=='__main__':
    parser=argparse.ArgumentParser();group=parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--source',type=Path);group.add_argument('--zip',type=Path)
    args=parser.parse_args()
    if args.source:audit_source(args.source)
    else:audit_zip(args.zip)
