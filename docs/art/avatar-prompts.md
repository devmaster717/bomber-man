# Avatar prompts: Three Kingdoms heroes

The 10 avatars are head-and-shoulders portraits of Three Kingdoms heroes in a modern 3D animated-film style. They're
generated with an AI image tool (Midjourney, ChatGPT / DALL·E, Ideogram or similar) and imported by the build.

## How to make them

1. Generate **#0 Liu Bei** first with the prompt below. Keep regenerating until you like the look: this picture sets the
   style for the rest.
2. Generate the other nine with the **same style line**, using Liu Bei's picture as the style reference so they match:
   - Midjourney: add `--sref <Liu Bei image URL> --ar 1:1`
   - ChatGPT: in the same chat, write "Same art style, lighting and framing as the previous image:" before the prompt.
   - Ideogram / others: upload Liu Bei's picture as the style or remix reference.
3. Save each one as a square PNG (1024 × 1024 or larger) named exactly as listed, into `Game/Assets/Art/Avatars/`.
4. Build (`Tools/Build-Android.ps1`) or run *Bomb Arena > Set Up Palace Look* in Unity. Each picture is cut into a
   round portrait with a gold ring; missing ones show a plain coloured disc.

Tips: the face should fill the middle of the picture with a little space above the head (the circle trims the
corners). Avoid naming film studios in the prompt: some tools refuse that, and the style words below get the same look.

## Style line (paste at the start of every prompt)

> Stylized 3D animated feature-film character portrait, appealing modern family-animation style, expressive friendly
> eyes, smooth stylized skin, soft cinematic studio lighting with a warm rim light, head and shoulders, facing the
> viewer and turned slightly, centred, plain smooth gradient background, square 1:1, no text, no border, no watermark.

## The 10 heroes

| # | File | Hero | Prompt (after the style line) |
|---|------|------|-------------------------------|
| 0 | `0-liubei.png` | Liu Bei (Shu) | Liu Bei from Romance of the Three Kingdoms, a kind and noble young lord of ancient China, warm gentle smile, long earlobes, neat thin black moustache and short beard, black hair in a topknot under a small gold crown, jade-green silk robe with gold embroidery. Deep emerald-green background. |
| 1 | `1-guanyu.png` | Guan Yu (Shu) | Guan Yu from Romance of the Three Kingdoms, a proud loyal general of ancient China, deep red face, long narrow phoenix eyes, calm confident look, very long flowing black beard down to his chest, green cloth headwrap, green war robe over bronze armour. Deep emerald-green background. |
| 2 | `2-zhangfei.png` | Zhang Fei (Shu) | Zhang Fei from Romance of the Three Kingdoms, a big fierce but lovable warrior of ancient China, round face, wide bold eyes, huge grin, bushy spiky black beard all around his jaw, black headband, dark iron armour with a black leopard-fur collar. Deep emerald-green background. |
| 3 | `3-zhugeliang.png` | Zhuge Liang (Shu) | Zhuge Liang from Romance of the Three Kingdoms, a brilliant young strategist of ancient China, calm knowing smile, thin moustache and small goatee, tall black scholar's cap, white robe with black trim, holding a white crane-feather fan near his chin. Deep emerald-green background. |
| 4 | `4-zhaoyun.png` | Zhao Yun (Shu) | Zhao Yun from Romance of the Three Kingdoms, a handsome brave young general of ancient China, clean-shaven, determined heroic smile, shining silver-white armour, white helmet with a red tassel on top. Deep emerald-green background. |
| 5 | `5-caocao.png` | Cao Cao (Wei) | Cao Cao from Romance of the Three Kingdoms, a clever ambitious warlord of ancient China, sharp narrow eyes, confident sly smirk, short neat black beard and moustache, black official's hat, dark navy-blue robe with gold dragon embroidery. Deep royal-blue background. |
| 6 | `6-sunquan.png` | Sun Quan (Wu) | Sun Quan from Romance of the Three Kingdoms, a young confident king of ancient China, friendly assured smile, green-tinted eyes, short reddish-brown beard, hair in a topknot under a gold crown, crimson and gold royal robe. Deep crimson-red background. |
| 7 | `7-lubu.png` | Lü Bu (neutral) | Lü Bu from Romance of the Three Kingdoms, the mightiest warrior of ancient China, handsome and arrogant, bold confident grin, ornate golden armour, golden headdress with two very long pheasant tail feathers curving up behind him. Deep royal-purple background. |
| 8 | `8-sunshangxiang.png` | Sun Shangxiang (Wu) | Sun Shangxiang from Romance of the Three Kingdoms, a spirited young warrior princess of ancient China, bright fearless smile, sparkling eyes, dark hair in a high bun tied with red ribbons, light red and gold armour, a bow over her shoulder. Deep crimson-red background. |
| 9 | `9-diaochan.png` | Diaochan (neutral) | Diaochan from Romance of the Three Kingdoms, a graceful and beautiful young woman of ancient China, gentle sweet smile, elegant hair bun with gold hairpins and pink peony flowers, flowing pink and lavender silk robe. Soft rose-pink background. |

The background colours group the heroes by kingdom: green for Shu, blue for Wei, red for Wu, and purple or pink for
the two who belong to none.
