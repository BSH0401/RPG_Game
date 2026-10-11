"""
달빛 우체국 — 재배포가 금지된 무료 에셋을 드라이브의 zip 에서 프로젝트로 가져온다.

    python Tools/import_art.py "<달빛우체국_에셋 폴더>"

필요한 zip (itch.io 에서 무료로 받은 그대로):
  - Pixel Art Top Down - Basic v*.zip            (Cainos)          → Assets/Resources/Art/Cainos
  - FREE Mana Seed Character Base Demo *.zip     (Seliel the Shaper) → Assets/Resources/Art/ManaSeed
  - Pixel Monsters Free Pack.zip                 (Matseb2611 / Matthew Ashworth) → Assets/Resources/Art/Monsters
세 폴더는 .gitignore 에 들어 있어 저장소(공개)에는 올라가지 않는다. 없으면 게임은 Ninja Adventure 그림을 쓴다.
Mana Seed 는 페이퍼돌 층(몸·옷·머리·모자·칼·방패)을 미리 겹쳐 캐릭터마다 완성된 페이지 PNG 를 만든다.
칼·방패가 몸 앞/뒤 어느 쪽인지는 가이드 그림(layer order, …)의 칸 색(빨강 = 앞, 파랑 = 뒤)으로 정한다.
필요: Python 3, Pillow
"""
import sys, os, glob, shutil, tempfile, zipfile
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
C = 64

PAGES = {'p1': 'char_a_p1', 'pONE1': 'char_a_pONE1', 'pONE2': 'char_a_pONE2', 'pONE3': 'char_a_pONE3'}
GUIDE = {'pONE1': 'ONE-hand 1', 'pONE2': 'ONE-hand 2', 'pONE3': 'ONE-hand 3'}

# 이름: (피부, 옷, 머리, 모자, 무기?)
CHARS = {
    'Inspector':     ('v01', 'pfpn_v04', 'dap1_v11', None, True),   # 배달부(플레이어)
    'Woman':         ('v02', 'pfpn_v02', 'bob1_v05', None, False),        # 미라(빵집)
    'Child':         ('v03', 'fstr_v05', 'bob1_v03', None, False),        # 노아
    'Hunter':        ('v04', 'fstr_v01', 'dap1_v11', 'pfht_v04', False),  # 오웬
    'Princess':      ('v01', 'pfpn_v05', 'bob1_v13', None, False),        # 세린(등대지기)
    'OldWoman':      ('v02', 'pfpn_v03', 'bob1_v01', None, False),        # 도라
    'OldMan':        ('v06', 'fstr_v03', 'dap1_v10', 'pfht_v03', False),  # 테오
    'OldMan2':       ('v03', 'pfpn_v01', 'dap1_v01', 'pfht_v02', False),  # 바우
    'SorcererBlack': ('v01', 'fstr_v04', 'bob1_v07', None, False),        # 유나
}


def load(page, rel):
    path = os.path.join(root, PAGES[page], rel)
    return Image.open(path).convert('RGBA') if os.path.exists(path) else None


def guide_front(page, kind):
    """칸마다 True(몸 앞) / False(몸 뒤)."""
    g = Image.open(os.path.join(root, 'guides', 'layer order, page %s (%s).png' % (GUIDE[page], kind))).convert('RGB')
    res = {}
    for r in range(8):
        for c in range(8):
            px = g.getpixel((c * C + 2, r * C + 2))
            res[(r, c)] = px[0] > px[2]
    return res


