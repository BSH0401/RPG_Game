"""
달빛 우체국 — 드라이브의 무료 에셋 zip 을 프로젝트로 가져온다.

    python Tools/import_art.py "<달빛우체국_에셋 폴더>"

필요한 zip (itch.io 에서 무료로 받은 그대로):
  - Pixel Art Top Down - Basic v*.zip    (Cainos, 재배포 금지)             → Assets/Resources/Art/Cainos   (저장소에 넣지 않음)
  - Pixel Monsters Free Pack.zip         (Matthew Ashworth, 조건 없음)     → Assets/Resources/Art/Monsters (저장소에 넣지 않음)
  - puny-characters.zip                  (Shade 「Puny Characters」, CC0)  → Assets/Resources/Art/Puny     (CC0 라 저장소에 들어 있다)
  - Houses_Pack.zip                      (Szadi art, 퍼블릭 도메인)        → Assets/Resources/Art/Houses   (조립한 건물, 저장소에 들어 있다)
Cainos·Monsters 두 폴더는 .gitignore 에 들어 있다. 없으면 게임은 Ninja Adventure 그림을 쓴다.

Mana Seed(Seliel the Shaper)는 쓰지 않는다: 그 라이선스가 AI 로 만든 코드·글과 함께 쓰는 것을 금지한다.
Puny 캐릭터는 배역 이름으로 복사하고(PUNY), 대화창 초상화(<배역>_face.png)를 아래를 보고 선 그림의 머리에서 만든다.
필요: Python 3, Pillow
"""
import sys, os, glob, shutil, tempfile, zipfile
from PIL import Image

PROJECT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 배역 → Puny 시트
PUNY = {
    'Inspector':     'Archer-Green',    # 배달부(플레이어): 두건 쓴 초록 옷
    'Woman':         'Mage-Red',        # 미라(빵집)
    'Child':         'Soldier-Yellow',  # 노아(노란 옷, 작게)
    'Hunter':        'Archer-Purple',   # 오웬
    'Princess':      'Mage-Cyan',       # 세린(등대지기)
    'OldWoman':      'Mage-Cyan',       # 도라(옷을 보랏빛으로 바꾼다, HUE)
    'OldMan':        'Warrior-Red',     # 테오
    'OldMan2':       'Soldier-Red',     # 바우
    'SorcererBlack': 'Warrior-Blue',    # 유나
    'Merchant':      'Soldier-Blue',    # 떠돌이 상인 루카
}


# 같은 시트를 쓰는 배역은 옷 색(청록 계열)을 돌려 구별한다: 배역 → 색상환에서 돌릴 각도(도)
HUE = {'OldWoman': 95}


def shift_hue(im, degrees):
    """청록·파랑 계열 픽셀만 색상을 돌린다(피부·머리·외곽선은 그대로)."""
    import colorsys
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if s < 0.25 or not (0.42 < h < 0.72):
                continue
            h = (h + degrees / 360.0) % 1.0
            r, g, b = colorsys.hsv_to_rgb(h, s, v)
            px[x, y] = (round(r * 255), round(g * 255), round(b * 255), a)
    return im


def unzip(pattern, folder, dest):
    found = sorted(glob.glob(os.path.join(folder, pattern)))
    if not found:
        sys.exit('zip 을 찾지 못함: ' + pattern)
    with zipfile.ZipFile(found[-1]) as z:
        z.extractall(dest)
    return dest


def puny(folder, tmp):
    src = os.path.join(unzip('puny-characters*.zip', folder, os.path.join(tmp, 'puny')), 'Puny-Characters')
    out = os.path.join(PROJECT, 'Assets', 'Resources', 'Art', 'Puny')
    os.makedirs(out, exist_ok=True)
    for actor, sheet in PUNY.items():
        im = Image.open(os.path.join(src, sheet + '.png')).convert('RGBA')
        if actor in HUE:
            im = shift_hue(im, HUE[actor])
        im.save(os.path.join(out, actor + '.png'))
        # 초상화: 아래를 보고 선 칸(0행 0열)의 머리와 어깨를 7배로
        face = im.crop((8, 2, 24, 18)).resize((112, 112), Image.NEAREST)
        face.save(os.path.join(out, actor + '_face.png'))
    shutil.copy(os.path.join(src, 'Slime.png'), os.path.join(out, 'Slime.png'))
    print('Puny →', out, '(배역 %d명 + 슬라임)' % len(PUNY))


