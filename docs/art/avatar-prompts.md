# Avatar pictures

The 10 avatars are cartoon headshots in a modern 3D animated-film style, made with an AI image tool as **one picture
with all ten in a 5 x 2 grid** (which keeps them in one style). The current set is
`Game/Assets/Art/Avatars/sheet.png`: ten young people and parents in casual clothes. The build cuts the grid into ten
round, gold-ringed portraits, centring each on the face.

## Making a new set

1. Write a prompt like the one below (it describes Three Kingdoms heroes; describe whoever you want instead). In
   Midjourney add `--ar 5:2` or `--ar 3:2`; in other tools pick a wide landscape size.
2. Regenerate until all ten look right.
3. Save it over `Game/Assets/Art/Avatars/sheet.png` (PNG or JPG; WebP must be converted first).
4. Build (`Tools/Build-Android.ps1`) or run *Bomb Arena > Set Up Palace Look* in Unity.

To redo one avatar without regenerating the sheet, save a single square headshot as `Game/Assets/Art/Avatars/<n>.png`,
where `<n>` is its cell: 0 to 4 along the top row, 5 to 9 along the bottom; it replaces that cell.

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
