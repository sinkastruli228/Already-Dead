# Промпт: пещерный персонаж

Лучше отправлять генератору на английском — так он точнее держит ракурс и структуру листа.

```text
Create a modular pixel-art character sprite sheet for a fast top-down action game.

CAMERA AND ORIENTATION:
- Strict orthographic bird's-eye view from exactly 90 degrees above.
- The camera looks straight down at the crown of the character's head.
- No three-quarter view, no isometric angle, no perspective and no horizon.
- Do not show the front of the face, chest or belly.
- Neutral pose faces screen-right: head points right, feet trail left. This is Unity +X.

CHARACTER:
- A chunky, readable caveman with tanned skin and broad shoulders.
- Large messy dark-brown hair.
- One large white bone tied horizontally across the hair, clearly visible from above.
- Ochre-yellow leopard-hide tunic with sparse irregular dark-brown spots and a jagged hem.
- Bare arms, lower legs and feet.
- Strong silhouette that remains readable at 48x48 pixels.
- No weapon and no held item.

SPRITE SHEET:
- Six equal 48x48 pixel cells arranged in a clean 3x2 grid.
- Cell 1: complete assembled character.
- Cell 2: legs and feet only.
- Cell 3: torso and head only, without arms or legs.
- Cell 4: detached left arm and fist only.
- Cell 5: detached right arm and fist only.
- Cell 6: simple pixelated oval ground shadow only.
- All parts use identical scale, palette and attachment points.
- Every asset is centered on the same 48x48 canvas so the layers align in Unity.

PIXEL ART:
- Crisp hand-placed pixel clusters, hard edges and a limited warm palette of 16-24 colors.
- No antialiasing, no blur, no gradients and no soft glow.
- Genuine transparent RGBA background.
- No floor, backdrop, vignette, lighting presentation, labels, text, borders or grid lines.
- Game assets only, not a character illustration or presentation board.
```

Если генератор снова делает ракурс сбоку, отдельной правкой отправить:

```text
Camera correction only: redraw the character from a strict 90-degree overhead orthographic view. Show the crown of the head and top surfaces only. Do not show the front of the face or chest. Preserve the design and the right-facing direction.
```

После генерации: проверить альфа-канал, привести каждый элемент к холсту 48x48, сохранить отдельными PNG и импортировать с `Pixels Per Unit = 48`, `Filter Mode = Point`, `Compression = None`, `Mip Maps = Off`.
