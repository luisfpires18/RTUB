# Sprite Folder for Avoid Questions Game

This folder contains optional custom sprites for the "Avoid Questions" game.

## Default Behavior

The game works perfectly **without** any custom sprites. If no sprites are found, the game will render:
- **Player**: A simple figure with a purple cape (traje) and ponytail
- **Questions**: Colored rectangles with text
- **Background**: A gradient from dark blue to darker blue

## Custom Sprites (Optional)

To customize the game's appearance, you can add the following PNG files to this folder:

### 1. `player.png`
- **Description**: The player character sprite
- **Recommended size**: 40px width × 60px height
- **Format**: PNG with transparency
- **Notes**: Should represent a character in Portuguese academic traje with ponytail

### 2. `question.png`
- **Description**: The falling question sprite
- **Recommended size**: 120px width × 40px height
- **Format**: PNG with transparency
- **Notes**: Should be a generic question box or speech bubble

### 3. `background.png`
- **Description**: The game background image
- **Recommended size**: Match canvas size (responsive, typically 800-1200px width × 500px height)
- **Format**: PNG or JPG
- **Notes**: Should be a subtle background that doesn't distract from gameplay

## Implementation Notes

- Sprites are loaded asynchronously and gracefully fallback to defaults if not found
- No errors will occur if sprites are missing
- Sprites are loaded once when the game initializes
- All sprite files are optional - add only the ones you want to customize
