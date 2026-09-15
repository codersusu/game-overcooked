# Bara Kitchen — game design

Version 0.2.0 · 15 September 2026 · Current four-kitchen demo

## Intent

A cute, single-player 3D cooking game starring an upright capybara. Prepare food, serve animal customers, keep clean dishes moving and solve increasingly interesting kitchen routes. The demo tests whether these actions feel satisfying alone before adding more content. Overcooked informs the cooking loop and readable service pressure; the animal café, art, recipes and tuning here are Bara Kitchen's own.

The loop is **order → collect → chop → cook if needed → assemble → serve → return → wash**. Task order, free worktops, plate availability and travel create the challenge. Every essential station remains reachable by one chef. Throws improve batching; nobody has to catch them. No upgrades, economy, online multiplayer or helper chef in this version.

## Food and interactions

| Dish | Exact recipe |
|---|---|
| Garden salad | One chopped tomato + one chopped cucumber on a clean universal dish |
| Woodland soup | One chopped carrot + one chopped mushroom cooked in the pot; collect on a clean universal dish |

Four ingredients each have whole and chopped appearances. One serving vessel supports both recipes; no separate bowls or salad station. Ordinary counters are interchangeable staging spaces.

Raw or prepared ingredients can be placed on plates and counters, up to six portions. Wrong, extra, duplicate or uncooked combinations remain visible food piles. They are allowed rather than rejected with instructions. With empty paws, R lifts the newest portion from a plate; exact recipes are recalculated. A plate can tip suitable chopped ingredients into a pot while retaining unsuitable portions. Throwing targets empty ordinary counters; it does not throw plates or automatically plate food.

The chef carries one object, interacts only when facing a station within reach, and aligns to the work surface during preparation. Tap Space once to chop, wash or extinguish; work continues until completion or movement cancellation. Partial chopping/washing progress is retained. Invalid interactions quietly preserve items. Plates keep a consistent world size when picked up or put down. The bin clears food, not the finite plate supply.

Cooking takes **12 seconds**, followed by **5 seconds ready**, then an **8-second warning** with increasing urgency in the final three seconds. Fire follows if ignored. A reusable extinguisher suppresses it, leaving blackened food in the pot. Put down the extinguisher, pick up the burnt pot with E, carry it to the bin and press E to discard only its contents. Return the same empty pot to the stove before cooking again. The pot can be staged on a free counter; returning it while still burnt does not clear it. Pots cannot be served or thrown. The first practice meal cannot burn while waiting to be collected. These are prototype parameters, not claimed Overcooked timings. Fire does not spread between stations.

## Four distinct kitchens

[Approved drawings and layout JSON](../art/concepts/levels-round-02/)

| Kitchen | Layout and lesson | Food | Round / order patience | Plates / ticket cap |
|---|---|---|---|---|
| 1 — First Service | Open U; six free counters. Learn collection, chopping, assembly, delivery and reuse. | Salad | 180s / 100s | 3 / 2 |
| 2 — Around the Island | Central island; eight free counters and two circulation paths. Introduce cooking and dash after practice. | Soup | 210s / 125s | 3 / 2 |
| 3 — Across the Divide | Upper preparation and lower service separated by a low rail; left walking passage, eight free counters. Introduce ingredient throws and batching. | Both | 240s / 145s | 4 / 2 |
| 4 — The Zigzag Café | Pantry, preparation and service; two rails with offset passages, two chopping boards, one pot, thirteen free counters. Combine all skills. | Both | 270s / 180s | 5 / 3 |

Each kitchen begins with a static picture guide and an untimed first order. Serving that meal starts the round. All kitchens are selectable in the demo. Calm mode multiplies round and patience limits by 1.5 and keeps separate scores. No third recipe is necessary yet.

Rails block walking, dashing and placement but permit legal throws over them. Every area has a walking route. Counters beyond rails are ordinary counters, with no dedicated landing type. Throwing an ingredient and walking around once can support a batch; throwing alone does not teleport the chef or guarantee a shorter service cycle.

## Customers, orders and rewards

Each kitchen has an adjoining café with an entrance, aisle, tables, chairs, flowers and seat anchors. Kitchens 1–2 have four seats; 3–4 have six. Customers enter, reserve a seat, sit, order, eat for about six seconds, return a dirty dish and leave. Expired orders cause their customer to leave. The chef serves through the hatch; manual table delivery is outside the demo.

Tickets display a recipe image, ingredient pictures, order number and patience bar. Matching delivery satisfies the oldest matching live order. Salad earns 60 base points; soup earns 80. Tips scale with remaining patience and an oldest-order streak capped at ×4. Out-of-order matching delivery still earns points but resets the streak to ×1. Expiry deducts up to 15 points, resets the streak and records a miss; score cannot become negative.

The round ends once at zero time, with score, deliveries, misses and stars. Star thresholds are 100 / 250 / 430 in kitchens 1–2 and 150 / 350 / 580 in kitchens 3–4. Best results and settings save locally. Pause freezes orders, cooking, customers and the round clock. Retry resets the service cleanly.

## Optional live voice companion

