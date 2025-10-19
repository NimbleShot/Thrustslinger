## Core Gameplay

Thrustslinger is a wave-based arena shooter where players defend against incoming targets using **planar locomotion** - a unique movement system that constrains thrust-based flight to a 2D plane in 3D space. Players control their movement by gripping their VR controllers and aiming their palms, creating an intuitive and comfortable VR experience.

## Notable Features

### VR-Specific Systems

- **Planar Locomotion**: Hand-grip thrust controls constrained to a customizable plane, preventing VR motion sickness while maintaining freedom of movement
- **Perspective-Following Pause Menu**: UI automatically positions itself in front of the player when paused, regardless of their orientation
- **Player-Facing HUD**: Score, health, and ammo displays follow the player's head for easy readability

### Gameplay Mechanics

- **Projectile Weapons**: Physics-based projectiles with magazine and reload mechanics
- **Dynamic Target Spawning**: Difficulty scales over time with increasing spawn rates, spawn spread and targets speed.
- **Score & Combo System**: Tracks player performance with combo multipliers
- **Health System**: Target breach damage with game-over state

### Technical Systems

- **Object Pooling**: Centralized pool management for projectiles and targets - zero runtime `Instantiate()`/`Destroy()` calls for smooth performance
- **State Machine**: Clean game state management (`Boot → MainMenu → Playing → Paused → GameOver`)
- **Scene Persistence**: Singleton managers survive scene transitions with proper cleanup
- **Interface-Based Services**: Modular design allows easy swapping of score/health/combo implementations

### Visual Feedback

- **Motion Reference Particles**: Stationary dust-like particles across the play plane help players perceive their movement
- **Planar Constraint Visualization**: Debug gizmos show the locomotion plane and arena boundaries
- **Target Spawning Area Visualization**: Real-time display of spawn regions with difficulty-based spread

### Advanced Patterns

- **Coroutines**: Delayed spawning, timed weapon reloading, and state transitions
- **Input System Integration**: Unity's new Input System for flexible VR controller binding
- **ScriptableObject Configs**: Data-driven tuning for weapons, targets, and locomotion
- **Component-Based Design**: `[DisallowMultipleComponent]` and `RequireComponent` ensure clean architecture

### Quality-of-Life

- **Pause System**: Time-scale-based pause with dedicated pause UI
- **Debug Tooling**: Context menus, serialized debug flags, and Gizmo visualization throughout
- **Restart Functionality**: Clean run state reset without scene reload
- **Profile Support**: Player-configurable difficulty, comfort settings, and game modes

## Architecture Highlights

- **Inspector-Based Interface Resolution**: MonoBehaviour fields cast to interfaces for flexible system binding
- **Lifecycle Gates**: GameManager controls when gameplay systems are active
- **Pool Service Scene Transitions**: Automatic cleanup and recreation across scene loads