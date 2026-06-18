Muddy Jump
Muddy Jump is a fast-paced, hyper-casual survival game built in Unity. Originally designed for mobile platforms and ported to WebGL, the game challenges players to survive against an escalating horde of enemies while managing power-ups and spatial awareness.

Play it in your browser here: https://kaankirant.itch.io/muddy-jump

🛠️ Technical Highlights & Architecture
While the game features a minimalist, low-poly aesthetic, the backend is built with robust, scalable systems to ensure performance and prevent gameplay stalling.

Parallel Spawning Framework: Built a custom SpawnManager utilizing parallel coroutine loops to completely decouple enemy spawning from consumable item drops. This ensures dynamic pacing without internal timing conflicts.

Spatial Congestion & Soft-Lock Prevention: Engineered a two-pass spatial verification system for spawn points. The engine checks XZ-plane distances to prevent enemies and items from overlapping on spawn. To prevent core-loop stalling when the board is highly congested, the system features a priority-override fallback that injects subtle displacement vectors, ensuring the action never stops.

Dynamic Difficulty Scaling: Implemented a normalized difficulty curve that dynamically shrinks spawn intervals and scales enemy health pools based on game progression.

Decoupled Architecture: Utilized Singleton patterns for Game, Spawn, and Sound Managers to keep scripts modular. Items and enemies handle their own interaction logic upon triggering, keeping the Manager classes lean and focused on state.

Cross-Platform Input: Supports both native mobile touch controls and standard PC keyboard inputs for frictionless WebGL play.

🎮 Controls
WebGL (PC/Mac):

Movement: WASD or Arrow Keys

Action: Spacebar

Mobile:

Movement/Action: Touch and swipe controls

🧠 What I Learned
This project was an exercise in understanding scope and system design. My biggest takeaway was resolving the "Defensive Architecture Trap." Initially, my strict spatial-overlap checks caused the spawning system to lock up when items cluttered the board. Rewriting the spawn logic to include a fallback hierarchy taught me how to balance clean mathematical code with the practical realities of game design—the player should never be left waiting.
