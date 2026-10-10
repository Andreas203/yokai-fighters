---
title: Tone
status: draft
source: GDD §2.1, §2.2, §2.4, §4.5; AM2; data/story, data/modifiers
updated: 2026-10-10
---

# Tone

## The tone line

Yokai Fighters is mischievous folklore with a melancholy edge, never horror (S1). Yokai are tricksters and lonely spirits, not monsters, and every victory carries a small cost. The setting is a rural Japanese province at the end of autumn, so the mood is dusk, lantern light and things about to end, not night and dread.

Three checks for any line of text:

- Mischief: someone in the line is teasing, bargaining or getting away with something.
- Melancholy: something is owed, borrowed, waiting or left behind.
- No horror: nothing is gory, cruel, demonic or frightening. A yokai that wins walks away; it does not hurt Ryo for pleasure.

## Do and don't examples

| Do | Don't | Why |
|---|---|---|
| "Kitsune slips off between the lanterns, tails swaying, and does not look back." | "The fox demon sinks its fangs into Ryo and howls over his broken body." | Yokai are tricksters, not monsters. A loss is a yokai leaving, not a killing. |
| "Somewhere a bound thing is still waiting for someone to return it." | "Ryo has failed, and the darkness swallows the province forever." | Melancholy is small and specific: a debt, a wait. It is never doom. |
| "Shame about that Foxfire. I'd have taken good care of it." | "Your Foxfire is MINE now, fool! Tremble before me!" | The Tanuki is cheerful and covetous. It never gloats or threatens. |
| "Forgive me, Kitsune. I'll set your spirit free." | "Got you! Another one for the collection." | Ryo apologises. Collecting is what the Tanuki does. |
| "Which of us is the collector, little exorcist?" | "LOL, nice talismans, bro." | Wit is dry and in period. No modern slang, no memes, no fourth wall. |

## Card form

- A story card is one or two sentences (S2), at most 280 characters.
- Present tense, plain words, one concrete image per card: a lantern, a paper screen, a tail, a teacup, an ink stroke.
- Names come from the player's run through `{yokai}` and `{move}` placeholders (S4). A card never hard-codes a move the player may not own.
- Images come from the game's own world: paper talismans, ink, brush strokes, red seals, lanterns, bamboo, snow on a shrine gate, a riverbank, a tea house with paper screens.
- A card does not explain a rule in system words. It does not say "health", "frames", "level" or "roguelite"; it says what a person standing there would see.

## Voice: Ryo

Ryo is earnest and stubborn. He speaks rarely and briefly, he never jokes at a yokai's expense, and he apologises to every yokai he binds because his master taught him that binding borrows a spirit's strength and a debt must be named.

- His binding line is fixed and verbatim: "Forgive me, {yokai}. I'll set your spirit free." (S3, AM2)
- His ending line is fixed: "I said I'd set you free."
- He does not boast, threaten or call a yokai a monster.

## Voice: Master Sōen

Master Sōen is patient and wry. She is seen only in the intro card. Her rule is "name the debt", and the ending pays that line off. She speaks in short teaching sentences, never in speeches.

## Voice: the merchant and the Tanuki

The merchant is the Tanuki in disguise, and both speak in the same voice: cheerful, generous and quietly hungry. It collects techniques the way others collect teacups.

- It compliments what it wants: "That {move} of yours... I'd love one."
- After a loss it is sorry in the way a collector is sorry: "Shame about that Foxfire. I'd have taken good care of it."
- It teases Ryo as "little exorcist" and asks questions he cannot answer yet.
- It never shouts, never threatens and never sounds evil. The unease comes from how pleasant it is.

## Voice: the narrator

The narrator is quiet and observant, in present tense. In the GDD's sample cards the wake-up card addresses the player as "you" ("You wake at the province's edge.") and the win card names Ryo ("Ryo has no answer yet."). The narrator describes what is seen and lets the cost stay unspoken or half spoken.

## Voice: yokai

Yokai rarely speak on cards. They show character through what they do: the Kitsune teases and slips away, the Oni is proud and blunt, the Kappa is formal, stubborn and polite. They are real spirits trapped in the illusion and made to fight again each loop, which is why Ryo's apologies matter.

## Reward card voice

Reward cards are plain language first, with frame data behind a toggle (A12).

- `plain` is one sentence, at most 140 characters, that says what changes when the player fights. It may open with a short folklore image, then states the effect in ordinary words.
- `frames` is the exact number from the ability table, with no flavour.
- A plain line never promises more than the table gives.

| Ability | plain | frames |
|---|---|---|
| Foxfire | "Launch a slow fox flame that drifts across the screen." | "15 / 4 / 30 frames, 70 damage, slow projectile" |
| Will-o'-wisp | "Wisp-light lends your projectile wings: it flies faster across the screen." | "Projectile speed +30% (x1.3, rounded to nearest)" |
| Fox's Patience | "The fox waits and learns: this special builds more spirit meter when the foe blocks it." | "Meter gain on block +25% (C5 default 3 becomes 4, rounded to nearest)" |
| Oni's Hide (GDD mockup, attached to Rising Talisman) | "Your uppercut can't be interrupted early on." | not written yet |

## Open questions

- Narrator person: the GDD samples use "you" on the wake-up card and "Ryo" on the win card. Is that split intended for every card of each kind?
- The yokai personalities under "Voice: yokai" are proposed for the Oni and the Kappa (see [[Characters]]).
- Do yokai ever speak a full line on a card, or only act?
