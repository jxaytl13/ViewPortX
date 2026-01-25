# ViewPortX – Unity Editor Asset Preview Tool

Quickly preview 3D models, prefabs, and particle effects in Unity without placing them in the scene. Dramatically speed up art asset review and debugging workflows.

---

## 🎯 Key Features

### Quick Preview of Multiple Asset Types
- **3D Models**: Real-time preview of FBX, Mesh, and other model assets
- **Prefabs**: Complete prefab appearance and structure inspection
- **Particle Effects**: Real-time playback and debugging of particle systems
- **UI Components**: Preview UGUI components (Unity UI)

### Flexible View Controls
- **Rotate View**: Use mouse middle button or right button to rotate and view all angles of the model
- **Pan**: Adjust view position to see details
- **Zoom**: Enlarge or shrink assets for optimal viewing
- **Quick Axis Views**: One-click switching to X, Y, Z axis views
- **Auto Focus**: Automatically adjust view to display the entire asset

### Practical Helper Tools
- **Reference Grid**: Display grid to help judge model scale and position
- **Environment Lighting**: Preview in bright and dark environments to verify lighting effects
- **Projection Mode**: Switch between perspective and orthographic projection
- **Auto Rotate**: Hands-free asset rotation for automatic display
- **Particle Playback**: Play, pause, and restart particle effects

---

## 🚀 Getting Started

### Step 1: Open the Tool
From the menu bar, select: `Window → T·L NEXUS → ViewPortX`

A new editor window will open.

### Step 2: Select Assets to Preview
Select the asset you want to preview in the Project window (models, prefabs, etc.), and the ViewPortX window will automatically load and display the preview.

You can also drag assets directly into the ViewPortX window.

### Step 3: Adjust the View
- **Rotate**: Hold down the mouse middle button (or right button) and drag
- **Zoom**: Scroll the mouse wheel (or right button + Shift drag)
- **Pan**: Hold middle button + Ctrl (or right button + Ctrl) and drag

---

## 🎮 Toolbar Guide

The toolbar at the top of the window contains the following commonly used buttons:

| Button | Purpose | Description |
|--------|---------|-------------|
| ▶️ / ⏸️ | Play / Pause | Play and pause particle effects |
| 🔄 | Restart | Restart the particle system from the beginning |
| 📊 | Grid | Show or hide the reference grid |
| 🔃 | Auto Rotate | Enable auto-rotation to showcase the model |
| 💡 | Lighting | Toggle environment lighting on/off |
| 📐 | Projection | Switch between perspective and orthographic projection |
| 🔍 | Refresh | Reload the currently selected asset |
| ↺ | Reset | Restore default view and configuration |
| X / Y / Z | Axis Views | Quick switch to coordinate axis views |
| 📍 | Focus | Auto-adjust view to display the entire asset |
| ⚙️ | Settings | Open settings menu (language, configuration, etc.) |

---

## 📖 Common Usage Scenarios

### 📦 Inspect Newly Imported Models
1. Select the imported model file in Project
2. ViewPortX automatically displays the preview
3. Use X / Y / Z buttons to inspect the model from all angles
4. Check details like UV maps, normals, etc.
5. Quickly verify model quality without entering the scene

### ✨ Debug Particle Effects
1. Select a prefab containing a particle system
2. Click ▶️ to play the particle effect
3. Use 🔃 auto-rotate to observe from multiple angles
4. After modifying particle parameters, click 🔄 to restart and see changes
5. Quickly iterate and optimize effects

### 🎨 Review Art Assets
1. Select assets one by one in the Project window
2. Use 💡 lighting switch to view different environmental appearances
3. Use 📊 grid to reference model scale and position
4. Quickly batch-check asset quality and completeness

### 🔍 Verify Prefab Appearance
1. Select the prefab file
2. Use various view buttons (X / Y / Z) for comprehensive inspection
3. Click 📍 focus to ensure you see the complete content
4. Verify appearance without instantiating into the scene

### 🎬 Showcase and Presentation
1. Select the asset to showcase
2. Click 🔃 to enable auto-rotation
3. Adjust 💡 lighting and 📐 projection for optimal effect
4. Assets will automatically rotate for display

---

## ⚙️ Settings Options

### Open Settings Menu
Click the ⚙️ settings button in the toolbar to open the settings panel.

### Configurable Items
- **Language**: Choose between English or 中文 (Chinese)
- **Other Settings**: Stored in the project's Library folder and automatically saved

### Reset to Default Settings
To reset all settings to defaults, delete the configuration file at:
```
Library/ViewPortX/ViewPortXConfig.json
```

After restarting the ViewPortX window, all settings will be reset to default values.

---

## ❓ Frequently Asked Questions

**Q: The window doesn't show a preview after selecting an asset?**  
A: Check that you've selected a supported asset type (models, prefabs, particles, etc.). If the window displays an error message, check the Console window for specific error details.

**Q: Particle effects won't play?**  
A: Make sure you've selected a prefab containing a particle system. Regular asset files may not contain particle components. Click the ▶️ button to ensure particles are in play state.

**Q: How do I save my preferences?**  
A: ViewPortX automatically saves settings (such as grid display state, lighting toggle, etc.). No need to manually save; the previous configuration will be automatically applied the next time you open the tool.

**Q: What file types can I preview?**  
A: Primarily supports prefabs, models (FBX), and Mesh assets. After selecting an asset in the Project window, ViewPortX will determine support and display a preview.

**Q: The window shows "Cannot find UXML" or "Cannot find USS" errors?**  
A: This is a file path issue. Ensure the completeness of the ViewPortX folder; UI-related files (UXML and USS) should be in the UI subdirectory. If the problem persists, re-import the tool package.

---

## 🎓 Workflow Tips

### Quick Review of Multiple Assets
In the Project window, select assets one by one, and ViewPortX will update the preview in real-time. With fast view controls, you can efficiently inspect large numbers of assets.

### Parallel Workflow
Keep the ViewPortX window and Project window on opposite sides of your screen for quick asset selection and inspection without needing to open multiple preview windows.

### Leveraging Grid and Lighting
Enable 📊 grid to quickly judge model position and scale; toggle 💡 lighting to verify texture effects in different environments.

### Auto-Showcase Mode
In meetings or presentations, enable 🔃 auto-rotation and optimized 💡 lighting settings to let assets display automatically—professional and effortless.

---

## 🔗 Quick Navigation

| Operation | Path / Shortcut |
|-----------|-----------------|
| Open ViewPortX | `Window → T·L NEXUS → ViewPortX` |
| Open Settings | Click the ⚙️ button in the toolbar |
| View Config File | `Library/ViewPortX/ViewPortXConfig.json` |
| Reset Configuration | Delete the config file above and restart |

---

**Make asset inspection easier—ViewPortX is always ready!** ✨
