"""Package/publish from the shared build version. Stable publication needs explicit authorization."""
import argparse, hashlib, json, os, re, urllib.error, urllib.parse, urllib.request, zipfile
from pathlib import Path
import xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parent.parent
REPO='https://api.github.com/repos/szalaiz87/CAT-Price-Calculator'
def build_version():
    version=ET.parse(ROOT/'Directory.Build.props').findtext('.//Version')
    if not version or not re.fullmatch(r'\d+\.\d+\.\d+(?:-beta\.[1-9]\d*)?',version): raise ValueError('Unsupported build version')
    return version

def package(exe,output):
    version=build_version();tag='v'+version
    if exe.read_bytes()[:2]!=b'MZ': raise ValueError('Not a Windows executable')
    output.mkdir(parents=True,exist_ok=True)
    if any(output.iterdir()):raise ValueError('Use an empty output directory to avoid stale assets')
    archive=output/'CAT-Price-Calculator-Windows-x64.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:z.write(exe,exe.name)
    data=archive.read_bytes();sha=hashlib.sha256(data).hexdigest();parts=[]
    for offset in range(0,len(data),4194304):
        part=output/f'CAT-Windows-x64.zip.part{len(parts)+1:02}';part.write_bytes(data[offset:offset+4194304]);parts.append(part)
    if not 1<=len(parts)<=99:raise ValueError('Unsupported part count')
    with zipfile.ZipFile(output/'CAT-Price-Calculator-Source.zip','w',zipfile.ZIP_DEFLATED) as z:
        for path in sorted(ROOT.rglob('*')):
            if path.is_file() and not any(x in path.relative_to(ROOT).parts for x in ['.git','bin','obj','artifacts','__pycache__']):z.write(path,path.relative_to(ROOT))
    installer=(ROOT/'scripts/windows-download.cmd.in').read_bytes().decode()
    for key,value in {'TAG':tag,'PART_COUNT':str(len(parts)),'SHA256':sha,'PART_JOIN':'+'.join(f'part{i+1:02}' for i in range(len(parts)))}.items():installer=installer.replace('@@'+key+'@@',value)
    (output/f'CAT-Letoltes-Windows-{tag}.cmd').write_bytes(installer.encode())
    (output/'SHA256SUMS.txt').write_text(sha+'  '+archive.name+'\n')
    (output/'LIS-Germany.svg').write_bytes((ROOT/'src/CatPriceCalculator/Assets/LIS-Germany.svg').read_bytes())
    print(f'Package {tag}: {len(parts)} parts, ZIP {len(data)} bytes, SHA256 {sha}')

def publish(output,notes,stable_approved,target):
    version=build_version();tag='v'+version;beta='-beta.' in version
    if not beta and not stable_approved:raise ValueError('Stable publication requires explicit user authorization and --stable-approved')
    expected='beta' if beta else 'stable'
    if target!=expected:raise ValueError(f'{tag} must publish from the {expected} branch')
    token=os.environ['GH_RELEASE_TOKEN']
    headers={'Authorization':'Bearer '+token,'User-Agent':'LinserHungary-release','Accept':'application/vnd.github+json'}
    def request(url,method='GET',payload=None,ctype='application/json'):
        data=payload if isinstance(payload,bytes) else json.dumps(payload).encode() if payload is not None else None
        req=urllib.request.Request(url,data=data,method=method,headers={**headers,'Content-Type':ctype})
        with urllib.request.urlopen(req,timeout=60) as response:return json.load(response)
    # Ensure the selected source branch really carries this version before creating the tag.
    props=request(REPO+'/contents/Directory.Build.props?ref='+target)
    import base64
    if ET.fromstring(base64.b64decode(props['content'])).findtext('.//Version')!=version:raise ValueError('Remote source branch version does not match the package')
    assets=[p for p in sorted(output.iterdir()) if p.name!='CAT-Price-Calculator-Windows-x64.zip']
    if not assets or any(p.stat().st_size>8*1024*1024 for p in assets):raise ValueError('Missing/oversized assets')
    release=request(REPO+'/releases','POST',{'tag_name':tag,'target_commitish':target,'name':'Linser Hungary '+tag+(' – béta' if beta else ' – stabil'),'body':notes.read_text(),'draft':True,'prerelease':beta})
    print('Draft created:',release['id'],flush=True)
    for path in assets:
        result=request(release['upload_url'].split('{')[0]+'?name='+urllib.parse.quote(path.name),'POST',path.read_bytes(),'application/octet-stream')
        if result['size']!=path.stat().st_size or result['state']!='uploaded':raise ValueError('Upload verification failed')
        print('Uploaded:',path.name,flush=True)
    check=request(REPO+'/releases/'+str(release['id']))
    if len(check['assets'])!=len(assets):raise ValueError('Incomplete release')
    result=request(REPO+'/releases/'+str(release['id']),'PATCH',{'draft':False,'prerelease':beta,'make_latest':'false' if beta else 'true'})
    print('Published:',result['html_url'],flush=True)

if __name__=='__main__':
    parser=argparse.ArgumentParser();subs=parser.add_subparsers(dest='action',required=True)
    p=subs.add_parser('package');p.add_argument('--exe',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p=subs.add_parser('publish');p.add_argument('--output',type=Path,required=True);p.add_argument('--notes-file',type=Path,required=True);p.add_argument('--target',choices=['stable','beta'],required=True);p.add_argument('--stable-approved',action='store_true')
    args=parser.parse_args()
    if args.action=='package':package(args.exe,args.output)
    else:publish(args.output,args.notes_file,args.stable_approved,args.target)
