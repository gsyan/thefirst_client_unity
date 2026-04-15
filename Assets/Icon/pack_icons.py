"""
Icon Atlas Packer
- Icon 폴더 루트의 PNG 파일들을 하나의 아틀라스 PNG로 합침
- 출력: IconAtlas.png, IconAtlas.json (슬라이스 좌표)
- 실행: python pack_icons.py
- 필요: pip install pillow
"""

from PIL import Image
import os, math, json, platform, subprocess


def open_file_location(file_path):
    """생성된 파일이 있는 폴더를 운영체제의 기본 파일 탐색기로 열기"""
    try:
        abs_path = os.path.abspath(file_path)
        folder_path = os.path.dirname(abs_path)

        system = platform.system()
        if system == "Windows":
            subprocess.run(f'explorer /select,"{abs_path}"', shell=True)
        elif system == "Darwin":
            subprocess.run(['open', '-R', abs_path], check=True)
        elif system == "Linux":
            subprocess.run(['xdg-open', folder_path], check=True)
        else:
            print(f"Unsupported operating system: {system}")
            return

        print(f"Opened file location: {folder_path}")
    except Exception as e:
        print(f"Failed to open file location: {e}")
        print(f"File location: {os.path.abspath(file_path)}")

ICON_SIZE = 128   # 아이콘 1개 크기 (px) - 원본이 다른 크기면 여기 맞춰 리사이즈
PADDING   = 2     # 아이콘 간 여백

bMakeBlackToTransparency = True   # True: 검정 배경 투명 처리 / False: 원본 유지
BLACK_THRESHOLD          = 30     # 0~255, 값이 클수록 더 넓은 범위의 어두운 색을 투명 처리
INPUT_DIR   = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Images")
SCRIPT_DIR  = os.path.dirname(os.path.abspath(__file__))
OUTPUT_PNG  = os.path.join(SCRIPT_DIR, "../Resources/Icon/IconAtlas.png")  # 런타임 참조 경로
OUTPUT_JSON = os.path.join(SCRIPT_DIR, "IconAtlas.json")

# INPUT_DIR 하위 전체 PNG 수집 (재귀)
OUTPUT_FILENAME = os.path.abspath(OUTPUT_PNG)
pngs = sorted([
    os.path.abspath(os.path.join(root, f))
    for root, dirs, files in os.walk(INPUT_DIR)
    for f in files
    if f.endswith(".png") and os.path.abspath(os.path.join(root, f)) != OUTPUT_FILENAME
])

count = len(pngs)
cols  = math.ceil(math.sqrt(count))
rows  = math.ceil(count / cols)
cell  = ICON_SIZE + PADDING * 2

atlas_w = cols * cell
atlas_h = rows * cell

atlas = Image.new("RGBA", (atlas_w, atlas_h), (0, 0, 0, 0))
sprites = []

def remove_black_background(img, threshold):
    """검정 배경(R,G,B 모두 threshold 이하)을 투명으로 변환"""
    img = img.convert("RGBA")
    pixels = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = pixels[x, y]
            if r <= threshold and g <= threshold and b <= threshold:
                pixels[x, y] = (r, g, b, 0)
    return img

for i, fname in enumerate(pngs):
    img = Image.open(os.path.join(INPUT_DIR, fname)).convert("RGBA")
    if bMakeBlackToTransparency == True:
        img = remove_black_background(img, BLACK_THRESHOLD)
    img = img.resize((ICON_SIZE, ICON_SIZE), Image.LANCZOS)
    col = i % cols
    row = i // cols
    x = col * cell + PADDING
    y = row * cell + PADDING
    atlas.paste(img, (x, y))
    name = os.path.splitext(os.path.basename(fname))[0]
    sprites.append({"name": name, "x": x, "y": y, "w": ICON_SIZE, "h": ICON_SIZE})
    print(f"  [{i+1:2d}/{count}] {name}")

atlas.save(OUTPUT_PNG)

# TexturePacker Json Array 형식으로 저장 (TMP Sprite Importer + Renamer 공용)
atlas_data = {
    "frames": [
        {
            "filename":         s["name"],
            "frame":            {"x": s["x"], "y": s["y"], "w": s["w"], "h": s["h"]},
            "rotated":          False,
            "trimmed":          False,
            "spriteSourceSize": {"x": 0,      "y": 0,      "w": s["w"], "h": s["h"]},
            "sourceSize":       {"w": s["w"], "h": s["h"]},
            "pivot":            {"x": 0.0,    "y": 0.0}
        }
        for s in sprites
    ],
    "meta": {
        "app":     "http://www.texturepacker.com",
        "version": "1.0",
        "image":   "IconAtlas.png",
        "format":  "RGBA8888",
        "size":    {"w": atlas_w, "h": atlas_h},
        "scale":   "1"
    }
}
with open(OUTPUT_JSON, "w", encoding="utf-8") as f:
    json.dump(atlas_data, f, indent=2, ensure_ascii=False)

print(f"\n완료: {OUTPUT_PNG}")
print(f"아틀라스 크기: {atlas_w}x{atlas_h}, 아이콘 {count}개")
print(f"슬라이스 정보: {OUTPUT_JSON}")

open_file_location(OUTPUT_PNG)
