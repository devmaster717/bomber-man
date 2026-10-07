# Bomb Arena

A grid-based bomb game for Android, drawn in 3D or flat 2D: a 100-stage single-player mode and a 2–3 player Bluetooth battle mode, tied together by a jewel currency.

## People and play

**Player**:
The person playing the game; owns the jewels, lives, avatar and stage progress.
_Avoid_: User, account (when meaning the person)

**Nickname**:
The name a player chooses, shown to other players in rounds.
_Avoid_: Username, device name, display name

**Bomber**:
The character a player controls inside the arena.
_Avoid_: Player (when meaning the character), hero

**Stage**:
One of the 100 numbered single-player levels, each with a fixed arena layout and a target time.
_Avoid_: Level, map

**Attempt**:
One play of a stage, from its start until it is cleared or failed.
_Avoid_: Try, run

**Round**:
One Bluetooth battle between 2–3 bombers, ending with a winner or a draw, or cut short by a lost connection.
_Avoid_: Match, game

**Draw**:
A round that ends with no single survivor (all remaining bombers die at once, or time runs out); every entry fee is refunded.
_Avoid_: Tie

**Forfeit**:
A player leaving a round on purpose; their bomber counts as dead and their entry fee stays in the pot.
_Avoid_: Quit, disconnect (a disconnect is a lost connection, not a forfeit)

**Host**:
The player whose device creates a round and runs it; the other players in the round are **Guests**.

## Arena

**Arena**:
The tile grid a stage or round is played on, enclosed by an outer wall.
_Avoid_: Board, map, field

**View**:
How the arena is drawn: 3D (the default, a tilted camera following the bomber) or the original flat 2D from above. Chosen in Settings; it never changes play.
_Avoid_: Mode (that word is for stage mode and Bluetooth battles)

**Hard block**:
An indestructible tile: the outer wall, and the pillars on every tile whose coordinates are both even.
_Avoid_: Wall (except for the outer wall), pillar (as a separate kind)

**Soft block**:
A destructible tile that blocks movement and stops fire, and may hide the exit or a power-up.
_Avoid_: Brick, crate

**Exit**:
The tile hidden under one soft block that ends a stage when the bomber walks into it after all enemies are dead.
_Avoid_: Door, portal

## Bombs and fire

**Bomb**:
A solid object a bomber places on its tile; it explodes when its fuse runs out or when fire reaches it.

**Remote bomb**:
A bomb with no fuse, placed while the bomber holds Remote Control, that explodes when its owner presses Detonate (or when fire reaches it).

**Fire**:
The plus-shaped area set burning by an exploding bomb; deadly for a short time to anything in it.
_Avoid_: Flame, explosion (when meaning the burning tiles)

**Chain reaction**:
A bomb exploding because fire from another bomb reached it.

## Power-ups

**Power-up**:
An item that improves the bomber until the bomber dies (Speed Up alone expires after a time limit). Found under soft blocks or bought in the shop.
_Avoid_: Card, special card, item, bonus

**Fire Up**:
Power-up that raises blast range from 1 to 2 tiles.
_Avoid_: Power card

**Bomb Up**:
Power-up that lets the bomber have one more bomb on the arena at once.
_Avoid_: Bomb card

**Remote Control**:
Power-up that lets the bomber place a remote bomb.
_Avoid_: Remote card

**Speed Up**:
Power-up that doubles the bomber's speed for a limited time.
_Avoid_: Speed card

**Inventory**:
Power-ups a player has bought but not yet used; they are applied at the start of the player's next attempt or round.
_Avoid_: Bag, stash

## Enemies

**Enemy**:
A computer-controlled creature that kills a bomber on touch; all enemies must be dead before the exit opens.
_Avoid_: Monster, mob

**Walker**:
The basic enemy, slower than the bomber.
_Avoid_: Normal enemy

**Runner**:
An enemy twice as fast as the bomber; also released from the exit when fire reaches it.
_Avoid_: Speedy enemy

**Phantom**:
An enemy that periodically fades out where it stands and fades back in on a distant empty tile.
_Avoid_: Ghost

**Wall-passer**:
An enemy that moves through soft blocks and cannot be harmed by fire while inside one.
_Avoid_: Passing enemy

## Economy

**Jewel**:
The game's only currency, earned in stages, won in rounds, or bought, and spent on lives, power-ups, avatars and entry fees.
_Avoid_: Gem, coin, credit

**Life**:
A game-wide resource (not per stage) that regenerates over time; a stage attempt cannot start without one.
_Avoid_: Heart, energy

**Entry fee**:
The jewels every player in a round pays when the host starts it.

**Pot**:
The sum of all entry fees in a round, paid to the winner.

**Avatar**:
A headshot portrait shown next to the player's nickname (home screen, room, battle header). It is display only: the bomber in the arena looks the same whichever avatar is chosen.
_Avoid_: Skin, character

**Arena theme**:
How the arena looks: Palace, Fortress (Three Kingdoms), Garden or Frozen (citadel), chosen in Settings > Arena. It changes the floor, the blocks, the exit and the lighting in both views, never the rules; bombers, bombs, enemies and power-ups look the same in every theme. Each player sees their own choice, also in a Bluetooth round.
_Avoid_: Skin, level style
