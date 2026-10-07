# Avatar prompt: Three Kingdoms heroes

The 10 avatars are head-and-shoulders portraits of Three Kingdoms heroes in a modern 3D animated-film style. They're
made with an AI image tool (Midjourney, ChatGPT / DALL·E, Ideogram or similar) as **one picture with all ten in a
grid**, which also keeps their style consistent. The build cuts the grid into ten round, gold-ringed portraits.

## How to make them

1. Paste the prompt below into the image tool. In Midjourney add `--ar 5:2` at the end; in other tools pick the widest
   landscape size they offer.
2. Regenerate until all ten look right and are in the right order (top row left to right, then bottom row).
3. Save it as `Game/Assets/Art/Avatars/sheet.png` (PNG or JPG; at least 2000 pixels wide is best).
4. Build (`Tools/Build-Android.ps1`) or run *Bomb Arena > Set Up Palace Look* in Unity.

To redo one hero without regenerating the whole sheet, save a single square portrait as
`Game/Assets/Art/Avatars/<file name>.png` using the names in the table; it replaces that hero's cell.

## The prompt

> A character sheet of ten portraits in a grid of exactly 5 columns and 2 rows, all the same size, edge to edge with
> no gaps, borders, labels or text. Stylized 3D animated feature-film style, appealing modern family-animation look,
> expressive friendly eyes, smooth stylized skin, soft cinematic studio lighting with a warm rim light. Every portrait
> is head and shoulders, centred in its tile with a little space above the head, facing the viewer and turned
> slightly, on its own plain gradient background. Heroes from Romance of the Three Kingdoms, ancient China.
> Top row, left to right:
> 1. Liu Bei, kind noble young lord, warm gentle smile, long earlobes, thin black moustache and short beard, topknot
>    under a small gold crown, jade-green silk robe with gold embroidery, emerald-green background;
> 2. Guan Yu, proud loyal general, deep red face, long narrow eyes, very long flowing black beard, green cloth
>    headwrap, green war robe over bronze armour, emerald-green background;
> 3. Zhang Fei, big fierce but lovable warrior, round face, wide bold eyes, huge grin, bushy spiky black beard, black
>    headband, dark iron armour with a leopard-fur collar, emerald-green background;
> 4. Zhuge Liang, brilliant young strategist, calm knowing smile, thin moustache and small goatee, tall black scholar's
>    cap, white robe with black trim, white crane-feather fan near his chin, emerald-green background;
> 5. Zhao Yun, handsome brave young general, clean-shaven, heroic smile, shining silver-white armour, white helmet with
>    a red tassel, emerald-green background.
> Bottom row, left to right:
> 6. Cao Cao, clever ambitious warlord, sharp narrow eyes, sly confident smirk, short neat black beard, black
>    official's hat, navy-blue robe with gold dragon embroidery, royal-blue background;
> 7. Sun Quan, young confident king, assured smile, green-tinted eyes, short reddish-brown beard, gold crown, crimson
>    and gold royal robe, crimson-red background;
> 8. Lü Bu, mightiest warrior, handsome and arrogant grin, ornate golden armour, golden headdress with two long
>    pheasant tail feathers, royal-purple background;
> 9. Sun Shangxiang, spirited young warrior princess, bright fearless smile, high bun tied with red ribbons, light red
>    and gold armour, a bow over her shoulder, crimson-red background;
> 10. Diaochan, graceful beautiful young woman, gentle sweet smile, elegant hair bun with gold hairpins and pink peony
>     flowers, pink and lavender silk robe, rose-pink background.

Tip: avoid naming film studios; some tools refuse that, and the style words above get the same look.

## Grid order and single-hero file names

| Cell | Hero | Kingdom | File to replace one cell |
|------|------|---------|--------------------------|
| Top 1 | Liu Bei | Shu | `0-liubei.png` |
| Top 2 | Guan Yu | Shu | `1-guanyu.png` |
| Top 3 | Zhang Fei | Shu | `2-zhangfei.png` |
| Top 4 | Zhuge Liang | Shu | `3-zhugeliang.png` |
| Top 5 | Zhao Yun | Shu | `4-zhaoyun.png` |
| Bottom 1 | Cao Cao | Wei | `5-caocao.png` |
| Bottom 2 | Sun Quan | Wu | `6-sunquan.png` |
| Bottom 3 | Lü Bu | none | `7-lubu.png` |
| Bottom 4 | Sun Shangxiang | Wu | `8-sunshangxiang.png` |
| Bottom 5 | Diaochan | none | `9-diaochan.png` |

The background colours group the heroes by kingdom: green for Shu, blue for Wei, red for Wu, and purple or pink for
the two who belong to none.