One small **AI chat** button (V) starts a GPT-Live conversation; **End chat** / V stops it. Microphone access is explicit. The player keeps moving and cooking, can interrupt naturally, and never has to type. Chat does not pause service or cancel work. The game UI remains English; Bara is instructed to follow the player's spoken language, including English and Chinese.

Bara answers questions using actual orders, held items, plate portions, station contents, cooking state and verified routes relative to the current camera. It can discuss score, served/missed orders and streak without inventing achievements. A temporary blue marker indicates a suggested station. Burnt-pot recovery preserves the required bin-and-return sequence. Gameplay advice is normally one short next step.

While connected, the companion may give one warning per pot warning/fire state, an imminent service reminder for a matching completed dish in hand, an idle hint after at least 18 seconds, or occasional encouragement after three more deliveries. General hints have a 45-second cooldown and wait for conversation to settle; fire takes priority. No reminders during pause/menu/results or while actively extinguishing. “No hints” disables unsolicited guidance while questions remain available.

Chat is optional and requires the private OpenAI helper. The picture guide and offline loop remain available. The companion cannot move the chef, cook, change scores or unlock abilities. Human playtesting should assess conversational timing, interruptions, reminder frequency and occasional model mistakes.

## Character standard

**[Capybara C1](../art/approved/capybara-c1.png) is the approved proportion and face reference.** Preserve its oversized rounded head, smaller gently rounded body, extremely short connected legs, short broad muzzle, dark animal eyes and fingerless paws. Avoid white human eye sclera, long snouts, long shins, detached limbs and an excessively broad belly.

Approximate concept guides, with H measured floor to skull crown excluding hats/ears: head height 48–52% of H, head width 60–64%, clothed body width 48–53%, exposed feet/legs 5–7%. These are drawing aids inferred from the reference, not calibrated modeling measurements. Compare animals at common floor and skull height; rabbit ears do not shrink its body.

Use broad colors and soft surfaces rather than strand fur. Capybara is caramel/cocoa/ivory. Cat is ginger with broad tabby marks, soft triangular ears and a curved tail. Dog is golden tan with floppy ears and a compact puppy muzzle. Species must remain recognizable without clothing.

Each species has **male chef, female chef, male customer and female customer** cosmetic presets, with identical abilities. Both presets need prominent contrasting decoration; tiny fur-colored flowers are insufficient. Chef outfits share an ivory family resemblance while hats, collars and aprons vary. Customer clothes differ in silhouette as well as color. Avoid requiring a unique entire wardrobe system for each species.

[Accepted round-2 sheets](../art/concepts/roster-round-02/) include capybara, cat, dog, red panda, rabbit, otter and optional raccoon. The playable production trio is capybara/cat/dog, twelve total looks. Raccoon remains optional and must read as cool gray with a dark mask, distinct from the warm red panda. Current 3D costumes are baked into meshes; truly interchangeable clothing is future work.

## Environment, animation and UI

Honey-oak counters, sage cabinets, cream tiles, teal cookware, warm wood dining floors and restrained floral decoration. Keep silhouettes rounded, colors bright, shadows soft and food visibly three-dimensional. Use common scale for vessels and ingredients. Worktops sit at the paws below the head, approximately 0.46–0.47m in the active interaction setup. Rooms use a 0.92m planning module, 15% larger than the initial room pass; the dining area was widened by 40% from the narrow review. Tables and chairs replace the couch.

Idle gently raises and lowers arms along the sides. Walking visibly alternates feet; dash leans forward with a short afterimage. Carrying holds a steady plate. Throwing swings a paw. Chopping keeps the head upright and the knife vertical, blade down. Washing uses a short up/down stroke at the plate rim, with the elbow directed outward. Extinguishing uses slight body sway with the tool following. Customers have seated idle and eating motion. Shared clips must be checked on every animal, especially dog sleeve/elbow deformation.

The kitchen/café occupy the main view. Compact order, score and time displays sit above it; small pot progress bars replace written countdowns. Animated station outlines and service markers guide interaction. Routine green text popups are removed. Minimal picture guides introduce each kitchen; H reopens them. Wooden signs, recipe cards, rounded type and the new capybara icon belong to the café art direction.

Players can tilt the camera 35–80° above the floor and turn horizontally within a front-side arc of −80° to +80°. Default tilt is 60°. Framing keeps the whole scene visible; HUD stays outside it. Movement is camera-relative. View preferences persist.

Music changes for normal service, the final 30 seconds and fire. Foley accompanies footsteps, pickups, chopping, washing and dishes; customers use cute nonverbal sounds. Fire music takes priority. Settings include separate volume controls and reduced flashing.

## What to evaluate next

Human playtests should assess first-time discovery, route length, solo ticket pressure, the value of batch throwing, star thresholds, and whether plate freedom helps exploration. Artist review should refine sleeves/elbows, foot contact and utensil contact in motion. Performance and build size need optimization before a wider launch.

Deferred: autonomous AI support chef, voice task assignment, remote oven control, upgrades, extra recipes/levels, remaining species and multiplayer. Expand only after the four-kitchen loop is enjoyable.