def compose(name, spec):
    skin, outfit, hair, hat, armed = spec
    pages = ['p1', 'pONE1', 'pONE2', 'pONE3'] if armed else ['p1']
    for page in pages:
        pfx = PAGES[page]
        body = [load(page, '%s_0bas_humn_%s.png' % (pfx, skin)),
                load(page, '1out/%s_1out_%s.png' % (pfx, outfit)),
                load(page, '4har/%s_4har_%s.png' % (pfx, hair))]
        if hat:
            body.append(load(page, '5hat/%s_5hat_%s.png' % (pfx, hat)))
        body = [b for b in body if b is not None]
        sword = load(page, '6tla/%s_6tla_sw01_v01.png' % pfx) if page != 'p1' else None
        shield = load(page, '7tlb/%s_7tlb_sh01_v01.png' % pfx) if page != 'p1' else None
        if sword is not None:
            sf, hf = guide_front(page, 'sword'), guide_front(page, 'shield')
        out = Image.new('RGBA', (512, 512), (0, 0, 0, 0))
        for r in range(8):
            for c in range(8):
                box = (c * C, r * C, c * C + C, r * C + C)
                cell = Image.new('RGBA', (C, C), (0, 0, 0, 0))
                behind = [] if sword is None else [l for l, f in ((shield, hf), (sword, sf)) if not f[(r, c)]]
                front = [] if sword is None else [l for l, f in ((shield, hf), (sword, sf)) if f[(r, c)]]
                for l in behind + body + front:
                    cell.alpha_composite(l.crop(box))
                out.alpha_composite(cell, (c * C, r * C))
        out.save(os.path.join(outdir, '%s_%s.png' % (name, page)))
    # 대화창 초상화: 아래를 보고 선 프레임의 머리·어깨를 잘라 4배로
    p1 = Image.open(os.path.join(outdir, '%s_p1.png' % name))
    face = p1.crop((20, 9, 44, 33)).resize((120, 120), Image.NEAREST)
    face.save(os.path.join(outdir, '%s_face.png' % name))



def unzip(pattern, folder, dest):
    found = sorted(glob.glob(os.path.join(folder, pattern)))
    if not found:
        sys.exit('zip 을 찾지 못함: ' + pattern)
    with zipfile.ZipFile(found[-1]) as z:
        z.extractall(dest)
    return dest


def main():
    folder = sys.argv[1]
    tmp = tempfile.mkdtemp()
    try:
        cainos = unzip('Pixel Art Top Down - Basic*.zip', folder, os.path.join(tmp, 'cainos'))
        out = os.path.join(PROJECT, 'Assets', 'Resources', 'Art', 'Cainos')
        os.makedirs(out, exist_ok=True)
        tex = os.path.join(cainos, 'Texture')
        for src, dst in [('TX Tileset Grass', 'Grass'), ('TX Tileset Stone Ground', 'StoneGround'), ('TX Plant', 'Plant'),
                         ('TX Props', 'Props'), ('TX Tileset Wall', 'Wall')]:
            shutil.copy(os.path.join(tex, src + '.png'), os.path.join(out, dst + '.png'))
        print('Cainos →', out)

        global root, outdir
        root = unzip('FREE Mana Seed Character Base Demo*.zip', folder, os.path.join(tmp, 'manaseed'))
        outdir = os.path.join(PROJECT, 'Assets', 'Resources', 'Art', 'ManaSeed')
        os.makedirs(outdir, exist_ok=True)
        for n, s in CHARS.items():
            compose(n, s)
        print('Mana Seed →', outdir, '(캐릭터 %d명)' % len(CHARS))

        mon = unzip('Pixel Monsters Free Pack*.zip', folder, os.path.join(tmp, 'monsters'))
        pack = os.path.join(mon, 'Pixel Monsters Free Pack')
        out = os.path.join(PROJECT, 'Assets', 'Resources', 'Art', 'Monsters')
        os.makedirs(out, exist_ok=True)
        for src, dst in [('Goblin/$Goblin', 'Goblin'), ('Mosquito/$Mosquito', 'Mosquito'), ('Evil Head/$Evil Head', 'EvilHead'),
                         ('Evil Skull/$Skull', 'Skull'), ('Armed Ghost/$Ghost Sword', 'GhostSword'), ('Armed Ghost/$Ghost Spear', 'GhostSpear')]:
            shutil.copy(os.path.join(pack, src + '.png'), os.path.join(out, dst + '.png'))
        print('Monsters →', out)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == '__main__':
    main()
