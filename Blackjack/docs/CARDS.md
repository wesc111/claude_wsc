# About cards

Cards are downloaded from [opengameart.org/content/playing-cards-vector-png](https://opengameart.org/content/playing-cards-vector-png)

Byron Knoll – Vector Playing Cards

Version 1.3. Classic look with traditional court cards. The author stated on his blog that he released the cards into the public domain.

These cards are available as svg in cards-svg and as png in the cards-png directories.

## Card back

`card_back.svg` and `card_back.png` are not part of Byron Knoll's set. They were generated for this project
(same size and outline as the other cards: 167.09 × 242.67 pt, PNG 500 × 726 px) and are public domain as well.
The pattern uses only plain lines and a clip path, no SVG `<pattern>`, so simple SVG renderers can draw it.
The PNG was rendered from the SVG with `rsvg-convert -w 500 -h 726`.

## App icon

`src/Blackjack.Gui/Assets/icon-1024.png` (1024 × 1024 px) shows `jack_of_hearts2.png` and `ace_of_spades.png`
fanned out on a green rounded square. It was composed once with ImageMagick and is public domain like the cards.
`scripts/make-macos-app.sh` turns it into `Blackjack.icns` with the macOS tools `sips` and `iconutil`.