# Szadi art 「Houses Pack」: 조각(지붕 · 2층 벽 · 문)을 이어 붙여 건물 하나씩 만든다. 색은 보라 · 회색 · 빨강.
HOUSE_OFF = {'purple': 0, 'grey': 320, 'red': 672}
HOUSE_ROOF = {'gable': (77, 438, 243, 640), 'flat': (72, 23, 248, 128), 'cross': (32, 160, 288, 416)}
HOUSE_FACADE = (83, 672, 237, 744)
HOUSE_DOOR = (256, 704, 288, 746)
HOUSES = {
    'PostOffice': ('cross', 'purple'),
    'Bakery':     ('flat', 'red'),
    'HouseA':     ('gable', 'grey'),
    'HouseB':     ('gable', 'red'),
    'HouseC':     ('flat', 'grey'),
    'PassHouseA': ('gable', 'purple'),
    'PassHouseB': ('flat', 'purple'),
}


def houses(folder, tmp):
    src_dir = unzip('Houses_Pack*.zip', folder, os.path.join(tmp, 'houses'))
    src = Image.open(os.path.join(src_dir, 'houses.png')).convert('RGBA')
    out_dir = os.path.join(PROJECT, 'Assets', 'Resources', 'Art', 'Houses')
    os.makedirs(out_dir, exist_ok=True)

    def piece(box, color):
        o = HOUSE_OFF[color]
        x0, y0, x1, y1 = box
        return src.crop((x0 + o, y0, x1 + o, y1))

    for name, (roof, color) in HOUSES.items():
        r, f, d = piece(HOUSE_ROOF[roof], color), piece(HOUSE_FACADE, color), piece(HOUSE_DOOR, color)
        w, h = max(r.width, f.width), r.height + f.height
        img = Image.new('RGBA', (w, h))
        img.alpha_composite(f, ((w - f.width) // 2, r.height))
        img.alpha_composite(r, ((w - r.width) // 2, 0))
        img.alpha_composite(d, (w // 2 - d.width // 2, h - d.height))
        img.save(os.path.join(out_dir, name + '.png'))
    print('Houses →', out_dir, '(건물 %d채)' % len(HOUSES))


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

        puny(folder, tmp)
        houses(folder, tmp)

        mon = unzip('Pixel Monsters Free Pack*.zip', folder, os.path.join(tmp, 'monsters'))
        pack = os.path.join(mon, 'Pixel Monsters Free Pack')
        out = os.path.join(PROJECT, 'Assets', 'Resources', 'Art', 'Monsters')
        os.makedirs(out, exist_ok=True)
        for src, dst in [('Goblin/$Goblin', 'Goblin'), ('Mosquito/$Mosquito', 'Mosquito'), ('Evil Head/$Evil Head', 'EvilHead'),
                         ('Evil Skull/$Skull', 'Skull'), ('Armed Ghost/$Ghost Sword', 'GhostSword'), ('Armed Ghost/$Ghost Spear', 'GhostSpear')]:
            shutil.copy(os.path.join(pack, src + '.png'), os.path.join(out, dst + '.png'))
        print('Monsters →', out)

        # 예전에 가져온 Mana Seed 그림이 남아 있으면 지운다(라이선스상 이 게임에 쓸 수 없다).
        old = os.path.join(PROJECT, 'Assets', 'Resources', 'Art', 'ManaSeed')
        if os.path.isdir(old):
            shutil.rmtree(old)
            if os.path.exists(old + '.meta'):
                os.remove(old + '.meta')
            print('Mana Seed 폴더를 지웠다:', old)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


if __name__ == '__main__':
    main()
