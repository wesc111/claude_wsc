# About sounds

The sounds of the GUI (spec E8) come from [kenney.nl/assets/casino-audio](https://kenney.nl/assets/casino-audio):

Kenney Vleugels – Casino Audio, version 1.1. License: Creative Commons Zero (CC0). Credit is not required, but
appreciated.

The pack was downloaded into `audio/` (ignored by git). The files used were converted from Ogg Vorbis to mono WAV
(16 bit, 44.1 kHz) with the macOS tool `afconvert`, e.g.
`afconvert -f WAVE -d LEI16 -c 1 audio/Audio/card-slide-1.ogg src/Blackjack.Gui/Assets/Sounds/card-slide-1.wav`.

| Event | Files in `src/Blackjack.Gui/Assets/Sounds` |
|---|---|
| A card is pulled out of the shoe | `card-slide-1…4` |
| The hole card turns over | `card-place-2`, `card-place-4` |
| The shoe is reshuffled | `card-shuffle` |
| A chip is put into the bet area | `chip-lay-1…3` |
| A chip is taken back, or Clear | `chips-handle-3`, `chips-handle-4` |
| Round won | `chips-stack-1`, `chips-stack-3` |
| Round won with a Blackjack | `chips-handle-5` |
| Round lost | `card-shove-1`, `card-shove-2` |

The mapping in code is `src/Blackjack.Gui/Sounds/SoundFiles.cs`.
